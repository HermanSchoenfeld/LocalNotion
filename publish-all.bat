@echo off
setlocal
for %%R in (win-x64 win-x86 win-arm64 linux-x64 linux-arm64 linux-arm osx-x64 osx-arm64) do (
    pwsh -NoLogo -NoProfile -File "%~dp0build\package.ps1" -Runtime %%R %*
    if errorlevel 1 exit /b 1
)
exit /b 0
