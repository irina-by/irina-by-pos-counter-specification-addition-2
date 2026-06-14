# fix_bilingual_names_gt20_header — реализовано (2026-06-12)

План-источник: `fix_bilingual_names_gt20_header.plan.md`.

## Изменения

### Этап 1 — имена (`TableGrid.cs`)

- `CapRowEndBeforeNextMarkNameLead` — не захватывать лидирующую строку col2 следующей марки.
- Ослаблен `cellOnly`: supplement при отсутствии кириллицы или неполном числе строк.
- `CollectNamePartsFromCellText` — всегда `EnumerateDisplayNameLines` + фильтр designation.
- `[NAME]` trace: `nameLeadCap`, `supplementCyrillic`.

### Этап 2 — шапка ГТ-20 (`TableGrid.cs`)

- `TryGetHeaderTopTextBandY` — якорь по верху сетки `GridYs`, не global maxY текстов.
- `DetectHeaderByTopGridRows` — строки 0–1, двуязычные токены (`MarkHeaderTokens`, `NameHeaderTokens`).
- `SanitizeMarkScoresForDigitOnlyHeaders` / `IsSpuriousDigitOnlyMarkHeader` — отсев col0 «9» при ≥10 марок в данных.
- `TryLockColumnSchema` — `CanLockColumnSchemaFromPass2` при стандартной схеме 0/2/3.

### Этап 3 — продолжение (`SpecGridService.cs`, `TableGrid.cs`)

- `IsContinuationPickTooSmall` (<80 obj или TextCount<30) — без `fallback infer-data`.
- CMD: «выделите весь лист Продолжение (~200+ объектов)».

### Этап 4 — docs

- `docs/DEVELOPER.md`, `docs/INSTRUCTION_ENGINEER.md`, `.cursor/DIALOGUE_LOG.md`.

## Проверка

Сборка net452 на ПК с AC 2016 → NETLOAD → малая + большая таблица по чеклисту плана.
