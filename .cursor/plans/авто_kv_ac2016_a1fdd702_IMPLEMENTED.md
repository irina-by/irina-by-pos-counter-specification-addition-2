# Реализовано: Авто KV AC2016 (авто_kv_ac2016_a1fdd702)

Дата: 2026-06-13. План-источник: `авто_kv_ac2016_a1fdd702.plan.md` (не редактировать).

## Выполнено

| Todo | Файлы | Суть |
|------|-------|------|
| mark-key-parser | `MarkKeyParser.cs`, `MTextPlainText.cs` | Префиксы как Engine; `TryParseMarkKey` делегирует |
| header-grid-scan | `TableGrid.cs` | `DetectHeaderBoundaryAndColumns` 0..5; topBand last-resort |
| name-pick-best | `TableGrid.cs` | `cellOnly = len≥20 && AllNameRowsHaveCellText`; `PickBestNameTextForRow` |
| bleed-continue | `TableGrid.cs` | NAME-STOP → skip row; `IsUpstreamBleedFromForeignMark` в фильтре |
| ac2016-dual-coords | `TableGrid.cs` | MText DataY=ExtentsTop; DBText AlignmentPoint; `[GEO]` |
| colqty-evidence | `TableGrid.cs` | Убран `ApplyMandatoryColQtyLayout`; `CanLock` без 0/2/3 |
| diag-policy | `SpecDiagPolicy.cs` | Бюджеты trace; нет `key==N` |
| diag-logs-cmd | `SpecGridService.cs`, `TableGrid.cs` | `[HEADER-SCAN][MARK][GEO][NAME][KV-SUMMARY]` |
| docs-test | `DEVELOPER.md` §21–22, `INSTRUCTION_ENGINEER.md` §8.2, `DIALOGUE_LOG.md` | |

## Приёмка на AC 2016

1. `build-ac2016.cmd` / VS Release x64 net452 → NETLOAD.
2. Тот же DWG (build 11:41): `[KV-SUMMARY]`, `headerPath=gridTokens`, WriteQty=49.
3. `grep SpecGrid`: нет `key == 52`, `ColMark != 0`, mandatory 0/2/3.

## Не проверено на агенте

Сборка требует AutoCAD 2016 SDK на ПК инженера.
