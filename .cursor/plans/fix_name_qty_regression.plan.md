---
name: Fix name qty binding
overview: "Восстановлена привязка марок/имён после этапа 3: pass2 по AlignmentPoint, имя только ColName, ColQty=ColName+1, qty из палитры в rowTop."
status: implemented
date: 2026-06-10
---

# Исправление распознавания наименований и записи «Кол.» — ВЫПОЛНЕНО

## Реализовано

| Шаг | Изменение |
|-----|-----------|
| pass2 Align | `CreateTextSampleFromDbText`: Data=AlignmentPoint; `AssignCellsData`: Align→Data |
| Имя col2 | `ColDesignation`, `IsTextInNameColumn`, убран neighbor fallback в Обозначение |
| ColQty col3 | `TryPreferQtyColumnAfterName`, numeric boost ColName+1, отсев массы |
| WriteQty diag | `WriteQtyDiagLines`, KeyToRowMark 55–75 |
| Continuation | `IsSectionHeaderRow` для «Продолжение…» |

## Проверка в AC 2016

1. `build-ac2016.cmd` → NETLOAD
2. Табл.2: `KeyToRowMark 55–75: 60→row…` … `70→row…`
3. `ColQty=3 источник=layout` (не col4)
4. Имена 60–70 заполнены, без текста из col1
5. `WriteQty key=62 rowTop=… qty=… (палитра)`
