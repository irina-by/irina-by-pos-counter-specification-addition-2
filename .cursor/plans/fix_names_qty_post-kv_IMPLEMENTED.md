# Реализовано: fix_names_qty_post-kv (post KV)

Дата: 2026-06-14. План-источник: `fix_names_qty_post-kv_dd94bc56.plan.md` (не редактировать).

## Выполнено

| Todo | Файлы | Суть |
|------|-------|------|
| grep-cleanup-hardcode | `TableGrid.cs` | `key==1` → любой key в CellText; `key<=3` → `SpecDiagPolicy.IsSampleKey`; нейтральные комментарии |
| fix-bilingual-rowend | `TableGrid.cs` | merged rowEnd; `HasCyrillicInMarkBlock`; `HasNameTextOwnedByKey`; cellOnly reasons |
| fix-qty-subrow-style | `SpecGridService.cs` | qty search rowTop; style from ColName rowTop per key |
| fix-palette-warning | `SpecGridService.cs`, `Commands.cs`, `PosCounterControl.xaml.cs`, `PaletteHost.cs` | `[POSC] Палитра: ключей=N, имён=M` |
| docs-diag-log | `DEVELOPER.md` §23, `INSTRUCTION_ENGINEER.md` §8.3, `DIALOGUE_LOG.md` | |

## Приёмка AC 2016

1. `build-ac2016.cmd` → NETLOAD.
2. Merged bilingual: `[NAME] reason=merged-block` или `missing-cyrillic`, `parts≥2`.
3. WriteQty: `[WRITEQTY] from=colName-rowTop`, qty на верхней суб-строке.
4. Палитра N > имён M: предупреждение в CMD и статусе.
5. `grep SpecGrid`: нет `key ==`, `ГТ-20`.

## Не проверено на агенте

Сборка требует AutoCAD 2016 SDK на ПК инженера.
