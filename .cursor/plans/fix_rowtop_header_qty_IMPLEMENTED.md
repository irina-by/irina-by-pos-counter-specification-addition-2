# fix_rowtop_header_qty — реализовано

План: `fix_rowtop_header_qty_18970ac5.plan.md` (Cursor plans).

## Изменения

| Файл | Что сделано |
|------|-------------|
| `TableGrid.cs` | `ResolveNameRowTopForKey` — не `rowTop++`, если ColName непустой над цифрой; trace `rowTopRaw/rowTop` |
| `TableGrid.cs` | `AlignRowDataStartToFirstMark` — `min(KeyToRowTopSub)` вместо `min(KeyToRowMark)` |
| `TableGrid.cs` | `FindHeaderEndRowByHorizontalBorders` — 2-я полноширинная H-линия; подключено в `EstimateHeaderEndRow`, `FindFirstDataRowAfterHeaderBoundary`, `DetectHeaderBoundaryAndColumns` |
| `SpecGridService.cs` | `[WRITEQTY]` diag: `rowBottomEx`, `merged` для sample keys |
| `docs/DEVELOPER.md` | §11, §22 — rowTop, H-line boundary, RowDataStart |

grep `SpecGrid`: нет `key ==`.

## Ручная проверка (ожидает инженера)

Универсальные паттерны на 2+ типах спецификаций:

| Паттерн | OK |
|---------|-----|
| merged N-line: первая строка ColName в палитре | |
| `rowTop < rowMark` при цифре ниже первой строки имени | |
| bilingual шапка: `hLineBoundary` в CMD | |
| qty в верхней суб-ячейке ColQty при 3+ строках блока | |

Сборка: `PosCounter.Net\build\build-ac2016.cmd` → NETLOAD.
