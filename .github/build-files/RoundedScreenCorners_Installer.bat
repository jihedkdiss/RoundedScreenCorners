@echo off
TITLE Rounded Screen Corners Installer
CLS

ECHO ===================================================
ECHO   Rounded Screen Corners Installer
ECHO ===================================================
ECHO.
ECHO This will:
ECHO   1. Add the Rounded Screen Corners signing certificate to your
ECHO      Trusted Root store (admin elevation required).
ECHO   2. Install the Rounded Screen Corners MSIX package.
ECHO   3. Launch the app.
ECHO.
ECHO Press any key to continue, or close this window to abort.
PAUSE >nul

:: Self-elevate to admin if not already
NET SESSION >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    PowerShell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    EXIT /B
)

cd /d "%~dp0"

PowerShell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-RoundedScreenCorners.ps1"

IF %ERRORLEVEL% NEQ 0 (
    ECHO.
    ECHO Installation did not complete cleanly. See messages above.
    PAUSE
    EXIT /B 1
)

ECHO.
ECHO Done. You can close this window.
TIMEOUT /T 3 >nul
EXIT /B 0
