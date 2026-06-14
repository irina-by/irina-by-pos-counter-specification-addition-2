---
name: Factual Program Architecture
overview: Фактическое описание PosCounter.Net v4.2.0 — от NETLOAD до записи «Кол.» и палитры. Соответствует коду на 2026-06-14.
todos: []
isProject: false
---

# Фактическая архитектура PosCounter.Net

Документ описывает **как программа реально работает** по текущему коду. Не ТЗ и не план изменений.

**Версия:** 4.2.0-table-grid-lines  
**Актуализация:** 2026-06-14

---

## 1. Общая схема

```mermaid
flowchart TD
    NETLOAD[NETLOAD DLL] --> Init[Commands.Initialize]
    Init --> Palette[PaletteHost.ShowPalette]
    Run[Кнопка ЗАПУСТИТЬ] --> Engine[PosCounterEngine.CountWithInfo]
    Engine --> UI1[Палитра: Марка Кол Слой]
    Spec[Выбрать спецификацию] --> Pick[TryPickAllSpecificationTables]
    Pick --> Build[TableGridBuilder.Build x N]
    Build --> Names[MarkNamePairs via ResolveNameForKey]
    Build --> Keys[KeyToRowMark KeyToRowTopSub]
    Names --> UI2[Палитра: Наименование]
    Keys --> Qty[SpecGridService.WriteQtyScope]
    Qty --> DWG[DBText/MText или AcadTable ColQty]
```

**Связь модулей:** номер марки (`Key`). Подсчёт выносок → количество в палитре. Спецификация → наименование в палитру + запись «Кол.» на чертёж.

**Палитра vs scope:** ЗАПУСТИТЬ даёт N ключей по всему чертежу; «Выбрать спецификацию» даёт M имён только по выделенным таблицам. M < N — нормально, если не все листы выделены.

---

## 2. Точка входа и команды

| Файл | Роль |
|------|------|
| `Commands.cs` | `IExtensionApplication`: NETLOAD, POSC, служебные POSC2_* |
| `PaletteHost.cs` | WPF-палитра, очереди команд, payload спецификации |
| `UI/PosCounterControl.xaml.cs` | Кнопки, таблица, фильтры, экспорт, Сброс |

### Команды AutoCAD

| Команда | Кто вызывает | Действие |
|---------|--------------|----------|
| `NETLOAD` | инженер | `Initialize()` → на `Idle` открывается палитра |
| `POSC` | инженер | `PaletteHost.ShowPalette()` |
| `POSC2_RUN_INTERNAL` | палитра «ЗАПУСТИТЬ» | `PosCounterEngine` → строки в UI |
| `POSC2_SPEC_INTERNAL` | «Выбрать спецификацию» | pick таблиц, Build, имена, writeback qty |
| `POSC2_HIGHLIGHT_INTERNAL` | «Показать на чертеже» | transient-подсветка handles |

Инженер вводит только **NETLOAD** и **POSC**. LISP-загрузка не используется.

---

## 3. Модуль 1 — подсчёт выносок (`PosCounterEngine`)

**Файл:** `Engine/PosCounterEngine.cs` (**PALETTE-COUNT-LOCK — не менять без ТЗ**)

- Источник: выделение **или** галочка «Все объекты в модели».
- Типы: `DBText`, `MText`, атрибуты блоков (рекурсия).
- **Не обрабатывается:** `MLeader`, proxy СПДС.
- `ExtractPositionNumber` / `MarkKeyParser.TryParse` — цифры 1..10000; группировка `(слой, текст)` → `Quantity`.
- `MTextPlainText.ResolveLayer` — слой `0` → слой блока; xref `|` отрезается.

---

## 4. Модуль 2 — спецификация (orchestration)

**`SpecGridService.RunSelectSpecification`:**

1. `TryPickAllSpecificationTables` — N рамок, Enter без выделения = конец.
2. `TableGridBuilder.Build(i, ids, tr, sharedGridLayer, log)` на каждую рамку.
3. `MergeScopeNames` → `BuildCombinedMarkNames` → палитра.
4. `WriteQtyInTransaction` → `UpsertQtyText` (LINE) или `UpsertQtyInAcadTable` (native Table).

**`SpecGridSession`:** список scope'ов; `SharedGridLayer` для нескольких таблиц на листе; **`SpecColumnSchema`** — наследование столбцов для 2-й таблицы без шапки.

---

## 5. Выбор пути Build

| Условие выборки | Путь |
|-----------------|------|
| есть `Table`, нет `Line` | **`BuildFromAcadTable`** |
| есть `Line` (с Table или без) | **LINE path** |
| Table + Line вместе | WARN `[POSC] Mixed selection…`, LINE path |

---

## 6. LINE path — `TableGridBuilder.Build()`

**Файл:** `SpecGrid/TableGrid.cs` (~7500 строк)

### 6.1. Сбор и сетка

- LINE → `GridLineSeg`; DBText/MText → `TextSample`.
- `AutoDetectGridLayer` (≥30%, `MinGridLineLen=5000` для выбора слоя).
- `BuildMergedGridAxes` — Y **сверху вниз** (`sortAsc: false`).
- Лимиты: `MaxLines/Texts=20000`, `MaxCells=5000`.

### 6.2. Pass 1 — шапка

| # | Метод | Назначение |
|---|-------|------------|
| 1 | `AssignCellsHeader` | Row/Col по HeaderX/Y = ExtentsCenter |
| 2 | `BuildCellMatrix(false)` | CellText, все слои |
| 3 | `DetectHeader` / `DetectHeaderByGridRows` | grid rows → columns → top-band (fallback) |
| 4 | **`ApplyMarkAnchoredHeaderBoundary`** | цифра в ColMark → `RowDataStart` / `HeaderEndRow` |
| 5 | `FindHeaderEndRowByHorizontalBorders` | H-линии **только до firstMarkRow−1** |
| 6 | `ApplyHeaderBoundaryFromGridScan` | скан строк; `IsGridScanDataRow` — марка только ColMark |
| 7 | `ComputeRowDataStart` | уточнение; `AlignRowDataStartToFirstMark` по `min(KeyToRowTopSub)` |
| 8 | `BuildPrimaryNameLayer`, `BuildTableContentLayers` | слои ColName / allowed |
| 9 | **`TryLockColumnSchema`** | 2-я таблица без шапки — наследует ColMark/ColName/ColQty |

### 6.3. Pass 2 — данные + KV

| # | Метод | Назначение |
|---|-------|------------|
| 10 | `AssignCellsData` | DataX/Y; Row по точке; DominantRow |
| 11 | `SplitNameColumnRowsData` | MText+DBText в одной ячейке NAME |
| 12 | `BuildTextsByRow` | кэш ColName по Row |
| 13 | `BuildCellMatrix(true)` | CellText, filtered layers |
| 14 | **`BindKeysFromProperties`** | KeyToRowMark (ключ) |
| 15 | `BindKeys` | KeyToRowTopSub, KeyToMarkBlockEnd |
| 16 | **`FillMarkNamesFromMergeGroups`** | MarkNamePairs через **`ResolveNameForKey`** |

---

## 7. Распознавание шапки (факт)

### 7.1. Приоритет границы шапки / данных

1. **Якорь ColMark (`markAnchor`)** — первая цифра-марка в столбце «Поз.» (`TryParseMarkKey`, не подпись «поз.») = выход из шапки. `RowDataStart = blockTop` (`FindRowTopSub` для merged ColMark).
2. **Токены шапки** — `HeaderTokenEndRow`, `ResolveHeaderOnlyEndRow`, `BuildHeaderOnlyColumnText`.
3. **H-линии** — `FindHeaderEndRowByHorizontalBorders`: 2-я полноширинная линия, но **не ниже firstMarkRow−1** (bilingual RU+EN).
4. **Grid scan** — `FindFirstDataRowByGridScan` / `IsGridScanDataRow`: марка только в ColMark; qty-hint только ColQty (не «масса»).

**Без** привязки к номеру листа, без `key==N`.

CMD: `[HEADER-DATA-ROW] markAnchor firstMarkRow=… blockTop=… rule=colMark-digit`.

### 7.2. Столбцы шапки — порядок DetectHeader

1. **`DetectHeaderByGridRows`** (primary) — `BuildHeaderOnlyColumnText` по строкам `0..HeaderEndRow−1`; `ScoreHeader` / **`ScoreQtyHeader`** (без «ед», штраф «масса»); `SanitizeColQtyColumn`; `RefineColMarkByDataMarks`.
2. **`DetectHeaderByColumns`** — fallback по ячейкам сетки.
3. **`DetectHeaderByTopTextBand`** — last-resort: Y-полоса maxY−2000, фильтр `Row < HeaderEndRow`, skip data marks.

`EnsureUniqueHeaderColumns`: **Марка → Кол. → Наименование**.

### 7.3. Вторая таблица (заголовок раздела)

- Если шапки нет, но первая таблица уже дала схему — **`TryLockColumnSchema`** / `SpecColumnSchema` копирует ColMark, ColName, ColQty.
- Заголовок раздела («Хозяйственно-pитьевой») без цифры в ColMark **не попадает в палитру** — ожидаемое поведение.

---

## 8. Ключ (марка) — LINE path

**`BindKeysFromProperties`** + **`IsBindableDataText`:**

- `t.Row >= RowDataStart` (основной фильтр).
- Запасной Y: `DataY < ResolveDataYCutoff` = `GridYs[RowDataStart]`.
- `IsTextInColumnXBand(ColMark)`, `MarkKeyParser.TryParse`, не `IsSectionHeaderRow`.
- Bleed: `t.Col != ColMark` и длина > 4 → skip.

**Границы:** `BindKeys` → `KeyToRowTopSub`, `KeyToMarkBlockEnd`, `GetNextKeyRowExclusive`.  
`AlignRowDataStartToFirstMark` — по **`min(KeyToRowTopSub)`**, не только по цифре.

---

## 9. Значение (наименование) — `ResolveNameForKey`

**Точка входа:** `FillMarkNamesFromMergeGroups` → **`ResolveNameForKey(key)`** (LINE, native Table, N scopes).

### 9.1. Диапазон строк

- `ResolveNameRowTopForKey`: `≥ HeaderEndRow`, `≥ RowDataStart`, skip секций; не сдвигать `rowTop`, если ColName на строке непустой.
- Диапазон continuation: **`[rowTop, blockEnd)`** — первая строка имени над цифрой в merged ColMark включается.
- `rowEndExclusive` = `GetNextKeyRowExclusive(key)`; для merged — расширение без жёсткого `nextKeyTop` cap.

### 9.2. Сбор имени

1. **`CollectNamePartsFromCellText`** — `TryAddNamePartExact`.
2. Если `cellJoined.Length ≥ 20` → **cell-only** (кроме merged-block / missing-cyrillic), `[NAME-DEDUPE]`.
3. Иначе dual-pass: `CollectNamePartsForPositionRange` + `SupplementNamePartsInVerticalBand` + `FilterTextPartsNotInCellText`.
4. **`CollapseDuplicateNamePhrase`** — «A A» → «A».
5. Fallback: `CellText[rowMark, ColName]`, соседние col ±1, `[KV-ANCHOR]`.

### 9.3. Фильтры dual-pass

- `PassesCellLayerFilter`, `IsTextInColumnXBand(ColName)`, `TextOverlapsRowBand`.
- **`NameTextBelongsToMarkKey`** / `ResolveOwnerMarkKeyForNameText` — anti foreign bleed.
- `[NAME-FOREIGN-SKIP]`, `[NAME-ROW]`, `[NAME-SUPPLEMENT]`, `[KV-PAIR]`, `[NAME-BOUNDARY]`.

---

## 10. Native AutoCAD Table — `BuildFromAcadTable`

1. `CellText` из `table.Cells[r,c].TextString`.
2. `DetectHeaderFromCellMatrix` — те же score/refine.
3. `RowDataStart` — первая марка в ColMark + markAnchor.
4. `BindKeysFromAcadTableCellMatrix`, merged → `KeyToRowTopSub`.
5. `FillMarkNamesFromAcadTableCells` → `ResolveNameForKey`.
6. Qty: **`UpsertQtyInAcadTable`**.

---

## 11. Геометрия TextSample

| Поле | Pass-1 | Pass-2 |
|------|--------|--------|
| HeaderX/Y | ExtentsCenter | — |
| DataX/Y | — | DBText: точка; MText: X=Location.X, **Y=YMax** |
| Row/Col | по Header | по DataX/Y + DominantRow |
| YMin/YMax | экстент | экстент |

---

## 12. Запись «Кол.» (`SpecGridService`)

- Qty: `TryBuildQtyByKeyForWriteback` — сумма по **видимым** строкам палитры.
- Точка: `ResolveQtyInsertPoint` — центр ColQty по сетке.
- Merged ColQty: `ResolveQtyCellRowBottomExByColQtyGrid`, cap `ResolveNextKeyRowTopEx`; при merged span — полоса `rowTop`.
- Стиль: `ResolveQtyTableTextAppearanceForScope(tr, scope, rowTop)` — ColQty → ColName на rowTop → body → 2.5.
- **На чертёж пишется только ColQty.** Наименование не перезаписывается. Примечания инженера сохраняются.

---

## 13. Вспомогательные модули

| Файл | Назначение |
|------|------------|
| `CellIndex.cs` | TryGetCellIndex, GetCellText, GetDominantRow, IsDuplicateCandidate |
| `MTextPlainText.cs` | санитизация, NameScore, section/standalone; делегирует марку в MarkKeyParser |
| `MarkKeyParser.cs` | единый разбор марки (поз., №, 1..10000) |
| `SpecColumnSchema.cs` | ColMark/ColName/ColQty для наследования между scope |
| `SpecDiagPolicy.cs` | sample keys для диагностики CMD |
| `SpecGridLog.cs` | Info/Debug/Success в CMD |
| `SpecGridSession.cs` | сессия scope'ов, ColumnSchema |
| `ExportService.cs` | Excel/CSV из палитры |
| `PosSettingsStore.cs` | настройки UI |

---

## 14. Сборка и защита от дубликатов

| Артефакт | Назначение |
|----------|------------|
| `build/build-ac2016.cmd` | сборка net452 → `dll 2016\` или `bin\x64\Release\net452\` |
| `build/verify-no-duplicate-sources.ps1` | CS0101: склеенные копии SpecGridService и др. |
| `build/repair-duplicate-specgrid.ps1` | авто-ремонт дубликатов при копировании с облака |
| MSBuild `VerifyNoDuplicateSources` | проверка перед CoreCompile в `.csproj` |
| Маркер `POSC-SINGLE-FILE-SVC` | в `SpecGridService.cs` — один экземпляр класса |

---

## 15. Красные зоны (не ломать без ТЗ)

- `PosCounterEngine` — PALETTE-COUNT-LOCK.
- `BuildMergedGridAxes`, порядок GridYs desc.
- Pass-1 шапка: `AssignCellsHeader`, markAnchor, `DetectHeader*`.
- Qty — только ColQty; палитра qty = вывод engine.

---

## 16. Чеклист ручной проверки

| # | Чертёж / кейс | Ожидание |
|---|---------------|----------|
| H | любая таблица | CMD «Распознана шапка», ColMark/ColName/ColQty |
| 1 | первая марка после шапки | `[HEADER-DATA-ROW] markAnchor`, `minKey=1`, имя марки 1 не пустое |
| 2 | вторая таблица (раздел) | марка 1 OK; заголовок раздела не в палитре |
| Qty | Ушко / merged ColQty | ColQty = «Кол.», не «Масса»; цифра по центру ячейки |
| 64 | **_tex_fek** | одно имя без дубля «Кран… Кран…» |
| 52 | _tex_fek | многострочное имя (dual-pass + continuation) |
| 35NK | большая спец. bilingual | шапка RU+EN, первая строка данных не отсечена |
| Pal | N ключей, M имён | M < N если не все листы выделены — не баг |

---

## 17. Связанные документы

| Документ | Аудитория |
|----------|-----------|
| `docs/DEVELOPER.md` | разработчик — методы и условия |
| `docs/INSTRUCTION_ENGINEER.md` | инженер — пошаговая работа |
| `Работа программы.md` | все — Q&A простым языком |
| `docs/BUILD.md` | сборка DLL |
| `.cursor/DIALOGUE_LOG.md` | история правок и неудачных попыток |
