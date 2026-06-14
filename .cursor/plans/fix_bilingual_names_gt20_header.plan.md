# fix_bilingual_names_gt20_header — план (2026-06-12)

Источник: `fix_spec_names_header_80584b8d.plan.md` (не редактировать).

## Проблема

После NETLOAD `build=2026-06-12 14:48`:
- Малая таблица (98 obj): `[SCHEMA] locked`, но имена bleed (марки 4–6) и пропуск RU строки у марки 1.
- Большая таблица (472+26): `pass1=-1`, шапка «Марка col0 «9»», нет `[SCHEMA] locked`, табл. 2 (26 obj) без WriteQty.

## Этапы

1. **Имена** — `CapRowEndBeforeNextMarkNameLead`, ослабить `cellOnly`, `EnumerateDisplayNameLines` всегда.
2. **Шапка ГТ-20** — якорь top-band по GridYs, `DetectHeaderByTopGridRows`, токены RU/EN, отсев col0-цифры, `TryLockColumnSchema` pass2.
3. **Продолжение** — `IsContinuationPickTooSmall` (<80 obj) блокирует infer fallback.
4. **Docs** — DEVELOPER, INSTRUCTION_ENGINEER, DIALOGUE_LOG.

## Чеклист AutoCAD

- [ ] Малая таблица: имена 1–7 без bleed, RU+EN у марки 1
- [ ] Большая таблица 1: `[SCHEMA] locked`, шапка не «9»
- [ ] Табл. 2 полная (~200+ obj): `[SCHEMA] inherited` + WriteQty
- [ ] Табл. 2 26 obj: предупреждение, без ложного infer
