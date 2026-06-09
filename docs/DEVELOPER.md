# TABLE-GRID-LINES v1 — заметки разработчика

## Версия

- Сборка: `4.2.0-table-grid-lines`
- DLL 2016–2024: `dll 2016/PosCounter.Net.dll` (`net46`)
- DLL 2025+: `dll 2026/PosCounter.Net.dll` (`net8.0-windows`)
- План: `.cursor/plans/etalon_verify_v1.md`
- Сборка (подробно): **`docs/BUILD.md`**
- Инженер: `docs/INSTRUCTION_ENGINEER.md`

## Сборка

| AutoCAD | Скрипт | Framework |
|---------|--------|-----------|
| 2016–2024 | `build\build-ac2016.cmd` | `net46` |
| 2025+ | `build\build-ac2026.cmd` | `net8.0-windows` |

Пути к установленному AutoCAD: `build\AutoCAD.props` (из `build\AutoCAD.props.template`), импорт через `Directory.Build.props`.

**net46 (AutoCAD 2016):** NuGet `System.ValueTuple` — кортежи в `CellIndex.cs` и ключи `Dictionary (r,c)` в `TableGrid.cs`; без пакета VS выдаёт CS8179/CS8137.

## Команды AutoCAD


| Команда                    | Назначение                                                          |
| -------------------------- | ------------------------------------------------------------------- |
| `NETLOAD`                  | Загрузка DLL → **палитра открывается сама** (`Commands.Initialize`) |
| `POSC`                     | Единственная команда для пользователя — показать палитру            |
| `POSC2_RUN_INTERNAL`       | Служебная: подсчёт (палитра «ЗАПУСТИТЬ»)                            |
| `POSC2_SPEC_INTERNAL`      | Служебная: pick spec + qty + имена                                  |
| `POSC2_HIGHLIGHT_INTERNAL` | Служебная: подсветка handles                                        |


Служебные: `CommandFlags.NoHistory | NoActionRecording | Session`.

## Сценарий в палитре

1. **ЗАПУСТИТЬ** — `PosCounterEngine` (LOCK).
2. **Выбрать спецификацию** — N рамок, `SpecGridSession.Scopes`, writeback **Кол.** из видимых qty.
3. **Сброс** — очистка in-memory сессии без `NETLOAD`: `_lastCountRows`, `_rowsAll`, `_lastMarkNames`, `SpecGridSession.ClearScopes()`, фильтры палитры, таблица и индикаторы; подсветка на чертеже — `Commands.ClearDrawingHighlight()`. Настройка «Все объекты в модели» не сбрасывается.
4. **Экспорт** (низ палитры) — `ExportService`, не CellText таблицы.

## Логи

Диагностика отключена. В CMD: `[INFO]` при pick таблиц, `[POSC]` при отсутствии qty, смешанных слоях сетки, пустой «Поз.». Статус — `PosCounterControl.SetStatus`.

## Сетка таблицы (spec-grid-qty-fix)

- `TableGridBuilder.BuildMergedGridAxes` — доминирующий слой (≥30%) + виртуальное дополнение осей X/Y с других слоёв (`GridAxesMergedFromMixedLayers`, `GridMergeLayerNote`).
- `GridLineSeg.SegmentLength`, `Y1/Y2` — вертикали участвуют в `AutoDetectGridLayer` / `IsGridCandidate`.
- `SpecGridService.ResolveQtyCellRowBottomExByColQtyGrid` — нижняя граница ячейки «Кол.» (exclusive) по H-линиям в полосе X ColQty (`HasHBorderAt` + `scope.HorizontalLines`); потолок — `ResolveNextKeyRowTopEx` (верх следующей марки), **не** `KeyToMarkBlockEnd`.
- `SpecGridService.ResolveQtyInsertPoint` — Y = `(GridYs[rowTop] + GridYs[rowBottomEx]) / 2`; при однострочной ColQty (`rowBottomEx == rowTop+1`) поведение как после отката; X по геометрическому центру ячейки (`gridCenter`).
- `FindQtyTextInCell` — при merged ColQty (`rowBottomEx > rowTop+1`): `IsPointInQtyCellSpan` + tie-break ближайший к Y центру; иначе одна строка + tie-break по длине текста.
- `QtyTableTextAppearance.TextHeight` — из ColQty → тело таблицы; fallback `QtyTextHeightFallback = 2.5`.
- CMD: `ReportGridBuildWarnings`, `ReportEmptyMarkColumnWarnings`.

## Шапка таблицы (header-col-detect-log)

- `TableGridBuilder.DetectHeader` — после `EstimateHeaderEndRow` читает шапку по динамическому `HeaderEndRow` (fallback `HeaderScanMaxRow+1`); использует `AllTexts` для устойчивости к MText/разметке.
- `SpecGridService.ReportDetectedHeader` — пишет в CMD: «Распознана шапка…» и перечисляет найденные столбцы (`ColMark`, `ColName`, `ColQty`) с короткой подписью из шапки.

## Шапка MText + уникальные столбцы (header-qty-cell-fix)

- `CreateTextSampleFromMText` — для привязки к ячейке (`AssignCells`) точка MText = **центр** `GeometricExtents` (`ExtentsCenter`), fallback `Location`. DBText без изменений.
- `BuildHeaderTextForColumn` — единый источник текста шапки (`AllTexts` + fallback `CellText`); используется в `DetectHeader` и `DescribeHeaderColumn` (CMD).
- `EnsureUniqueHeaderColumns` — порядок назначения: **Марка → Кол. → Наименование**; один столбец — одна роль; при коллизии берётся следующий по score; вызывается в `DetectHeader` до pass-2 `CellText` и `BindKeys`.
- `ResolveVisualQtyColumnCenterX` — **не менялся**: X = геометрический центр ячейки сетки (`gridCenter`).
- План: `plans/header-qty-cell-fix.md`

## Ложные столбцы шапки (fix-header-phantom-cols)

- `MinHeaderScore = 10` — столбец назначается только при совпадении слова в шапке (`PickBestHeaderColumn`); score 0 → `Col = -1`, не «столбец 0 «—»».
- `BuildHeaderTextForColumn` — после `Row/Col` добавлен геометрический проход: X в полосе `GridXs[col]`, Y в полосе шапки (`GridYs[0]..GridYs[HeaderScanMaxRow+1]`).
- Расширенные токены: марка (`п/п`, `№`, `номер`…), наименование (`назван`…), кол. (`кол-во`, `к-во`, `ед`).
- `ReportDetectedHeader` — отдельные `[POSC]` при `Col = -1` и при пустом заголовке «—».
- План: `plans/fix-header-phantom-cols.md`

## Шапка DBText (fix-header-dbtext-geom)

- `CreateTextSampleFromDbText` — центр `GeometricExtents`, fallback `GetDbTextPoint`; `[CELL-ASSIGN] kind=DBText` лимит 20.
- `CollectHeaderTextForColumn` — проходы A (Row/Col) → B (геометрия, всегда) → C (`CellText` fallback).
- `TryGetHeaderBandY` — `yTop = Max(GridYs[0], GridYs[n])`, `yBottom = Min(...)`.
- `BuildHeaderDiagnosticMessage` — CMD `[POSC] Диагностика шапки` только при `ColMark < 0` или `ColQty < 0`.
- Токен `поз.` в score марки.
- План: `plans/fix-header-dbtext-geom.md`

## Динамическая полоса шапки по Y (fix-header-y-band)

- `ScopeGridResult.HeaderEndRow` — нижняя граница шапки (exclusive): строки `0 .. HeaderEndRow-1`.
- `EstimateHeaderEndRow` — до `DetectHeader`: H-линии на полной ширине (`MaxHeaderBorderScanRow=12`), fallback — первая строка с `TryParseMarkKey` (r≥2) или `min(6, rows-1)`.
- `FindHeaderBoundaryRow` — общая логика границы по горизонталям; используется в `EstimateHeaderEndRow` и `FindFirstDataRowAfterHeaderBoundary`.
- `ResolveHeaderEndRow` — `HeaderEndRow` если >0, иначе `HeaderScanMaxRow+1`.
- `TryGetHeaderBandY` / `CollectHeaderTextForColumn` — полоса Y и проходы A/C по `headerEndRow`, не фиксированные 0..3.
- `IsAllHeaderGeomZero` + `BuildHeaderExtendedDiagnostic` — при `geom=0` на всех столбцах: Y-полоса, до 3 текстов (DBText→MText) с `colHit` и `Y в полосе?`.
- Порядок в `Build`: pass1 `CellText` → `EstimateHeaderEndRow(filteredH)` → `DetectHeader`.
- План: `plans/fix-header-y-band.md`

## Шапка по верхней полосе текстов (header-text-top-band)

- **Проблема:** при смешанных слоях линий `GridYs` может быть неверным; тексты шапки имеют корректные X/Y из `GeometricExtents`.
- `HeaderTopBandHeight = 2000` — полоса `[maxY−2000 .. maxY]` (СПДС: от «Спецификация» до строки «Поз./Кол.»).
- `DetectHeaderByTopTextBand` — основной путь: ключевые слова в текстах полосы, столбец только по `ResolveColumnIndexByX` (без `Row`/`Col` из сетки).
- `DetectHeader` → сначала текстовая полоса; при `ColMark<0` или `ColQty<0` — fallback `DetectHeaderByColumns` (старый путь + `fix-header-y-band`).
- `HeaderDetectedByTopTextBand` — флаг успеха; `BuildHeaderTextForColumn` для CMD берёт тексты полосы только при этом флаге.
- CMD: `BuildHeaderTopBandDiagnostic` при сбое; `BuildHeaderExtendedDiagnostic` — только если fallback и `IsAllHeaderGeomZero`.
- План: `plans/header-text-top-band.md`

## Уточнение ColMark по цифрам в данных (refine-colmark-by-data)

- `CountDataMarkKeysInColumn` — как `BindKeys`: `Row≥0`, `!IsSectionHeaderRow`, `IsTextInColumnXBand`, `Y<HeaderTopBandLo` + `CellText` по столбцу; при refine пропуск `ColQty`.
- `RefineColMarkByDataMarks` — после `EnsureUniqueHeaderColumns`: если в ColMark `< 2` марок, выбрать столбец с max марок, boost `markScores`, **повторный** `EnsureUniqueHeaderColumns`.
- `ColMarkRefinedFrom` / `ColMarkRefinedTo` — CMD `FormatColMarkRefineMessage`; при пустом `KeyToRowMark` — `FormatDataMarkCountsDiagnostic`.
- План: `plans/refine-colmark-by-data.md`

## Исправление порядка GridYs при merge слоёв (fix-grid-ys-merge-order)

- `BuildMergedGridAxes` при mixed layers вызывает `MergeAxisClusters` с `sortAsc: false` для Y (сверху вниз) и `true` для X.
- Раньше `MergeAxisClusters` всегда сортировал по возрастанию → `GridYs` переворачивался → `TryGetCellIndex` не находил строки (`Row=-1`) → `BindKeys` и подсчёт марок = 0.
- План: `plans/fix-grid-ys-merge-order.md`

## Properties KV pipeline (properties-kv-v2)

План: `properties_kv_v2_f507a30c.plan.md` (заменяет `strict_properties_kv_92255370`).

- **Ключ:** `BindKeysFromProperties` — `Contents`/`Raw` из ColMark, геометрия `DataX`/`DataY`; `KeyToRowMark` заполняется только здесь. Конфликт двух марок в одной строке → победитель по `CellText` или верхний по `DataY`; CMD `[POSC] Марка: наложение…`.
- **Значение:** `CollectNamePartsFromProperties` — `Contents` из ColName по диапазону строк марки; `PickNameTextForRow` — один текст на строку (PrimaryNameLayer → ExtraNameLayers → NameScore); CMD `[POSC] NAME-OVERLAY…`.
- **Геометрия:** `TextSample` — `HeaderX/Y` (шапка, ExtentsCenter), `DataX/Y` (Position/Location), `YMin`/`YMax`, `TextHeight`, `SourceIndex`. Pass-1 `AssignCellsHeader`; pass-2 `AssignCellsData` (экстент для ColName, точка для ColMark).
- **Экстент:** `TryGetRowOverlapFraction` / `TextOverlapsRowBand`; fallback высоты: `TextHeight` → `PrimaryNameTextHeight` → `MedianRowStep` → `QtyTextHeightFallback`.
- **Spanning MText:** `SourceIndex` + `consumedSources` — `\P`-строки из одного объекта не дублируются на нижних строках.
- **Парсер марки:** `TryParseMarkKey` — хвостовые `. , ; : ) ]` (`43.` → 43).
- **Палитра:** без изменений — `MarkNamePairs` → `BuildCombinedMarkNames` → `ApplyMarkNamesToPalette`.
- **Лог:** `[KV-PAIR]`, `[NAME-JOIN]` с `texts=N`.

## Properties KV v2.1 — bleed / stop (kv-v2-bleed-fix)

- `CellIndex.TryGetRowByExtent`, `GetDominantRow`; `TextSample.DominantRow` в `AssignCellsData`.
- `RowToKeyMark` + `ResolveMarkKeyAtRow` — properties-first границы марок.
- `IsUpstreamBleedFromForeignMark` — skip текста, чей dominant row принадлежит другой марке; **не** отбрасывает `t.Row == row`; CMD `[NAME-BLEED]`.
- `PickBestTextSampleForRow` — `NameScore > 0` или `IsAcceptableNameContinuation`; CMD `[NAME-OVERLAY]`.
- `StopAtSecondStandaloneName` — break при втором standalone; CMD `[NAME-STOP]`.
- `LooksLikeSectionHeaderLine`, `IsStandaloneProductName` в `MTextPlainText`; CMD `[NAME-SECTION]`.
- `TextsByRow` — по `t.Row`; fallback на `AllTexts`, если кэш строки пуст.

## Ключевые классы

- `Commands.cs` — `Initialize` → auto `ShowPalette`; POSC + POSC2_*
- `PaletteHost.cs` — `RequestRun`, `RequestSelectSpec`, `TryBuildQtyByKeyForWriteback`
- `SpecGrid/TableGrid.cs` — сетка, merge имён
- `SpecGrid/SpecGridService.cs` — pick, `WriteQtyScope`, стиль qty per scope
- `SpecGrid/CellIndex.cs` — ячейка eps=2
- `Engine/PosCounterEngine.cs` — **не менять** без ТЗ

## Ограничения

- `PosCounterEngine` — PALETTE-COUNT-LOCK.
- DWG: только колонка «Кол.».
- Имена в палитру — с фильтром слоя.

## Ручная проверка

- NETLOAD → палитра без ввода POSC
- ЗАПУСТИТЬ → строки в таблице
- Выбрать спецификацию → NameFromSpec + «Кол.» в DWG
- Стиль «Кол.» как основной текст таблицы, сноски на месте
- POSC повторно открывает палитру

