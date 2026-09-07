@echo off

dotnet build -c Release
if %ERRORLEVEL% NEQ 0 goto Exit

dotnet publish -c Release /p:PublishProfile=linux-x64
del "%~dp0publish\linux-x64\*.pdb"
del "%~dp0publish\linux-x64\*.dll"

:Exit
pause