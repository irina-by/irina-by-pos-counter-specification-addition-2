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
