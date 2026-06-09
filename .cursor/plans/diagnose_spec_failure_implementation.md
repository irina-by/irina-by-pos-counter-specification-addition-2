# Реализация: LINE-таблицы + явная AutoCAD Table

Статус: **выполнено** (2026-06-09). Исходный план: `diagnose_spec_failure_8c43e6d0.plan.md` (не редактировался).

## Фаза 1 — LINE (fix C)

- `ResolveDataYCutoff` — `GridYs[RowDataStart]` / `GridYs[HeaderEndRow]`, не maxY−2000.
- `IsBindableDataText` — основной фильтр `Row >= RowDataStart`.
- `BindKeysFromProperties`, `CountDataMarkKeysInColumn` — через `IsBindableDataText`.
- `GetCellText(..., preferMarkColumn)` — приоритет цифрам в ColMark.
- Bleed filter: длинный текст не из ColMark (>4 символов) пропускается.

## Фаза 2 — AutoCAD Table

- Детект в начале `Build`: Table без Line → `BuildFromAcadTable`; Mixed → WARN + LINE.
- Поля `ScopeGridResult`: `IsNativeAcadTable`, `NativeTableId`, `MixedTableLineSelection`.
- Запись qty: `UpsertQtyInAcadTable` — замена только числа в ячейке.
- CMD: native table info, mixed selection, точные сообщения при пустых марках.

## Сборка

`build\build-ac2026.cmd` — OK, `dll 2026\PosCounter.Net.dll`.

## Ручной тест (инженер)

| Чертёж | Проверка |
|--------|----------|
| Ушко (LINE) | col0=4, KeyToRowMark 1–4, «Кол.» после ЗАПУСТИТЬ |
| _tex_fek, 35NK | без регрессии имён/шапки |
| Явная Table | key/value, qty в ячейку Table |
| Table «5 шт.» | только число заменено |
| Mixed Table+Line | WARN, LINE path |
