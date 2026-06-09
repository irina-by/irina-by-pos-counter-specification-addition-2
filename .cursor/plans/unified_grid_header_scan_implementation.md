# Реализация: унификация шапки по строкам сетки

Статус: **выполнено** (2026-06-09). План: `unified_grid_header_scan_88e10436.plan.md`.

## Изменения

### Убрано

- `SmallTableHeaderBandFraction` и ветка «малая/большая таблица» в `TryGetHeaderTopTextBandY`.

### Добавлено (`TableGrid.cs`)

- `ApplyHeaderBoundaryFromGridScan` / `FindFirstDataRowByGridScan` — скан строк 0..N до первой data-строки (марка или наименование+qty).
- `DetectHeaderByGridRows` — primary: scoring только по `CellText` строк шапки.
- `DetectHeader` порядок: grid rows → columns → top-band (last-resort).
- Top-band fallback: фильтр `Row < HeaderEndRow`, skip mark keys.

### CMD (`SpecGridService.cs`)

- `[POSC] Граница шапки/данных: HeaderEndRow=… RowDataStart=…`

## Сборка

`build\build-ac2026.cmd`

## Ручной тест

Ушко, _tex_fek mark 64, 35NK — NETLOAD свежей DLL.
