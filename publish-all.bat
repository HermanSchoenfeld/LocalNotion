@echo off

dotnet build -c Release
if %ERRORLEVEL% NEQ 0 goto Exit

dotnet publish -c Release /p:PublishProfile=win-x64
del Z:\Builds\LocalNotion\latest\win-x64\*.pdb
del Z:\Builds\LocalNotion\latest\win-x64\*.dll

dotnet publish -c Release /p:PublishProfile=win-x86
del Z:\Builds\LocalNotion\latest\win-x86\*.pdb
del Z:\Builds\LocalNotion\latest\win-x86\*.dll

dotnet publish -c Release /p:PublishProfile=win-arm64
del Z:\Builds\LocalNotion\latest\win-arm64\*.pdb
del Z:\Builds\LocalNotion\latest\win-arm64\*.dll

dotnet publish -c Release /p:PublishProfile=win-arm
del Z:\Builds\LocalNotion\latest\win-arm\*.pdb
del Z:\Builds\LocalNotion\latest\win-arm\*.dll

dotnet publish -c Release /p:PublishProfile=osx-x64
del Z:\Builds\LocalNotion\latest\osx-x64\*.pdb
del Z:\Builds\LocalNotion\latest\osx-x64\*.dll

dotnet publish -c Release /p:PublishProfile=osx-arm64
del Z:\Builds\LocalNotion\latest\osx-arm64\*.pdb
del Z:\Builds\LocalNotion\latest\osx-arm64\*.dll

dotnet publish -c Release /p:PublishProfile=linux-x64
del Z:\Builds\LocalNotion\latest\linux-x64\*.pdb
del Z:\Builds\LocalNotion\latest\linux-x64\*.dll

dotnet publish -c Release /p:PublishProfile=linux-arm64
del Z:\Builds\LocalNotion\latest\linux-arm64\*.pdb
del Z:\Builds\LocalNotion\latest\linux-arm64\*.dll

dotnet publish -c Release /p:PublishProfile=linux-arm
del Z:\Builds\LocalNotion\latest\linux-arm\*.pdb
del Z:\Builds\LocalNotion\latest\linux-arm\*.dll

:Exit
pause