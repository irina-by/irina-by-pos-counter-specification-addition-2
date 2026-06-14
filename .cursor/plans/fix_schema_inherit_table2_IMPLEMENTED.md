# fix_schema_inherit_table2 — реализовано (2026-06-12)

План-источник: `fix_schema_inherit_table2_63fc7ffa.plan.md` (не редактировался).

## Проблема

Таблица 1: `[SCHEMA] locked`, 552 объекта — OK.  
Таблица 2: 59 объектов (фрагмент), `TryApplyInheritedColumnSchema` отказ — жёсткое сравнение `GridXs[i]` на одном индексе при смещении таблицы на чертеже (X≈112k vs X≈134k).

## Изменения

- `TableGrid.TryAlignScopeColumnsToAnchorSchema` — центры X-полос якоря + offset левого края → индексы столбцов в scope
- `TableGrid.TryApplyInheritedColumnSchema` — выравнивание вместо сравнения абсолютных X; `out failReason`; лог `inherited` / `aligned … dx=`
- `SpecGridService` — `[SCHEMA] inherit-fail reason=…`; `[POSC] Рамка продолжения слишком мала (N объектов)`; fallback `inherit-fail → fallback infer-data`
- `docs/INSTRUCTION_ENGINEER.md`, `docs/DEVELOPER.md`, `.cursor/DIALOGUE_LOG.md`

## Чеклист AutoCAD

- [ ] Таблица 1 со шапкой (~552 obj) → `[SCHEMA] locked`
- [ ] Таблица 2 **полная** (~214 obj) → `[SCHEMA] inherited` или `aligned` + имена + WriteQty
- [ ] Таблица 2 **59 obj** → «рамка слишком мала» + `inherit-fail` + fallback infer при достаточной сетке
- [ ] Одна таблица-продолжение без якоря → старый inference (регрессия)
