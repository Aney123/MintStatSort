@echo off
if not exist "%~dp0Windows\Instalar-mod.ps1" (
    echo Extrae el ZIP completo antes de instalar.
    echo Haz clic derecho en el ZIP, elige Extraer todo y abre la carpeta extraida.
    pause
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Windows\Instalar-mod.ps1" %*
set "mssExitCode=%ERRORLEVEL%"
pause
exit /b %mssExitCode%
