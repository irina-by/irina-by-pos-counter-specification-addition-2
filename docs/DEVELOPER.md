# PosCounter.Net — техническая документация (факт по коду)

**Версия:** 4.2.0-table-grid-lines  
**Сборки:** `dll 2016` (net452, .NET 4.5.2+ / AC 2016 SP1–2024), `dll 2026` (net8.0-windows)  
**Дата актуализации:** 2026-06-13 (diag_log_gt20: unassigned→name, continuation name row end, skip reasons)

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

| AutoCAD | Framework | Скрипт | Деплой |
|---------|-----------|--------|--------|
| 2016 SP1–2024 | net452 (.NET 4.5.2+) | `build\build-ac2016.cmd` (корень репо) или `PosCounter.Net\build\build-ac2016.cmd` (портативно) | `dll 2016\` — **PosCounter.Net.dll** + **System.ValueTuple.dll** |
| 2025+ | net8.0-windows | `build\build-ac2026.cmd` | `dll 2026\PosCounter.Net.dll` |

Пути: `build\AutoCAD.props` (из template) или `PosCounter.Net\build\AutoCAD.props`. Подробно: `docs/BUILD.md`.

### Портативная сборка AC 2016 (2026-06-10)

План: `.cursor/plans/poscounter_vs_ac2016_kit.plan.md`.

Для копирования **только** папки `PosCounter.Net` на рабочий ПК (без корня репозитория):

| Файл | Назначение |
|------|------------|
| `PosCounter.Net\Directory.Build.props` | Импорт `build\AutoCAD.props` и `build\NuGet.local.props` |
| `PosCounter.Net\build\AutoCAD.props` | Путь к AC 2016 (`AutoCADSdkDirNet46`) |
| `PosCounter.Net\build\NuGet.local.props` | Запасной `PkgSystem_ValueTuple` через `$(USERPROFILE)\.nuget\...` |
| `PosCounter.Net\build\build-ac2016.cmd` | Сборка net452 → копия в `PosCounter.Net\dll 2016\` |
| `PosCounter.Net\build\СБОРКА_VS_AC2016.md` | Инструкция для Visual Studio |

Инструкция для инженера: `PosCounter.Net\build\СБОРКА_VS_AC2016.md`.

### Диагностика AC 2016 — `[POSC-DIAG]` (2026-06-10)

План этапа 1: `.cursor/plans/fix_ac2016_spec_recognition.plan.md`.  
План этапа 2 (ColQty fallback): `.cursor/plans/fix_ac2016_spec_recognition_stage2.plan.md`.  
План этапа 3 (DBText шапка + numeric AllTexts): `.cursor/plans/fix_ac2016_dbtext_header.plan.md`.  
План fix name/qty regression: `.cursor/plans/fix_name_qty_regression.plan.md`.  
План fix_colqty_gt20_names: `.cursor/plans/fix_colqty_gt20_names.plan.md`.  
План fix_gt-20_bleed_colqty: `.cursor/plans/fix_gt-20_bleed_colqty.plan.md`.  
План diag_logs_colqty_names: `.cursor/plans/diag_logs_colqty_names_66750185.plan.md`.  
План fix_gt20_names_qty: `.cursor/plans/fix_gt20_names_qty_85bae939.plan.md` → отчёт `.cursor/plans/fix_gt20_names_qty_IMPLEMENTED.md`.  
План header_anchor_schema: `.cursor/plans/header_anchor_schema.plan.md` → отчёт `.cursor/plans/header_anchor_schema_IMPLEMENTED.md`.  
План fix_schema_inherit_table2: `.cursor/plans/fix_schema_inherit_table2_63fc7ffa.plan.md` → отчёт `.cursor/plans/fix_schema_inherit_table2_IMPLEMENTED.md`.

| Компонент | Назначение |
|-----------|------------|
| `SpecColumnSchema` | Эталон ColMark/ColName/ColQty с таблицы 1 (реальная шапка pass1) |
| `SpecGridSession.ColumnSchema` | Сессия якоря; сброс в `ClearScopes` |
| `TryLockColumnSchema` | scope 0: `Pass1ColMark>=0 && Pass1ColName>=0` → lock, `[SCHEMA] locked` |
| `TryAlignScopeColumnsToAnchorSchema` | Сопоставление столбцов продолжения по центрам X-полос якоря + сдвиг левого края сетки (допуск 50 мм) |
| `TryApplyInheritedColumnSchema` | scope 1+: выравнивание → `inherited`/`aligned`, `ColQtySource=inherited`; `failReason`: `gridCols`, `X-mismatch`, `texts-outside`, `invalid` |
| `ColumnsInheritedFromSchema` | Флаг scope; `RebindScopeKeysAndNames` skip DetectHeader |
| `SpecGridService` inherit-fail | `[SCHEMA] inherit-fail reason=…`; предупреждение «рамка слишком мала»; fallback `TryInferColumnsFromData` → `inherit-fail → fallback infer-data` |
| `ProcessInferColumnsFallback` | Старый путь «столбцы по данным» если якорь не зафиксирован или inherit не удался |

| Компонент | Назначение |
|-----------|------------|
| `CreateTextSampleFromDbText` pass2 | `DataX/Y` = AlignmentPoint (не ExtentsTop); YMin/YMax из bbox для overlap |
| `TryGetMTextBounds` pass2 | `DataX/Y` = `MText.Location`; YMin/YMax из Extents/BoundingPoints; `BoundsMethod=Location+Extents` |
| `AssignCellsData` | Привязка Row/Col: `AlignX/Y` → `DataX/Y` |
| `ColDesignation` | Столбец «Обозначение» (индекс 1) — не в имя |
| `LooksLikeDesignationText` | Фильтр ГОСТ/TB100/GOST… (короткие коды) |
| `IsTextInNameColumn` | Имя только `t.Col == ColName`, без designation |
| `BuildCellMatrix` (ColName) | В ячейку col2 — только тексты с `t.Col == ColName` |
| `ResolveNameForKey` | `useCellTextFromBlock`: cell-only при `cellJoined≥20` или RU на rowTop + multi-row; **без** `SupplementNamePartsInVerticalBand` при `cellJoined≥20`; cap `nextKeyTop` в supplement |
| `RebindScopeKeysAndNames` | `TryResolveMissingColQty` только при `ColMark≥0 && ColName≥0` (нет `numeric col=0` до inference) |
| `TryInferColQtyFromNumericColumn` | Пропуск столбца если `CountDataMarkKeysInColumn≥2` (не путать марки с qty) |
| `FindQtyTextInCell` | Merged: поиск только `rowTop..rowTop+1` (как `ResolveQtyInsertY`) |
| `UpsertQtyText` | Если `|entY-targetY|>halfRowStep` → `skip-far-entY` + create DBText |
| `DescribeHeaderColumn` | При `ColumnsInferredFromData` без fallback на данные → «— (продолжение)» |
| `[NAME]` trace | key 1–7, 52–57, 105–109 (в т.ч. `empty`) |
| `ApplyMandatoryColQtyLayout` | Схема 0/1/2/3: **всегда** `ColQty = ColName+1`, в т.ч. при `ColQty=-1` |
| `ApplyStandardColumnLayout` | После inference, rebind, numeric fallback и **перед WriteQty** |
| `TryInferColQtyFromNumericColumn` | Стандарт 0/1/2/3: prefer col3 если `CountQtyLikeInColumn(col3)>=3` |
| `ResolveQtyInsertY` | Y центра **первой суб-строки** `rowTop..rowTop+1` (не середина span) |
| `DbTextHeaderMaxPlainLen` | 60 (было 25); отсев данных: `TryParseMarkKey` + `LooksLikeDesignationText` |
| `AppendHeaderTextPart` | MText в шапке — по строкам (RU/EN в одной ячейке) |
| `ColumnLooksLikeMassData` | Отсев col4 «Масса» при numeric (нестандартная схема) |
| `ColQtyLayoutFixDiag` | `[POSC-DIAG]` например `4→3` |
| `NameCol2DiagLines` | `[POSC-DIAG]` имя key=1..7 col2-lines, excluded-designation |
| `KeyToRowMarkSampleDiag` | `[POSC-DIAG]` марки 55–75 |
| `WriteQtyDiagLines` | `[POSC-DIAG]` `WriteQty key=N colQty=3 qty=Q Y=… (палитра)` |
| `ReportScopeSummaryDiagnostic` | ИТОГ + `layout=4→3` + `ВНИМАНИЕ` при `ColQty=-1` (через `WriteDiagTail`) |
| `SpecGridLog.WriteTrace` | `[POSC-DIAG] [COLQTY\|HEADER\|NAME\|WRITEQTY]` — видимые трассировки методов |
| `SpecGridLog.FormatDllBuildStamp` | `build=` в начале и конце «Выбрать спецификацию» |
| `ReportHeaderTraceDiagnostic` | `[HEADER]` scores по col0–5 |
| `BuildHeaderOnlyColumnText` | При `ColumnsInferredFromData` — отсев ячеек с NameScore≥4 (не данные в шапке) |
| `CapRowEndBeforeNextMarkNameLead` | Граница имени: не включать col2 строку перед цифрой следующей марки |
| `ContainsCyrillic` / `HasCyrillicNameTextsInBand` | Supplement имён при `cellOnly` без русской строки |
| `DetectHeaderByTopGridRows` | Шапка по строкам 0–1 (двуязычные подписи ГТ) |
| `MarkHeaderTokens` / `NameHeaderTokens` | Расширенные токены: «марка поз», «mark it», «list of materials» |
| `SanitizeMarkScoresForDigitOnlyHeaders` | Отсев col0 с заголовком-цифрой («9») при ≥10 марок в данных |
| `CanLockColumnSchemaFromPass2` | `[SCHEMA] locked` при pass1=-1 и схеме 0/2/3 (grid/layout/dbTextBand) |
| `IsContinuationPickTooSmall` | &lt;80 объектов — без infer на продолжении |
| `AssignUnassignedTextsToNameColumn` | MText/DBText вне сетки: `DataX` в полосе `ColName` → `Col`+`Row`; CMD `unassigned→name` |
| `UnassignedNameFixLines` | `[POSC-DIAG]` привязанные тексты (до 10 на scope) |
| `ResolveContinuationNameRowEnd` | Продолжение: расширить диапазон col2 до `markBlockEnd`, если col2 пуст и нет standalone у следующей марки |
| `ShouldLogNameRejectReason` | `[NAME] skip=designation/section/not-acceptable` для keys 52–57, 105–109 |

| Компонент | Назначение |
|-----------|------------|
| `SpecGridLog.WriteDiag` | CMD `[POSC-DIAG]`, лимит ~55 строк на «Выбрать спецификацию» |
| `ScopeGridResult.Pass1Col*` | Снимок столбцов после pass1 `DetectHeader` |
| `InferenceColQtyScoresSummary` | Scores ColQty при `TryInferColumnsFromData` |
| `ScopeGridResult.ColQtySource` | Источник ColQty: `grid`, `dbTextBand`, `topBand`, `inference`, `simple01`, `allTexts`, `numeric` |
| `TryResolveMissingColQty` | Цепочка fallback при `ColQty<0`: simple01 → allTexts → numeric; при провале — `ColQtyFallbackDiag` |
| `DetectHeaderByDbTextHeaderBand` | Шапка DBText/MText в полосе `TryGetHeaderBandY` (GridYs), до top-band |
| `IsTextYPlausibleForHeaderBand` | Отсев Y вне таблицы (|Y-median| &lt; 8×rowStep) |
| `DetectHeaderSimpleRows01` | OLD-стиль: шапка из строк 0–1 `CellText` |
| `DetectColQtyFromAllTexts` | Поиск «Кол.» в `AllTexts` (зона `TryGetHeaderBandY` + region) |
| `TryInferColQtyFromNumericColumn` | Столбец по числам: `CellText` **и** `AllTexts` (`CountQtyValuesInColumnTexts`) |
| `BuildColQtyFallbackDiagnostic` | per-col `cell=` / `text=` при провале fallback |
| `ScopeGridResult.DbTextHeaderBandSummary` | `[POSC-DIAG]` найденные подписи в полосе GridYs |
| `AssignCellsHeader` | Цепочка: `HeaderY` (AlignmentPoint) → `AlignY` → `DataY` (ExtentsTop) |
| `CreateTextSampleFromDbText` | Header=AlignmentPoint, Data=ExtentsTop; `BoundsMethod=AlignmentPoint+ExtentsTop` |
| `TextSample.AlignX/Y` | Точка вставки DBText для fallback pass1 |
| `Commands.TryGetAssemblyBuildStamp` | NETLOAD: `build=yyyy-MM-dd HH:mm` |
| `BuildHeaderTopBandDiagnosticHeaderCoords` | Top-band по HeaderX/Y (при inference) |
| `ReportScopeSummaryDiagnostic` | ИТОГ по таблице + `источник ColQty=` + имена в палитру |

### AC 2016 vs 2026 (2026-06-10)

План: `.cursor/plans/ac2016_dll_fix.plan.md`.

| Проблема | Решение |
|----------|---------|
| ValueTuple при сборке/NETLOAD net452 | `.csproj`: `GeneratePathProperty` + `Reference` `lib\netstandard1.0\System.ValueTuple.dll`, `Private=True` |
| AC 2016 SP1 (.NET 4.5.2) | Целевой framework **net452** (не net46 — иначе NETLOAD может требовать .NET 4.6) |
| `(столбцы по данным): Марка — не найдена` | `RebindScopeKeysAndNames`: не вызывать `DetectHeader` если `ColumnsInferredFromData` |
| Пустой `CellText` на AC 2016 | `TryGetMTextBounds`: fallback `GetBoundingPoints()`; CMD `Текстов вне ячеек сетки` |
| Версия DLL | `Commands.Initialize` → `[POSC] … (net452) build=…` — дата файла DLL |
| Старый LISP | Не использовать `pos_counter_2016_2026.lsp`; только NETLOAD |

---

## 20. Missing qty vs пустое имя (2026-06-13)

План: `.cursor/plans/диагноз_лога_gt-20_00c5083c.plan.md` (не редактировать).

### «Количество не найдено в палитре для марок: …»

`CollectMissingQtyMarksForScope` сравнивает `scope.KeyToRowMark.Keys` с `qtyByKey` из палитры (`TryBuildQtyByKeyFromAllRowsSnapshot` — **все** строки `_lastCountRows`, фильтры палитры не режут qty для записи).

| Симптом | Причина | Действие |
|---------|---------|----------|
| `WriteQty итог: записано=N, пропущено=0` и список missing | Марка **есть в спецификации**, **нет в подсчёте** | ЗАПУСТИТЬ по зоне с выносками; проверить слой/тип объектов |
| `пропущено>0` | Геометрия ячейки ColQty / `ColQty=-1` | CMD `ИТОГ` + `[WRITEQTY]` |

Это **не сбой WriteQty**, если `пропущено=0`.

### Пустые наименования в хвосте продолжений

| Группа | Причина | Правка в коде |
|--------|---------|---------------|
| MText вне сетки (`Row=-1`, сдвиг DataX) | `AssignCellsData` не привязал строку | `AssignUnassignedTextsToNameColumn` после pass2 и в `RebindScopeKeysAndNames` |
| Текст найден, `parts=0` | Отсев `LooksLikeDesignationText` / `IsAcceptableNameContinuation` | `[NAME] skip=…` по `SpecDiagPolicy` |
| `texts=0` на листе-продолжении | Наименование только на предыдущем листе | `ResolveContinuationNameRowEnd` расширяет band до `markBlockEnd` при пустом col2 |

После `RebindScopeKeysAndNames` при `unassignedFixed>0` пересобирается `CellText`.

---

## 21. Универсальный KV (2026-06-13)

План: `.cursor/plans/авто_kv_ac2016_a1fdd702.plan.md` (не редактировать). Реализация: `.cursor/plans/авто_kv_ac2016_a1fdd702_IMPLEMENTED.md`.

### Принцип

Явно заданы только **токены шапки** (`MarkHeaderTokens`, `NameHeaderTokens`, `DesignationHeaderTokens`, `ScoreQtyHeader`). Марки, столбцы и имена — из геометрии сетки, overlap и палитры.

### Ключевые модули

| Модуль | Роль |
|--------|------|
| `MarkKeyParser.cs` | Префиксы Поз./Марка/№ как в Engine; `TryParseMarkKey` делегирует сюда |
| `DetectHeaderBoundaryAndColumns` | Скан строк 0..5 по токенам; `headerPath=gridTokens`; topBand 2000 — last-resort |
| `ResolveNameForKey` | `cellOnly` при `len≥20 && AllNameRowsHaveCellText`; иначе CellText+AllTexts |
| `PickBestNameTextForRow` | Один текст на строку по NameScore/длине/YMax |
| `TryGetMTextBounds` | DataY = **ExtentsTop**; overlap по YMin/YMax |
| `SpecDiagPolicy.cs` | Trace без `key==N`; бюджеты [NAME]/[MARK]/[GEO] |
| ColQty | Evidence-only: header/numeric/inherited; без mandatory 0/2/3 |

### Qty

По-прежнему **только из палитры** (`qtyByKey`); не из ячеек таблицы.

---

## 22. Диагностика KV в CMD (2026-06-13)

Префикс: `[POSC-DIAG] [КАТЕГОРИЯ] …`. Итог: `[KV-SUMMARY]` (вне бюджета, `WriteDiagTail`).

| Категория | Что смотреть |
|-----------|--------------|
| `[HEADER-SCAN]` | `path=gridTokens` — primary OK; `path=topBand fallback` — предупреждение |
| `[MARK]` | `parse … prefix=Поз.`; `KeyToRowMark count=… sample:` |
| `[GEO]` | `bindY=ExtentsTop` (MText); `unassigned→name` |
| `[NAME]` | `mode=cellOnly/cell+all`; `pick-best`; `skip=designation`; `empty texts=N parts=0` |
| `[COLQTY]` | `source=grid\|layout-evidence\|inherited`; `reason=mass-column` |
| `[KV-SUMMARY]` | `headerPath`, `keys`, `names`, `empty`, `qtyWritten`, `outside`, `schema` |

`grep` по `SpecGrid`: нет `key == 52`, `ColMark != 0`, mandatory `0/2/3`.

### Merged-блоки и cellOnly (2026-06-14, post KV)

План: `.cursor/plans/fix_names_qty_post-kv_dd94bc56.plan.md`.

| Правило | Реализация |
|---------|------------|
| Граница имени merged | `nextKeyTop` cap **не** применяется при `isMerged`; расширение до `nextMarkRow+1`; `HasNameTextOwnedByKey` для строк с чужой цифрой в ColMark |
| cellOnly | Отключается при `reason=merged-block` или `missing-cyrillic` (`HasCyrillicInMarkBlock` — ColName + ColDesignation) |
| Qty sub-row | `FindQtyTextInCell` — только полоса `rowTop` при merged span |
| Стиль qty | `ResolveQtyTableTextAppearanceForScope(tr, scope, rowTop)` — образец из `ColName` на `rowTop` |
| Палитра vs scope | `[POSC] Палитра: ключей=N, имён=M` + первые 12 ключей без имени |

---

## 23. Палитра vs scope таблиц (2026-06-14)

- **ЗАПУСТИТЬ** считает марки на всём чертеже → N ключей в палитре (qty).
- **Выбрать спецификацию** даёт имена только для марок на **выделенных** листах → M имён.
- Если M < N — это не баг: на выделенных листах нет остальных марок. CMD: `[POSC] Палитра: ключей=…, имён=…` + подсказка выделить все листы.
- Статус палитры: `имён=M из N ключей`.

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
