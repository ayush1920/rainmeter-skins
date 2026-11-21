@echo off
setlocal
cd /d "%~dp0"

echo [1/2] Locating .NET Framework C# Compiler...
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo [ERROR] Could not find csc.exe. Please ensure .NET Framework 4.5+ is installed.
    exit /b 1
)

echo [2/2] Compiling ContextBridge.exe...
set "OUT_DIR=..\skin\@Resources"
if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"

"%CSC%" /nologo /optimize+ /target:winexe /main:ContextBridge ^
  /out:"%OUT_DIR%\ContextBridge.exe" ^
  /r:System.dll /r:System.Core.dll ^
  ContextBridge.cs

if %ERRORLEVEL% equ 0 (
    echo [OK] Build successful! Output: %OUT_DIR%\ContextBridge.exe
) else (
    echo [ERROR] Compilation failed with error code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)
