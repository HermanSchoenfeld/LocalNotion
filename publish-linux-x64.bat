@echo off

dotnet build -c Release
if %ERRORLEVEL% NEQ 0 goto Exit

dotnet publish -c Release /p:PublishProfile=linux-x64
del Z:\Builds\LocalNotion\latest\linux-x64\*.pdb
del Z:\Builds\LocalNotion\latest\linux-x64\*.dll

:Exit
pause