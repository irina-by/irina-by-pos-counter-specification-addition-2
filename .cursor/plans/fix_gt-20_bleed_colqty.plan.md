---
name: Fix GT-20 bleed ColQty
overview: ColQty=-1 (qty не пишется); имена bleed; русская шапка не распознаётся; qty из палитры в col3 верхняя суб-ячейка. Сверка с 2016.
todos:
  - id: sync-verify-dll
    content: Синхронизировать Yandex→Salnikava.I; NETLOAD bin\x64\Release\net452; проверить build= и ColQty layout в CMD
    status: completed
  - id: colqty-force-layout
    content: "ColQty=-1: принудительно ColQty=ColName+1 при схеме 0/1/2/3 после inference+rebind+перед WriteQty; numeric prefer col3"
    status: completed
  - id: name-cellonly-restore
    content: "ResolveNameForKey: useCellTextOnly как OLD; nextKeyTop cap; LooksLikeDesignationText; марка 1 RU+EN"
    status: completed
  - id: writeqty-top-subcell
    content: ResolveQtyInsertY — Y первой суб-строки rowTop; значение только из qtyByKey; диагностика WriteQty
    status: completed
  - id: header-russian-score
    content: "ScoreHeader: русские токены кол/наимен/поз без английского; DbTextHeaderMaxPlainLen 60; не путать данные с шапкой"
    status: completed
  - id: diag-and-docs
    content: DEVELOPER, DIALOGUE_LOG, INSTRUCTION_ENGINEER; чеклист по логу 2026-06-10
    status: completed
isProject: false
---

# Исправление: ColQty + имена + qty из палитры (лог 2026-06-10)

## Новый лог — главные проблемы

### A. ColQty = -1 → «Кол.» вообще не пишется

```
[POSC-DIAG] Таблица 1 WriteQty пропущен: ColQty=-1
[POSC-DIAG] Таблица 2 WriteQty пропущен: ColQty=-1
[POSC-DIAG] WriteQty итог: записано=0
[POSC-DIAG] Таблица 1 ИТОГ: ColMark=0 ColName=2 ColQty=-1
```

Это **хуже**, чем раньше (`ColQty=4`): столбец «Кол.» не найден, fallback numeric/layout **не сработал** в загруженной DLL.

При `ColMark=0`, `ColName=2` (схема 0/1/2/3) программа **обязана** ставить `ColQty=3` источник=`layout` — даже если numeric и шапка не сработали.

### B. Таблицы с русской шапкой / без английского

Обе таблицы: `pass1: ColMark=-1 ColName=-1 ColQty=-1` → только inference.

Таблица 2: в «шапке» оказались **данные** (`81 82 83`, `ГОСТ`, `1.18 Тройник…`), score=0 для всех столбцов → `Кол. — не найдена`.

Таблица с **только русскими** подписями («Кол.», «Наименование») не распознаётся, если:
- top-band ловит цифры/данные вместо заголовков;
- `ScoreHeader` не находит «кол» в объединённом тексте ячейки.

### C. ГТ-20 — палитра vs чертёж (скриншот)

| Марка | Палитра (должно быть) | Палитра сейчас (ошибка) | На чертеже (старое) |
|-------|----------------------|-------------------------|---------------------|
| 1 | RU + EN, qty **1** | только `Floor trap…`, qty 1 | qty **6** |
| 2 | `Отвод…` + `Elbow…`, qty **5** | (проверить) | qty **9** |
| 4 | `Заглушка…` + `Plug…`, qty **1** | + лишнее `Трубы чугунные…` | qty **2** |
| 5 | 3 строки col2, qty **4** | bleed от 6 | qty **6** |

**На чертеже** числа 6, 9, 3… — это **старые значения**. После исправления программа должна **записать из палитры** (1, 5, 1, 1, 4, 1) в col3.

**Qty по Y:** на скрине у марок 1, 5, 6 цифра у второй суб-строки; у 2, 3, 4 — у первой. Правило: вставка в **первую суб-строку блока марки** (`rowTop`, линия с русским наименованием).

---

## Сравнение с проектом 2016

| Область | OLD 2016 | NEW — регрессия |
|---------|----------|-----------------|
| `useCellTextOnly` | вкл. при полном CellText | выкл. при `ColDesignation>=0` → bleed 4→5→6 |
| ColQty после inference | без жёсткого layout | иногда -1 или col4 вместо col3 |
| `WriteQtyScope` | qty из `qtyByKey`, `rowTop=KeyToRowTopSub` | тот же код |
| `ResolveQtyInsertPoint` | центр span rowTop..rowBottomEx | нужна **верхняя** суб-ячейка rowTop..rowTop+1 |

---

## Шаг 1. ColQty=3 принудительно (критично)

**Файлы:** [TableGrid.cs](PosCounter.Net/SpecGrid/TableGrid.cs), [SpecGridService.cs](PosCounter.Net/SpecGrid/SpecGridService.cs)

1. `ApplyMandatoryColQtyLayout`: при `ColName == ColMark + 2` **всегда** `ColQty = ColName + 1`, в т.ч. когда `ColQty == -1` (не ждать numeric).
2. Вызывать `ApplyStandardColumnLayout`:
   - после `TryInferColumnsFromData`;
   - после `TryResolveMissingColQty`;
   - в конце `RebindScopeKeysAndNames`;
   - **перед** `WriteQtyInTransaction` (уже есть в коде Yandex-копии).
3. `TryInferColQtyFromNumericColumn`: при стандартной схеме предпочитать col3, если `CountQtyLikeInColumn(col3) >= MinNumericQtyCells`.
4. ИТОГ: `layout=→3` или `4→3`; **ВНИМАНИЕ** если `ColQty=-1` после всех шагов.

**Критерий:** `WriteQty итог: записано>0`, `ColQty=3 источник=layout`.

---

## Шаг 2. Имена (как OLD 2016 + без ГОСТ)

**Файл:** [TableGrid.cs](PosCounter.Net/SpecGrid/TableGrid.cs) — `ResolveNameForKey`

1. Убрать `grid.ColDesignation < 0` из `useCellTextOnly`.
2. `rowEndExclusive = Min(..., GetNextKeyRowExclusive(key))`.
3. Оставить `LooksLikeDesignationText` — без ГОСТ/TB100 в имени.
4. Марка 1: **RU + EN** из col2 (`Трап чугунный…` + `Floor trap…`).

| Марка | Имя в палитре |
|-------|----------------|
| 1 | `Трап чугунный вертикальный Ø100` + `Floor trap plastic vertical Ø100` |
| 4 | `Заглушка З-100` + `Plug З-100` |
| 5 | `Трубы чугунные…` + `раструбные ТЧК-100-2000` + `Cast iron…` |
| 6 | `Патрубок…` + `раструбный П-100-200` + `Cast iron…` |

---

## Шаг 3. Запись qty — палитра → col3, первая суб-ячейка

**Файл:** [SpecGridService.cs](PosCounter.Net/SpecGrid/SpecGridService.cs)

1. `ResolveQtyInsertY(scope, rowTop, rowBottomEx)` — Y центра **rowTop..rowTop+1** (первая суб-строка, русская линия).
2. `FindQtyTextInCell`: `targetCenterY` = тот же Y.
3. Значение: **только** `qtyByKey[key]` — не копировать число с чертежа.
4. Диагностика: `WriteQty key=1 colQty=3 qty=1 Y=… (палитра)`.

**Критерий ГТ-20 после записи:**

| Марка | col3 на чертеже |
|-------|-----------------|
| 1 | **1** (не 6) |
| 2 | **5** (не 9) |
| 4 | **1** (не 2) |

---

## Шаг 4. Русская шапка (таблицы без English)

**Файл:** [TableGrid.cs](PosCounter.Net/SpecGrid/TableGrid.cs)

1. `ScoreHeader` / `ScoreQtyHeader`: достаточно русских токенов `кол`, `кол.`, `наимен`, `поз` (английский не обязателен).
2. `DetectHeaderByDbTextHeaderBand`: `DbTextHeaderMaxPlainLen` 25 → **60**; не считать марки (`81`,`82`) и ГОСТ шапкой — фильтр `TryParseMarkKey` / `LooksLikeDesignationText`.
3. `BuildHeaderOnlyColumnText`: разбивать MText по строкам (RU / EN в одной ячейке).
4. Таблица-продолжение без шапки: оставить inference + **шаг 1** (layout col3).

---

## Шаг 5. Сборка и проверка

1. Код из `E:\Yandex.Disk-ananchenkoiren\TatbelGit\Pos_counter addition — 2` → сборка Salnikava.I.
2. NETLOAD: `bin\x64\Release\net452\PosCounter.Net.dll`.
3. CMD: `build=…`, `ColQty=3 layout`, `WriteQty key=1 qty=1`.

### Чеклист

| Тест | Ожидание |
|------|----------|
| DWG 2 табл. (рус. продолжение) | ColQty=3, WriteQty>0 |
| DWG табл.2 (81–111) | ColQty=3, без регрессии |
| ГТ-20 7 марок | имена по таблице выше; qty = палитра в col3 верхняя суб-ячейка |

---

## Todos

- sync-verify-dll
- colqty-force-layout
- name-cellonly-restore
- writeqty-top-subcell
- header-russian-score
- diag-and-docs
