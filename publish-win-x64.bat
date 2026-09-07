@echo off
setlocal
pwsh -NoLogo -NoProfile -File "%~dp0build\package.ps1" -Runtime win-x64 %*
exit /b %ERRORLEVEL%
