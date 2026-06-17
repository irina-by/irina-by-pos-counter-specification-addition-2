Готовые DLL для AutoCAD 2016–2024 (сборка net46)
================================================

После сборки сюда копируются два файла:

  PosCounter.Net.dll
  System.ValueTuple.dll

Как получить:
  - Запустить build\build-ac2016.cmd
  - или собрать в Visual Studio (Release, x64, net46) и скопировать из bin\x64\Release\net46\

Загрузка в AutoCAD 2016:
  NETLOAD → выбрать PosCounter.Net.dll из этой папки.
  System.ValueTuple.dll должен лежать рядом (в той же папке).
