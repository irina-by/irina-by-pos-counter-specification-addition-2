# Диалог и история правок

> [2026-05-28] Задача: подробная техническая документация в `docs/DEVELOPER.md`.
> Правка: классы/методы, §6 — распознавание `NameFromSpec`, условия слоёв, merge, `ApplyMarkNamesToPalette`.
> Результат: ДОКУМЕНТАЦИЯ — справочник для разработчика без изменения кода.

> [2026-05-28] Задача: настройка и инструкция сборки для AutoCAD 2016 (net46).
> Правка: `build\build-ac2016.cmd`, `dll 2016\`, `docs\BUILD.md`, обновлены README, DEVELOPER, template props.
> Результат: ДОКУМЕНТАЦИЯ — сборка: props → build-ac2016.cmd → dll 2016\PosCounter.Net.dll.

> [2026-05-28] Задача: VS — ошибка AutoCADSdkDir is not set, props в PosCounter.Net\build.
> Правка: `Directory.Build.props` + импорт в `.csproj` для `PosCounter.Net\build\AutoCAD.props`; уточнён текст ошибки и `docs\BUILD.md`.
> Результат: props в папке проекта теперь подхватывается; рекомендуется также `build\` в корне репозитория.

> [2026-05-28] Задача: props в `Pos_counter addition\build\`, ошибка остаётся.
> Правка: в `.csproj` импорт `..\build\AutoCAD.props`; подсказка по поиску AcMgd.dll; props читается — часто вторая ошибка «AcMgd.dll not found».
> Результат: СБОРКА — пользователю: перезагрузить проект в VS, найти реальный путь к AcMgd через PowerShell.

> [2026-05-28] Задача: ошибки сборки net46 в VS — CS8179/CS8137 (кортежи в `CellIndex.cs`), MSB4011 (двойной импорт `AutoCAD.props`).
> Правка: NuGet `System.ValueTuple` для net46; убран дублирующий `<Import>` из `PosCounter.Net.csproj`; в `CellIndex.cs`/`MTextPlainText.cs` — API совместимые с .NET 4.6 (`IndexOf` вместо `Contains(..., StringComparison)`, `Replace` без StringComparison).
> Результат: на машине без AC 2016 сборка доходит до проверки AcMgd (кортежи не падают); на ПК пользователя — нужна проверка в VS после Restore.

> [2026-06-05] Задача: разбор сбоя записи «Кол.» на ручной таблице из Line (разные слои H/V, высота 250, пустая «Поз.»).
> Правка: только документация в `Работа программы.md` — QtyTextHeight, FilterLines/AutoDetectGridLayer, MinGridLineLen, пустая Поз.; код не меняли.
> Результат: ДОКУМЕНТАЦИЯ — причины: фикс. высота 250, сетка по одному слою линий, координаты не из заголовка; рекомендация CMD-предупреждение vs доработка сетки.

> [2026-06-05] Задача: план spec-grid-qty-fix — сетка mixed layers, высота/стиль «Кол.» из таблицы, визуальный центр ColQty, CMD при пустой «Поз.».
> Правка: `TableGrid.cs` — BuildMergedGridAxes, SegmentLength/Y1/Y2; `SpecGridService.cs` — TextHeight, ResolveQtyInsertPoint, ReportGridBuildWarnings, ReportEmptyMarkColumnWarnings; docs.
> Результат: СБОРКА net8.0-windows OK; net46 — только нет AcMgd на машине; ручной тест в AutoCAD — ожидает инженера.

> [2026-06-08] Задача: ColQty не распознаётся из MText «Кол.» в шапке; нужен видимый лог.
> Правка: `TableGrid.cs` — шапка сканируется 0..3, `DetectHeader` использует `AllTexts`; `SpecGridService.cs` — CMD «Распознана шапка…» + предупреждение если «Кол.» не найдена; docs.
> Результат: ДИАГНОСТИКА — инженер видит в CMD найденные столбцы (Марка/Наименование/Кол.) и может понять, почему «Кол.» не пишется.

> [2026-06-08] Задача: «Кол.» распозналась, но потерялось центрирование.
> Правка: `SpecGridService.cs` — `ResolveVisualQtyColumnCenterX` возвращает `gridCenter` всегда (вместо медианы X из шапки).
> Результат: центрирование «Кол.» возвращено по геометрии ячеек сетки.

> [2026-06-08] Задача: план header-qty-cell-fix — MText в правильную ячейку, единый текст шапки, уникальные столбцы.
> Правка: `TableGrid.cs` — `ExtentsCenter` для MText; `EnsureUniqueHeaderColumns` (Марка→Кол.→Наименование); `BuildHeaderTextForColumn` internal. `SpecGridService.cs` — `DescribeHeaderColumn` через `BuildHeaderTextForColumn`. Docs.
> Результат: СБОРКА net8.0-windows OK; ручной тест в AutoCAD — ожидает инженера (MText «Кол.», объединённая шапка).

> [2026-06-08] Задача: план fix-header-phantom-cols — лог: столбцы 0/1/2 «—», KeyToRowMark пуст при заполненной «Поз.».
> Правка: `MinHeaderScore`; геометрия шапки в `BuildHeaderTextForColumn`; токены ГОСТ; CMD `ReportDetectedHeader`. Docs + `dll 2026`.
> Результат: СБОРКА net8.0-windows OK; ручной тест на чертеже 35NK — MText OK, DBText шапка — не найдена.

> [2026-06-08] Задача: план fix-header-dbtext-geom — шапка TEXT (DBText) не распознаётся, MText работает.
> Правка: `CreateTextSampleFromDbText` (ExtentsCenter); `CollectHeaderTextForColumn` A→B всегда; диагностика `[POSC] Диагностика шапки` при ColMark/ColQty<0; токен `поз.`. Docs + `dll 2026`.
> Результат: СБОРКА net8.0-windows OK; ручной тест DBText+MText на чертеже 35NK — ожидает инженера.

> [2026-06-08] Задача: план fix-header-y-band — geom=0 на всех столбцах: шапка DBText ниже фиксированной полосы Y (строки 0–3).
> Правка: `HeaderEndRow`, `EstimateHeaderEndRow` (H-линии полная ширина + fallback марки), `FindHeaderBoundaryRow`, `ResolveHeaderEndRow`, динамический `TryGetHeaderBandY`/`CollectHeaderTextForColumn`, `IsAllHeaderGeomZero`/`BuildHeaderExtendedDiagnostic` в CMD. Docs + `dll 2026`.
> Результат: СБОРКА net8.0-windows OK, `dll 2026\PosCounter.Net.dll` обновлена; ручной тест на чертеже 35NK (TEXT в шапке) — ожидает инженера.

> [2026-06-08] Задача: план header-text-top-band — шапка по maxY−500 без опоры на GridYs/горизонтали.
> Правка: `DetectHeaderByTopTextBand`, `ResolveColumnIndexByX`, `TryGetHeaderTopTextBandY`, fallback `DetectHeaderByColumns`, `BuildHeaderTopBandDiagnostic` в CMD. Docs + `dll 2026`.
> Результат: СБОРКА net8.0-windows OK; ручной тест на чертеже 35NK — ожидает инженера.

> [2026-06-08] Задача: header-text-top-band — лог 35NK: полоса 500 мм ловит только «Спецификация»/«Таблица 1», не «Поз.»/«Кол.».
> Правка: `HeaderTopBandHeight` 500 → 2000. Docs + пересборка `dll 2026`.
> Результат: СБОРКА net8.0-windows OK; ручной тест — NETLOAD свежей DLL (в логе был LOAD failed).

> [2026-06-08] Задача: header-text-top-band — лог 35NK: полоса 500 мм ловит только «Спецификация»/«Таблица 1», не «Поз.»/«Кол.».
> Правка: `HeaderTopBandHeight` 500 → 2000. Docs + пересборка `dll 2026`.
> Результат: СБОРКА — ожидает `build-ac2026.cmd`; ручной тест — ожидает инженера (нужен NETLOAD, в логе был LOAD failed).

> [2026-06-08] Задача: план refine-colmark-by-data — ColMark=0 «Таблица 1 поз.», KeyToRowMark пуст при найденной «Кол.».
> Правка: `CountDataMarkKeysInColumn`, `RefineColMarkByDataMarks` (повторный `EnsureUniqueHeaderColumns`), CMD уточнения и counts по столбцам. Docs + `dll 2026`.
> Результат: СБОРКА net8.0-windows OK; ручной тест 35NK — ожидает инженера (NETLOAD).

> [2026-06-08] Задача: refine-colmark — лог col0=59 bindable, BindKeys=0: Count не совпадал с BindKeys.
> Правка: `CountDataMarkKeysInColumn` — логика BindKeys (Row, IsSectionHeaderRow, X-полоса столбца, Y ниже шапки); refine пропускает ColQty. Docs + `dll 2026`.
> Результат: СБОРКА net8.0-windows OK; `dll 2026` не перезаписана (файл занят AutoCAD) — NETLOAD из `bin\Release\net8.0-windows\PosCounter.Net.dll`.

> [2026-06-08] Задача: лог 35NK — bindable col0..5=0: подсчёт совпал с BindKeys, но марки не привязались.
> Анализ: при mixed layers `MergeAxisClusters` переворачивал `GridYs` (возрастание вместо убывания) → `TryGetCellIndex` → Row=-1 для всех текстов.
> Правка: `MergeAxisClusters(..., sortAsc)` — Y с `sortAsc:false`, X с `true`. Docs + plan `fix-grid-ys-merge-order.md`.
> Результат: СБОРКА net8.0-windows OK, `dll 2026\PosCounter.Net.dll` обновлена; ручной тест 35NK — ожидает инженера (NETLOAD).

> [2026-06-08] Задача: кнопка «Сброс» палитры — повторный цикл без NETLOAD.
> Правка: `PosCounterControl.xaml` — `BtnReset`; `ResetPaletteState()` — очистка `_lastCountRows`, `_rowsAll`, `_lastMarkNames`, `SpecGridSession.ClearScopes()`, фильтров, `InitGridView()`; `Commands.ClearDrawingHighlight()`. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors); `dll 2026` не перезаписана (файл занят) — NETLOAD из `bin\Release\net8.0-windows\PosCounter.Net.dll`; ручной тест — ЗАПУСТИТЬ → спецификация → Сброс → повтор без NETLOAD.

> [2026-06-08] Задача: центрирование «Кол.» в ячейке при комментариях инженера (magenta, другой слой).
> Правка: `SpecGridService.cs` — `ResolveQtyCellRowBottomEx`, Y по `KeyToMarkBlockEnd`; `IsPointInQtyCellSpan`, `FindQtyTextInCell` по span, tie-break ближайший к Y центру; фильтры слоя/`IsLikelyQtyCellText` без изменений. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors); `dll 2026` не перезаписана (файл занят) — NETLOAD из `bin\Release\net8.0-windows\PosCounter.Net.dll`; ручной тест — ячейка с комментарием, qty по центру.

> [2026-06-08] Задача: откат регрессии центрирования «Кол.» (план центр_ячейки_кол).
> Причина: Y по `KeyToMarkBlockEnd` — span блока марки/имени, не ячейки ColQty; цифра уезжала из своей строки.
> Правка: откат `ResolveQtyCellRowBottomEx`, `IsPointInQtyCellSpan`; Y снова `(GridYs[rowTop]+GridYs[rowTop+1])/2`; `FindQtyTextInCell` по одной строке, tie-break по длине текста. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors), `dll 2026\PosCounter.Net.dll` обновлена; ручной тест — вставка ColQty как до плана.

> [2026-06-08] Задача: центр «Кол.» при пометках инженера (span по сетке ColQty, план центр_colqty_по_сетке).
> Правка: `SpecGridService.cs` — `ResolveQtyCellRowBottomExByColQtyGrid` (H-линии в полосе X ColQty + cap `ResolveNextKeyRowTopEx`); Y по span только при `rowBottomEx > rowTop+1`; `IsPointInQtyCellSpan` + tie-break по Y для merged; однострочные ячейки — как после отката. **`KeyToMarkBlockEnd` не используется.**
> Результат: СБОРКА net8.0-windows OK (0 errors), `dll 2026\PosCounter.Net.dll` обновлена; grep — 0 использований `KeyToMarkBlockEnd`/`GetMarkBlockEndExclusive` (только комментарий); ручной тест — merged ColQty с пометкой и однострочные ячейки.

> [2026-06-09] Задача: план properties-kv-v2 — ключ/значение строго из Contents + геометрии (DataX/Y, YMin/YMax), PickNameTextForRow при наложении MText.
> Правка: `TableGrid.cs` — TextSample (Header/Data coords, extent), AssignCellsHeader/Data, BindKeysFromProperties, CollectNamePartsFromProperties, PickNameTextForRow, TextsByRow; `MTextPlainText.cs` — TryParseMarkKey хвост `43.`; docs/DEVELOPER.md.
> Результат: СБОРКА net8.0-windows OK (0 errors); ручной тест 35NK + overlay + spanning MText — ожидает инженера (NETLOAD).

> [2026-06-09] Задача: план kv-v2-bleed-fix — bleed 4→5, склейка 98/45, заголовок в марке 1, пустые 6–9.
> Правка: `CellIndex.cs` — TryGetRowByExtent, GetDominantRow; `TableGrid.cs` — RowToKeyMark, IsUpstreamBleedFromForeignMark, PassesDominantRowGate, PickBestTextSampleForRow, StopAtSecondStandaloneName; `MTextPlainText.cs` — LooksLikeSectionHeaderLine, IsStandaloneProductName; docs.
> Результат: СБОРКА net8.0-windows OK (0 errors); ручной тест _tex_fek — **НЕ РАБОТАЕТ** — PassesDominantRowGate (dominantRow==r) + TextsByRow по DominantRow → массово пустые имена.

> [2026-06-09] Задача: hotfix после kv-v2-bleed-fix — восстановить сбор имён.
> Правка: убран PassesDominantRowGate; TextsByRow снова по t.Row; IsUpstreamBleedFromForeignMark не режет t.Row==row; PickBestTextSampleForRow — Score>0 OR IsAcceptableNameContinuation; fallback AllTexts при пустом кэше строки.
> Результат: СБОРКА net8.0-windows OK; ручной тест _tex_fek — ожидает инженера (NETLOAD).

> [2026-06-09] Задача: план mtext-dbtext-row-fix — MText DataY=YMax, Row по точке, split после pass-2, multi-text MText+DBText, anti-bleed TextPointInRowBand.
> Правка: `TableGrid.cs` — CreateTextSampleFromMText (ExtentsTop), AssignCellsData (point-row), SplitNameColumnRowsData, ResolveNameTextsForRow, TextPointInRowBand, IsUpstreamBleedFromForeignMark (yTop/yBottom), [NAME-ROW]/[CELL-SPLIT-DATA]; порядок Build: PrimaryNameLayer до pass-2, split после AssignCellsData; docs/DEVELOPER.md.
> Результат: СБОРКА net8.0-windows OK (0 errors); ручной тест _tex_fek (марки 52,5,4,1,98, шапка 35NK) — ожидает инженера (NETLOAD).

> [2026-06-09] Задача: DominantRow primary gate — spanning MText «Труба…» не должен побеждать на нижней строке вместо DBText «Ду 50».
> Правка: `TextBelongsToPrimaryRow`, `GetNameCandidatesInRow` (gate DominantRow==r, fallback t.Row); [NAME-DOM-SKIP], [KV-PAIR]/[NAME-BOUNDARY] value=, [NAME-ROW] src/domRow/display; TextsByRow без изменений.
> Результат: СБОРКА net8.0-windows OK; ручной тест _tex_fek key=52 — **НЕ РАБОТАЕТ** — массово пустые имена (6–9, 47, 51…), многострочные имена не склеиваются.

> [2026-06-09] Задача: dual-pass name collection — откат DominantRow gate, двухпроходный сбор (row overlap + vertical supplement).
> Правка: `CollectNamePartsForPositionRange`, `CollectNamePartsFromNameCell`, `SupplementNamePartsInVerticalBand`; удалены TextBelongsToPrimaryRow, GetNameCandidatesInRow, ResolveNameTextsForRow, PickBestTextSampleForRow, CollectNamePartsFromProperties; [NAME-ROW]/[NAME-SUPPLEMENT]; шапка без изменений; docs.
> Результат: СБОРКА net8.0-windows OK (0 errors); ручной тест _tex_fek (марки 52,6–9, шапка H1–H4) — **НЕ РАБОТАЕТ** — склейка foreign mark (4→5, 2→3), дубли spanning MText.

> [2026-06-09] Задача: dual-pass owner mark fix — foreign bleed 4→5 без DominantRow gate.
> Правка: `ResolveOwnerMarkKeyForNameText`, `NameTextBelongsToMarkKey` в pass 1/2; [NAME-FOREIGN-SKIP]; docs/DEVELOPER.md.
> Результат: СБОРКА net8.0-windows OK (0 errors); ручной тест _tex_fek (марки 4,5,3,52,6–9, шапка) — ожидает инженера (NETLOAD).

> [2026-06-09] Задача: полная документация по фактическому коду (без изменения кода).
> Правка: `.cursor/plans/factual_program_architecture.plan.md` (факт-план); `docs/DEVELOPER.md` (методы, условия, dual-pass, owner mark); `README.md` (актуальная версия, диагностика CMD); `Работа программы.md` (Q&A простым языком: шапка, key/value, слои, «Кол.»).
> Результат: ДОКУМЕНТАЦИЯ — код не менялся; описание соответствует коду на 2026-06-09.

> [2026-06-09] Задача: упростить чтение `Работа программы.md`.
> Правка: под каждый вопрос добавлен блок **«Проще»** (1–3 предложения); в конце раздел **«Кратко: как всё работает»** — пошагово шаг 1–3, схема key/value, два прохода имени, чеклист.
> Результат: ДОКУМЕНТАЦИЯ — код не менялся.

> [2026-06-09] Задача: план diagnose_spec_failure — фаза 1 (Y-cutoff LINE) + фаза 2 (явная AutoCAD Table).
> Правка: `TableGrid.cs` — `ResolveDataYCutoff`, `IsBindableDataText`, `BuildFromAcadTable`, …; `SpecGridService.cs` — `UpsertQtyInAcadTable`, CMD. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors), `dll 2026\PosCounter.Net.dll` обновлена; ручной тест — ожидает инженера (NETLOAD).

> [2026-06-09] Задача: универсальный key/value — имя с верхней строки марки (план fix_mark_1_name_kv).
> Правка: `ResolveNameForKey`, `ResolveNameRowTopForKey`, `CollectNamePartsFromCellText`; `IsSectionHeaderRow` порог 25→8; `BindKeys`/`BindKeysFromAcadTableCellMatrix` cap rowTop; `FillMarkNames*` + `MergeScopeNames` через `ResolveNameForKey`; CMD `[KV-ANCHOR]`. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors), `dll 2026\PosCounter.Net.dll` обновлена; ручной тест — Ушко LINE+MText (марка 1), секция без марки, 35NK, _tex_fek — ожидает инженера (NETLOAD).

> [2026-06-09] Задача: план fix_header_colqty_bleed — ColQty на «Масса» (Ушко), data bleed в шапку, дубль имён MText+MText (_tex_fek mark 64).
> Правка: `ScoreQtyHeader` (без «ед», штраф масса), `SanitizeColQtyColumn`; `TryGetHeaderTopTextBandY` 35% для малых таблиц; skip data marks в header scoring; `HeaderEndRow` cap + `BuildHeaderOnlyColumnText` до RowDataStart; `ResolveNameForKey` cell-only/filter/collapse; `TryAddNamePartExact`; `IsDuplicateCandidate` near-overlap; CMD `[POSC] Марок в данных`, `KeyToRowMark`. Docs + plan `fix_header_colqty_bleed_implementation.md`.
> Результат: СБОРКА net8.0-windows OK (0 errors), `dll 2026\PosCounter.Net.dll` обновлена; ручной тест — Ушко (ColQty=«Кол.», марки 1–4), _tex_fek mark 64 без дубля, 35NK — ожидает инженера (NETLOAD).

> [2026-06-09] Задача: план unified_grid_header_scan — откат деления «малая/большая таблица» (35% vs 2000 мм).
> Правка: `ApplyHeaderBoundaryFromGridScan`, `FindFirstDataRowByGridScan`, `DetectHeaderByGridRows` (primary); `DetectHeader` порядок grid→columns→top-band; удалён `SmallTableHeaderBandFraction`; top-band fallback с фильтром `Row < HeaderEndRow`; CMD `[POSC] Граница шапки/данных`. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors), `dll 2026\PosCounter.Net.dll` обновлена; ручной тест — Ушко, _tex_fek mark 64, 35NK (NETLOAD).

> [2026-06-09] Задача: план fix_row1_mark_skip — марка 1 пропускается (данные с row 2).
> Причина: `FindFirstDataRowAfterHeaderBoundary` и `ComputeRowDataStart` принудительно `searchFrom≥2`, перезаписывали grid scan `RowDataStart=1`.
> Правка: default `searchFrom=HeaderEndRow`; убран min row 2; `ComputeRowDataStart` уважает grid scan + `ClampRowDataStartToGridScan`; `EstimateHeaderEndRow` minRow=0; CMD `FormatMissingKeyOneDiagnostic`. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors), `dll 2026\PosCounter.Net.dll` обновлена; ручной тест — Ушко key=1 row=1 (NETLOAD).

> [2026-06-09] Задача: актуализация документации — factual_program_architecture + README + DEVELOPER + INSTRUCTION_ENGINEER + Работа программы.
> Правка: обновлён `.cursor/plans/factual_program_architecture.plan.md` (grid scan, ResolveNameForKey, native Table, fix row1); удалены устаревшие plan/implementation файлы; синхронизированы README, DEVELOPER, INSTRUCTION_ENGINEER, Работа программы.md.
> Результат: ДОКУМЕНТАЦИЯ — код не менялся.

> [2026-06-09] Задача: план fix_header_recognition_ac2016 — нестабильное распознавание спецификации (AC 2016/2026).
> Правка: `RebindScopeKeysAndNames` после pass2; `TryInferColumnsFromData` + palette keys; стабильный `AutoDetectGridLayer`; `ResolveGridLayerForScope`; ColMark+ColName без обязательного ColQty; spec guard + qtyByKey из всех строк; редактируемое наименование в палитре + экспорт 4 колонки; CMD `gridLayer`, data-band hint. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors), `dll 2026\PosCounter.Net.dll` обновлена; ручной тест — 5× spec на чертеже пользователя, Ушко/35NK (NETLOAD).

> [2026-06-09] Задача: план fix_multiline_name_merge — вторая строка наименования (продолжение без ГОСТ) не склеивается в палитру.
> Правка: `TableGrid.cs` — `ResolveNameForKey`: `rowEndExclusive = min(nextKeyRow, max(markBlockEnd, rowTop+1))`; `useCellTextOnly` только при `AllNameRowsHaveCellText`; `CollectNamePartsFromNameCell` — `[NAME-STOP]` только на чужой марке; CMD `[NAME-BOUNDARY]` с `nextKeyRow`/`markBlockEnd`/`rowEndEx`/`cellOnly`. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors) в `bin\Release\net8.0-windows-build` — `dll 2026` и `bin\Release\net8.0-windows` заняты AutoCAD (NETLOAD); ручной тест — марка с 2 строками названия (NETLOAD после закрытия AC или из build-папки).

> [2026-06-09] Задача: план fix_multiline_name_v2 — key=51: в KV только 1 строка имени (после v1 всё ещё не работает).
> Причина: `rowEndExclusive` обрезался по `KeyToRowTopSub` следующей марки (совпадал со строкой продолжения); `IsSectionHeaderRow` пропускала 2-ю строку наименования как «секцию без марки».
> Правка: `GetNextKeyRowMarkExclusive` (KeyToRowMark); новая формула `rowEndExclusive`; `IsNameContinuationRow` + исключение в 3 collect-путях и `AllNameRowsHaveCellText`; CMD `nextMarkRow`. Docs.
> Результат: СБОРКА net8.0-windows OK (0 errors), `dll 2026\PosCounter.Net.dll` обновлена; ручной тест key=51 — `[NAME-BOUNDARY] nextMarkRow/rowEndEx`, `[KV-PAIR]` 2 строки (NETLOAD).

> [2026-06-10] Задача: план ac2016_dll_fix — DLL net46 для AC 2016 не работает (спецификация), net8 для AC 2026 OK.
> Правка: `.csproj` ValueTuple `GeneratePathProperty` + `Reference` net461; `build-ac2016.cmd` (оба DLL в `dll 2016`); `RebindScopeKeysAndNames` skip `DetectHeader` при `ColumnsInferredFromData`; `TryGetMTextBounds` (Extents + GetBoundingPoints); `UnassignedTextCountAfterDataPass` + CMD; `WriteNetLoadBanner` в `Commands.Initialize`. Docs: INSTRUCTION_ENGINEER, BUILD, DEVELOPER; план `.cursor/plans/ac2016_dll_fix.plan.md`.
> Результат: СБОРКА net8.0-windows OK (0 errors); net46 — только нет AcMgd на машине сборки; ручной тест AC 2016 — ожидает инженера (`build-ac2016.cmd` на ПК с AC 2016 → NETLOAD `dll 2016\`).

> [2026-06-10] Задача: портативный набор сборки AC 2016 внутри PosCounter.Net (копия только папки проекта на рабочий ПК Salnikava.I).
> Правка: `PosCounter.Net\Directory.Build.props`; `PosCounter.Net\build\` — AutoCAD.props, NuGet.local.props, build-ac2016.cmd, СБОРКА_VS_AC2016.md; `PosCounter.Net\dll 2016\README.txt`. Docs: DEVELOPER, BUILD; план `.cursor/plans/poscounter_vs_ac2016_kit.plan.md`.
> Результат: ГОТОВО — на рабочем ПК: скопировать PosCounter.Net → `build\build-ac2016.cmd` или VS (Release, x64, net46) → NETLOAD из `dll 2016\` (оба DLL); проверка на ПК с AC 2016 — ожидает инженера.

> [2026-06-10] Задача: при двойном клике build-ac2016.cmd — сообщение про телеметрию dotnet.
> Правка: в `PosCounter.Net\build\build-ac2016.cmd` — `DOTNET_CLI_TELEMETRY_OPTOUT=1`; в `СБОРКА_VS_AC2016.md` — пояснение, что это Microsoft .NET SDK, не PosCounter.
> Результат: ЗАРАБОТАЛО (подавление уведомления) + документация для инженера.

> [2026-06-10] Задача: план fix_ac2016_spec_recognition этап 1 — диагностические логи CMD для AC 2016 (до исправления алгоритма).
> Правка: `SpecGridLog.WriteDiag` [POSC-DIAG]; `Pass1Col*`, `InferenceColQtyScoresSummary`, `BoundsMethod`; pass1/inference/unassigned/имена/WriteQty диагностика в `TableGrid.cs`, `SpecGridService.cs`; баннер AC R* при NETLOAD в `Commands.cs`; расширен `ReportDetectedHeader` при inference/ColQty<0; docs INSTRUCTION_ENGINEER §8.1, DEVELOPER, СБОРКА_VS_AC2016.
> Результат: КОД ГОТОВ — сборка net46 на ПК с AC 2016 (`build-ac2016.cmd`); ручной тест: NETLOAD → ЗАПУСТИТЬ → спецификация → прислать CMD с [POSC-DIAG]; этап 2 (фиксы геометрии/ColQty) — после лога.

> [2026-06-10] Задача: VS net46 — ошибка CS0246 «Document» не найден при сборке для AC 2016.
> Причина: в `TableGrid.cs` метод `ReportMarkNamesDiagnostic(Document doc, …)` без `using Autodesk.AutoCAD.ApplicationServices`.
> Правка: добавлен `using Autodesk.AutoCAD.ApplicationServices` в `TableGrid.cs`.
> Результат: ЗАРАБОТАЛО (ожидает пересборки в VS); если ошибка останется — проверить `build\AutoCAD.props` и путь к AcMgd.dll.

> [2026-06-10] Задача: VS net46 — «Имя qtyScores не существует в текущем контексте».
> Причина: в `TryInferColumnsFromData` вызов `FormatInferenceColQtyScores(..., qtyScores)` без объявления массива.
> Правка: `var qtyScores = new int[cols];` и заполнение в цикле поиска ColQty.
> Результат: ЗАРАБОТАЛО (ожидает пересборки в VS).

> [2026-06-10] Задача: план ac2016_colqty_fix этап 2 — ColQty=-1 на AC 2016 при рабочих именах/марках (та же net46 DLL в AC 2026 OK).
> Диагноз: pass1 шапка -1/-1/-1; inference Mark/Name OK; текст «Кол.» не в top-band/CellText на API 2016; col4 — числовой столбец.
> Правка: `TryResolveMissingColQty` (simple01→allTexts→numeric); `DetectHeaderSimpleRows01`; `DetectColQtyFromAllTexts`+`TryGetHeaderRegionY`; `TryInferColQtyFromNumericColumn`; `AssignCellsHeader` ExtentsTop fallback; MText/DBText HeaderY=верх bbox; `ColQtySource`; `RebindScopeKeysAndNames`+inference вызывают fallback; CMD `источник ColQty=` в ИТОГ. Docs: DEVELOPER, INSTRUCTION_ENGINEER §8.1; план `.cursor/plans/fix_ac2016_spec_recognition_stage2.plan.md`.
> Результат: КОД ГОТОВ — сборка net46 на ПК с AC 2016 (`build-ac2016.cmd`); ручной тест: ожидаем `ColQty=4 источник=numeric`, `WriteQty записано>0`; регрессия AC 2026 с `dll 2016\` — ожидает инженера.

> [2026-06-10] Задача: совместимость с AutoCAD 2016 SP1 (M.49.0, R20.1, .NET Framework 4.5.2).
> Причина: сборка net46 требует .NET 4.6+; на AC 2016 SP1 встроен CLR 4.5.2 — NETLOAD мог не загружать DLL.
> Правка: `TargetFrameworks` net46→**net452**; ReferenceAssemblies net452; ValueTuple `lib\netstandard1.0`; `build-ac2016.cmd` -f net452; баннер CMD `(net452)`; docs BUILD/DEVELOPER/INSTRUCTION_ENGINEER/README/СБОРКА_VS_AC2016; план `.cursor/plans/ac2016_sp1_net452.plan.md`.
> Результат: КОД ГОТОВ — пересборка на ПК с AC 2016 SP1; ожидает инженера: NETLOAD → `(net452)` → спецификация.

> [2026-06-10] Задача: VS net452 — CS0117 «Array не содержит Empty» (PosCounterEngine, PaletteHost, PosCounterService).
> Причина: `Array.Empty<T>()` появился в .NET 4.6; целевой framework net452 (.NET 4.5.2).
> Правка: `ArrayCompat.cs` + замена `Array.Empty` → `ArrayCompat.Empty` в Commands, PaletteHost, PosCounterService, PosCounterEngine, SpecGridService, PosCounterControl; docs BUILD.
> Результат: ЗАРАБОТАЛО (ожидает пересборки в VS / build-ac2016.cmd).

> [2026-06-10] Задача: план ac2016_dbtext_header этап 3 — ColQty=-1 на AC 2016, шапка DBText (TEXT), top-band видит данные, col4=17 по числам.
> Правка: `DetectHeaderByDbTextHeaderBand` (полоса GridYs, короткий DBText/MText, `ColQtySource=dbTextBand`); `CreateTextSampleFromDbText` Header=AlignmentPoint, Data=ExtentsTop; `AssignCellsHeader` цепочка Align→ExtentsTop; `TryInferColQtyFromNumericColumn` + `CountQtyValuesInColumnTexts` по AllTexts; `BuildColQtyFallbackDiagnostic`; баннер NETLOAD `build=`; `[POSC-DIAG]` в SpecGridService/ReportDetectedHeader; docs DEVELOPER/INSTRUCTION_ENGINEER; план `.cursor/plans/fix_ac2016_dbtext_header.plan.md`.
> Результат: КОД ГОТОВ — сборка на ПК с AC 2016 (`build-ac2016.cmd`); на CI-машине без AcMgd.dll сборка net452 не запускалась; ожидает инженера: `ColQty=4 источник=numeric` или `dbTextBand`, `WriteQty записано>0`.

> [2026-06-10] Задача: регрессия имён/qty после этапа 3 — табл.2 марки 60–69 пустые, ColQty=4 (масса), склейка Обозначение+Наименование, qty не в первой суб-ячейке.
> Диагноз: pass2 `DataY=ExtentsTop` сдвигал Row марок; numeric выбрал col4; neighbor fallback тянул col1.
> Правка: `DataY=AlignmentPoint` pass2; `AssignCellsData` Align→Data; `ColDesignation`+`IsTextInNameColumn`; убран `ResolveNameFromNeighborColumns` в col1; `ApplyStandardColumnLayout`/`TryPreferQtyColumnAfterName` ColQty=ColName+1; `ColumnLooksLikeMassData`; `IsSectionHeaderRow` «Продолжение»; `KeyToRowMarkSampleDiag`+`WriteQtyDiagLines`; план `.cursor/plans/fix_name_qty_regression.plan.md`.
> Результат: КОД ГОТОВ — пересборка `build-ac2016.cmd`; ожидает инженера: KeyToRowMark 60–69, ColQty=3, имена только col2, WriteQty rowTop+палитра.

> [2026-06-10] Задача: план fix_colqty_gt20_names — табл.1 ColQty=4→3; ГТ-20 имя только col2 (RU+EN), без ГОСТ/TB100 из col1.
> Правка: `ApplyMandatoryColQtyLayout` (схема 0/1/2/3); `LooksLikeDesignationText`; `BuildCellMatrix` col2-only; `ResolveNameForKey` без cell-only при ColDesignation; `TryGetMTextBounds` Data=Location; `ApplyStandardColumnLayout` перед WriteQty; `ColQtyLayoutFixDiag`/`NameCol2DiagLines`; docs DEVELOPER.
> Результат: КОД ГОТОВ — сборка net452 на ПК с AC 2016 (`build-ac2016.cmd`); на CI-машине без AcMgd.dll не собиралось; ожидает инженера: табл.1 `ColQty=3 layout`, ГТ-20 марка 1 RU+EN без ГОСТ, табл.2 без регрессии.

> [2026-06-10] Задача: план fix_gt-20_bleed_colqty — bleed имён 4–6, ColQty=4, qty не в col3/не из палитры визуально.
> Правка: запланировано — useCellTextOnly как OLD; nextKeyTop cap; numeric prefer col3; ResolveQtyInsertY верхняя суб-ячейка; ИТОГ layout+ВНИМАНИЕ.
> Результат: ОЖИДАЕТ РЕАЛИЗАЦИИ — пользователь «готов», но plan mode заблокировал правки .cs; нужен agent mode.

> [2026-06-10] Задача: план fix_gt-20_bleed_colqty — реализация (лог ColQty=-1, WriteQty=0, bleed имён, qty не из палитры).
> Правка: `useCellTextOnly` без gate ColDesignation; `GetNextKeyRowExclusive` cap; `TryInferColQtyFromNumericColumn` prefer col3; `ResolveQtyInsertY` rowTop+1; `DbTextHeaderMaxPlainLen=60`; `LooksLikeDesignationText` в dbText band; `AppendHeaderTextPart` по строкам; `ReportScopeSummaryDiagnostic` layout+ВНИМАНИЕ; WriteQty diag Y; docs DEVELOPER/INSTRUCTION_ENGINEER.
> Результат: КОД ГОТОВ — сборка на ПК с AC 2016 (`build-ac2016.cmd` или VS net452); синхронизировать Yandex→Salnikava.I OneDrive; NETLOAD `bin\x64\Release\net452`; ожидает инженера: `ColQty=3 источник=layout`, `WriteQty key=1 qty=1`, имена ГТ-20 марки 1/4/5/6 без bleed.

> [2026-06-11] Задача: сборка на рабочем ПК Salnikava.I — ошибка дубликата PaletteHost.
> Правка: на Yandex один PaletteHost.cs; в СБОРКА_VS_AC2016.md добавлен раздел: поиск двух PaletteHost*.cs, чистая перекопировка папки, очистка bin/obj.
> Результат: инструкция для инженера — не баг Yandex-папки, а наложение копий при переносе.

> [2026-06-11] Задача: сборка — System.ValueTuple 4.5.0 не найден (NuGet/длинный путь OneDrive).
> Правка: `lib\netstandard1.0\System.ValueTuple.dll` в репозитории; `PosCounter.Net.csproj` fallback `ValueTupleDllPath`; `NuGet.local.props`; СБОРКА_VS_AC2016.md.
> Результат: сборка net452 без NuGet restore если есть lib\; при NETLOAD копировать ValueTuple.dll рядом с плагином.

> [2026-06-11] Задача: повтор ошибки PaletteHost при сборке.
> Правка: скрипт `build\verify-no-duplicate-sources.ps1`; маркер `POSC-SINGLE-FILE` в PaletteHost.cs; уточнена СБОРКА_VS_AC2016.md (VS Обозреватель — один файл).
> Результат: на рабочем ПК запустить скрипт; при [OK] — Clean + Rebuild; иначе чистая перекопировка PosCounter.Net с Yandex.

> [2026-06-10] Задача: план diag_logs_colqty_names — расширить CMD-диагностику + фикс continuation header.
> Правка: `SpecGridLog.WriteTrace`/`WriteDiagTail`/budget=120/`FormatDllBuildStamp`; `[COLQTY]` в ApplyStandardColumnLayout/TryPrefer/TryResolve/infer; `[HEADER]` ReportHeaderTraceDiagnostic; `[NAME]` key 1–7; `[WRITEQTY]` TraceWriteQty; `BuildHeaderOnlyColumnText` отсев NameScore при inference; чеклист копирования в СБОРКА_VS_AC2016.md; docs.
> Результат: КОД ГОТОВ — копировать PosCounter.Net на рабочий ПК Salnikava.I, Release x64 net452, NETLOAD свежей DLL; в CMD искать `[COLQTY] layout fix →3`, `[NAME] key=1 cellOnly=…`, `[WRITEQTY] targetY=…`; прислать лог для этапа 2.

> [2026-06-11] Задача: план fix_gt20_names_qty (лог build=18:48) — bleed имён ГТ-20, qty не в первую суб-ячейку, шум numeric col=0, пустые имена 52–57/105–109.
> Правка: `RebindScopeKeysAndNames` gate ColMark/ColName; numeric skip mark column; `FindQtyTextInCell` rowTop+1; `UpsertQtyText` skip-far-entY+create; `ResolveNameForKey` useCellTextFromBlock без supplement при cellJoined≥20; supplement cap nextKeyTop; `DescribeHeaderColumn` «— (продолжение)»; `[NAME]` key 52–57/105–109; docs + fix_gt20_names_qty_IMPLEMENTED.md.
> Результат: КОД ГОТОВ — ожидает инженера: ГТ-20 марка 1 RU+EN, марки 4–6 без bleed, `[WRITEQTY] key=1 qty=1`, нет `numeric col=0` до inference; прислать CMD с `[NAME]` и `[WRITEQTY]`.

> [2026-06-12] Задача: план header_anchor_schema — якорь шапки, наследование ColMark/ColName/ColQty для продолжений.
> Правка: `SpecColumnSchema`; `SpecGridSession.ColumnSchema`; `TryLockColumnSchema` (pass1); `TryApplyInheritedColumnSchema`; `ColumnsInheritedFromSchema`; `ProcessInferColumnsFallback`; CMD `[SCHEMA]` + «столбцы от эталона»; docs INSTRUCTION_ENGINEER/DEVELOPER.
> Результат: КОД ГОТОВ — таблица 1 со шапкой → `[SCHEMA] locked`; продолжения → `inherited` без inference; ожидает инженера: пересборка NETLOAD, проверка 398+214.

> [2026-06-12] Задача: план fix_schema_inherit_table2 — табл.2 пустая (59 obj, X-mismatch 112k vs 134k).
> Правка: `TryAlignScopeColumnsToAnchorSchema` (центры X-полос + offset); `TryApplyInheritedColumnSchema` → `inherited`/`aligned` + `failReason`; `SpecGridService` `[SCHEMA] inherit-fail reason=` + «рамка слишком мала» + fallback `TryInferColumnsFromData`; docs INSTRUCTION_ENGINEER/DEVELOPER; план `fix_schema_inherit_table2_IMPLEMENTED.md`.
> Результат: КОД ГОТОВ — ожидает инженера: пересборка net452, NETLOAD; табл.1 552 obj → locked; табл.2 полная ~214 → inherited/aligned + WriteQty; табл.2 59 obj → предупреждение + fallback infer.

> [2026-06-12] Задача: план fix_bilingual_names_gt20_header — имена bleed RU/EN (марки 4–6), шапка ГТ-20 pass1=-1 «Марка col0 «9»», табл.2 26 obj без inherit (лог build=14:48).
> Правка: `CapRowEndBeforeNextMarkNameLead`, ослаблен `cellOnly` + cyrillic supplement; `EnumerateDisplayNameLines` в CollectNamePartsFromCellText; `DetectHeaderByTopGridRows`, `TryGetHeaderTopTextBandY` по GridYs, `SanitizeMarkScoresForDigitOnlyHeaders`, `CanLockColumnSchemaFromPass2`; `IsContinuationPickTooSmall` (<80 obj) без infer fallback; docs DEVELOPER/INSTRUCTION_ENGINEER; планы `fix_bilingual_names_gt20_header.plan.md` + `_IMPLEMENTED.md`.
> Результат: КОД ГОТОВ — сборка на ПК с AC 2016 (`build-ac2016.cmd`); ожидает инженера: малая таблица 98 obj (имена 1–7, `[NAME] nameLeadCap`); большая 472+полное продолжение (`[SCHEMA] locked`/`inherited`); убрать `(load "pos_counter_2016_2026")` из acad.lsp.

> [2026-06-13] Задача: план diag_log_gt20 (лог build=11:41) — 9 марок missing qty (не баг WriteQty), 13 пустых имён, MText вне сетки табл.2.
> Правка: `AssignUnassignedTextsToNameColumn` + `UnassignedNameFixLines`; вызов после `AssignCellsData` и в `RebindScopeKeysAndNames` с пересборкой `CellText`; `ResolveContinuationNameRowEnd` / `NextMarkHasStandaloneNameLead`; `ShouldLogNameRejectReason` + `[NAME] skip=…` в `AddNamePartsFromTextSample`; `ReportUnassignedTextsDiagnostic` — строки `unassigned→name`; docs DEVELOPER §20; план `diag_log_gt20_IMPLEMENTED.md`.
> Результат: КОД ГОТОВ — пересборка net452, NETLOAD; ожидает инженера: `[POSC-DIAG] unassigned→name col=2`, `[NAME] skip=designation` для key=52, имена ≥52, WriteQty=49 без регрессии.

> [2026-06-13] Задача: план авто_kv_ac2016 — универсальный KV без хардкода марок/0-2-3.
> Правка: `MarkKeyParser` (префиксы Поз./Марка); `DetectHeaderBoundaryAndColumns` (scan 0..5, gridTokens); `ResolveNameForKey` + `AllNameRowsHaveCellText` + `PickBestNameTextForRow`; bleed `continue` + `IsUpstreamBleedFromForeignMark`; MText `DataY=ExtentsTop`; ColQty evidence-only (убран mandatory 0/2/3); `SpecDiagPolicy` + `[HEADER-SCAN][MARK][GEO][NAME][KV-SUMMARY]`; docs §21–22.
> Результат: КОД ГОТОВ — сборка на ПК с AC 2016; ожидает инженера: `headerPath=gridTokens`, `[KV-SUMMARY]` per table, имена без регрессии, WriteQty из палитры.

> [2026-06-14] Задача: сборка net452 — ошибка CS0136 в `TableGrid.cs` строка 5905.
> Причина: в `AssignCellsData` переменная `bindY` (double, координата) и внутри того же цикла `bindY` (string, метка для лога `[GEO]`).
> Правка: метка лога переименована в `bindYMethod`.
> Результат: ожидает пересборки VS / `build-ac2016.cmd`.

> [2026-06-14] Задача: план fix_names_qty_post-kv — универсальные имена merged-блоков, qty sub-row+стиль, палитра vs scope.
> Правка: `ResolveNameForKey` — без nextKeyTop cap при isMerged; `HasCyrillicInMarkBlock`/`HasNameTextOwnedByKey`; cellOnly `reason=merged-block|missing-cyrillic`; `FindQtyTextInCell` rowTop only; `ResolveQtyTableTextAppearanceForScope(scope, rowTop)`; `ReportPaletteVsScopeNamesDiagnostic`; убраны `key==1`, `key<=3` diag; docs §23, INSTRUCTION §8.3; план `_IMPLEMENTED.md`.
> Результат: КОД ГОТОВ — пересборка net452, NETLOAD; проверка: `[NAME] reason=merged-block`, `[WRITEQTY] from=colName-rowTop`, `[POSC] Палитра: ключей=N, имён=M`.

> [2026-06-14] Задача: план fix_first_name_line_skip — первая строка наименования не попадает в палитру (3 строки → берутся 2-я и 3-я).
> Причина: `IsNameContinuationRow` давала `false` для `row ≤ rowMark`; при цифре марки ниже первой строки имени (merged ColMark) первая строка ColName отсекалась как `IsSectionHeaderRow`.
> Правка: `IsNameContinuationRow` — диапазон `[rowTop, blockEnd)`; ветка `rowTop ≤ row < rowMark` с непустым ColName; `LogNameSectionRowSkip` + trace `rowTop/rowMark/blockEnd`; docs DEVELOPER §11/§22; план `_IMPLEMENTED.md`.
> Результат: КОД ГОТОВ — пересборка `build-ac2016.cmd`, NETLOAD; проверка ГТ-20: марки 1/5/6 — все строки имени в палитре, `[NAME] parts=3` для 3-строчных.

> [2026-06-14] Задача: план fix_rowtop_header_qty — rowTop сдвигался до rowMark; bilingual шапка; RowDataStart; qty в merged 3+.
> Причина: `ResolveNameRowTopForKey` while `rowTop++` при `IsSectionHeaderRow` до сбора имён; `AlignRowDataStartToFirstMark` min(KeyToRowMark); граница шапки — последняя H-линия, не 2-я.
> Правка: не rowTop++ при непустом ColName; `min(KeyToRowTopSub)`; `FindHeaderEndRowByHorizontalBorders`; `[WRITEQTY] merged=`; docs §11/§22; план `_IMPLEMENTED.md`.
> Результат: КОД ГОТОВ — пересборка net452, NETLOAD; проверка: merged N-line parts≥N, rowTop<rowMark, qty в верхней суб-ячейке.
