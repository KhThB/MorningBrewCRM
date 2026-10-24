@echo off
setlocal
cd /d "%~dp0"
set ASPNETCORE_ENVIRONMENT=Development
where dotnet >nul 2>nul
if errorlevel 1 goto missing
echo API MorningBrew - http://localhost:5099
echo Connection string: MorningBrew.Api\appsettings.Development.json
dotnet run --project "%~dp0MorningBrew.Api\MorningBrew.Api.csproj" --no-launch-profile --urls http://localhost:5099
pause
exit /b
:missing
echo Can cai .NET 10 SDK. Xem README.md.
pause
