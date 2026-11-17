@echo off
rem One-time setup for Sync.bat on Windows. Double-click to run. Safe to run again.
rem Installs Git for Windows (which includes Git LFS and Git Bash) if it is missing,
rem then runs Setup.command, the same setup script the Macs use.
setlocal
cd /d "%~dp0"

call :find_bash
if defined GITBASH goto run

echo == Git for Windows
echo    Installing Git for Windows. This can take a few minutes.
echo    If it asks you to agree to terms, type Y and press Enter.
echo    If Windows asks "Do you want to allow this app to make changes", click Yes.
where winget >nul 2>&1
if errorlevel 1 goto no_winget
winget install --id Git.Git --exact --source winget
call :find_bash
if defined GITBASH goto run
echo.
echo SETUP STOPPED
echo Git for Windows did not install.
echo Send a screenshot of this window to Hubert.
goto fail

:no_winget
echo.
echo SETUP STOPPED
echo This PC cannot install Git automatically.
echo Download and install Git for Windows from https://git-scm.com/download/win
echo (keep all the default options), then double-click Setup.bat again.
goto fail

:run
"%GITBASH%" ./Setup.command
exit /b %errorlevel%

:fail
echo.
pause
exit /b 1

:find_bash
set "GITBASH="
if exist "%ProgramFiles%\Git\bin\bash.exe" set "GITBASH=%ProgramFiles%\Git\bin\bash.exe"
if not defined GITBASH if exist "%LocalAppData%\Programs\Git\bin\bash.exe" set "GITBASH=%LocalAppData%\Programs\Git\bin\bash.exe"
exit /b 0
