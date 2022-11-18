@echo off

dotnet build -c Release
if %ERRORLEVEL% NEQ 0 goto Exit

dotnet publish -c Release /p:PublishProfile=win-x64
del Z:\Builds\LocalNotion\win-x64\*.pdb
del Z:\Builds\LocalNotion\win-x64\*.dll

dotnet publish -c Release /p:PublishProfile=win-x86
del Z:\Builds\LocalNotion\win-x86\*.pdb
del Z:\Builds\LocalNotion\win-x86\*.dll

dotnet publish -c Release /p:PublishProfile=linux-arm64
del Z:\Builds\LocalNotion\linux-arm64\*.pdb
del Z:\Builds\LocalNotion\linux-arm64\*.dll

dotnet publish -c Release /p:PublishProfile=linux-x64
del Z:\Builds\LocalNotion\linux-x64\*.pdb
del Z:\Builds\LocalNotion\linux-x64\*.dll

dotnet publish -c Release /p:PublishProfile=osx-x64
del Z:\Builds\LocalNotion\osx-x64\*.pdb
del Z:\Builds\LocalNotion\osx-x64\*.dll

dotnet publish -c Release /p:PublishProfile=win-arm
del Z:\Builds\LocalNotion\win-arm\*.pdb
del Z:\Builds\LocalNotion\win-arm\*.dll

dotnet publish -c Release /p:PublishProfile=win-arm64
del Z:\Builds\LocalNotion\win-arm64\*.pdb
del Z:\Builds\LocalNotion\win-arm64\*.dll

:Exit