@echo off
setlocal
cd /d "%~dp0"
set DB=MorningBrewCRM_AdminMC
echo Cap nhat database %DB% (khong xoa du lieu)...
sqlcmd -S "(localdb)\MSSQLLocalDB" -d %DB% -E -C -I -b -f 65001 -i "database\sql\12_workforce.sql"
if errorlevel 1 goto failed
sqlcmd -S "(localdb)\MSSQLLocalDB" -d %DB% -E -C -I -b -f 65001 -i "database\sql\13_updates.sql"
if errorlevel 1 goto failed
echo DA CAP NHAT XONG.
pause
exit /b 0
:failed
echo Cap nhat that bai. Doc loi phia tren.
pause
exit /b 1
