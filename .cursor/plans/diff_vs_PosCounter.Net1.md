# Сравнение: PosCounter.Net (текущий) vs PosCounter.Net1 (эталон) и vs commit `9117b5a`

**Эталон Net1:** `E:\Yandex.Disk-ananchenkoiren\TatbelGit\3\PosCounter.Net1`  
**Текущий проект:** `E:\Yandex.Disk-ananchenkoiren\TatbelGit\Pos_counter addition — 2\PosCounter.Net`  
**Базовая точка в git:** commit `9117b5a` — *«рабочая программа для акад 2016 до правок»* (2026-06-15 18:16)  
**Текущая рабочая копия:** HEAD `31e7aa3` + незакоммиченные правки (упрощение подсчёта, только C4)  
**Дата сравнения:** 2026-06-16  
**Метод:** `git diff 9117b5a` по исходникам `.cs` + анализ рабочей копии (без `obj/`, `bin/`)

---

## 0. Что изменилось: текущая версия vs commit `9117b5a`

Commit `9117b5a` — последняя **зафиксированная** рабочая сборка до правок 15–16 июня. По подсчёту выносок она близка к Net1: `ExtractPositionNumber` → сразу в палитру, без `CalloutMarkGate`, без диагностики в CMD.

### 0.1. Краткий итог

| Область | `9117b5a` | Текущая версия |
|---------|-----------|----------------|
| Подсчёт выносок | `ExtractPositionNumber(textValue)` → `acc.Increment` | `SanitizeRawContents` + `ExtractPositionNumber` + **C4** (`CalloutMarkGate`) |
| `CalloutMarkGate.cs` | **нет** | **новый файл** (~365 строк, только круги + C4) |
| Фильтры C1/C3 | нет | нет (убраны в упрощении; в промежуточных правках были, в `9117b5a` не было) |
| Фильтр C4 (цифра в круге) | нет — круглые марки **считаются** | есть — цифра в круге **не считается** |
| Диагностика CMD после ЗАПУСТИТЬ | нет | `[POSC] count source=… texts=… circles=… rejectC4=…` |
| Запись «Кол.» (`PaletteHost`) | все строки `_lastCountRows` | только **видимые** строки палитры |
| Таблица «Ушко» (`CellIndex`) | дубликат по близости режет цифру | цифра-марка не режется по близости |
| `MTextPlainText` | без `IsExactCalloutDigitText` | метод добавлен (для gate; C3 снят, метод остаётся) |
| Палитра UX | «Подсчёт…», кнопка не блокируется | «Идёт подсчёт…», `BtnRun` disabled, сброс фильтра слоя |
| Время подсчёта | не замеряется | `Stopwatch` → `CountElapsedMs` в результате и CMD |

### 0.2. Сводка по файлам (`git diff 9117b5a` → рабочая копия)

| Файл | Статус | Суть изменений |
|------|--------|----------------|
| `Engine/CalloutMarkGate.cs` | **НОВЫЙ** (в `9117b5a` отсутствует) | Индекс кругов, C4, `PopulateViewportGeometry` (selection `CIRCLE`) |
| `Engine/PosCounterEngine.cs` | **ИЗМЕНЁН** (+~158 строк) | Gate, диагностика, координаты текста, `SanitizeRawContents` |
| `Commands.cs` | **ИЗМЕНЁН** (+23 строки) | Строка `[POSC-DIAG] count …` |
| `PaletteHost.cs` | **ИЗМЕНЁН** (1 строка) | `TryBuildQtyByKeyFromVisibleRows` вместо `FromAllRowsSnapshot` |
| `UI/PosCounterControl.xaml.cs` | **ИЗМЕНЁН** (+14 строк) | UX подсчёта, сброс `_filterLayer` |
| `SpecGrid/CellIndex.cs` | **ИЗМЕНЁН** (+16 строк) | `IsExactDigitMark` в `IsDuplicateCandidate` |
| `SpecGrid/MTextPlainText.cs` | **ИЗМЕНЁН** (+11 строк) | `IsExactCalloutDigitText` |
| `TableGrid.cs`, `SpecGridService.cs` | без изменений | как в `9117b5a` |

### 0.3. `PosCounterEngine.cs` — детали относительно `9117b5a`

**Было (`9117b5a`):**

```csharp
// ProcessTextValue
var position = ExtractPositionNumber(textValue);
if (!position.HasValue) return;
acc.Increment(layer, position.Value.ToString(), sourceId);
```

- Нет `geoIndex`, координат текста, `diag`.
- Нет `CalloutMarkGate.BuildIndex` / `PopulateViewportGeometry`.
- `PosCountResult` без `SeenDigits`, `RejectC4`, `LayerSample`, `CountElapsedMs`.

**Стало (текущая):**

| Изменение | Назначение |
|-----------|------------|
| `CalloutMarkGate.BuildIndex(tr, sourceIds)` | Сбор кругов из sourceIds + рекурсия в блоки |
| `PopulateViewportGeometry` при viewport | Один selection `CIRCLE` по полигону viewport |
| `SanitizeRawContents` перед `ExtractPositionNumber` | Корректный разбор MText |
| `ShouldCountAsCalloutMark` (только C4) | Цифра в круге не попадает в палитру |
| `CountDiagnostics` + поля `PosCountResult` | `SeenDigits`, `RejectC4`, `LayerSample`, `GeoCircleCount`, `CountElapsedMs` |
| `ProcessEntity` / `ProcessTextValue` | Передают `Point3d`, `textHeight`, `geoIndex`, `diag` |

### 0.4. Поведение подсчёта: `9117b5a` vs текущая

| Ситуация на чертеже | `9117b5a` | Текущая |
|---------------------|-----------|---------|
| Голая цифра «3» | считает | считает |
| «Поз. 3», «№5» | считает | считает |
| Цифра **в круге** | **считает** | **не считает** (C4) |
| «дет.» + «3» рядом | считает | считает |
| Треугольник LINE + цифра | считает | считает |
| Фильтр по слою при скане | нет | нет |

### 0.5. `CalloutMarkGate.cs` — только в текущей версии

В `9117b5a` файла нет. Добавлен в рабочей копии (пока не в git).

| Компонент | Описание |
|-----------|----------|
| `GeoIndex` | Только `CircleAnchor` + buckets 15 мм |
| `PopulateViewportGeometry` | Один `SelectCrossingPolygon` / `SelectWindowPolygon`, фильтр `CIRCLE` |
| `ShouldCountAsCalloutMark` | Только C4: `IsDigitInsideCircleMarker` |
| `GateStats` | `RejectC4` для CMD |
| `BuildIndex` | Рекурсия `BlockReference` → вложенные блоки, сбор только `Circle` |

### 0.6. Остальные отличия от `9117b5a`

**`Commands.cs`** — после `CountWithInfo`:

```
[POSC-DIAG] count source=viewport|model|selection texts=N circles=K ms=… layers=N layerSample=… rejectC4=M
```

**`PaletteHost.cs`** — qty в «Кол.» только из видимых строк палитры (в `9117b5a` — из всех строк снимка).

**`PosCounterControl.xaml.cs`** — блокировка «ЗАПУСТИТЬ» на время подсчёта; после результата сброс фильтра по слою.

**`CellIndex.cs`** — цифра в ColMark не отбрасывается как «дубликат по близости» (фикс таблицы «Ушко»).

---

## Краткий итог (текущий vs Net1)
| Область | PosCounter.Net1 | Текущий проект |
|---------|-----------------|----------------|
| Подсчёт выносок (`PosCounterEngine`) | `ExtractPositionNumber` → сразу в палитру | то же **+** `CalloutMarkGate` (только C4), геоиндекс кругов, диагностика |
| Новый файл `CalloutMarkGate.cs` | **нет** | **есть** (~360 строк, только CIRCLE + C4) |
| Спецификация (`TableGrid`, `SpecGridService`) | эталон | **идентичны** (байт в байт) |
| Запись «Кол.» (`PaletteHost`) | все строки `_lastCountRows` | только **видимые** строки палитры |
| Таблица «Ушко» (`CellIndex`) | дубликаты по близости режут цифру | цифра-марка **не** режется по близости |
| `MTextPlainText` | без `IsExactCalloutDigitText` | добавлен метод (для C3 в gate) |
| Палитра UX | без блокировки кнопки при подсчёте | «Идёт подсчёт…», `BtnRun` disabled |
| CMD после ЗАПУСТИТЬ | нет строки диагностики | `[POSC-DIAG] count source=… layerSample=… rejects …` |

**Для инженера:** подсчёт **не фильтрует по слою** в обоих проектах. Если в палитре мало слоёв — причина может быть **C4 «цифра в круге»** (кружки на ГТ не попадут в палитру) или объекты не попали в viewport/выделение.

---

## 1. Сводка по файлам (SHA256)

| Файл | Статус |
|------|--------|
| `Engine/PosCounterEngine.cs` | **ИЗМЕНЁН** |
| `Engine/CalloutMarkGate.cs` | **ТОЛЬКО в текущем** (в Net1 нет) |
| `Commands.cs` | **ИЗМЕНЁН** |
| `PaletteHost.cs` | **ИЗМЕНЁН** (1 строка по qty) |
| `UI/PosCounterControl.xaml.cs` | **ИЗМЕНЁН** (UX подсчёта, сброс фильтра слоя) |
| `SpecGrid/CellIndex.cs` | **ИЗМЕНЁН** |
| `SpecGrid/MTextPlainText.cs` | **ИЗМЕНЁН** |
| `SpecGrid/TableGrid.cs` | идентичен |
| `SpecGrid/SpecGridService.cs` | идентичен |
| `Services/ExportService.cs` | идентичен |
| `Services/PosCounterService.cs` | идентичен |

---

## 2. `Engine/PosCounterEngine.cs` — что конкретно поменялось

### 2.1. Net1: цепочка подсчёта

```csharp
// ProcessTextValue — Net1
var position = ExtractPositionNumber(textValue);
if (!position.HasValue) return;
acc.Increment(GetBaseLayer(entityLayer), position.Value.ToString(), sourceId);
```

- Нет проверки геометрии рядом с цифрой.
- Нет `SanitizeRawContents` перед `ExtractPositionNumber` (сырой MText).
- `ProcessEntity` **без** `geoIndex`, координат текста, диагностики.

### 2.2. Текущий: добавлено

| Изменение | Назначение |
|-----------|------------|
| `CalloutMarkGate.BuildIndex(tr, sourceIds)` | Индекс только CIRCLE для C4; рекурсия в блоки |
| `PopulateViewportGeometry` при viewport | Один раз CIRCLE по полигону viewport |
| `ProcessTextValue` + `SanitizeRawContents` + `ExtractPositionNumber` | Как Net1 по тексту, но с очисткой MText |
| `ShouldCountAsCalloutMark` перед `acc.Increment` | Только C4 (цифра в круге) |
| `CountDiagnostics` + поля в `PosCountResult` | `SeenDigits`, `RejectC4`, `LayerSample`, `CountElapsedMs` |
| `Stopwatch` в `CountWithInfo` | Время подсчёта в мс |

### 2.3. Поведенческая разница (подсчёт)

| Ситуация | Net1 | Текущий |
|----------|------|---------|
| Голая цифра «3» | считает | считает |
| «Поз. 3», «№5» | считает (`ExtractPositionNumber`) | считает |
| Цифра в круге на чертеже | **считает** | **не считает** (C4, строгий вариант) |
| «дет.» + «3» рядом | считает | **считает** (C3 убран) |
| Треугольник LINE + цифра | считает | **считает** (C1 убран) |
| Фильтр по слою при скане | нет | нет |

---

## 3. `Engine/CalloutMarkGate.cs` — новый файл (только текущий)

**В Net1 отсутствует.**

| Компонент | Описание |
|-----------|----------|
| `GeoIndex` | Только круги + пространственные buckets 15 мм |
| `PopulateViewportGeometry` | Один `SelectCrossingPolygon` только CIRCLE |
| `ShouldCountAsCalloutMark` | Только C4: цифра внутри круга → не выноска |
| `GateStats` | `RejectC4` для CMD |
| `BuildIndex` | Обход BlockReference + вложенных блоков (только Circle) |

---

## 4. `PaletteHost.cs` — запись количества в «Кол.»

**Единственное отличие в коде:**

| | Net1 | Текущий |
|---|------|---------|
| Строка ~225 | `TryBuildQtyByKeyFromAllRowsSnapshot()` | `TryBuildQtyByKeyFromVisibleRows(out snapshotMap)` |

**Смысл:** в «Кол.» на чертеже пишется qty только из **видимых** строк палитры (после фильтров Марка/Слой/Кол.), а не сумма по скрытым строкам.

---

## 5. `UI/PosCounterControl.xaml.cs`

| Изменение | Net1 | Текущий |
|-----------|------|---------|
| Статус при ЗАПУСТИТЬ | «Подсчёт…» | «Идёт подсчёт…» + пояснение про лист/viewport |
| `BtnRun.IsEnabled` | не блокируется | `false` на время подсчёта |
| После `ApplyRunResult` | — | `_filterLayer.Clear()`, `TxtSearchLayer` сброс |
| `TryBuildQtyByKeyFromVisibleRows` | метод **есть**, но PaletteHost его **не вызывает** | PaletteHost **вызывает** |

Оба проекта: `GetDistinctLayers()` — только слои из **найденных** строк (не все слои DWG).

---

## 6. `Commands.cs`

**Добавлено в текущем** после `CountWithInfo`:

```
[POSC-DIAG] count source=viewport|model|selection texts=N circles=K ms=…
  layers=N layerSample=слой:count,... rejectC4=M
```

В Net1 этой строки нет.

---

## 7. `SpecGrid/CellIndex.cs` — таблица спецификации

**Изменение:** `IsDuplicateCandidate`

- **Net1:** любой близкий текст в ячейке ColMark может выкинуть цифру как «дубликат».
- **Текущий:** если текст — **цифра-марка** (`IsExactDigitMark`), дедуп только по **точному** совпадению строки; по близости цифра не отбрасывается.

**Зачем:** ячейка «Марка» с цифрой + «21 ОСТ…» — цифра не теряется, `markAnchor` / имена «Ушко» работают.

`TableGrid.cs` и `SpecGridService.cs` при этом **не менялись**.

---

## 8. `SpecGrid/MTextPlainText.cs`

**Добавлено в текущем:**

```csharp
public static bool IsExactCalloutDigitText(string raw)
```

Используется в `CalloutMarkGate` (ранее C3; с 2026-06-16 C3 убран, метод остаётся в коде). В Net1 метода нет.

`IsExactDigitMark` — был и остаётся (для спецификации).

---

## 9. Что совпадает с Net1 (без изменений)

- Обход ModelSpace / viewport / выделения (`sourceIds`) — **без фильтра Layer**
- `ResolveLayer` (слой 0 → слой блока), `GetBaseLayer` (xref `A|B` → `B`)
- `TableGrid`, `SpecGridService` — распознавание шапки, ColMark/ColName/ColQty, наследование схемы
- `ExportService`, `PosCounterService` (в т.ч. `GetAllLayers` — **не подключён** к палитре в обоих)
- Proxy СПДС, MLeader — не обрабатываются в обоих

---

## 10. Связь с проблемой «не все слои в палитре»

На чертеже ГТ марки часто **в кружках**. Net1 их считает; текущий проект отсекает **C4** (осознанный компромисс: близко к Net1, но круглые марки не в палитре).

План `все_слои_на_чертеже` (отключить C4, C3 same-layer) — **отменён** планом `упростить_подсчёт_выносок` (только C4).

---

## 11. Связанные документы

- [factual_program_architecture.plan.md](factual_program_architecture.plan.md)
- [docs/DEVELOPER.md](../../docs/DEVELOPER.md)
- [.cursor/DIALOGUE_LOG.md](../DIALOGUE_LOG.md)

---

## 12. Хронология: `9117b5a` → текущая

| Этап | Commit / состояние | Подсчёт выносок |
|------|-------------------|-----------------|
| Базовая рабочая | `9117b5a` (2026-06-15) | Как Net1, без gate |
| Промежуточный | `31e7aa3` (2026-06-16) | Часть правок в `PosCounterEngine`, `CellIndex`, UI (без `CalloutMarkGate` в git) |
| Текущая рабочая копия | незакоммичено | `CalloutMarkGate` (только C4), упрощённая диагностика |

**Итог для инженера:** откат к поведению `9117b5a` по подсчёту = убрать `CalloutMarkGate` и связанные вызовы в `PosCounterEngine` / `Commands`. Откат qty = одна строка в `PaletteHost` (`FromAllRowsSnapshot`).
