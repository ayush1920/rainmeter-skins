@echo off
setlocal enabledelayedexpansion
title Antigravity Limits - Rainmeter Skin Installer
cd /d "%~dp0"

echo ===============================================================================
echo          Google Antigravity Limits HUD - Rainmeter Skin Installer
echo ===============================================================================
echo.

:: -----------------------------------------------------------------------------
:: Step 1: Detect Rainmeter Skins Directory
:: -----------------------------------------------------------------------------
echo [*] Detecting Rainmeter skins directory...
set "SKIN_PATH="

:: Check Rainmeter.ini in %APPDATA%\Rainmeter\Rainmeter.ini
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "$ini = Join-Path $env:APPDATA 'Rainmeter\Rainmeter.ini'; if (Test-Path $ini) { foreach ($l in (Get-Content $ini)) { if ($l -match '^SkinPath=(.+)') { $Matches[1].Trim(); break } } }" 2^>nul`) do (
    set "SKIN_PATH=%%I"
)

:: Fallback 1: Registry query
if not defined SKIN_PATH (
    for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Rainmeter" /v "SkinPath" 2^>nul ^| findstr /i "SkinPath"') do (
        set "SKIN_PATH=%%B"
    )
)

:: Fallback 2: OneDrive Documents
if not defined SKIN_PATH (
    if defined OneDrive (
        if exist "%OneDrive%\Documents\Rainmeter\Skins" set "SKIN_PATH=%OneDrive%\Documents\Rainmeter\Skins\"
    )
)

:: Fallback 3: UserProfile Documents
if not defined SKIN_PATH (
    if exist "%USERPROFILE%\Documents\Rainmeter\Skins" (
        set "SKIN_PATH=%USERPROFILE%\Documents\Rainmeter\Skins\"
    )
)

:: Fallback 4: Windows Known Folder (MyDocuments)
if not defined SKIN_PATH (
    for /f "usebackq delims=" %%D in (`powershell -NoProfile -Command "[Environment]::GetFolderPath('MyDocuments')" 2^>nul`) do (
        if exist "%%D\Rainmeter\Skins" set "SKIN_PATH=%%D\Rainmeter\Skins\"
    )
)

if not defined SKIN_PATH (
    echo [!] Could not automatically locate your Rainmeter Skins folder.
    echo     Please make sure Rainmeter is installed.
    echo.
    set /p "SKIN_PATH=Enter the full path to your Rainmeter Skins folder: "
)

if not defined SKIN_PATH (
    echo [ERROR] No destination path provided. Installation cancelled.
    pause
    exit /b 1
)

:: Remove trailing slash if present
if "%SKIN_PATH:~-1%"=="\" set "SKIN_PATH=%SKIN_PATH:~0,-1%"

if not exist "%SKIN_PATH%" (
    echo [ERROR] Specified skins directory does not exist:
    echo         "%SKIN_PATH%"
    pause
    exit /b 1
)

echo [+] Rainmeter Skins directory found:
echo     "%SKIN_PATH%"
echo.

:: -----------------------------------------------------------------------------
:: Step 2: Ensure AntigravityBridge.exe Binary Exists (Build from src if missing)
:: -----------------------------------------------------------------------------
set "TARGET_DIR=%SKIN_PATH%\AntigravityQuota"
set "HUD_DIR=%TARGET_DIR%\QuotaHUD"
set "RES_DIR=%TARGET_DIR%\@Resources"

if not exist "skin\@Resources\AntigravityBridge.exe" (
    echo [*] AntigravityBridge.exe not found in prebuilt folder. Compiling from source...
    set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if not exist "!CSC!" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    
    if not exist "!CSC!" (
        echo [ERROR] .NET Framework C# compiler (csc.exe) not found.
        pause
        exit /b 1
    )
    
    if not exist "skin\@Resources" mkdir "skin\@Resources"
    pushd src
    "!CSC!" /nologo /optimize+ /target:winexe /main:Program ^
      /out:"..\skin\@Resources\AntigravityBridge.exe" ^
      /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
      /r:System.Security.dll /r:System.Management.dll /r:System.Web.Extensions.dll ^
      AccountDialog.cs AccountManagerForm.cs VaultAndAuth.cs QuotaPoller.cs Program.cs
    popd
    
    if not exist "skin\@Resources\AntigravityBridge.exe" (
        echo [ERROR] Compilation failed.
        pause
        exit /b 1
    )
    echo [+] Compilation complete.
)

:: -----------------------------------------------------------------------------
:: Step 3: Deploy Files to Rainmeter Skins Directory
:: -----------------------------------------------------------------------------
echo [*] Deploying AntigravityQuota skin...
if not exist "%HUD_DIR%" mkdir "%HUD_DIR%"
if not exist "%RES_DIR%" mkdir "%RES_DIR%"

copy /y "skin\QuotaHUD\QuotaHUD.ini" "%HUD_DIR%\" >nul
copy /y "skin\@Resources\HUDLayout.lua" "%RES_DIR%\" >nul
copy /y "skin\@Resources\AccountsMeters.inc" "%RES_DIR%\" >nul
copy /y "skin\@Resources\AntigravityBridge.exe" "%RES_DIR%\" >nul

:: Create initial LiveQuota.inc and UserState.inc if they don't exist
powershell -NoProfile -Command "
$res = '%RES_DIR%';
$utf16 = New-Object System.Text.UnicodeEncoding($false, $true);
$live = Join-Path $res 'LiveQuota.inc';
$state = Join-Path $res 'UserState.inc';
if (-not (Test-Path $live)) { [System.IO.File]::WriteAllText($live, \"[Variables]`r`nAccCount=0`r`n\", $utf16) }
if (-not (Test-Path $state)) { [System.IO.File]::WriteAllText($state, \"[Variables]`r`nTheme=obsidian`r`nWidgetAlpha=155`r`n\", $utf16) }
"

echo [+] Files deployed successfully to:
echo     "%TARGET_DIR%"
echo.

:: -----------------------------------------------------------------------------
:: Step 4: Refresh Rainmeter & Activate Skin
:: -----------------------------------------------------------------------------
set "RM_EXE=%ProgramFiles%\Rainmeter\Rainmeter.exe"
if not exist "%RM_EXE%" set "RM_EXE=%ProgramFiles(x86)%\Rainmeter\Rainmeter.exe"

if exist "%RM_EXE%" (
    echo [*] Refreshing Rainmeter and activating AntigravityQuota HUD...
    "%RM_EXE%" !RefreshApp
    timeout /t 1 /nobreak >nul
    "%RM_EXE%" !ActivateConfig "AntigravityQuota\QuotaHUD" "QuotaHUD.ini"
    timeout /t 1 /nobreak >nul
    "%RM_EXE%" !Refresh "AntigravityQuota\QuotaHUD"
    echo [+] Skin activated!
) else (
    echo [!] Rainmeter executable not found at default location.
    echo     Please open Rainmeter manually and load "AntigravityQuota\QuotaHUD\QuotaHUD.ini".
)

echo.
echo ===============================================================================
echo                           INSTALLATION COMPLETE!
echo ===============================================================================
echo.
echo Would you like to open the Account Manager now to link your Google account?
set /p "LAUNCH_MGR=[Y/N] (Default is Y): "
if /i "%LAUNCH_MGR%"=="N" goto finish

start "" "%RES_DIR%\AntigravityBridge.exe" manage obsidian

:finish
echo.
echo Enjoy your new Antigravity Limits HUD!
pause
