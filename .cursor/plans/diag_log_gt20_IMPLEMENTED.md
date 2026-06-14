# diag_log_gt20 — реализовано (2026-06-13)

Источник: `диагноз_лога_gt-20_00c5083c.plan.md` (не редактировать).

## Диагноз по логу build=2026-06-13 11:41

- WriteQty: 49, пропущено=0 — запись «Кол.» работает.
- Missing qty (35, 43, 46, 49, 59, 97, 99, 102, 111) — нет в палитре после ЗАПУСТИТЬ, не баг WriteQty.
- Пустые имена: 46, 52–57, 99, 105–109 (13 из 58 марок).

## Правки

| Файл | Изменение |
|------|-----------|
| `TableGrid.cs` | `AssignUnassignedTextsToNameColumn` — DataX в ColName → Row/Col |
| `TableGrid.cs` | Вызов после `AssignCellsData` и в `RebindScopeKeysAndNames` + rebuild `CellText` |
| `TableGrid.cs` | `ResolveContinuationNameRowEnd`, `NextMarkHasStandaloneNameLead` |
| `TableGrid.cs` | `ShouldLogNameRejectReason`, `[NAME] skip=…` в `AddNamePartsFromTextSample` |
| `SpecGridService.cs` | `ReportUnassignedTextsDiagnostic` — `unassigned→name` |
| `docs/DEVELOPER.md` | §20 missing qty vs empty name |

## Чеклист AutoCAD

- [ ] `[POSC-DIAG] unassigned→name col=2 row=… «Ниппель…»` (табл. 2)
- [ ] `[NAME] skip=designation` для key=52 (если текст — ГОСТ)
- [ ] Имена в палитру ≥ 52 (было 45)
- [ ] WriteQty=49 без регрессии
- [ ] Missing qty 9 марок — только после повторного ЗАПУСТИТЬ
