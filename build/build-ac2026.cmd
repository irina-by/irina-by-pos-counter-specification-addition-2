@echo off
setlocal
REM Сборка PosCounter.Net для AutoCAD 2025+ (.NET 8, net8.0-windows).
REM В build\AutoCAD.props нужен AutoCADSdkDirNet8 (путь к AutoCAD 2025/2026).

set "ROOT=%~dp0.."
set "PROJ=%ROOT%\PosCounter.Net\PosCounter.Net.csproj"
set "OUT=%ROOT%\PosCounter.Net\bin\Release\net8.0-windows\PosCounter.Net.dll"
set "DEPLOY=%ROOT%\dll 2026\PosCounter.Net.dll"

dotnet build "%PROJ%" -c Release -f net8.0-windows
if errorlevel 1 exit /b 1

if not exist "%OUT%" (
  echo [ОШИБКА] DLL не найдена: %OUT%
  exit /b 1
)

if not exist "%ROOT%\dll 2026" mkdir "%ROOT%\dll 2026"
copy /Y "%OUT%" "%DEPLOY%" >nul
if errorlevel 1 (
  echo [ПРЕДУПРЕЖДЕНИЕ] Не удалось скопировать в "%DEPLOY%"
) else (
  echo Скопировано: %DEPLOY%
)

echo Готово: %OUT%
endlocal
