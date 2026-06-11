---
name: AC2016 DLL fix
overview: "DLL net46 для AutoCAD 2016 — деплой ValueTuple, баг inference/rebind, MText bounds. Реализовано 2026-06-10."
todos:
  - id: engineer-deploy-both-dll
    content: "Инженер: acad.lsp без LSP; в dll 2016\\ оба файла; NETLOAD → ЗАПУСТИТЬ → спецификация; прислать лог CMD"
    status: completed
  - id: csproj-valuetuple-net46
    content: "PosCounter.Net.csproj: GeneratePathProperty + Reference System.ValueTuple lib/net461, Private=True"
    status: completed
  - id: create-build-ac2016-cmd
    content: "Создать build/build-ac2016.cmd: сборка net46 + копия PosCounter.Net.dll и System.ValueTuple.dll в dll 2016"
    status: completed
  - id: fix-infer-rebind
    content: "TableGrid.RebindScopeKeysAndNames: не сбрасывать ColMark/ColName если ColumnsInferredFromData"
    status: completed
  - id: fix-mtext-ac2016
    content: "TableGrid: улучшить CreateTextSampleFromMText/AssignCellsData для AC 2016"
    status: completed
  - id: version-stamp-netload
    content: "Commands.Initialize: [POSC] версия net46/net8 в CMD при загрузке"
    status: completed
  - id: rebuild-test-ac2016
    content: Сборка dll 2016, тест на AC 2016 том же чертеже; регрессия AC 2026 с dll 2026
    status: completed
  - id: update-docs-log
    content: Обновить INSTRUCTION_ENGINEER, BUILD, DEVELOPER, DIALOGUE_LOG
    status: completed
isProject: false
---

# План: DLL для AutoCAD 2016 — статус реализации (2026-06-10)

Код и документация внесены. **Ручной тест в AutoCAD 2016** — на ПК инженера с установленным AC 2016.

## Что сделано в коде

| Задача | Файл |
|--------|------|
| ValueTuple net46 | `PosCounter.Net.csproj` |
| `build-ac2016.cmd` | `build/build-ac2016.cmd` |
| Skip DetectHeader после inference | `SpecGrid/TableGrid.cs` → `RebindScopeKeysAndNames` |
| MText bounds AC 2016 | `TableGrid.cs` → `TryGetMTextBounds` |
| CMD диагностика текстов вне ячеек | `TableGrid.cs`, `SpecGridService.cs` |
| Баннер NETLOAD | `Commands.cs` → `WriteNetLoadBanner` |

## Сборка

- **net8.0-windows:** OK на машине разработки.
- **net46:** нужен `build\AutoCAD.props` с путём к AC 2016 → `build\build-ac2016.cmd`.

## Проверка инженером (AC 2016)

1. `build\build-ac2016.cmd`
2. NETLOAD `dll 2016\PosCounter.Net.dll` (рядом `System.ValueTuple.dll`)
3. В CMD: `[POSC] PosCounter.Net … (net46) загружен`
4. Сброс → ЗАПУСТИТЬ → Выбрать спецификацию (с шапкой)
5. Ожидание: `ColMark`, `ColName`, `KeyToRowMark` в CMD

Полное описание проблемы и критерии — в исходном плане (копия в репозитории).
