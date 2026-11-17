@echo off
rem Sync your work with the team on Windows. Double-click to run.
rem Runs Sync.command, the same sync script the Macs use, with Git Bash.
rem See the top of Sync.command for what it does.
setlocal
cd /d "%~dp0"

set "GITBASH="
if exist "%ProgramFiles%\Git\bin\bash.exe" set "GITBASH=%ProgramFiles%\Git\bin\bash.exe"
if not defined GITBASH if exist "%LocalAppData%\Programs\Git\bin\bash.exe" set "GITBASH=%LocalAppData%\Programs\Git\bin\bash.exe"
if defined GITBASH goto run

echo.
echo SYNC STOPPED
echo Git for Windows is not installed.
echo Double-click Setup.bat first, then run Sync again.
echo.
pause
exit /b 1

:run
"%GITBASH%" ./Sync.command
exit /b %errorlevel%
