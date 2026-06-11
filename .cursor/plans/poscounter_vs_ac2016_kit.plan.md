---
name: PosCounter VS AC2016 kit
overview: "Создать внутри PosCounter.Net автономный набор для сборки net46 в Visual Studio на рабочем ПК Salnikava.I: Directory.Build.props, build\\ с вашим AutoCAD.props, NuGet.local.props для ValueTuple, скрипт сборки и инструкция. Копируется только папка PosCounter.Net."
todos:
  - id: create-directory-build-props
    content: Создать PosCounter.Net/Directory.Build.props с импортом build/AutoCAD.props и build/NuGet.local.props
    status: completed
  - id: create-build-folder
    content: "Создать PosCounter.Net/build/: AutoCAD.props (ваш вариант), template, NuGet.local.props, build-ac2016.cmd, СБОРКА_VS_AC2016.md"
    status: completed
  - id: create-dll2016-readme
    content: Создать PosCounter.Net/dll 2016/README.txt
    status: completed
  - id: update-docs
    content: Обновить docs/DEVELOPER.md, docs/BUILD.md, .cursor/DIALOGUE_LOG.md и сохранить план в .cursor/plans/
    status: completed
isProject: false
---

# Портативный набор сборки AC 2016 в PosCounter.Net

## Статус: реализовано (2026-06-10)

## Задача

На рабочий ПК копируется **только** папка `PosCounter.Net/` (без корневого репозитория). Автономный набор настроек — как в `2016 и dll/`, но внутри проекта.

**AutoCAD.props:**

```xml
<Project>
  <PropertyGroup>
    <AutoCADSdkDirNet46>C:\Program Files\Autodesk\AutoCAD 2016\</AutoCADSdkDirNet46>
  </PropertyGroup>
</Project>
```

Путь ValueTuple: `$(USERPROFILE)\.nuget\packages\system.valuetuple\4.5.0` (на ПК Salnikava.I — `C:\Users\Salnikava.I\.nuget\...`).

## Созданная структура

```text
PosCounter.Net/
  Directory.Build.props
  build/
    AutoCAD.props
    AutoCAD.props.template
    NuGet.local.props
    build-ac2016.cmd
    СБОРКА_VS_AC2016.md
  dll 2016/
    README.txt
```

## Проверка на рабочем ПК

1. Скопировать `PosCounter.Net` на рабочий стол.
2. `build\build-ac2016.cmd` или сборка в VS (Release, x64, net46).
3. В `dll 2016\` — `PosCounter.Net.dll` + `System.ValueTuple.dll`.
4. NETLOAD в AC 2016 → CMD: `[POSC] … (net46) загружен`.
