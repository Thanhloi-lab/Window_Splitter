@echo off
setlocal

rem Resolve the repository root so this script works from any working directory.
set "REPO_ROOT=%~dp0.."
for %%I in ("%REPO_ROOT%") do set "REPO_ROOT=%%~fI"
pushd "%REPO_ROOT%"
if errorlevel 1 (
    echo [ERROR] Could not locate the repository root.
    pause
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK was not found. Install the .NET 8 SDK and try again.
    goto :failed
)

echo [1/2] Publishing a self-contained Windows x64 build...
dotnet publish "WindowsSplitter.csproj" -c Release -r win-x64 --self-contained true -o "publish\win-x64"
if errorlevel 1 (
    echo [ERROR] Failed to build or publish Windows Splitter.
    goto :failed
)

rem Find Inno Setup from INNO_SETUP_COMPILER, PATH, or its standard per-user/system install locations.
set "ISCC="
if defined INNO_SETUP_COMPILER if exist "%INNO_SETUP_COMPILER%" set "ISCC=%INNO_SETUP_COMPILER%"
if not defined ISCC (
    where ISCC.exe >nul 2>nul
    if not errorlevel 1 set "ISCC=ISCC.exe"
)
if not defined ISCC if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%LOCALAPPDATA%\Programs\InnoSetup\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\InnoSetup\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC (
    echo [ERROR] Inno Setup 6 was not found.
    echo Install Inno Setup 6 or add ISCC.exe to PATH, then run this script again.
    goto :failed
)

echo [2/2] Compiling the installer with Inno Setup...
pushd "%~dp0"
"%ISCC%" "WindowsSplitter.iss"
set "ISCC_EXIT_CODE=%ERRORLEVEL%"
popd
if not "%ISCC_EXIT_CODE%"=="0" (
    echo [ERROR] Failed to compile the installer.
    goto :failed
)

for %%F in ("%~dp0output\WindowsSplitter-Setup-*-x64.exe") do (
    powershell -NoProfile -Command "$h=(Get-FileHash -Algorithm SHA256 '%%~fF').Hash.ToLower(); Set-Content -Path '%%~fF.sha256' -Value ($h + '  ' + '%%~nxF')" >nul 2>nul
)

echo ========================================================
echo SUCCESS! Installer created in:
echo %~dp0output\
echo SHA-256 checksum generated alongside the installer (.sha256).
echo ========================================================
popd
pause
exit /b 0

:failed
popd
echo.
echo Packaging failed. Review the error above, then press any key to close this window.
pause
exit /b 1
