---
name: Fix AC2016 spec recognition
overview: "Этап 1: расширенные логи в CMD AutoCAD 2016, чтобы точно понять, где ломается цепочка (шапка / ячейки / имена / Кол.). Этап 2 (позже): исправления по результатам лога."
todos:
  - id: diag-cmd-framework
    content: "SpecGridLog: маршрутизация [POSC-DIAG] в CMD (без файлов); баннер AC версии при NETLOAD"
    status: completed
  - id: diag-header-pipeline
    content: "TableGrid + SpecGridService: лог pass1 DetectHeader (scores), inference-триггер, ColQty scores при inference"
    status: completed
  - id: diag-cells-texts
    content: "TableGrid: лог текстов вне ячеек (первые N: MText/DBText, method, Header/Data XY, plain)"
    status: completed
  - id: diag-names-qty
    content: "SpecGridService: лог MarkNamePairs (найдено/пусто), ключи палитры qty, WriteQty пропуски"
    status: completed
  - id: diag-inference-path
    content: "ReportDetectedHeader: диагностика шапки и top-band даже при ColumnsInferredFromData и ColQty<0"
    status: completed
  - id: build-test-ac2016-log
    content: "Сборка dll 2016, инструкция инженеру: NETLOAD → ЗАПУСТИТЬ → спецификация → прислать CMD; DIALOGUE_LOG"
    status: completed
  - id: fixes-deferred
    content: "ОТЛОЖЕНО: геометрия DBText, ColQty fallback, AssignCells — после анализа лога этапа 1"
    status: cancelled
isProject: false
---

# AC 2016: сначала логи, потом исправления

## Решение

Сейчас **не чиним алгоритм** — добавляем **понятные логи в командную строку AutoCAD** (CMD), чтобы на рабочем ПК с AC 2016 увидеть, на каком шаге всё ломается.

Файловые логи **не включаем** (по правилам проекта — только CMD и статус палитры).

## Что уже видно из вашего лога

| Факт | Вывод |
|------|--------|
| NETLOAD / выбор таблиц работает | Плагин загружен |
| `(столбцы по данным)` | Сработал fallback inference — шапка на pass1 не распознана |
| `ColQty` — не найдена | Запись «Кол.» отключена |
| `25 из 292` вне ячеек | Координаты текста на AC 2016 не попадают в сетку |
| `KeyToRowMark` есть (10…59) | Марки в таблице найдены |
| Диагностика top-band **не выводилась** | В коде она только при `ColMark<0 \|\| ColName<0`; после inference оба ≥0 → блок пропущен |

**Пробел:** при inference путь «успешный» для ColMark/ColName, но **без логов** почему нет ColQty и почему имена пустые.

```mermaid
flowchart LR
  netload[NETLOAD баннер]
  pass1[pass1 DetectHeader]
  infer[inference]
  cells[тексты вне ячеек]
  names[MarkNamePairs]
  palette[палитра]
  qty[WriteQty]

  netload --> pass1
  pass1 -->|"лог: scores"| infer
  infer -->|"лог: ColQty scores"| cells
  cells -->|"лог: sample N"| names
  names -->|"лог: пустые ключи"| palette
  palette --> qty
```

---

## Этап 1. Что добавляем в код

### 1. Каркас логов — [`SpecGridLog.cs`](PosCounter.Net/SpecGrid/SpecGridLog.cs)

- Метод `WriteDiag(Document doc, string message)` → префикс **`[POSC-DIAG]`** в CMD
- Лимит строк за один «Выбрать спецификацию» (~40–60), чтобы не забить CMD
- **Не** восстанавливать полный `Info/Debug` в файлы

### 2. Версия AutoCAD — [`Commands.cs`](PosCounter.Net/Commands.cs)

При NETLOAD дополнительно (если удаётся прочитать):

`[POSC-DIAG] AutoCAD R20.x (2016) / net46 DLL`

### 3. Шапка и inference — [`TableGrid.cs`](PosCounter.Net/SpecGrid/TableGrid.cs), [`SpecGridService.cs`](PosCounter.Net/SpecGrid/SpecGridService.cs)

**После pass1 `DetectHeader`** (до inference):

```
[POSC-DIAG] Таблица 1 pass1: ColMark=… ColName=… ColQty=… (gridRows/column/topBand)
```

Если сработал inference:

```
[POSC-DIAG] Таблица 1: включён fallback «столбцы по данным» (причина: ColMark/ColName не найдены на pass1)
[POSC-DIAG] inference ColQty: col3=0 col4=0 … (scores по столбцам)
```

**Важно:** вызывать [`BuildHeaderTopBandDiagnostic`](PosCounter.Net/SpecGrid/TableGrid.cs) и краткую диагностику шапки **также когда** `ColumnsInferredFromData=true` **или** `ColQty<0` (сейчас пропускается — это баг диагностики).

### 4. Тексты вне ячеек — [`TableGrid.cs`](PosCounter.Net/SpecGrid/TableGrid.cs)

Если `UnassignedTextCountAfterDataPass > 0`:

```
[POSC-DIAG] Вне сетки: 25/292. Примеры (до 5):
  #12 MText ExtentsTop Header=(…) Data=(…) «Поз.»
  #45 DBText AlignmentPoint … «Кол.»
```

Поля: тип (MText/DBText), method из `[CELL-ASSIGN]`, HeaderX/Y, DataX/Y, обрезанный plain (до 30 символов).

### 5. Имена и палитра — [`SpecGridService.cs`](PosCounter.Net/SpecGrid/SpecGridService.cs), [`PosCounterControl`](PosCounter.Net/UI/PosCounterControl.xaml.cs)

После `FillMarkNames` / `BuildCombinedMarkNames`:

```
[POSC-DIAG] Имена: MarkNamePairs=45, пустых=12, в палитру=33
[POSC-DIAG] Пустые имена (примеры ключей): 10, 11, 12…
[POSC-DIAG] Палитра qty: ключей=52 (перед спецификацией)
```

При `WriteQty` если `ColQty<0`:

```
[POSC-DIAG] WriteQty пропущен: ColQty=-1
```

### 6. Сводка в конце обработки таблицы

```
[POSC-DIAG] Таблица 1 ИТОГ: ColMark=0 ColName=2 ColQty=-1 | KeyToRowMark=50 | имена=33 | QtyWritten=0
```

---

## Этап 1. Действия инженера (после сборки)

1. Убрать `(load "pos_counter_2016_2026")` из `acad.lsp` (ошибка LOAD в начале сессии).
2. `build\build-ac2016.cmd` → NETLOAD из `dll 2016\` (оба DLL).
3. **ЗАПУСТИТЬ** → **Выбрать спецификацию** (тот же чертёж).
4. Скопировать **весь** текст CMD от `[POSC]` / `[POSC-DIAG]` и прислать.
5. Опционально: тот же чертёж в AC 2026 с `dll 2026` — для сравнения логов.

---

## Этап 2. Исправления (после лога — не делаем сейчас)

Отложено до анализа `[POSC-DIAG]`:

- геометрия DBText / MText на AC 2016
- ColQty из AllTexts band + numeric fallback
- AssignCells fallback HeaderX/Y
- правки inference / RebindScopeKeysAndNames

Критерий перехода к этапу 2: по логу однозначно видно узкое место (шапка / ячейки / имена / qty палитры).

---

## Файлы

| Файл | Изменения |
|------|-----------|
| [`SpecGridLog.cs`](PosCounter.Net/SpecGrid/SpecGridLog.cs) | `WriteDiag`, лимит строк |
| [`Commands.cs`](PosCounter.Net/Commands.cs) | версия AC в CMD |
| [`TableGrid.cs`](PosCounter.Net/SpecGrid/TableGrid.cs) | pass1/inference/cell samples |
| [`SpecGridService.cs`](PosCounter.Net/SpecGrid/SpecGridService.cs) | расширить `ReportDetectedHeader`, имена/qty |
| [`docs/INSTRUCTION_ENGINEER.md`](docs/INSTRUCTION_ENGINEER.md) | как снять лог |
| [`.cursor/DIALOGUE_LOG.md`](.cursor/DIALOGUE_LOG.md) | запись после реализации |

---

## Критерий готовности этапа 1

- Собрана `dll 2016\` с новыми логами
- На AC 2016 в CMD есть блоки `[POSC-DIAG]` по каждой таблице
- По логу можно ответить: **почему ColQty=-1**, **сколько имён пусто и почему**, **какие тексты вне сетки**
