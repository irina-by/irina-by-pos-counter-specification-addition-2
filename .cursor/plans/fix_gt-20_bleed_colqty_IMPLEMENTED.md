# fix_gt-20_bleed_colqty — выполнено 2026-06-10

План-источник: `fix_gt-20_bleed_colqty.plan.md` (не редактировался).

## Сделано в коде

| Шаг | Файл | Изменение |
|-----|------|-----------|
| ColQty layout | TableGrid.cs | `ApplyMandatoryColQtyLayout` при схеме 0/1/2/3; `TryInferColQtyFromNumericColumn` — prefer col3 |
| Имена | TableGrid.cs | `useCellTextOnly` как OLD 2016; cap `GetNextKeyRowExclusive`; `[NAME-BOUNDARY]` key 4–6 |
| WriteQty | SpecGridService.cs | `ResolveQtyInsertY` (rowTop..rowTop+1); diag `Y=`; key≤7 |
| Русская шапка | TableGrid.cs | `DbTextHeaderMaxPlainLen=60`; фильтр ГОСТ/марок; `AppendHeaderTextPart` по строкам |
| Диагностика | SpecGridService.cs | ИТОГ `layout=` + `ВНИМАНИЕ` при ColQty=-1 |

## Сборка и проверка (инженер)

1. Скопировать `PosCounter.Net` из Yandex в OneDrive Salnikava.I.
2. `build\build-ac2016.cmd` или VS Release x64 net452.
3. NETLOAD: `bin\x64\Release\net452\PosCounter.Net.dll` (+ `System.ValueTuple.dll`).
4. CMD: `build=` свежий; `ColQty=3 источник=layout`; `WriteQty key=1 qty=1 Y=…`.

## Чеклист ГТ-20

| Марка | col3 | Имя |
|-------|------|-----|
| 1 | 1 | RU + EN |
| 2 | 5 | Отвод + Elbow |
| 4 | 1 | Заглушка + Plug (без Трубы…) |
| 5 | 4 | 3 строки col2 (без bleed 6) |
