---
name: Header anchor schema
overview: Первая таблица с шапкой Поз./Наимен./Кол. фиксирует ColMark/ColName/ColQty; продолжения наследуют 0/2/3 без DetectHeader и без TryInferColumnsFromData.
todos:
  - id: schema-type
    content: SpecColumnSchema.cs + SpecGridSession.ColumnSchema + ColumnsInheritedFromSchema
    status: completed
  - id: schema-lock-inherit
    content: "TableGrid: TryLockColumnSchema, TryApplyInheritedColumnSchema, Rebind skip DetectHeader"
    status: pending
  - id: specgrid-loop
    content: "SpecGridService: scope0 lock, scope1+ inherit + rebind, CMD [SCHEMA]"
    status: completed
  - id: docs-schema
    content: DEVELOPER.md, INSTRUCTION_ENGINEER.md, DIALOGUE_LOG.md
    status: completed
isProject: false
---

# Якорь шапки — эталон столбцов для продолжений

## Цель

| Шаг | Действие инженера | Программа |
|-----|-------------------|-----------|
| 1 | Рамка **таблица 1** — лист **со шапкой** Поз./Наимен./Кол. | `TryLockColumnSchema` → `[SCHEMA] locked Mark=0 Name=2 Qty=3` |
| 2 | Рамка **таблица 2..N** — «Продолжение» (шапка не обязательна) | `TryApplyInheritedColumnSchema` → без DetectHeader/inference |
| 3 | ЗАПУСТИТЬ уже выполнен | Имена col2 + WriteQty col3 из палитры |

## Новые файлы и типы

### [`PosCounter.Net/SpecGrid/SpecColumnSchema.cs`](PosCounter.Net/SpecGrid/SpecColumnSchema.cs)

```csharp
internal sealed class SpecColumnSchema
{
    public bool IsLocked;
    public int ColMark, ColName, ColQty, ColDesignation;
    public List<double> AnchorGridXs;
    public int HeaderEndRow;
}
```

### [`SpecGridSession.cs`](PosCounter.Net/SpecGrid/SpecGridSession.cs)

- `public static SpecColumnSchema ColumnSchema`
- `ClearScopes()` → `ColumnSchema = null`

### [`ScopeGridResult`](PosCounter.Net/SpecGrid/TableGrid.cs)

- `bool ColumnsInheritedFromSchema`

## TableGrid.cs

### `TryLockColumnSchema(scope, log)` — только scope 0

Условия:
- `scope.Valid`
- `Pass1ColMark >= 0 && Pass1ColName >= 0` (шапка в pass1, не inference)
- После `ApplyStandardColumnLayout` + `TryResolveMissingColQty`: `ColMark/ColName/ColQty >= 0`
- Сохранить в `SpecGridSession.ColumnSchema`, `IsLocked=true`
- `SpecGridLog.WriteTrace("SCHEMA", "locked ...")`

### `TryApplyInheritedColumnSchema(scope, schema, log)` — scope 1+

- Проверка `gridCols > ColQty`
- Проверка X: `|GridXs[ColMark] - AnchorGridXs[ColMark]| <= EpsAxis*20`
- Установить `ColMark/ColName/ColQty/ColDesignation` из schema
- `ColumnsInheritedFromSchema=true`, `ColQtySource="inherited"`
- `WriteTrace("SCHEMA", "inherited ...")`

### `RebindScopeKeysAndNames`

Пропускать `DetectHeader` если `ColumnsInheritedFromSchema` (как для `ColumnsInferredFromData`).

## SpecGridService.cs — цикл после `Build`

```text
scope = Build(i, ...)

if i == 0:
  if TryLockColumnSchema(scope, log):
    WriteDiag ColQty источник=...
  else if Valid && (ColMark<0 || ColName<0) && TryInferColumnsFromData:
    ... существующий infer-блок ...
  else if tablePicks.Count > 1:
    [POSC] Шапка не найдена в первой таблице — выделите лист с Поз./Наимен./Кол.

else if ColumnSchema?.IsLocked:
  if TryApplyInheritedColumnSchema(scope, schema, log):
    RebindScopeKeysAndNames(..., "inherited-schema")
    FillMarkNamesFromMergeGroupsPublic
    ApplyStandardColumnLayout
  else:
    [POSC] Схема столбцов не применена — проверьте рамку продолжения

else if Valid && infer needed:
  ... существующий infer-блок ...
```

**Важно:** для scope 1+ при locked schema **не вызывать** `TryInferColumnsFromData`.

## CMD-диагностика

| Строка | Значение |
|--------|----------|
| `[SCHEMA] locked scope=0 Mark=0 Name=2 Qty=3` | Эталон зафиксирован |
| `[SCHEMA] inherited scope=1 Mark=0 Name=2 Qty=3` | Продолжение |
| `[POSC] Шапка не найдена в первой таблице` | Нужен другой первый лист |
| `[POSC] Схема столбцов не применена` | X не совпал или мало столбцов |

## Чеклист AutoCAD

1. Таблица 1 — **со шапкой**, ~400 объектов → `[SCHEMA] locked`
2. Таблица 2 — продолжение, ~214 объектов → `[SCHEMA] inherited`, ColQty=3, WriteQty>0
3. Регрессия: без якоря (одна таблица-продолжение) — старый inference как fallback

## Ограничения

- Якорь один на сессию «Выбрать спецификацию»
- Таблицы должны иметь **одинаковые X-столбцы** на чертеже
- `PosCounterEngine` не меняем

## Связь с fix_gt20_names_qty

Сохранить: `ApplyMandatoryColQtyLayout`, `ResolveQtyInsertY`, `useCellTextFromBlock`, `skip-far-entY`.
