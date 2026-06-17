# План: краткие логи CMD для инженера (2026-06-16)

## Цель

В командной строке AutoCAD оставить только то, что нужно инженеру: понятно, что программа работает, и предупреждения, если что-то не так. Баннер NETLOAD с датой DLL — **без изменений**.

## Оставлено в CMD

| Когда | Префикс | Примеры |
|-------|---------|---------|
| NETLOAD | `[POSC]` | `PosCounter.Net … build=yyyy-MM-dd HH:mm загружен.` |
| ЗАПУСТИТЬ | `[POSC]` | `count source=… texts=… circles=… rejectC4=…` |
| Выбор таблиц | `[INFO]` | `Выбрана таблица N …` |
| Спецификация OK | `[POSC]` | `Распознана шапка…`, `WriteQty итог: записано=N` |
| Предупреждения | `[POSC]` | шапка/столбцы/сетка/палитра/рамка |

## Отключено (закомментировано / no-op)

- `SpecGridLog.WriteDiag` — все `[POSC-DIAG]`
- `SpecGridLog.WriteTrace` — `[COLQTY]`, `[HEADER]`, `[NAME]`, …
- Доп. строка AutoCAD R* после NETLOAD
- Verbose в `ReportDetectedHeader`: gridLayer, KeyToRowMark, диагностика шапки
- `ReportKvSummaryDiagnostic`, `ReportScopeSummaryDiagnostic` (ИТОГ ColMark)
- Детали WriteQty по каждой марке

## Файлы

- `PosCounter.Net/SpecGrid/SpecGridLog.cs`
- `PosCounter.Net/Commands.cs`
- `PosCounter.Net/SpecGrid/SpecGridService.cs`
- `docs/INSTRUCTION_ENGINEER.md`, `README.md`, `docs/DEVELOPER.md`, `Работа программы.md`
- `.cursor/plans/factual_program_architecture.plan.md`

## Проверка в AutoCAD

1. NETLOAD → одна строка `[POSC] … build=… загружен.`
2. ЗАПУСТИТЬ → одна строка `[POSC] count source=…`
3. Выбрать спецификацию → `[INFO]` при выборе, `[POSC] Распознана шапка`, `[POSC] WriteQty итог`
4. Нет строк `[POSC-DIAG]`, `[KV-SUMMARY]`, `[NAME]`
