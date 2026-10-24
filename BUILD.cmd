@echo off
setlocal
cd /d "%~dp0"
dotnet build "%~dp0MorningBrewCRM.slnx"
if errorlevel 1 goto failed
echo BUILD THANH CONG.
pause
exit /b 0
:failed
echo BUILD THAT BAI. Giu lai cua so nay de xem loi.
pause
exit /b 1
