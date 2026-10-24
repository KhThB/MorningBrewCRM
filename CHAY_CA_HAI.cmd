@echo off
setlocal
cd /d "%~dp0"
start "MorningBrew API" cmd /k call "%~dp0CHAY_API.cmd"
start "Admin_MC Web" cmd /k call "%~dp0CHAY_WEB.cmd"
echo Sau khi hai cua so hien "Now listening on", mo http://localhost:5199
pause
