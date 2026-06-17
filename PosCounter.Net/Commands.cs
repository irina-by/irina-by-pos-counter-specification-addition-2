using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using PosCounter.Net.Engine;
using PosCounter.Net.SpecGrid;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace PosCounter.Net
{
    public class Commands : IExtensionApplication
    {
        // Transient (temporary) highlight overlays for legacy AutoCAD where implied selection
        // can be barely visible (e.g., AutoCAD 2016). Keep per-session, clear on every new highlight.
        private static readonly object _transientGate = new object();
        private static readonly System.Collections.Generic.List<Entity> _transientEntities = new System.Collections.Generic.List<Entity>();
        private static bool _autoOpenPaletteDone;

        /// <summary>После NETLOAD один раз открывает палитру (инженеру не нужно вводить POSC).</summary>
        public void Initialize()
        {
            try
            {
                SpecGridSession.ClearScopes();
                WriteNetLoadBanner();
                AcApp.Idle += AutoOpenPaletteAfterNetLoad;
            }
            catch
            {
                // ignore
            }
        }

        private static void WriteNetLoadBanner()
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc?.Editor == null)
                {
                    return;
                }

                var asm = Assembly.GetExecutingAssembly();
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? asm.GetName().Version?.ToString()
                    ?? "?";
#if NET8_0
                const string targetLabel = "net8.0-windows";
#else
                const string targetLabel = "net46";
#endif
                doc.Editor.WriteMessage(
                    $"\n[POSC] PosCounter.Net {info} ({targetLabel}) загружен.\n");

                /*
                var acDiag = TryFormatAcadReleaseForDiag(targetLabel);
                if (!string.IsNullOrWhiteSpace(acDiag))
                {
                    SpecGridLog.ResetDiagSession(doc);
                    SpecGridLog.WriteDiag(doc, acDiag);
                }
                */
            }
            catch
            {
                // ignore
            }
        }

        private static string TryFormatAcadReleaseForDiag(string targetLabel)
        {
            try
            {
                var current = HostApplicationServices.Current;
                var prop = current?.GetType()?.GetProperty("ReleaseMajorVersion");
                var raw = prop?.GetValue(current, null) as string;
                if (int.TryParse(raw, out var major))
                {
                    return $"AutoCAD R{major} / DLL {targetLabel}";
                }
            }
            catch
            {
                // ignore
            }

            return string.IsNullOrWhiteSpace(targetLabel) ? null : $"AutoCAD ? / DLL {targetLabel}";
        }

        public void Terminate()
        {
            try
            {
                AcApp.Idle -= AutoOpenPaletteAfterNetLoad;
            }
            catch
            {
                // ignore
            }
        }

        private static void AutoOpenPaletteAfterNetLoad(object sender, EventArgs e)
        {
            if (_autoOpenPaletteDone)
            {
                return;
            }

            _autoOpenPaletteDone = true;
            try
            {
                AcApp.Idle -= AutoOpenPaletteAfterNetLoad;
            }
            catch
            {
                // ignore
            }

            try
            {
                PaletteHost.ShowPalette();
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>Единственная команда для инженера (повторный вызов — снова показать палитру).</summary>
        [CommandMethod("POSC")]
        public void PosCounterCommand()
        {
            PaletteHost.ShowPalette();
        }

        /// <summary>Служебная: подсчёт из палитры. Вводить вручную не нужно.</summary>
        [CommandMethod("POSC2_RUN_INTERNAL", CommandFlags.Session | CommandFlags.NoHistory | CommandFlags.NoActionRecording)]
        public void PosCounterRunInternal()
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
                var ed = doc?.Editor;

                var engine = new PosCounterEngine();
                var res = engine.CountWithInfo(PaletteHost.PendingCountAllInModel);

                var control = PaletteHost.Control;
                if (control == null)
                {
                    return;
                }

                // Detach plain data on the command thread before any UI hop (palette/WPF + ElementHost is sensitive).
                PosCounterEngine.PosCountResult uiPayload;
                try
                {
                    uiPayload = res?.CloneDetached() ?? new PosCounterEngine.PosCountResult();
                }
                catch
                {
                    uiPayload = new PosCounterEngine.PosCountResult();
                }

                // First leave command context (Idle), then marshal to WPF. Double-defer avoids hard crashes after heavy DB work.
                EventHandler idleOnce = null;
                idleOnce = (s, e) =>
                {
                    try
                    {
                        AcApp.Idle -= idleOnce;
                    }
                    catch
                    {
                        // ignore
                    }

                    try
                    {
                        control.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                try
                                {
                                    control.ApplyRunResult(uiPayload);
                                }
                                catch
                                {
                                    // ignore
                                }
                            }),
                            DispatcherPriority.Loaded);
                    }
                    catch
                    {
                        // ignore
                    }
                };

                try
                {
                    AcApp.Idle += idleOnce;
                }
                catch
                {
                    // Fallback: old single defer
                    control.Dispatcher.BeginInvoke(
                        new Action(() => control.ApplyRunResult(uiPayload)),
                        DispatcherPriority.Loaded);
                }
            }
            catch
            {
                // Never throw from AutoCAD command; avoid destabilizing host.
            }
        }

        [CommandMethod("POSC2_SPEC_INTERNAL", CommandFlags.Session | CommandFlags.NoHistory | CommandFlags.NoActionRecording)]
        public void PosCounterSpecInternal()
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    return;
                }

                var log = new SpecGridLog();
                Dictionary<int, int> qtyByKey = null;
                PaletteHost.TryBuildQtyByKeyForWriteback(out qtyByKey);
                var pick = SpecGridService.RunSelectSpecification(doc, qtyByKey ?? new Dictionary<int, int>(), log);
                var names = SpecGridService.BuildCombinedMarkNames();

                var control = PaletteHost.Control;
                if (control == null)
                {
                    return;
                }

                var payload = new PaletteHost.SpecApplyPayload
                {
                    Success = pick.Success,
                    Error = pick.Error,
                    MarkNames = names,
                    QtyWritten = pick.QtyWritten,
                    QtySkipped = pick.QtySkipped,
                    MissingQtyMarks = pick.MissingQtyMarks ?? new List<int>()
                };

                EventHandler idleOnce = null;
                idleOnce = (s, e) =>
                {
                    try { AcApp.Idle -= idleOnce; } catch { /* ignore */ }
                    try
                    {
                        control.Dispatcher.BeginInvoke(
                            new Action(() => control.ApplySpecResult(payload)),
                            DispatcherPriority.Loaded);
                    }
                    catch
                    {
                        // ignore
                    }
                };

                try { AcApp.Idle += idleOnce; } catch { /* ignore */ }
            }
            catch
            {
                // never throw from command
            }
            finally
            {
                PaletteHost.ExitSpecPick();
            }
        }

        [CommandMethod("POSC2_HIGHLIGHT_INTERNAL", CommandFlags.Session | CommandFlags.NoHistory | CommandFlags.NoActionRecording)]
        public void PosCounterHighlightInternal()
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
                var ed = doc?.Editor;
                if (doc == null)
                {
                    return;
                }

                if (!PaletteHost.TryConsumePendingHighlightHandles(doc, out var handles) || handles.Length == 0)
                {
                    return;
                }

                int highlighted = 0;
                string error = null;

                using (doc.LockDocument())
                {
                    try
                    {
                        TryForceSelectionVisualsForLegacyAutoCad();
                        var db = doc.Database;
                        var ids = handles
                            .Select(h =>
                            {
                                try
                                {
                                    var handle = new Handle(long.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                    var id = db.GetObjectId(false, handle, 0);
                                    return (!id.IsNull && id.IsValid && id.Database == db) ? id : ObjectId.Null;
                                }
                                catch
                                {
                                    return ObjectId.Null;
                                }
                            })
                            .Where(id => !id.IsNull && id.IsValid)
                            .Distinct()
                            .ToArray();

                        ed.SetImpliedSelection(ids);
                        highlighted = ids.Length;

                        // AutoCAD 2016–2024: implied selection may be very subtle.
                        // Add transient "bold boxes" to make it clearly visible.
                        if (ids.Length > 0 && IsAutoCad2016())
                        {
                            using (var tr = db.TransactionManager.StartTransaction())
                            {
                                ClearTransientHighlight();
                                AddTransientBoxes(tr, ids);
                                tr.Commit();
                            }

                            // Some legacy builds repaint transients only after regen.
                            try { ed?.Regen(); } catch { /* ignore */ }
                        }
                        else
                        {
                            // Keep 2025+ behavior unchanged (selection highlight looks good there).
                            ClearTransientHighlight();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        error = ex.GetType().Name + ": " + ex.Message;
                        try { ed.SetImpliedSelection(Array.Empty<ObjectId>()); } catch { /* ignore */ }
                        try { ClearTransientHighlight(); } catch { /* ignore */ }
                    }
                }

                // Notify UI (best-effort) after we leave command context.
                var control = PaletteHost.Control;
                if (control == null)
                {
                    return;
                }

                EventHandler idleOnce = null;
                idleOnce = (s, e) =>
                {
                    try { AcApp.Idle -= idleOnce; } catch { /* ignore */ }
                    try
                    {
                        control.Dispatcher.BeginInvoke(
                            new Action(() => control.ApplyHighlightResult(highlighted, error)),
                            DispatcherPriority.Loaded);
                    }
                    catch
                    {
                        // ignore
                    }
                };

                try { AcApp.Idle += idleOnce; } catch { /* ignore */ }
            }
            catch
            {
                // ignore
            }
        }

        private static void TryForceSelectionVisualsForLegacyAutoCad()
        {
            // Best-effort: older AutoCAD versions may have these toggled off in profiles.
            // Even when set, the visual style of implied selection can still differ between versions.
            try { Autodesk.AutoCAD.ApplicationServices.Core.Application.SetSystemVariable("HIGHLIGHT", 1); } catch { /* ignore */ }
            try { Autodesk.AutoCAD.ApplicationServices.Core.Application.SetSystemVariable("SELECTIONEFFECT", 1); } catch { /* ignore */ }
            try { Autodesk.AutoCAD.ApplicationServices.Core.Application.SetSystemVariable("SELECTIONPREVIEW", 3); } catch { /* ignore */ }
        }

        private static bool IsAutoCad2016()
        {
            try
            {
                // Only AutoCAD 2016: implied selection can be barely visible depending on profile/theme/GPU.
                // We add transient "bold boxes" only there.
                // On some APIs (net8) ReleaseMajorVersion is not available; use reflection and fallback to false.
                var current = HostApplicationServices.Current;
                var prop = current?.GetType()?.GetProperty("ReleaseMajorVersion");
                var raw = prop?.GetValue(current, null) as string;
                if (int.TryParse(raw, out var major))
                {
                    // AutoCAD 2016 is R20 (ReleaseMajorVersion == 20)
                    return major == 20;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        internal static void ClearDrawingHighlight() => ClearTransientHighlight();

        private static void ClearTransientHighlight()
        {
            lock (_transientGate)
            {
                if (_transientEntities.Count == 0)
                {
                    return;
                }

                try
                {
                    var tm = TransientManager.CurrentTransientManager;
                    foreach (var e in _transientEntities)
                    {
                        try
                        {
                            tm.EraseTransient(e, new IntegerCollection());
                        }
                        catch
                        {
                            // ignore
                        }

                        try { e.Dispose(); } catch { /* ignore */ }
                    }
                }
                finally
                {
                    _transientEntities.Clear();
                }
            }
        }

        private static void AddTransientBoxes(Transaction tr, ObjectId[] ids)
        {
            if (tr == null || ids == null || ids.Length == 0)
            {
                return;
            }

            var tm = TransientManager.CurrentTransientManager;
            var viewports = new IntegerCollection(); // empty => all viewports

            foreach (var id in ids)
            {
                Entity ent = null;
                try
                {
                    ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (ent == null)
                    {
                        continue;
                    }

                    // GeometricExtents can throw for some entity types.
                    Extents3d ex;
                    try
                    {
                        ex = ent.GeometricExtents;
                    }
                    catch
                    {
                        continue;
                    }

                    var box = CreateExtentsBox(ex);
                    if (box == null)
                    {
                        continue;
                    }

                    box.SetDatabaseDefaults();
                    // Extremely visible for AutoCAD 2016: bright + thick.
                    box.Color = Color.FromColorIndex(ColorMethod.ByAci, 2); // yellow
                    box.LineWeight = LineWeight.LineWeight211;

                    // AutoCAD 2016 transient modes can be finicky: try a couple of modes.
                    try { tm.AddTransient(box, TransientDrawingMode.DirectShortTerm, 0, viewports); }
                    catch { tm.AddTransient(box, TransientDrawingMode.Highlight, 0, viewports); }

                    lock (_transientGate)
                    {
                        _transientEntities.Add(box);
                    }
                }
                catch
                {
                    // ignore individual entity issues
                }
            }
        }

        private static Autodesk.AutoCAD.DatabaseServices.Polyline CreateExtentsBox(Extents3d ex)
        {
            try
            {
                var min = ex.MinPoint;
                var max = ex.MaxPoint;

                // Add small padding so the box is visible even for thin geometry.
                var dx = Math.Max(1.0, (max.X - min.X) * 0.03);
                var dy = Math.Max(1.0, (max.Y - min.Y) * 0.03);
                min = new Point3d(min.X - dx, min.Y - dy, min.Z);
                max = new Point3d(max.X + dx, max.Y + dy, max.Z);

                var pl = new Autodesk.AutoCAD.DatabaseServices.Polyline(5);
                pl.AddVertexAt(0, new Point2d(min.X, min.Y), 0, 0, 0);
                pl.AddVertexAt(1, new Point2d(max.X, min.Y), 0, 0, 0);
                pl.AddVertexAt(2, new Point2d(max.X, max.Y), 0, 0, 0);
                pl.AddVertexAt(3, new Point2d(min.X, max.Y), 0, 0, 0);
                pl.AddVertexAt(4, new Point2d(min.X, min.Y), 0, 0, 0);
                pl.Closed = true;
                return pl;
            }
            catch
            {
                return null;
            }
        }
    }
}
