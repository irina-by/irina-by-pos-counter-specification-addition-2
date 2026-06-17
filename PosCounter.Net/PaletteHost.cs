using System.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Autodesk.AutoCAD.Windows;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using PosCounter.Net.UI;

namespace PosCounter.Net
{
    // POSC-SINGLE-FILE: PaletteHost — в проекте должен быть только этот файл (не PaletteHost (1).cs).
    public static class PaletteHost
    {
        // NOTE: PaletteSet.Size is the outer window size (includes non-client area).
        // WPF layout needs enough *client* size; PaletteSet.Size is outer window size (includes non-client area).
        // Slightly wider than 526 to fit comfortably on AutoCAD 2016.
        // Initial (startup) outer size of the floating palette.
        // Engineers can resize it freely after opening.
        // New UI is denser; keep a comfortable default width without forcing an overly wide palette.
        private static readonly Size PreferredPaletteSize = new Size(780, 680);
        // Allow resizing down to a reasonable minimum.
        private static readonly Size MinimumPaletteSize = new Size(520, 520);
        // AutoCAD persists palette docking/size by this name. Bump when we need to reset persisted state.
        // Bump name to reset persisted palette size/docking when layout changes materially.
        private const string PaletteName = "POS COUNTER v4.2.0";
        private static PaletteSet _palette;
        private static PosCounterControl _control;
        private static ElementHost _host;
        private static bool _layoutFixScheduled;
        private const int LayoutFixAttempts = 10; // keep brief: reduces visible flicker on show
        private static bool _docEventsHooked;
        private static bool _hostSizeHooked;
        private static bool _isNudgingSize;
        private static bool _wpfReattached;
        private static bool _nativeNudged;
        private static bool _initialLocationSet;
        private static int _specPickRunning;

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_ALLCHILDREN = 0x0080;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RedrawWindow(
            IntPtr hWnd,
            IntPtr lprcUpdate,
            IntPtr hrgnUpdate,
            uint flags);

        public static PosCounterControl Control => _control;

        internal static bool PendingCountAllInModel { get; private set; }

        // Highlight request (handles) queued from UI; executed inside AutoCAD command context.
        private static string[] _pendingHighlightHandles = ArrayCompat.Empty<string>();
        private static string _pendingHighlightDbFp;

        internal static bool TryConsumePendingHighlightHandles(Document doc, out string[] handles)
        {
            handles = ArrayCompat.Empty<string>();
            if (doc == null)
            {
                _pendingHighlightHandles = ArrayCompat.Empty<string>();
                _pendingHighlightDbFp = null;
                return false;
            }

            var fp = doc.Database.FingerprintGuid.ToString();
            if (!string.IsNullOrWhiteSpace(_pendingHighlightDbFp)
                && !string.Equals(_pendingHighlightDbFp, fp, StringComparison.OrdinalIgnoreCase))
            {
                _pendingHighlightHandles = ArrayCompat.Empty<string>();
                _pendingHighlightDbFp = null;
                return false;
            }

            if (_pendingHighlightHandles == null || _pendingHighlightHandles.Length == 0)
            {
                return false;
            }

            handles = _pendingHighlightHandles;
            _pendingHighlightHandles = ArrayCompat.Empty<string>();
            _pendingHighlightDbFp = null;
            return true;
        }

        // Snapshot PickFirst at the moment user clicks RUN (before SendStringToExecute can clear it).
        private static ObjectId[] _pendingPickFirstIds = ArrayCompat.Empty<ObjectId>();
        private static string _pendingPickFirstDbFp;

        internal static bool TryConsumePendingPickFirst(Document doc, out ObjectId[] ids)
        {
            ids = ArrayCompat.Empty<ObjectId>();
            if (doc == null)
            {
                _pendingPickFirstIds = ArrayCompat.Empty<ObjectId>();
                _pendingPickFirstDbFp = null;
                return false;
            }

            var fp = doc.Database.FingerprintGuid.ToString();
            if (!string.IsNullOrWhiteSpace(_pendingPickFirstDbFp)
                && !string.Equals(_pendingPickFirstDbFp, fp, StringComparison.OrdinalIgnoreCase))
            {
                // Stale snapshot from another document; discard.
                _pendingPickFirstIds = ArrayCompat.Empty<ObjectId>();
                _pendingPickFirstDbFp = null;
                return false;
            }

            if (_pendingPickFirstIds == null || _pendingPickFirstIds.Length == 0)
            {
                return false;
            }

            ids = _pendingPickFirstIds;
            _pendingPickFirstIds = ArrayCompat.Empty<ObjectId>();
            _pendingPickFirstDbFp = null;
            return true;
        }

        internal static void RequestRun(bool countAllInModel)
        {
            PendingCountAllInModel = countAllInModel;
            try
            {
                var doc = AcAp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    return;
                }

                // Capture implied selection BEFORE queuing the command (palette click often clears PickFirst).
                if (!countAllInModel)
                {
                    try
                    {
                        var implied = doc.Editor.SelectImplied();
                        if (implied.Status == PromptStatus.OK)
                        {
                            _pendingPickFirstIds = implied.Value
                                .GetObjectIds()
                                .Where(id => !id.IsNull && id.IsValid && !id.IsErased)
                                .Distinct()
                                .ToArray();
                            _pendingPickFirstDbFp = doc.Database.FingerprintGuid.ToString();
                        }
                        else
                        {
                            _pendingPickFirstIds = ArrayCompat.Empty<ObjectId>();
                            _pendingPickFirstDbFp = null;
                        }
                    }
                    catch
                    {
                        _pendingPickFirstIds = ArrayCompat.Empty<ObjectId>();
                        _pendingPickFirstDbFp = null;
                    }
                }
                else
                {
                    _pendingPickFirstIds = ArrayCompat.Empty<ObjectId>();
                    _pendingPickFirstDbFp = null;
                }

                // Run counting inside a real AutoCAD command context to avoid palette-thread crashes.
                // Important: do NOT send ESC here, otherwise AutoCAD clears PickFirst selection.
                // When user preselects callouts, we must preserve that selection.
                doc.SendStringToExecute("_.POSC2_RUN_INTERNAL ", true, false, false);
            }
            catch
            {
                // ignore
            }
        }

        public sealed class SpecApplyPayload
        {
            public bool Success;
            public string Error;
            public Dictionary<int, string> MarkNames = new Dictionary<int, string>();
            public int PaletteKeyCount;
            public int QtyWritten;
            public int QtySkipped;
            public List<int> MissingQtyMarks = new List<int>();
        }

        internal static bool TryBuildQtyByKeyForWriteback(out Dictionary<int, int> qtyByKey)
        {
            qtyByKey = new Dictionary<int, int>();
            var control = _control;
            if (control == null)
            {
                return false;
            }

            try
            {
                Dictionary<int, int> snapshotMap = null;
                control.Dispatcher.Invoke(() =>
                {
                    control.TryBuildQtyByKeyFromVisibleRows(out snapshotMap);
                });
                qtyByKey = snapshotMap ?? new Dictionary<int, int>();
                return qtyByKey.Count > 0;
            }
            catch
            {
                qtyByKey = new Dictionary<int, int>();
                return false;
            }
        }

        internal static bool TryEnterSpecPick() =>
            System.Threading.Interlocked.CompareExchange(ref _specPickRunning, 1, 0) == 0;

        internal static void ExitSpecPick() =>
            System.Threading.Interlocked.Exchange(ref _specPickRunning, 0);

        internal static void RequestSelectSpec()
        {
            try
            {
                var doc = AcAp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    return;
                }

                if (!TryEnterSpecPick())
                {
                    _control?.Dispatcher.BeginInvoke(new Action(() =>
                        _control?.SetStatusFromHost("Выбор спецификации уже выполняется — дождитесь завершения.")));
                    return;
                }

                doc.SendStringToExecute("_.POSC2_SPEC_INTERNAL ", true, false, false);
            }
            catch
            {
                ExitSpecPick();
            }
        }

        internal static void RequestHighlightByHandles(IEnumerable<string> handles)
        {
            try
            {
                var doc = AcAp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    return;
                }

                var list = (handles ?? Enumerable.Empty<string>())
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Select(h => h.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (list.Length == 0)
                {
                    return;
                }

                _pendingHighlightHandles = list;
                _pendingHighlightDbFp = doc.Database.FingerprintGuid.ToString();

                doc.SendStringToExecute("_.POSC2_HIGHLIGHT_INTERNAL ", true, false, false);
            }
            catch
            {
                // ignore
            }
        }

        public static void ShowPalette()
        {
            EnsurePalette();
            _palette.Visible = true;
            ForceFloating();
            ScheduleLayoutFix();
            _control.RefreshFromDocument();
        }

        public static void HidePalette()
        {
            if (_palette != null)
            {
                _palette.Visible = false;
            }
        }

        private static void EnsurePalette()
        {
            if (_palette != null)
            {
                return;
            }

            _palette = new PaletteSet(PaletteName)
            {
                // AutoHide makes the palette behave like a "tab" that disappears; keep UX stable.
                Style = PaletteSetStyles.ShowCloseButton
                        | PaletteSetStyles.ShowPropertiesMenu,
                // AutoCAD 2016 + WPF(ElementHost) in docked palettes can cause "projection/bleed-through" artifacts.
                // Floating palette reduces these artifacts significantly.
                DockEnabled = DockSides.None,
                Dock = DockSides.None,
                MinimumSize = MinimumPaletteSize,
                Size = PreferredPaletteSize
            };

            _control = new PosCounterControl();
            _host = new ElementHost
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BackColor = System.Drawing.SystemColors.Control,
                BackColorTransparent = false,
                Child = _control
            };

            _palette.Add("Счетчик позиций", _host);
            // Ensure underlying WinForms handle exists early; helps avoid "blank until resize" in AutoCAD 2016.
            _host.CreateControl();
            EnsureHostSizingHooks();
            EnsureDocumentEventHooks();
            ScheduleLayoutFix();
            ForceFloating();
        }

        private static void ForceFloating()
        {
            if (_palette == null)
            {
                return;
            }

            try
            {
                // Try to break out of any persisted/tabbed palette container.
                _palette.DockEnabled = DockSides.None;
                _palette.Dock = DockSides.None;
                if (_palette.Size.Width < MinimumPaletteSize.Width || _palette.Size.Height < MinimumPaletteSize.Height)
                {
                    _palette.Size = PreferredPaletteSize;
                }

                // Give AutoCAD an explicit floating location (screen coords).
                // Without a location, it may reattach to the last palette container.
                if (!_initialLocationSet)
                {
                    _initialLocationSet = true;
                    _palette.Location = new Point(120, 120);
                }
            }
            catch
            {
                // ignore
            }
        }

        private static void EnsureHostSizingHooks()
        {
            if (_palette == null || _host == null || _hostSizeHooked)
            {
                return;
            }

            _hostSizeHooked = true;
            try
            {
                // Keep the WinForms host sized to the palette client area.
                _palette.SizeChanged += (s, e) => SyncHostToPalette();
                _palette.StateChanged += (s, e) => SyncHostToPalette();
            }
            catch
            {
                // ignore
            }

            SyncHostToPalette();
        }

        private static void SyncHostToPalette()
        {
            if (_palette == null || _host == null)
            {
                return;
            }

            try
            {
                // PaletteSet sometimes doesn't re-layout hosted controls on first show/dock changes.
                // Force the host to occupy the available space.
                _host.Dock = DockStyle.Fill;
                _host.PerformLayout();
                _host.Invalidate(true);
                _host.Update();
                _host.Refresh();

                _control?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _control.InvalidateVisual();
                        _control.UpdateLayout();
                    }
                    catch
                    {
                        // ignore
                    }
                }), DispatcherPriority.Background);
            }
            catch
            {
                // ignore
            }
        }

        private static void EnsureDocumentEventHooks()
        {
            if (_docEventsHooked)
            {
                return;
            }

            _docEventsHooked = true;
            try
            {
                var dm = AcAp.DocumentManager;
                dm.DocumentActivated += OnDocumentActivated;
                dm.DocumentCreated += OnDocumentCreated;
            }
            catch
            {
                // best-effort: palette should still work without auto-refresh
            }
        }

        private static void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            RefreshControlFromActiveDocument();
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            RefreshControlFromActiveDocument();
        }

        private static void RefreshControlFromActiveDocument()
        {
            if (_palette == null || _control == null)
            {
                return;
            }

            // Only refresh when palette is shown; avoids unnecessary work on every doc switch.
            if (!_palette.Visible)
            {
                return;
            }

            try
            {
                _control.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _control.RefreshFromDocument();
                }), DispatcherPriority.Background);
            }
            catch
            {
                // ignore
            }
        }

        private static void ScheduleLayoutFix()
        {
            if (_palette == null || _host == null || _control == null)
            {
                return;
            }

            // AutoCAD 2016 can apply persisted docking/size AFTER Visible=true.
            // WPF inside ElementHost may render incorrectly until a resize triggers a new layout pass.
            // We retry a few times over a short window to catch the final size.
            if (_layoutFixScheduled)
            {
                return;
            }

            _layoutFixScheduled = true;
            var attemptsLeft = LayoutFixAttempts;
            var didHeavyFix = false;
            var timer = new Timer { Interval = 50 };
            timer.Tick += (s, e) =>
            {
                attemptsLeft--;

                try
                {
                    // Re-apply floating + size to trigger AutoCAD to re-run internal layout.
                    ForceFloating();
                    SyncHostToPalette();

                    // Heavy fixes (reattach / native resize) can cause visible flicker.
                    // Do them at most once per session, and only early.
                    if (!didHeavyFix)
                    {
                        didHeavyFix = true;
                        NudgePaletteSize();
                        ForceReattachWpfOnce();
                        TryNudgeNativePaletteWindowOnce();
                    }

                    _host.PerformLayout();

                    _control.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _control.InvalidateMeasure();
                        _control.InvalidateArrange();
                        _control.InvalidateVisual();
                        _control.UpdateLayout();
                    }), DispatcherPriority.Loaded);
                }
                catch
                {
                    // best-effort: layout fix should never crash AutoCAD
                }

                if (attemptsLeft <= 0)
                {
                    timer.Stop();
                    timer.Dispose();
                    _layoutFixScheduled = false;
                }
            };
            timer.Start();
        }

        private static void TryNudgeNativePaletteWindowOnce()
        {
            if (_nativeNudged || _palette == null)
            {
                return;
            }

            // Only attempt after the palette is visible, otherwise Handle can be zero.
            if (!_palette.Visible)
            {
                return;
            }

            try
            {
                var hwnd = _palette.Handle;
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                _nativeNudged = true;

                // Force a real native resize (like user dragging) and repaint.
                var w = PreferredPaletteSize.Width;
                var h = PreferredPaletteSize.Height;

                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, w + 1, h, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, w, h, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
                RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_UPDATENOW | RDW_ALLCHILDREN);
            }
            catch
            {
                // ignore
            }
        }

        private static void ForceReattachWpfOnce()
        {
            if (_wpfReattached || _host == null || _control == null)
            {
                return;
            }

            _wpfReattached = true;
            try
            {
                // In AutoCAD 2016 palettes, ElementHost + WPF can remain visually stale until first user interaction.
                // Reattaching the Child forces WPF to rebuild and repaint.
                var child = _control;
                _host.Child = null;
                _host.Child = child;
                _host.PerformLayout();
                _host.Invalidate(true);
                _host.Update();
                _host.Refresh();
            }
            catch
            {
                // ignore
            }
        }

        private static void NudgePaletteSize()
        {
            if (_palette == null)
            {
                return;
            }

            if (_isNudgingSize)
            {
                return;
            }

            _isNudgingSize = true;
            try
            {
                // AutoCAD 2016 sometimes repaints the WPF/ElementHost content only after a *real* size change
                // (equivalent to user dragging the resize grip). Nudge by 1px and restore.
                var w = PreferredPaletteSize.Width;
                var h = PreferredPaletteSize.Height;
                _palette.Size = new Size(w + 1, h);
                _palette.Size = new Size(w, h);
            }
            catch
            {
                // ignore
            }
            finally
            {
                _isNudgingSize = false;
            }
        }
    }
}
