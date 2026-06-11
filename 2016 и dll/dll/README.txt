Готовые DLL для AutoCAD 2016 SP1+ — 2024 (сборка net452, .NET Framework 4.5.2+)
================================================================================

Подходит для AutoCAD 2016 SP1 (R20.1, M.49.0, .NET 4.5.2) и новее до 2024.

После сборки сюда копируются два файла:

  PosCounter.Net.dll
  System.ValueTuple.dll

Как получить:
  - Запустить build\build-ac2016.cmd
  - или собрать в Visual Studio (Release, x64, net452) и скопировать из bin\x64\Release\net452\

Загрузка в AutoCAD 2016:
  NETLOAD → выбрать PosCounter.Net.dll из этой папки.
  System.ValueTuple.dll должен лежать рядом (в той же папке).
  В CMD: [POSC] PosCounter.Net … (net452) загружен.
