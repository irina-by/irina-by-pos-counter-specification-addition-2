# Реализация: ColQty «Масса» + KV Ушко + дубль имён (_tex_fek)

Статус: **выполнено** (2026-06-09). План: `fix_header_colqty_bleed_bbe9dd13.plan.md`.

## Изменения

### ColQty / шапка (`TableGrid.cs`)

- `ScoreQtyHeader` — веса `кол.` +30, `кол` +20; **без** токена «ед»; штраф −50 для «масса», «обознач», «примеч».
- `SanitizeColQtyColumn` — repick ColQty, если заголовок похож на «Масса».
- ~~`TryGetHeaderTopTextBandY` 35% для малых таблиц~~ → **заменено** планом `unified_grid_header_scan`: `ApplyHeaderBoundaryFromGridScan` + `DetectHeaderByGridRows` (единый алгоритм по строкам сетки).
- `DetectHeaderByTopTextBand` — last-resort fallback; тексты с `TryParseMarkKey` и `Row >= HeaderEndRow` не участвуют в scoring.
- `HeaderEndRow = min(HeaderEndRow, RowDataStart)` после `AlignRowDataStartToFirstMark`.
- `BuildHeaderOnlyColumnText` — только строки `r < RowDataStart` (или `HeaderEndRow`).

### Имена / dedupe (`TableGrid.cs`, `CellIndex.cs`)

- `CollectNamePartsFromCellText` — `TryAddNamePartExact` (dedupe по merge-блоку).
- `ResolveNameForKey` — cell-only path при `cellJoined.Length ≥ 20`; иначе AllTexts с `FilterTextPartsNotInCellText`.
- `CollapseDuplicateNamePhrase` — «A A» → «A».
- KV fallback: `CellText[rowMark, ColName]`, `ResolveNameFromNeighborColumns` (col ±1).
- `IsDuplicateCandidate` — near-overlap (4× eps) для ColName MText+MText.

### CMD (`SpecGridService.cs`)

- `[POSC] Марок в данных по столбцам: col0=N…`
- `[POSC] KeyToRowMark: 1→row…`

## Сборка

`build\build-ac2026.cmd` — OK (0 errors), `dll 2026\PosCounter.Net.dll`.

## Ручной тест

| Чертёж | Ожидание |
|--------|----------|
| **Ушко** | ColQty = «Кол.»; марки 1–4 с именами; `[KV-PAIR] key=1` |
| **_tex_fek mark 64** | одно «Кран шаровой…» без повтора; 2 строки палитры (разные слои) — OK |
| **35NK** | шапка без регрессии |

NETLOAD: `PosCounter.Net\bin\Release\net8.0-windows\PosCounter.Net.dll`.
