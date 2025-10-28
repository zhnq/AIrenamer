@echo off
setlocal
rem Restart Windows Explorer (explorer.exe)

rem kill explorer if running (ignore errors)
taskkill /f /im explorer.exe >nul 2>&1

rem wait about 1 second using ping (more compatible)
ping -n 2 127.0.0.1 >nul

rem start explorer again
start "" explorer.exe

endlocal
exit /b 0