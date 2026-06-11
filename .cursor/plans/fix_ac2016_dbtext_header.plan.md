---
name: AC2016 DBText header
overview: "Этап 3 реализован: DBText шапка по GridYs, координаты AlignmentPoint, numeric ColQty по AllTexts, диагностика fallback, баннер build=."
status: implemented
date: 2026-06-10
---

# AC 2016: шапка DBText + ColQty (этап 3) — ВЫПОЛНЕНО

## Реализовано

| Пункт | Файл | Статус |
|-------|------|--------|
| Баннер `build=` при NETLOAD | `Commands.cs` | ✅ |
| `DetectHeaderByDbTextHeaderBand` | `TableGrid.cs` | ✅ |
| DBText Header=AlignmentPoint, Data=ExtentsTop | `TableGrid.cs` | ✅ |
| Numeric ColQty по AllTexts | `TableGrid.cs` | ✅ |
| `[POSC-DIAG]` fallback + dbText band | `TableGrid.cs`, `SpecGridService.cs` | ✅ |
| Документация | `DEVELOPER.md`, `INSTRUCTION_ENGINEER.md`, `DIALOGUE_LOG.md` | ✅ |

## Критерий успеха (проверка инженером в AC 2016)

1. `build-ac2016.cmd` → NETLOAD из `dll 2016\` (оба DLL).
2. CMD: `[POSC] … (net452) build=…` — свежая дата.
3. После «Выбрать спецификацию»:
   - `[POSC-DIAG] Таблица N ColQty=4 источник=dbTextBand` или `numeric`
   - `[POSC-DIAG] WriteQty итог: записано > 0`
4. На чертеже заполнен столбец «Кол.»; имена в палитре без регрессии.

## Порядок DetectHeader (актуальный)

1. `DetectHeaderByGridRows`
2. `DetectHeaderByColumns`
3. **`DetectHeaderByDbTextHeaderBand`** (новое)
4. `DetectHeaderByTopTextBand`
5. `DetectHeaderSimpleRows01`

При `ColQty=-1` после inference: `TryResolveMissingColQty` (simple01 → allTexts → numeric).

## LISP (без кода)

Убрать `(load "pos_counter_2016_2026")` из `acaddoc.lsp` / `acad2016.lsp`.
