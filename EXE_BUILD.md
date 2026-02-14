# EXE Oluşturma (Başka bilgisayarda test için)

## Tek seferde EXE üretmek

PowerShell’de proje klasöründeyken:

```powershell
cd "c:\Users\izcif\Desktop\CAKA v1-14.02"
dotnet publish CAKA.PerformanceApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

EXE şurada olur:

`bin\Release\net8.0-windows\win-x64\publish\`

- **Caka Personel Takip.exe** – Sadece bu dosyayı dağıtmanız yeterli. Config dosyası gerekmez; API adresi programın içinde (varsayılan: https://caka-api.onrender.com).

## EXE ikonu (uygulama logosu)

- EXE’nin ikonu **projeye gömülü**: `Assets\Images\ExeLogo.png` → `Assets\Images\app.ico` (derlemede EXE bu ikonu kullanır).
- Logoyu değiştirmek için: `Assets\Images\ExeLogo.png` dosyasını değiştirip şunu çalıştırın:
  ```powershell
  powershell -ExecutionPolicy Bypass -File "Assets\Images\CreateExeIcon.ps1"
  ```
  Sonra projeyi yeniden build/publish edin.

## Başka bilgisayarda test

1. `publish` klasörünün tamamını (EXE + CAKA.config.json) kopyalayın.
2. Hedef bilgisayarda **Caka Personel Takip.exe**’ye çift tıklayın.
3. İnternet gerekir (varsayılan: https://caka-api.onrender.com).
4. Giriş: **admin** / **1234**.

.NET veya başka kurulum gerekmez; EXE tek başına çalışır.
