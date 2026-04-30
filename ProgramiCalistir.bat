@echo off
chcp 65001 >nul
setlocal
title CAKA - Yerel calistirma (Git / push gerekmez)

cd /d "%~dp0"

echo.
echo  CAKA Personel Takip - Yerel calistirma
echo  Proje klasoru: %cd%
echo.

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [HATA] dotnet bulunamadi. .NET 8 SDK kurulu olmali.
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/2] Restore + Debug derlemesi...
dotnet restore "CAKA.PerformanceApp.csproj"
if %errorlevel% neq 0 goto :fail

dotnet build "CAKA.PerformanceApp.csproj" -c Debug --no-restore
if %errorlevel% neq 0 goto :fail

echo [2/2] Uygulama aciliyor...
echo.

set "EXE=%cd%\bin\Debug\net8.0-windows\Caka Personel Takip.exe"
if not exist "%EXE%" (
    echo [HATA] EXE bulunamadi:
    echo %EXE%
    echo Derleme ciktisi yolunu kontrol edin.
    pause
    exit /b 1
)

start "" "%EXE%"
exit /b 0

:fail
echo.
echo Derleme basarisiz. Yukaridaki mesajlari kontrol edin.
pause
exit /b 1
