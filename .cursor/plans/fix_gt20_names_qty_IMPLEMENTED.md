# fix_gt20_names_qty — реализовано (2026-06-11)

План-источник: `fix_gt20_names_qty_85bae939.plan.md` (не редактировался).

## Изменения в коде

### 1. ColQty — нет раннего numeric col=0
- `TableGrid.RebindScopeKeysAndNames`: `TryResolveMissingColQty` только при `ColMark≥0 && ColName≥0`.
- `TryInferColQtyFromNumericColumn`: пропуск столбца с `CountDataMarkKeysInColumn ≥ MinDataMarkKeysForColMark`.

### 2. WriteQty — верхняя суб-ячейка
- `FindQtyTextInCell`: для merged-блока поиск только в полосе `rowTop..rowTop+1`.
- `UpsertQtyText`: при `|entY - targetY| > halfRowStep` → `[WRITEQTY] skip-far-entY` и создание нового DBText.
- `ResolveHalfRowStepY` — половина шага строки для порога.

### 3. Имена ГТ-20 — без bleed
- `ResolveNameForKey`: `useCellTextFromBlock` — cell-only при достаточном `cellJoined` или RU на rowTop + multi-row блок.
- `SupplementNamePartsInVerticalBand` не вызывается при `cellJoined ≥ CellTextOnlyNameMinLength`.
- В supplement: отсев текста ниже `GridYs[nextKeyTop]`.

### 4. CMD шапка
- `DescribeHeaderColumn`: при `ColumnsInferredFromData` — «— (продолжение)», без данных первой строки.

### 5. Диагностика имён
- `[NAME]` для key 1–7, 52–57, 105–109; метка `empty` для пустых.

## Чеклист проверки в AutoCAD

**Русские таблицы-продолжения (регрессия):**
- [ ] `ColQty=3 источник=layout`, `WriteQty итог: записано>0`
- [ ] Нет `[COLQTY] resolved numeric col=0` до inference

**ГТ-20 (7 марок):**
- [ ] Палитра марка 1: RU + EN
- [ ] Марки 4–5: без текста соседних марок
- [ ] `[NAME] key=4,5,6` в CMD
- [ ] col3: qty 1,5,1,1,4,1 на первой суб-строке
- [ ] `[WRITEQTY] key=1 qty=1 targetY=…` (или `skip-far-entY` + `create`)

**Сборка:** Yandex → VS Release x64 net452 → NETLOAD → `build=` в первой строке `[POSC-DIAG]`.
