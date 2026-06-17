# Исправление позиции «Кол.» при пометках инженера

Полный план: `fix_qty_y_with_notes_3d00b3d1.plan.md` (Cursor plans).

## Пример с чертежа (2026-06-16)

- **Слева:** пометка инженера «14» (белая, сверху) + вставленное qty «17» (фиолетовое, облако) — **выше центра**
- **Справа:** эталон — «14» (зелёная) **по центру** ячейки
- **Цель:** «17» выровнять по высоте с зелёной «14»; пометку не трогать

## Суть правки

Вернуть в [SpecGridService.cs](../PosCounter.Net/SpecGrid/SpecGridService.cs) логику Y и Upsert из проекта **Pos_counter addition — 2026**:

1. `ResolveQtyInsertPoint`: Y = `(GridYs[rowTop] + GridYs[rowBottomEx]) / 2`
2. `FindQtyTextInCell`: полный `rowBottomEx` (не только `rowTop+1`)
3. Убрать `skip-far-entY` из `UpsertQtyText`

## Проверка

Тот же чертёж, что на скриншоте: соседние ячейки «Кол.» с пометкой и без.
