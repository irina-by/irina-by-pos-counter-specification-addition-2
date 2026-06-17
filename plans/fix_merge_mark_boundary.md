# План: диагностика + исправление границы марки (merge)

## Статус

| Этап | Статус |
|------|--------|
| 1. Логи в CMD | **РЕАЛИЗОВАН** (2026-06-17) |
| 2. Фикс границы (`ResolveNextMarkBoundaryExclusive`) | ожидает лог с `ВНИМАНИЕ` |
| 3. Проверка + docs | частично (DEVELOPER §11) |

## Этап 1 — что сделано

- `SpecGridLog.TryWriteMergeBoundaryLine` — до 25 строк `[POSC]` на «Выбрать спецификацию»
- `TableGrid.ReportMergeBoundaryDiagnostic` — вызывается из `ReportMarkNamesDiagnostic`
- Bleed: `rowEndEx > nextKeyTop` + текст в ColName → `ВНИМАНИЕ марка K: захвачено …`

## Проверка в AutoCAD

1. Собрать DLL (`build-ac2016.cmd` или `build-ac2026.cmd`)
2. NETLOAD → дата `build=` **новее** предыдущей
3. ЗАПУСТИТЬ → Выбрать спецификацию (большая таблица)
4. В CMD искать:
   - `[POSC] Табл.N марка K: имя строки … | след.: верх=… цифра=…`
   - `[POSC] ВНИМАНИЕ марка …` — подтверждение bleed
5. Прислать лог → этап 2

## Этап 2 (код ещё не внедрён)

`ResolveNextMarkBoundaryExclusive`: обрезка по `min(nextKeyTop, nextMarkRow)` в `ResolveNameForKey` и `GetMarkBlockEndExclusive`.

## Лог 028_МВ_AR-rev2 (до этапа 1)

DLL `build=2026-06-17 16:02` — диагностики границ не было. После пересборки должны появиться строки `[POSC] Табл.… марка …`.
