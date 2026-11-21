@echo off
setlocal
title Antigravity Context HUD - Rainmeter Skin Installer
cd /d "%~dp0"

echo ===============================================================================
echo         Google Antigravity Context HUD - Rainmeter Skin Installer
echo ===============================================================================
echo.

:: -----------------------------------------------------------------------------
:: Step 1: Detect Rainmeter Skins Directory
:: -----------------------------------------------------------------------------
echo [*] Detecting Rainmeter skins directory...
set "SKIN_PATH="

for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "$ini = Join-Path $env:APPDATA 'Rainmeter\Rainmeter.ini'; if (Test-Path $ini) { foreach ($l in (Get-Content $ini)) { if ($l -match '^SkinPath=(.+)') { $Matches[1].Trim(); break } } }" 2^>nul`) do (
    set "SKIN_PATH=%%I"
)

if not defined SKIN_PATH (
    for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Rainmeter" /v "SkinPath" 2^>nul ^| findstr /i "SkinPath"') do (
        set "SKIN_PATH=%%B"
    )
)

if not defined SKIN_PATH (
    if defined OneDrive (
        if exist "%OneDrive%\Documents\Rainmeter\Skins" set "SKIN_PATH=%OneDrive%\Documents\Rainmeter\Skins\"
    )
)

if not defined SKIN_PATH (
    if exist "%USERPROFILE%\Documents\Rainmeter\Skins" (
        set "SKIN_PATH=%USERPROFILE%\Documents\Rainmeter\Skins\"
    )
)

if not defined SKIN_PATH (
    for /f "usebackq delims=" %%D in (`powershell -NoProfile -Command "[Environment]::GetFolderPath('MyDocuments')" 2^>nul`) do (
        if exist "%%D\Rainmeter\Skins" set "SKIN_PATH=%%D\Rainmeter\Skins\"
    )
)

if not defined SKIN_PATH (
    echo [!] Could not automatically locate your Rainmeter Skins folder.
    set /p "SKIN_PATH=Enter the full path to your Rainmeter Skins folder: "
)

if not defined SKIN_PATH (
    echo [ERROR] No destination path provided. Installation cancelled.
    pause
    exit /b 1
)

set "SKIN_PATH=%SKIN_PATH:"=%"
if "%SKIN_PATH:~-1%"=="\" set "SKIN_PATH=%SKIN_PATH:~0,-1%"

if not exist "%SKIN_PATH%" (
    echo [ERROR] Specified skins directory does not exist: "%SKIN_PATH%"
    pause
    exit /b 1
)

echo [+] Rainmeter Skins directory found:
echo     "%SKIN_PATH%"
echo.

:: -----------------------------------------------------------------------------
:: Step 2: Ensure ContextBridge.exe Exists (Compile from source if missing)
:: -----------------------------------------------------------------------------
set "TARGET_DIR=%SKIN_PATH%\AntigravityContext"
set "HUD_DIR=%TARGET_DIR%\ContextHUD"
set "RES_DIR=%TARGET_DIR%\@Resources"

if not exist "skin\@Resources\ContextBridge.exe" (
    echo [*] ContextBridge.exe not found. Compiling from source...
    call "%~dp0src\build.bat"
    if not exist "skin\@Resources\ContextBridge.exe" (
        echo [ERROR] Compilation failed. Please check your .NET Framework installation.
        pause
        exit /b 1
    )
    echo [+] Compilation complete.
)

:: -----------------------------------------------------------------------------
:: Step 3: Deploy Files to Rainmeter Skins Directory
:: -----------------------------------------------------------------------------
echo [*] Deploying AntigravityContext skin...
if not exist "%HUD_DIR%" mkdir "%HUD_DIR%"
if not exist "%RES_DIR%" mkdir "%RES_DIR%"

copy /y "skin\ContextHUD\ContextHUD.ini" "%HUD_DIR%\" >nul
copy /y "skin\@Resources\ContextLayout.lua" "%RES_DIR%\" >nul
copy /y "skin\@Resources\ChatsMeters.inc" "%RES_DIR%\" >nul
copy /y "skin\@Resources\ContextBridge.exe" "%RES_DIR%\" >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command "$res = [System.Environment]::ExpandEnvironmentVariables('%RES_DIR%'); $utf16 = New-Object System.Text.UnicodeEncoding($false, $true); $live = Join-Path $res 'LiveContext.inc'; $state = Join-Path $res 'UserState.inc'; if (-not (Test-Path $live)) { [System.IO.File]::WriteAllText($live, \"[Variables]`r`nChatCount=0`r`n\", $utf16) }; if (-not (Test-Path $state)) { [System.IO.File]::WriteAllText($state, \"[Variables]`r`nTheme=obsidian`r`nWidgetAlpha=235`r`nTopChatCount=3`r`nCollapsed=0`r`n\", $utf16) }"

echo [+] Files deployed successfully to:
echo     "%TARGET_DIR%"
echo.

:: -----------------------------------------------------------------------------
:: Step 4: Refresh Rainmeter & Activate Skin
:: -----------------------------------------------------------------------------
set "RM_EXE=%ProgramFiles%\Rainmeter\Rainmeter.exe"
if not exist "%RM_EXE%" set "RM_EXE=%ProgramFiles(x86)%\Rainmeter\Rainmeter.exe"

if exist "%RM_EXE%" (
    echo [*] Refreshing Rainmeter and activating AntigravityContext HUD...
    "%RM_EXE%" !RefreshApp
    ping -n 2 127.0.0.1 >nul
    "%RM_EXE%" !ActivateConfig "AntigravityContext\ContextHUD" "ContextHUD.ini"
    ping -n 2 127.0.0.1 >nul
    "%RM_EXE%" !Refresh "AntigravityContext\ContextHUD"
    echo [+] Skin activated!
) else (
    echo [!] Rainmeter executable not found at default location.
    echo     Please open Rainmeter manually and load "AntigravityContext\ContextHUD\ContextHUD.ini".
)

echo.
echo ===============================================================================
echo                           INSTALLATION COMPLETE!
echo ===============================================================================
echo.
pause
