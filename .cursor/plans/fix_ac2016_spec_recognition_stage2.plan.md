---
name: Fix AC2016 spec recognition stage 2
overview: "Этап 2: fallback ColQty для AC 2016 (simple01, allTexts, numeric), геометрия ExtentsTop, Rebind не оставляет ColQty=-1."
todos:
  - id: numeric-colqty-fallback
    content: "TableGrid: TryInferColQtyFromNumericColumn + вызов при ColQty<0"
    status: completed
  - id: alltexts-colqty
    content: "TableGrid: DetectColQtyFromAllTexts + TryGetHeaderRegionY"
    status: completed
  - id: simple-rows01
    content: "TableGrid: DetectHeaderSimpleRows01 (OLD строки 0–1)"
    status: completed
  - id: header-geometry-fallback
    content: "TableGrid: AssignCellsHeader ExtentsTop fallback; MText/DBText HeaderY=top"
    status: completed
  - id: rebind-colqty
    content: "TableGrid: RebindScopeKeysAndNames + TryResolveMissingColQty"
    status: completed
  - id: build-test-docs
    content: "Документация; сборка на ПК с AC 2016"
    status: completed
isProject: false
---

# AC 2016: этап 2 — исправление «Кол.» (выполнено)

## Диагноз по логу пользователя

- Сборка net46 корректна (та же DLL в AC 2026 работает).
- На AC 2016: pass1 шапка -1/-1/-1, inference находит Mark/Name, ColQty=-1.
- Top-band не видит текст «Кол.»; col4 в данных — числовой столбец количества.

## Реализовано

| Изменение | Файл |
|-----------|------|
| `TryResolveMissingColQty` (simple01 → allTexts → numeric) | `TableGrid.cs` |
| `DetectHeaderSimpleRows01` | `TableGrid.cs` |
| `DetectColQtyFromAllTexts` + `TryGetHeaderRegionY` | `TableGrid.cs` |
| `TryInferColQtyFromNumericColumn` | `TableGrid.cs` |
| `AssignCellsHeader` fallback DataY | `TableGrid.cs` |
| MText/DBText HeaderY = верх bbox | `TableGrid.cs` |
| `ColQtySource` в `ScopeGridResult` + CMD ИТОГ | `TableGrid.cs`, `SpecGridService.cs` |
| `RebindScopeKeysAndNames` вызывает fallback при ColQty<0 | `TableGrid.cs` |

## Проверка инженером

1. `build\build-ac2016.cmd` → `dll 2016\`
2. AC 2016: NETLOAD → ЗАПУСТИТЬ → Выбрать спецификацию
3. Ожидаемо: `ColQty=4 источник=numeric` (или simple01/allTexts), `WriteQty итог: записано>0`
4. AC 2026 с `dll 2016\`: регрессия Ушко/35NK

## Критерий готовности

- AC 2016: ColQty>=0, WriteQty>0 на чертеже пользователя
- AC 2026: без регрессии ColQty
