@echo off
setlocal
REM Не показывать сообщение Microsoft про телеметрию dotnet (см. СБОРКА_VS_AC2016.md).
set DOTNET_CLI_TELEMETRY_OPTOUT=1
REM Сборка PosCounter.Net для AutoCAD 2016-2024 (.NET Framework, net46).
REM Автономный скрипт: запускать из PosCounter.Net\build\ (папка проекта на рабочем столе).
REM В build\AutoCAD.props нужен AutoCADSdkDirNet46 (путь к AutoCAD 2016).

set "ROOT=%~dp0.."
set "PROJ=%ROOT%\PosCounter.Net.csproj"
set "OUT_DIR=%ROOT%\bin\x64\Release\net46"
set "DEPLOY_DIR=%ROOT%\dll 2016"

dotnet restore "%PROJ%"
if errorlevel 1 exit /b 1

dotnet build "%PROJ%" -c Release -f net46
if errorlevel 1 exit /b 1

if not exist "%OUT_DIR%\PosCounter.Net.dll" (
  set "OUT_DIR=%ROOT%\bin\Release\net46"
)

if not exist "%OUT_DIR%\PosCounter.Net.dll" (
  echo [ОШИБКА] DLL не найдена в bin\x64\Release\net46 и bin\Release\net46
  exit /b 1
)

if not exist "%DEPLOY_DIR%" mkdir "%DEPLOY_DIR%"

copy /Y "%OUT_DIR%\PosCounter.Net.dll" "%DEPLOY_DIR%\PosCounter.Net.dll" >nul
if errorlevel 1 (
  echo [ОШИБКА] Не удалось скопировать PosCounter.Net.dll
  exit /b 1
)

if exist "%OUT_DIR%\System.ValueTuple.dll" (
  copy /Y "%OUT_DIR%\System.ValueTuple.dll" "%DEPLOY_DIR%\System.ValueTuple.dll" >nul
  if errorlevel 1 (
    echo [ПРЕДУПРЕЖДЕНИЕ] Не удалось скопировать System.ValueTuple.dll
  ) else (
    echo Скопировано: %DEPLOY_DIR%\System.ValueTuple.dll
  )
) else (
  echo [ПРЕДУПРЕЖДЕНИЕ] System.ValueTuple.dll не найдена в %OUT_DIR% — проверьте PackageReference в .csproj
)

echo Скопировано: %DEPLOY_DIR%\PosCounter.Net.dll
echo Готово: %OUT_DIR%
endlocal
