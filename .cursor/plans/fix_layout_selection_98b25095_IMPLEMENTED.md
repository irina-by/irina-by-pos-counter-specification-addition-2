# Реализовано: fix_layout_selection (лист + СПДС proxy)

План: `fix_layout_selection_98b25095.plan.md` (файл плана не менялся).

## Сделано

| ID | Изменение |
|----|-----------|
| snapshot-pickfirst | `PaletteHost.TrySnapshotPickFirst`, `HasPendingPickFirst`; `BtnRun.PreviewMouseDown`; повтор в `POSC2_RUN_INTERNAL` |
| diag-pick-cmd | `[POSC-DIAG] pick ctab cvport count sample` в `PosCounterEngine.ReportPickDiagnostic` |
| gate-safe-fallback | `BuildIndex` в try/catch; `DispatchApplyRunResult`; ошибки в CMD и палитре |
| fix-status-empty-selection | `ApplyRunResult`: выделение + 0 марок; `StatusHint` для proxy/ошибок |
| c4-standalone-circle | Убран blanket skip C4 по кругу — позиция в круге на плане считается |
| spds-proxy-explode | `ProxyEntityHelper.cs`; Engine, `CalloutMarkGate`, `TableGridBuilder` |
| spds-diag-status | `[POSC-DIAG] proxy picked=…`; статус PROXYGRAPHICS |
| docs | `DEVELOPER.md` §4, `INSTRUCTION_ENGINEER.md`, `Работа программы.md`, `DIALOGUE_LOG.md` |

## Проверка

1. `build-ac2016.cmd` → NETLOAD.
2. Лист + viewport MODEL + рамка → `pick count>0`.
3. DWG СПДС + PROXYGRAPHICS=1 → `proxy exploded=ok`.
4. Прислать CMD `[POSC-DIAG] pick` + `proxy`.
