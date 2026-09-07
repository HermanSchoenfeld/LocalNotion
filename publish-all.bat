@echo off

dotnet build -c Release
if %ERRORLEVEL% NEQ 0 goto Exit

dotnet publish -c Release /p:PublishProfile=win-x64
del "%~dp0publish\win-x64\*.pdb"
del "%~dp0publish\win-x64\*.dll"

dotnet publish -c Release /p:PublishProfile=win-x86
del "%~dp0publish\win-x86\*.pdb"
del "%~dp0publish\win-x86\*.dll"

dotnet publish -c Release /p:PublishProfile=win-arm64
del "%~dp0publish\win-arm64\*.pdb"
del "%~dp0publish\win-arm64\*.dll"

dotnet publish -c Release /p:PublishProfile=win-arm
del "%~dp0publish\win-arm\*.pdb"
del "%~dp0publish\win-arm\*.dll"

dotnet publish -c Release /p:PublishProfile=osx-x64
del "%~dp0publish\osx-x64\*.pdb"
del "%~dp0publish\osx-x64\*.dll"

dotnet publish -c Release /p:PublishProfile=osx-arm64
del "%~dp0publish\osx-arm64\*.pdb"
del "%~dp0publish\osx-arm64\*.dll"

dotnet publish -c Release /p:PublishProfile=linux-x64
del "%~dp0publish\linux-x64\*.pdb"
del "%~dp0publish\linux-x64\*.dll"

dotnet publish -c Release /p:PublishProfile=linux-arm64
del "%~dp0publish\linux-arm64\*.pdb"
del "%~dp0publish\linux-arm64\*.dll"

dotnet publish -c Release /p:PublishProfile=linux-arm
del "%~dp0publish\linux-arm\*.pdb"
del "%~dp0publish\linux-arm\*.dll"

:Exit
pause