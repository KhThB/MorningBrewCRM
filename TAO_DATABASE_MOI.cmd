@echo off
setlocal
cd /d "%~dp0"
echo Tao MorningBrewCRM tren LocalDB. Khong xoa database da co.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0database\tools\Apply.ps1"
if errorlevel 1 goto failed
echo DA TAO DATABASE.
pause
exit /b 0
:failed
echo Chua tao xong. Doc loi phia tren. Neu database da co, khong can tao lai.
pause
exit /b 1
