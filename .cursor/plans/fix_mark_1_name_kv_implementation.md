# Реализация: универсальный key/value — имя с верхней строки марки

Статус: **выполнено** (2026-06-09). План: `fix_mark_1_name_kv_d2b8006b.plan.md`.

## Изменения

- `ResolveNameForKey` — единая точка для LINE, native Table, N scopes.
- `ResolveNameRowTopForKey` — cap ≥ HeaderEndRow / RowDataStart, skip секций.
- `IsSectionHeaderRow` — порог имени 25 → 8.
- `CollectNamePartsFromCellText` + `MergeNamePartsPreferCellText`.
- `BindKeys` / native bind — capped `KeyToRowTopSub`.
- `MergeScopeNames` — `MarkNamePairs` → `ResolveNameForKey`.
- CMD: `[KV-ANCHOR]` при пустом имени.

## Сборка

`build\build-ac2026.cmd` — OK.

## Ручной тест

Ушко LINE+MText (марка 1), секция без марки, 35NK, _tex_fek, native Table.
