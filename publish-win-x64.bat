@echo off

dotnet build -c Release
if %ERRORLEVEL% NEQ 0 goto Exit

dotnet publish -c Release /p:PublishProfile=win-x64
del "%~dp0publish\win-x64\*.pdb"
del "%~dp0publish\win-x64\*.dll"

:Exit
pause