# fix_first_name_line_skip — реализовано

План: `fix_first_name_line_skip_cade9350.plan.md` (Cursor plans).

Дополнение: `fix_rowtop_header_qty_18970ac5.plan.md` — см. `.cursor/plans/fix_rowtop_header_qty_IMPLEMENTED.md`.

## Изменения (часть 1)

| Файл | Что сделано |
|------|-------------|
| `PosCounter.Net/SpecGrid/TableGrid.cs` | `IsNameContinuationRow`: диапазон `[rowTop, blockEnd)`; строки имени **над** цифрой марки (`rowTop ≤ row < rowMark`) — continuation при непустом ColName |
| `PosCounter.Net/SpecGrid/TableGrid.cs` | `LogNameSectionRowSkip` — `[NAME] skip=section-row` с `rowTop`, `rowMark`, `blockEnd` |
| `docs/DEVELOPER.md` | §11, §22 — описание диапазона и симптома |

## Ручная проверка (универсальные паттерны)

- merged N-line: все N строк ColName в палитре, `parts ≥ N`
- bilingual шапка: первая строка данных не пропущена
- qty в верхней суб-ячейке ColQty при `markBlockEnd > rowTop + 2`

CMD: `rowTop < rowMark` при merged; `[HEADER-SCAN] hLineBoundary=…`; нет ложного `skip=section-row` на `r=rowTop`.

Сборка: `PosCounter.Net\build\build-ac2016.cmd` → NETLOAD.
