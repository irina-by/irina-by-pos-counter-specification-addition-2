# PosCounter.Net — техническая документация (факт по коду)

**Версия:** 4.2.0-table-grid-lines  
**Сборки:** `dll 2016` (net46), `dll 2026` (net8.0-windows)  
**Дата актуализации:** 2026-06-09 (многострочное наименование, grid scan шапки, ResolveNameForKey, fix row1, native Table)

---

## 1. Назначение программы

AutoCAD-плагин из двух модулей:

1. **Подсчёт выносок** — `PosCounterEngine`: TEXT/MTEXT/атрибуты → палитра (Марка, Количество, Слой).
2. **Спецификация** — `TableGridBuilder` + `SpecGridService`: таблица из LINE → ключ (марка) + значение (наименование) → палитра; запись **только «Кол.»** на чертёж.

Связь модулей: **номер марки (Key)**.

---

## 2. Структура проекта

```
PosCounter.Net/
  Commands.cs              — NETLOAD, POSC, POSC2_* 
  PaletteHost.cs           — палитра WPF, очереди команд
  UI/PosCounterControl.*   — интерфейс палитры
  Engine/PosCounterEngine.cs   — подсчёт (LOCK — не менять)
  SpecGrid/
    TableGrid.cs           — сетка, шапка, KV key/value
    SpecGridService.cs     — pick, qty writeback
    CellIndex.cs           — привязка к ячейкам
    MTextPlainText.cs      — текст, марка, имя
    SpecGridSession.cs     — сессия scope'ов
    SpecGridLog.cs         — лог CMD
  Services/ExportService.cs
  Models/PosModels.cs, PosRow.cs
  State/PosSettingsStore.cs
```

---

## 3. Команды AutoCAD

| Команда | Файл | Назначение |
|---------|------|------------|
| *(NETLOAD)* | `Commands.Initialize` | подписка Idle → авто-палитра |
| `POSC` | `PosCounterCommand` | показать палитру |
| `POSC2_RUN_INTERNAL` | `PosCounterRunInternal` | подсчёт → UI |
| `POSC2_SPEC_INTERNAL` | `PosCounterSpecInternal` | spec + qty + имена |
| `POSC2_HIGHLIGHT_INTERNAL` | `PosCounterHighlightInternal` | подсветка handles |

Флаги служебных команд: `NoHistory | NoActionRecording | Session`.

---

## 4. Модуль подсчёта — `PosCounterEngine`

**Ограничение:** PALETTE-COUNT-LOCK — не менять без отдельного ТЗ.

### Публичные методы

| Метод | Вход | Выход |
|-------|------|-------|
| `Count(countAllInModel)` | bool | `List<PosRow>` |
| `CountWithInfo(countAllInModel, extractNumbersOnly)` | bool, bool | `PosCountResult` |

### Условия источника данных

| Условие | Поведение |
|---------|-----------|
| Есть pick-first / выделение | только выделенные объекты |
| Галочка «Все объекты в модели» | ModelSpace или viewport polygon |
| Иначе | пустой результат |

### Ключевые внутренние шаги

- `ProcessEntity` / `ProcessBlockReference` — рекурсия блоков.
- `ProcessTextValue` → `ExtractPositionNumber` — марка 1..10000, приставки «Поз.» и т.д.
- `Accumulator.Increment(layer, text)` — группировка количества.
- `MTextPlainText.ResolveLayer` — слой объекта / блока.

**Не обрабатывается:** MLeader, proxy СПДС.

---

## 5. Модуль спецификации — orchestration

### `SpecGridService.RunSelectSpecification`

1. `TryPickAllSpecificationTables` — N рамок.
2. `TableGridBuilder.Build(i, ids, tr, sharedGridLayer, log)` для каждой.
3. `MergeScopeNames` + `BuildCombinedMarkNames` → UI.
4. `WriteQtyInTransaction` → `WriteQtyScope` → `UpsertQtyText`.

### `SpecGridSession`

- `Scopes` — список `ScopeGridResult`.
- `SharedGridLayer` — общий слой сетки для 2+ таблиц на листе.

---

## 6. `ScopeGridResult` — поля состояния

| Поле | Назначение |
|------|------------|
| `GridXs`, `GridYs` | оси сетки (Y сверху вниз) |
| `ColMark`, `ColName`, `ColQty` | индексы столбцов (-1 = не найден) |
| `HeaderEndRow` | exclusive граница шапки |
| `RowDataStart`, `RowDataEnd` | диапазон строк данных |
| `CellText[row,col]` | матрица текста ячеек |
| `AllTexts` | все `TextSample` из рамки |
| `KeyToRowMark` | key → строка марки |
| `RowToKeyMark` | row → key |
| `KeyToRowTopSub` | верх merged-блока марки |
| `KeyToMarkBlockEnd` | низ блока марки (exclusive) |
| `MarkNamePairs` | key → итоговое имя |
| `PrimaryNameLayer`, `ExtraNameLayers` | слои ColName |
| `AllowedTableTextLayers`, `ExcludedAnnotationLayers` | фильтр слоёв |
| `HeaderTopBandLo/Hi`, `HeaderDetectedByTopTextBand` | шапка по текстам (fallback) |
| `TextsByRow` | кэш ColName по Row (pass-2) |
| `IsNativeAcadTable`, `NativeTableId` | явная AutoCAD Table |
| `MixedTableLineSelection` | Table+Line в одной рамке → LINE path |

---

## 7. `TextSample` — координаты и pass'ы

| Поле | Когда заполняется | Значение |
|------|-------------------|----------|
| `HeaderX`, `HeaderY` | pass-1 | центр GeometricExtents |
| `DataX`, `DataY` | pass-2 | DBText: точка; MText: X=Location.X, **Y=YMax** |
| `YMin`, `YMax` | оба | экстент |
| `Row`, `Col` | pass-1: header; pass-2: data | ячейка сетки |
| `DominantRow` | pass-2 | строка max overlap экстента |
| `SourceIndex` | создание | индекс в AllTexts |
| `Raw`, `Plain` | создание | Contents / sanitized |

---

## 8. `TableGridBuilder.Build()` — порядок шагов

### Выбор пути (начало Build)

| Условие выборки | Путь |
|-----------------|------|
| есть `Table`, нет `Line` | **`BuildFromAcadTable`** |
| есть `Line` (с Table или без) | LINE path (см. ниже) |
| Table + Line вместе | WARN `[POSC] Mixed selection…`, LINE path |

### Путь AutoCAD Table — `BuildFromAcadTable`

1. `CellText[r,c]` ← `table.Cells[r,c].TextString`.
2. `DetectHeaderFromCellMatrix` — те же `ScoreHeader`, `EnsureUniqueHeaderColumns`, `RefineColMarkByDataMarks`.
3. `RowDataStart` — первая строка с `TryParseMarkKey` в ColMark.
4. `BindKeysFromAcadTableCellMatrix` — KeyToRowMark по индексу строки (без Y-cutoff, без dual-pass).
5. Merged cells — `Cells[r,c].GetMergeRange()` → `KeyToRowTopSub`.
6. `FillMarkNamesFromAcadTableCells` — имена только из CellText.
7. `IsNativeAcadTable=true`, `NativeTableId`.

**Не вызываются:** `AssignCellsData`, `SplitNameColumnRowsData`, `BindKeysFromProperties`, `FillMarkNamesFromMergeGroups`.

**Запись «Кол.»:** `SpecGridService.UpsertQtyInAcadTable` — только ячейка Table; число заменяется в «5 шт.» → «12 шт.».

### Фаза A — LINE path: сбор и сетка

1. Чтение LINE → `GridLineSeg`; TEXT/MTEXT → `TextSample`.
2. `AutoDetectGridLayer` — слой ≥30% кандидатов (`MinGridLineLen=5000` для выбора слоя).
3. `BuildMergedGridAxes` — `ClusterAxis`, merge mixed layers, Y desc.
4. Проверка `rows*cols <= MaxCells`.

### Фаза B — pass 1 (шапка)

5. `AssignCellsHeader` — Row/Col по HeaderX/Y.
6. `BuildCellMatrix(filterTableLayers: false)`.
7. `EstimateHeaderEndRow(filteredH)`.
8. **`ApplyHeaderBoundaryFromGridScan`** — первая data-строка по скану CellText (марка / наименование+qty).
9. `DetectHeader` (pass1) → **`DetectHeaderByGridRows`** → fallback `DetectHeaderByColumns` → last-resort top-band.
10. pass2 `AssignCellsData` → `CellText` → **`RebindScopeKeysAndNames`** (повторный `DetectHeader` + `BindKeys`).
11. При `ColMark<0` или `ColName<0`: **`TryInferColumnsFromData`** (пересечение с `qtyByKey` палитры) → rebind.
10. `ComputeRowDataStart(horiz: null)` — `searchFrom` из grid scan (`RowDataStart`), **без** принудительного min row 2; `ClampRowDataStartToGridScan`.
11. `BuildPrimaryNameLayer`, `BuildTableContentLayers`.
12. `MedianRowStep`, `PrimaryNameTextHeight`.

### Фаза C — pass 2 (данные + KV)

13. `AssignCellsData` — Row по точке DataX/Y; DominantRow.
14. `SplitNameColumnRowsData` — разнос NAME по строкам (DataY).
15. `BuildTextsByRow`.
16. `BuildCellMatrix(filterTableLayers: true)`.
17. **`RebindScopeKeysAndNames`** — повторный grid scan + `DetectHeader` + `BindKeys` на pass2 CellText.
18. **`FillMarkNamesFromMergeGroups`** — значение.
19. В `RunSelectSpecification`: при провале шапки — **`TryInferColumnsFromData`** (overlap с палитрой) → rebind.

---

## 9. Распознавание шапки — методы и условия

### Основной путь

| Метод | Условие срабатывания | Результат |
|-------|----------------------|-----------|
| `TryGetHeaderTopTextBandY` | есть тексты (fallback CMD) | полоса maxY−2000..maxY — **одинаково для всех таблиц** |
| `ApplyHeaderBoundaryFromGridScan` | после pass-1 CellText | скан строк 0..N: первая строка с маркой или наименованием-данными → `HeaderEndRow`/`RowDataStart` |
| `RebindScopeKeysAndNames` | pass2 | `ApplyHeaderBoundaryFromGridScan` + `DetectHeader` + `BindKeys` на data CellText |
| `TryInferColumnsFromData` | fallback | ColMark/ColName по данным ячеек + overlap с палитрой; `ColumnsInferredFromData` |
| `DetectHeaderByGridRows` | primary | успех при `ColMark≥0 && ColName≥0` (ColQty опционально для имён) |
| `DetectHeaderByColumns` | fallback | `BuildHeaderTextForColumn` + GridYs |
| `DetectHeaderByTopTextBand` | last-resort | Y-полоса + фильтр `Row < HeaderEndRow`, без цифр-марок |
| `ScoreQtyHeader` | scoring «Кол.» | `кол.` +30, `кол` +20; **без** «ед»; штраф −50 для «масса»/«обознач»/«примеч» |
| `SanitizeColQtyColumn` | ColQty похож на «Масса» | repick ColQty по qtyScores |
| `EnsureUniqueHeaderColumns` | после detect | уникальные роли столбцов |
| `RefineColMarkByDataMarks` | ColMark < 2 марок в data | смена ColMark по цифрам |
| `FindFirstDataRowAfterHeaderBoundary` | fallback searchFrom | `ResolveHeaderEndRow` или H-line; **без** min row 2 |
| `ComputeRowDataStart` | после DetectHeader | `searchFrom = RowDataStart` из grid scan; `ClampRowDataStartToGridScan` |
| `FormatMissingKeyOneDiagnostic` | CMD | марка 1 в CellText, но не в KeyToRowMark |

### Fallback

| Метод | Когда |
|-------|-------|
| `DetectHeaderByColumns` | ColMark<0 или ColQty<0 после grid rows |
| `CollectHeaderTextForColumn` | проходы A (Row/Col), B (geom), C (CellText) |
| `PickBestHeaderColumn` | score ≥ MinHeaderScore(10) |

### Токены score

- Марка: поз, п/п, №, номер…
- Наименование: наимен, назван…
- Кол.: **`ScoreQtyHeader`** — кол., кол-во, qty; **не** «ед» (ложное совпадение с «Масса **ед**., кг»)

**CMD:** `ReportDetectedHeader` (+ `[POSC] Марок в данных по столбцам`, `[POSC] KeyToRowMark`), `BuildHeaderTopBandDiagnostic`, `BuildHeaderExtendedDiagnostic`.

---

## 10. Ключ (марка) — `BindKeysFromProperties` (LINE path)

**Условия включения текста в кандидаты (`IsBindableDataText`):**

- `t.Row >= 0`, не `IsSectionHeaderRow`.
- **Основной фильтр:** `t.Row >= RowDataStart` (если `RowDataStart > 0`).
- **Запасной:** `DataY < ResolveDataYCutoff` — `GridYs[RowDataStart]` или `GridYs[HeaderEndRow]` (не maxY−2000).
- `IsTextInColumnXBand(ColMark)`.
- `TryParseMarkKey(Raw/Plain)` успешен.
- Bleed: если `t.Col != ColMark` и длина > 4 — пропуск.

**ColMark в CellMatrix:** `GetCellText(..., preferMarkColumn: true)` — приоритет коротким цифрам-маркам.

**Разрешение конфликта в строке:** CellText mark или max DataY.

**Индексы:** `KeyToRowMark`, `RowToKeyMark`.

**Границы:** `BindKeys` → `FindRowTopSub`, `GetMarkBlockEndExclusive`, `GetNextKeyRowExclusive`.

---

## 11. Значение (наименование) — `ResolveNameForKey` + dual-pass

### Точка входа (универсальная)

`FillMarkNamesFromMergeGroups` / `FillMarkNamesFromAcadTableCells` → **`ResolveNameForKey(key)`** (LINE, native Table, N scopes).

**Правило:** для марки `key` — верхняя строка блока в ColMark (`ResolveNameRowTopForKey`), диапазон строк `[rowTop, rowEndExclusive)` для сбора имён, `CellText` + dual-pass `AllTexts` (LINE) при недостаточном CellText, fallback `CellText[rowMark, ColName]` и соседние col ±1, лог `[KV-ANCHOR]`.

**Диапазон строк имени (`rowEndExclusive`):**

```text
markBlockEnd = KeyToMarkBlockEnd[key] или GetMarkBlockEndExclusive(rowTop, key)
nextMarkRow = GetNextKeyRowMarkExclusive(key)   // KeyToRowMark следующей марки, НЕ rowTopSub
rowEndExclusive = min(markBlockEnd, nextMarkRow) при nextMarkRow > rowTop
rowEndExclusive = max(rowEndExclusive, rowTop + 1), min(..., GridRowCount)
```

`GetNextKeyRowExclusive` (rowTopSub) используется для ColQty, **не** для склейки имени: при объединённой ячейке «Поз.» rowTopSub следующей марки может совпадать со строкой продолжения предыдущего имени и обрезать диапазон до одной строки.

**Продолжение имени vs секция:** `IsNameContinuationRow(key, row)` — строка внутри `[rowMark, markBlockEnd)` без своей цифры; такие строки **не** пропускаются как `IsSectionHeaderRow` в путях KV/value (`CollectNamePartsFromCellText`, `CollectNamePartsForPositionRange`, `SupplementNamePartsInVerticalBand`, `AllNameRowsHaveCellText`).

CMD `[NAME-BOUNDARY]` логирует `nextMarkRow`, `markBlockEnd`, `rowEndEx`, `cellOnly`.

**Dedupe (MText+MText):**

| Шаг | Метод | Правило |
|-----|-------|---------|
| cell-only | `ResolveNameForKey` | `cellJoined.Length ≥ 20` **и** `AllNameRowsHaveCellText(rowTop..rowEndEx)` → без AllTexts pass, `[NAME-DEDUPE] reason=cell-only` |
| filter | `FilterTextPartsNotInCellText` | AllTexts часть не добавлять, если уже в cellJoined |
| merge rows | `TryAddNamePartExact` | dedupe по строкам merge-блока |
| phrase | `CollapseDuplicateNamePhrase` | «A A» → «A» |
| cell matrix | `IsDuplicateCandidate` | near-overlap (4× eps) один plain в ColName |

`ResolveNameRowTopForKey`: `≥ HeaderEndRow`, `≥ RowDataStart`, не секция без марки (`IsSectionHeaderRow`, порог имени ≥ 8 символов).

`ResolveNameFromMergeGroup` — алиас на `ResolveNameForKey`.

### Pass 1 — `CollectNamePartsForPositionRange`

Для `r = rowTop .. rowEndExclusive-1`:

- skip: `ResolveMarkKeyAtRow(r) != 0 && != key`.
- skip: `IsSectionHeaderRow(r)` кроме `IsNameContinuationRow(key, r)`.
- `CollectNamePartsFromNameCell`.

**Фильтры в `CollectNamePartsFromNameCell`:**

| Фильтр | Назначение |
|--------|------------|
| `PassesCellLayerFilter` | PrimaryNameLayer / Extra / Allowed |
| `IsTextInColumnXBand(ColName)` | X в столбце NAME |
| `TextOverlapsRowBand` | overlap ≥30% MText / ≥42% point |
| **`NameTextBelongsToMarkKey`** | owner mark == key |

**Без PickBest** — все hits добавляются. `consumedSources` — dedup SourceIndex. `[NAME-STOP]` — только если второй standalone на строке с **чужой** маркой (`ResolveMarkKeyAtRow != key`); продолжение имени той же марки на следующей строке блока не обрывает склейку.

### Pass 2 — `SupplementNamePartsInVerticalBand`

- Y: `GridYs[rowTop]` .. `GridYs[rowEndExclusive]`.
- `DataY` в полосе (без overlap test).
- Тот же `NameTextBelongsToMarkKey`.
- `TryAddNamePartExact` — dedup строк.

### Owner mark

| Метод | Логика |
|-------|--------|
| `ResolveOwnerMarkKeyForNameText` | markAtPoint(t.Row), markAtDom(DominantRow); конфликт → upper row |
| `NameTextBelongsToMarkKey` | owner==key или owner==0 && row в блоке |

Лог: `[NAME-FOREIGN-SKIP]`, `[NAME-ROW]`, `[NAME-SUPPLEMENT]`, `[KV-PAIR] value=`, `[NAME-BOUNDARY]`.

### Текстовые эвристики (`MTextPlainText`)

| Метод | Условие |
|-------|---------|
| `EnumerateDisplayNameLines` | split `\P` |
| `LooksLikeSectionHeaderLine` | заголовок раздела → skip |
| `IsStandaloneProductName` | полное изделие → stop на втором |
| `IsAcceptableNameContinuation` | длина/score для хвоста |
| `NameScore` | эвристика «похоже на имя» |

---

## 12. `CellIndex` — привязка к сетке

| Метод | eps | Назначение |
|-------|-----|------------|
| `TryGetCellIndex(x,y,xs,ys)` | 2.0 | row/col по точке |
| `GetCellText(samples)` | — | один winner на ячейку (CellText matrix) |
| `TryGetRowByExtent` | minFraction | строка по overlap YMin..YMax |
| `GetDominantRow(t, gridYs)` | 0.30 MText / 0.50 DBText | dominant row для bleed/owner |

---

## 13. Слои текста таблицы

### `BuildTableContentLayers`

- ~90% текстов в data columns → `AllowedTableTextLayers`.
- редкие → `ExcludedAnnotationLayers`.

### `BuildPrimaryNameLayer`

- ColName, Row ≥ RowDataStart: max count layer → `PrimaryNameLayer`.
- ≥5% + NameScore>0 → `ExtraNameLayers`.

### `PassesCellLayerFilter`

- ColName: Primary + Extra, не Excluded.
- прочие data cols: Allowed, не Excluded.

---

## 14. Запись «Кол.» — `SpecGridService`

| Метод | Назначение |
|-------|------------|
| `TryBuildQtyByKeyForWriteback` | qty из видимых строк палитры |
| `ResolveQtyCellRowBottomExByColQtyGrid` | низ merged ячейки ColQty |
| `ResolveNextKeyRowTopEx` | потолок ячейки |
| `ResolveQtyInsertPoint` | центр X/Y ячейки |
| `FindQtyTextInCell` | найти существующий qty-текст |
| `ResolveQtyTableTextAppearanceForScope` | стиль из ColQty → body → 2.5 |
| `UpsertQtyText` | update/create DBText (LINE path) |
| `UpsertQtyInAcadTable` | update native Table cell (число в «N шт.») |

**Не пишет:** наименование, примечания инженера (намеренно сохраняются при update).

---

## 15. Логи CMD (активные)

| Тег | Источник |
|-----|----------|
| `[POSC] Распознана шапка…` | ReportDetectedHeader |
| `[POSC] Граница шапки/данных…` | ReportDetectedHeader |
| `[POSC] KeyToRowMark` / `[POSC] Марок в данных…` | ReportDetectedHeader |
| `[POSC] Марка 1 в CellText…` | FormatMissingKeyOneDiagnostic |
| `[NAME-DEDUPE]` | ResolveNameForKey cell-only / collapse |
| `[KV-ANCHOR]` | ResolveNameForKey fallback |
| `[POSC] Марка: наложение…` | BindKeysFromProperties |
| `[POSC] Сетка таблицы: линии на разных слоях…` | Grid merge |
| `[KV-PAIR] key= value=` | FillMarkNames |
| `[NAME-BOUNDARY]` | ResolveNameFromMergeGroup |
| `[NAME-ROW]` | CollectNamePartsFromNameCell |
| `[NAME-SUPPLEMENT]` | SupplementNamePartsInVerticalBand |
| `[NAME-FOREIGN-SKIP]` | NameTextBelongsToMarkKey |
| `[NAME-BLEED]` | IsUpstreamBleedFromForeignMark (diag) |
| `[NAME-STOP]` / `[NAME-SECTION]` | name filters |
| `[CELL-SPLIT-DATA]` | SplitNameColumnRowsData |

---

## 16. Сборка

| AutoCAD | Framework | Скрипт |
|---------|-----------|--------|
| 2016–2024 | net46 | `build\build-ac2016.cmd` |
| 2025+ | net8.0-windows | `build\build-ac2026.cmd` |

Пути: `build\AutoCAD.props` (из template). Подробно: `docs/BUILD.md`.

---

## 17. Красные зоны

- `PosCounterEngine` — не менять.
- Pass-1 шапка: `AssignCellsHeader`, `DetectHeader*`, `EstimateHeaderEndRow`.
- `BuildMergedGridAxes`, sort GridYs desc.
- Qty — только ColQty; палитра qty = LOCK engine output.

---

## 18. Ручная проверка

1. NETLOAD → палитра автоматически.
2. ЗАПУСТИТЬ → марки/количества.
3. Выбрать спецификацию → имена + «Кол.» на DWG.
4. `_tex_fek`: марки 4/5 (owner), 52 (multi-line), 6–9 (не пусто), mark 64 (без дубля имени).
5. **Ушко:** `RowDataStart=1`, `KeyToRowMark: 1→row1`, ColQty = «Кол.».

---

## 19. Связанные документы

- Инженер: `docs/INSTRUCTION_ENGINEER.md`
- Простым языком: `Работа программы.md`
- Факт-план: `.cursor/plans/factual_program_architecture.plan.md`
- История правок: `.cursor/DIALOGUE_LOG.md`
