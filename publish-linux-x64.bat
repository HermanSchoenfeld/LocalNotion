@echo off
setlocal
pwsh -NoLogo -NoProfile -File "%~dp0build\package.ps1" -Runtime linux-x64 %*
exit /b %ERRORLEVEL%
