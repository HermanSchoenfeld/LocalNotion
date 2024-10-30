@echo off

dotnet build -c Release
if %ERRORLEVEL% NEQ 0 goto Exit

dotnet publish -c Release /p:PublishProfile=win-x64
del y:\builds\LocalNotion\latest\win-x64\*.pdb
del y:\builds\LocalNotion\latest\win-x64\*.dll

:Exit
pause