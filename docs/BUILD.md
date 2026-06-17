# Сборка PosCounter.Net (DLL)

Плагин собирается **на вашем компьютере** с установленным AutoCAD: из папки AutoCAD берутся только файлы `AcMgd.dll`, `AcDbMgd.dll`, `AcCoreMgd.dll` (в DLL не копируются).

---

## Что нужно заранее

| Для версии AutoCAD | Что установить | Целевой framework |
|--------------------|----------------|-------------------|
| **2016 – 2024** | AutoCAD **x64** (например 2016) | `net46` |
| **2025 – 2026+** | AutoCAD 2025+ | `net8.0-windows` |

Дополнительно:

- [.NET SDK](https://dotnet.microsoft.com/download) (для `dotnet build`)
- Visual Studio **не обязательна** — достаточно командной строки

---

## Шаг 1. Настройка пути к AutoCAD (один раз)

1. Скопируйте файл:

   ```
   build\AutoCAD.props.template  →  build\AutoCAD.props
   ```

   **Важно — куда класть файл:**

   | Правильно | Неправильно (сборка не увидит props) |
   |-----------|--------------------------------------|
   | `Pos_counter addition\build\AutoCAD.props` | только внутри `PosCounter.Net\` без корневого `build\` — *теперь тоже работает* |
   | (папка `build` **на одном уровне** с папкой `PosCounter.Net`) | случайная другая папка |

   Допустимы **оба** варианта (после обновления проекта):
   - **рекомендуется:** `…\Pos_counter addition\build\AutoCAD.props`
   - **или:** `…\Pos_counter addition\PosCounter.Net\build\AutoCAD.props`

2. Откройте `build\AutoCAD.props` в блокноте.

3. Укажите **свой** путь к папке AutoCAD (со слэшем в конце):

   **Только AutoCAD 2016 (сборка net46):**

   ```xml
   <AutoCADSdkDirNet46>C:\Program Files\Autodesk\AutoCAD 2016\</AutoCADSdkDirNet46>
   ```

   Строку `AutoCADSdkDirNet8` **закомментируйте или удалите**, если не собираете версию для 2025+:

   ```xml
   <!-- <AutoCADSdkDirNet8>C:\Program Files\Autodesk\AutoCAD 2026\</AutoCADSdkDirNet8> -->
   ```

   **Если установлены и 2016, и 2026** — можно указать оба пути; тогда в проекте будут два target, но каждую DLL собирают **отдельной** командой (см. ниже).

4. Проверьте, что в указанной папке есть файлы:

   - `AcMgd.dll`
   - `AcDbMgd.dll`
   - `AcCoreMgd.dll`

   Обычно они лежат прямо в `C:\Program Files\Autodesk\AutoCAD 2016\`.

Файл `build\AutoCAD.props` **личный** (у каждого инженера свой путь) — в git его обычно не коммитят.

---

## Сборка в Visual Studio (вручную)

Отдельного файла `.sln` в репозитории **нет** — открывается проект **`PosCounter.Net\PosCounter.Net.csproj`**.

### Что установить в Visual Studio

| Компонент | Зачем |
|-----------|--------|
| Рабочая нагрузка **«Разработка классических приложений .NET»** | WPF + WinForms |
| **.NET Framework 4.6 Targeting Pack** (или 4.6.1+) | Сборка `net46` для AutoCAD 2016 |
| **.NET 8 SDK** | Сборка `net8.0-windows` для AutoCAD 2025+ |

Подойдут Visual Studio 2019, 2022 или новее.

### Пошагово

1. Выполните **Шаг 1** выше (`build\AutoCAD.props` с путём к AutoCAD).
2. **Файл** → **Открыть** → **Проект или решение** → выберите  
   `PosCounter.Net\PosCounter.Net.csproj`.
3. Вверху панели инструментов:
   - **Конфигурация:** `Release` (не Debug).
   - **Платформа:** `x64` (если есть в списке; иначе оставьте по умолчанию — в проекте задано `PlatformTarget=x64`).
4. **Целевая платформа (Target Framework)** — важно:
   - для **AutoCAD 2016–2024** выберите **`net46`**;
   - для **AutoCAD 2025+** — **`net8.0-windows`**.

   Где искать переключатель:
   - на панели инструментов рядом с конфигурацией (если в props указаны оба пути Net46 и Net8), **или**
   - ПКМ по проекту → **Свойства** → вкладка **Приложение** / **Сборка** → **Целевая среда** / **Target framework**.

   Если в `AutoCAD.props` **закомментирован** `AutoCADSdkDirNet8`, в списке будет только **net46** — ничего переключать не нужно.

5. Меню **Сборка** → **Собрать проект** (или **Ctrl+Shift+B**).

6. Готовая DLL:

   | Версия AutoCAD | Папка после сборки |
   |----------------|-------------------|
   | 2016–2024 | `PosCounter.Net\bin\Release\net46\PosCounter.Net.dll` |
   | 2025+ | `PosCounter.Net\bin\Release\net8.0-windows\PosCounter.Net.dll` |

7. Скопируйте DLL в `dll 2016\` или `dll 2026\` (или укажите этот путь в **NETLOAD**).

### Если VS пишет про AcMgd.dll

| Ошибка | Решение |
|--------|---------|
| `AcMgd.dll not found` | AutoCAD 2016 не установлен **или** путь в props неверный (см. ниже) |
| `AutoCADSdkDir is not set` | Создайте `Pos_counter addition\build\AutoCAD.props` из шаблона; перезагрузите проект в VS (закройте/откройте `.csproj`) |
| **CS8179 / CS8137** (`ValueTuple`, `TupleElementNamesAttribute` в `CellIndex.cs`) | Для **net46** нужен NuGet **`System.ValueTuple`** — в `.csproj` явный `Reference` на `lib\net461`; выполните **Restore** |
| **Could not find reference: System.ValueTuple 4.0.3.0** | `dotnet restore`; в `.csproj` — `GeneratePathProperty` + `$(PkgSystem_ValueTuple)\lib\net461\...` |
| NETLOAD AC 2016: ValueTuple | В папке с DLL должен лежать **`System.ValueTuple.dll`** рядом с `PosCounter.Net.dll` |
| **MSB4011** — повторный импорт `AutoCAD.props` | Нормально игнорируется: props подключается один раз через **`Directory.Build.props`** в корне репозитория, не дублируйте `<Import>` в `.csproj` |
| Сборка net46, а путь ведёт на AutoCAD 2026 | В props для сборки 2016 используйте **`AutoCADSdkDirNet46`**, не Net8 |
| Нет пункта net46 | Установите targeting pack .NET Framework 4.6 в установщике VS |

### Отладка (Debug) в AutoCAD

1. Соберите **Debug** (та же целевая платформа `net46` или `net8.0-windows`).
2. **Проект** → **Свойства** → **Отладка** (Debug):
   - **Запускаемая программа:** путь к `acad.exe`, например  
     `C:\Program Files\Autodesk\AutoCAD 2016\acad.exe`
3. F5 — запустится AutoCAD; в нём **NETLOAD** вашей DLL из `bin\Debug\...\`.

---

## Шаг 2. Сборка для AutoCAD 2016

### Вариант А — скрипт (удобнее)

Из корня репозитория:

```bat
build\build-ac2016.cmd
```

Скрипт:

1. `dotnet restore` + сборка Release `net46`
2. Кладёт в **`dll 2016\`**: `PosCounter.Net.dll` и **`System.ValueTuple.dll`**

### Вариант Б — Visual Studio

См. раздел **«Сборка в Visual Studio»** выше.

### Вариант В — командная строка (PowerShell или cmd)

```bat
dotnet build PosCounter.Net\PosCounter.Net.csproj -c Release -f net46
```

Готовая DLL:

```
PosCounter.Net\bin\x64\Release\net46\PosCounter.Net.dll
PosCounter.Net\bin\x64\Release\net46\System.ValueTuple.dll
```

(Если нет `x64` в пути — смотрите `bin\Release\net46\`.)

Скопируйте **оба** файла в `dll 2016\` или загрузите через NETLOAD из папки сборки.

---

## Шаг 3. Сборка для AutoCAD 2025 / 2026 (.NET 8)

В `build\AutoCAD.props` должен быть путь **`AutoCADSdkDirNet8`** к AutoCAD 2025+.

```bat
build\build-ac2026.cmd
```

или:

```bat
dotnet build PosCounter.Net\PosCounter.Net.csproj -c Release -f net8.0-windows
```

Результат:

```
PosCounter.Net\bin\Release\net8.0-windows\PosCounter.Net.dll
```

Копия для раздачи: **`dll 2026\PosCounter.Net.dll`**.

---

## Найти папку с AcMgd.dll (если путь «C:\Program Files\Autodesk\AutoCAD 2016\» не подходит)

В **PowerShell**:

```powershell
Get-ChildItem "C:\Program Files\Autodesk" -Recurse -Filter AcMgd.dll -ErrorAction SilentlyContinue |
  Select-Object -First 5 FullName
```

Скопируйте **папку**, в которой лежит `AcMgd.dll` (не сам файл), в `build\AutoCAD.props`:

```xml
<AutoCADSdkDirNet46>C:\Program Files\Autodesk\AutoCAD 2016\</AutoCADSdkDirNet46>
```

(со слэшем `\` в конце)

Для сборки **только 2016** закомментируйте `AutoCADSdkDirNet8` в props — в VS не будет лишнего target `net8.0-windows`.

---

## Частые ошибки сборки

| Сообщение | Причина | Что сделать |
|-----------|---------|-------------|
| `AutoCADSdkDir is not set` | Нет `build\AutoCAD.props` | Создать из `.template` |
| `AcMgd.dll not found` | Неверный путь в props | Проверить папку AutoCAD |
| `requires AutoCAD 2016-2024` при сборке net46 | В props указан путь к **2026** вместо 2016 для Net46 | `AutoCADSdkDirNet46` → папка 2016 |
| `requires AutoCAD 2025+` при net8 | Путь Net8 ведёт на 2016 | `AutoCADSdkDirNet8` → папка 2025/2026 |
| `dotnet` не найден | SDK не установлен | Установить .NET SDK |

---

## Загрузка в AutoCAD после сборки

1. Закройте AutoCAD или убедитесь, что старая DLL не заблокирована.
2. **NETLOAD** → выберите:
   - `dll 2016\PosCounter.Net.dll` — для **AutoCAD 2016–2024** (в папке также `System.ValueTuple.dll`)
   - `dll 2026\PosCounter.Net.dll` — для **AutoCAD 2025+**
3. В CMD: `[POSC] PosCounter.Net … (net46)` или `(net8.0-windows)`; палитра откроется **сама**.
4. При необходимости снова: команда **POSC**.

**Важно:** DLL для 2016 (`net46`) **нельзя** загружать в AutoCAD 2026 (.NET 8) и наоборот.

---

## Портативная сборка (только папка PosCounter.Net)

Если копируете **только** `PosCounter.Net` на рабочий стол (без корня репозитория), внутри проекта уже есть автономный набор:

| Папка / файл | Назначение |
|--------------|------------|
| `PosCounter.Net\Directory.Build.props` | Подключает локальные props при открытии `.csproj` |
| `PosCounter.Net\build\AutoCAD.props` | Путь к AutoCAD 2016 |
| `PosCounter.Net\build\NuGet.local.props` | Запасной путь ValueTuple (`$(USERPROFILE)\.nuget\...`) |
| `PosCounter.Net\build\build-ac2016.cmd` | Сборка net46 → `PosCounter.Net\dll 2016\` |
| `PosCounter.Net\build\СБОРКА_VS_AC2016.md` | Пошаговая инструкция для Visual Studio |

1. Скопируйте папку `PosCounter.Net` на рабочий стол (можно без `bin\`, `obj\`).
2. Откройте `PosCounter.Net.csproj` в VS или запустите `build\build-ac2016.cmd`.
3. Готовые DLL — в `PosCounter.Net\dll 2016\` (оба файла для NETLOAD).

---

## Сводка папок

| Папка / файл | Назначение |
|--------------|------------|
| `build\AutoCAD.props.template` | Образец настроек |
| `build\AutoCAD.props` | Ваши пути (создаёте сами) |
| `build\build-ac2016.cmd` | Сборка + копия в `dll 2016` (из корня репо) |
| `build\build-ac2026.cmd` | Сборка net8 |
| `PosCounter.Net\build\` | Портативный набор для сборки только папки проекта |
| `dll 2016\` | Готовая DLL для AC 2016–2024 (корень репо) |
| `PosCounter.Net\dll 2016\` | Готовая DLL при портативной сборке |
| `dll 2026\` | Готовая DLL для AC 2025+ |
| `PosCounter.Net\bin\Release\net46\` | Выход MSBuild (2016) |
| `PosCounter.Net\bin\Release\net8.0-windows\` | Выход MSBuild (2026) |

---

## Техническая справка

- Импорт props: `Directory.Build.props` → `build\AutoCAD.props`
- Проект: `PosCounter.Net\PosCounter.Net.csproj`
- Платформа: **x64** (`PlatformTarget`)
- Ссылки на AutoCAD: `Private=false` (не копируются в output)

Подробнее о коде: `docs\DEVELOPER.md`.
