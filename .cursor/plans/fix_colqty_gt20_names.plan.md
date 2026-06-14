---
name: Fix ColQty and GT-20 names
overview: "Табл.2 OK. Табл.1: ColQty=4→3. ГТ-20: имя только из col2 (обе подстроки RU+EN), col1 (ГОСТ/TB100) не включать."
status: draft
date: 2026-06-10
---

# Доработка: ColQty табл.1 + имя col2 на ГТ-20

## Уточнение по скриншоту ГТ-20 (Таблица 1 / List of materials)

Структура **одной позиции** (марка 1):

| col0 Марка | col1 Обозначение | col2 Наименование | col3 Кол. |
|------------|------------------|-------------------|-----------|
| 1 (на всю высоту) | верх: `ГОСТ 1811-2019` | верх: `Трап чугунный вертикальный Ø100` | **6** (верхняя суб-ячейка) |
| | низ: `TB100` | низ: `Floor trap plastic vertical Ø100` | |

**Правило для программы:**

- **Брать из col2 (Наименование):** обе строки блока — русская **и** английская (это одно наименование в двух подстроках).
- **Не брать из col1 (Обозначение):** `ГОСТ …`, `TB100`, `GOST6942-98` и т.п.
- **Кол.** — только из палитры, запись в **верхнюю суб-ячейку** col3 (как на чертеже: 6, 9).

Текущая ошибка в палитре — не «лишний английский», а **подмешивание col1** (коды ГОСТ / TB100) и иногда **дубль** из col1 вместо второй строки col2.

### Пример ожидаемого имени

| Марка | Палитра (col2 only) |
|-------|---------------------|
| 1 | `Трап чугунный вертикальный Ø100` + `Floor trap plastic vertical Ø100` (обе строки col2) |
| 2 | `Отвод О135°-100` + `Elbow О135°-100` (обе строки col2) |
| **Не должно быть** | `ГОСТ 1811-2019`, `TB100`, `GOST6942-98` в наименовании |

---

## Что уже работает (первый DWG, таблица 2)

- `KeyToRowMark`: 60→111, 52 имени, `ColQty=3`, WriteQty=85.

## Что исправить

### Таблица 1 (первый DWG, марки 17–59)

- `ColQty=4 numeric` → должно быть **`ColQty=3 layout`**.

### ГТ-20

- Исключить col1 из имени; собрать **все тексты col2** в блоке марки (верх + низ суб-строк).

---

## Шаг 1. ColQty = col3 (обязательный layout)

**Файл:** [`TableGrid.cs`](PosCounter.Net/SpecGrid/TableGrid.cs) — `TryPreferQtyColumnAfterName`

- При `ColName == ColMark + 2` → **всегда** `ColQty = ColName + 1`, если источник `numeric`/`inference` выбрал col4 (масса) или col5.
- Исключение: pass1 нашёл ColQty с `grid`/`dbTextBand`/`topBand` и score ≥ MinHeaderScore.
- Финальный вызов `ApplyStandardColumnLayout` перед WriteQty.

---

## Шаг 2. Имя: только col2, многострочное (RU+EN)

**Файлы:** [`TableGrid.cs`](PosCounter.Net/SpecGrid/TableGrid.cs)

1. **`BuildCellMatrix`:** в bucket `(row, ColName)` — только `t.Col == ColName`.
2. **`ResolveNameForKey`:** при `ColDesignation >= 0`:
   - не использовать `ResolveNameFromNeighborColumns`;
   - собирать части из **AllTexts** с `IsTextInNameColumn` по всему блоку марки (`rowTop..rowEnd`);
   - **включать обе суб-строки** col2 (сортировка по Y: сверху вниз);
   - явно **отбрасывать** тексты с `t.Col == ColDesignation` и шаблоны ГОСТ/TB (`LooksLikeDesignationText`).
3. **`useCellTextOnly`** — отключить при `ColDesignation >= 0`.

Новый фильтр `LooksLikeDesignationText`: короткие коды `ГОСТ …`, `TB100`, `GOST…`, без длинного описания.

---

## Шаг 3. MText pass2 — Data = Location

**Файл:** [`TableGrid.cs`](PosCounter.Net/SpecGrid/TableGrid.cs) — `TryGetMTextBounds`

- `dataPt = mt.Location` для привязки Col/Row; ExtentsTop только для YMin/YMax.

---

## Шаг 4. Диагностика

- `[POSC-DIAG] ColQty layout: 4→3`
- `[POSC-DIAG] имя key=1 col2-lines=2 excluded-designation=…`
- NETLOAD: `build=…` свежая дата

---

## Критерии успеха

| Чертёж | Проверка |
|--------|----------|
| DWG табл.1 | `ColQty=3`, WriteQty в col3 |
| DWG табл.2 | без регрессии (60–111) |
| ГТ-20 марка 1 | имя содержит RU **и** EN из col2; **нет** `ГОСТ`, `TB100` |
| ГТ-20 марка 2 | `Отвод О135°-100` + `Elbow О135°-100`; **нет** `GOST6942` |
| Qty | из палитры, в верхней суб-ячейке col3 |

---

## Todos

- colqty-mandatory-layout
- cellmatrix-col2-multiline (col2 RU+EN, exclude col1)
- mtext-pass2-location
- diag-colqty-names
- build-docs-gt20
