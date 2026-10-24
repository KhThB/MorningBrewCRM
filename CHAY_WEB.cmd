@echo off
setlocal
cd /d "%~dp0"
set ASPNETCORE_ENVIRONMENT=Development
where dotnet >nul 2>nul
if errorlevel 1 goto missing
echo Web Admin_MC - http://localhost:5199
echo Hay mo dia chi tren sau khi thay "Now listening on".
dotnet run --project "%~dp0Admin_MC\CRMManagementWeb.csproj" --no-launch-profile --urls http://localhost:5199
pause
exit /b
:missing
echo Can cai .NET 10 SDK. Xem README.md.
pause
