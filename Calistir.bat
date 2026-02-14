@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
title CAKA - Performans Takip Sistemi

:: Betik klasörüne geç (bat nereden çalıştırılırsa çalışsın proje kökünde olsun)
cd /d "%~dp0"

echo.
echo  ========================================
echo   CAKA - Performans Takip Sistemi
echo  ========================================
echo.

:: 1) .NET 8 SDK var mı kontrol et
echo [1/3] Gereksinimler kontrol ediliyor...
where dotnet >nul 2>nul
if %errorlevel% neq 0 goto :install_dotnet

dotnet --list-sdks 2>nul | findstr /R "8\." >nul
if %errorlevel% neq 0 goto :install_dotnet
goto :build_and_run

:install_dotnet
echo.
echo  .NET 8 SDK bulunamadi. Yukleniyor...
echo.
where winget >nul 2>nul
if %errorlevel% neq 0 (
    echo  HATA: winget bulunamadi. Lutfen .NET 8 SDK'yi elle yukleyin:
    echo  https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)
echo  winget ile .NET 8 SDK yukleniyor (onay penceresi acilabilir)...
winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
if %errorlevel% neq 0 (
    echo  Yukleme basarisiz. Lutfen .NET 8 SDK'yi elle yukleyin:
    echo  https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
echo.
echo  .NET 8 SDK yuklendi. Bu pencereyi KAPATIP Calistir.bat'i TEKRAR calistirin.
echo  (Yeni PATH'in gecerli olmasi icin gerekli.)
echo.
pause
exit /b 0

:build_and_run
echo  .NET 8 SDK: OK
echo.

:: 2) Restore ve Build
echo [2/3] Proje derleniyor...
dotnet restore
if %errorlevel% neq 0 (
    echo.
    echo  Restore basarisiz.
    pause
    exit /b 1
)

dotnet build -c Release --no-restore
if %errorlevel% neq 0 (
    echo.
    echo  Derleme basarisiz.
    pause
    exit /b 1
)

echo  Derleme tamamlandi.
echo.

:: 3) Uygulamayı çalıştır
echo [3/3] Uygulama baslatiliyor...
echo.
dotnet run -c Release --no-build --project "CAKA.PerformanceApp.csproj"

if %errorlevel% neq 0 (
    echo.
    echo  Uygulama bir hata ile kapandi.
)

echo.
pause
