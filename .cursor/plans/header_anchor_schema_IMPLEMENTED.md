# header_anchor_schema — реализовано (2026-06-12)

План-источник: `header_anchor_schema.plan.md` (не редактировался).

## Изменения

- `PosCounter.Net/SpecGrid/SpecColumnSchema.cs` — эталон столбцов
- `SpecGridSession.ColumnSchema` — сессия якоря
- `TableGrid.TryLockColumnSchema` — таблица 1, pass1 шапка
- `TableGrid.TryApplyInheritedColumnSchema` — продолжения
- `RebindScopeKeysAndNames` — skip DetectHeader при `ColumnsInheritedFromSchema`
- `SpecGridService` — цикл scope0 lock / scope1+ inherit / fallback infer
- `ProcessInferColumnsFallback` — вынесен старый infer-путь

## Дополнение (fix_schema_inherit_table2, 2026-06-12)

- `TryAlignScopeColumnsToAnchorSchema` — выравнивание по X-полосам при смещении таблицы на чертеже
- `inherit-fail reason=` + fallback infer-data; предупреждение о малой рамке продолжения
- См. `.cursor/plans/fix_schema_inherit_table2_IMPLEMENTED.md`

## Чеклист AutoCAD

- [ ] Таблица 1 со шапкой → `[SCHEMA] locked`
- [ ] Таблица 2 продолжение (полная рамка ~214 obj) → `[SCHEMA] inherited` или `aligned`, ColQty=3, WriteQty>0
- [ ] Таблица 2 фрагмент (~59 obj) → «рамка слишком мала» + fallback infer
- [ ] Таблица 1 без шапки + таблица 2 → предупреждение в CMD
- [ ] Одна таблица-продолжение (без якоря) → старый inference (регрессия)
