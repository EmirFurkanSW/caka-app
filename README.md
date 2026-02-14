# CAKA - Çalışan Performans Takip Sistemi (Mockup)

Windows masaüstü uygulaması — WPF (.NET 8), MVVM, kurumsal tema.

## Gereksinimler

- **.NET 8 SDK** — [İndir](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11

## Derleme ve Çalıştırma

### Visual Studio 2022
1. `CAKA.PerformanceApp.sln` dosyasını açın.
2. Projeyi derleyin (Ctrl+Shift+B) ve çalıştırın (F5).

### Komut satırı
```bash
cd c:\Users\izcif\Desktop\CAKA
dotnet restore
dotnet build
dotnet run
```

## Giriş Bilgileri (Mock)

| Rol      | Kullanıcı adı | Şifre |
|----------|----------------|--------|
| Admin    | admin          | 1234   |
| Personel | user           | 1234   |

## Proje Yapısı

- **Core** — ViewModelBase, RelayCommand
- **Models** — User, UserRole, WorkLog, ActivityItem
- **Services** — IAuthService, IWorkLogService, IDashboardDataService, INavigationService (arayüzler + in-memory uygulamalar)
- **ViewModels** — Login, Admin (Shell, Dashboard, Çalışanlar, Raporlar, Ayarlar), Personel (Shell, Dashboard, İş Ekle, Geçmiş, Profil)
- **Views** — Her ekran için XAML + code-behind (minimum)
- **Styles** — CorporateTheme.xaml (lacivert #1E2A38, açık gri, soft mavi accent)
- **Converters** — StringToVisibility, ChartBarWidth, DecimalToString

## Mimari

- **MVVM**: View'lar sadece DataContext ve komutlara bağlı; iş mantığı ViewModel ve servislerde.
- **DI**: Microsoft.Extensions.DependencyInjection ile servis ve ViewModel kayıtları; ileride API/veritabanı eklenirken aynı arayüzler kullanılabilir.
- **Veri katmanı**: `IWorkLogService`, `IAuthService`, `IDashboardDataService` arayüzleri sayesinde ileride gerçek veritabanı veya API ile değiştirilebilir.

## Sonraki Adımlar (İsteğe Bağlı)

- Gerçek veritabanı (ör. Entity Framework Core) ile `IWorkLogService` ve `IAuthService` uygulamaları
- Material Design in XAML bileşenlerini daha fazla kullanma
- Raporlama ve çalışan listesi ekranlarına gerçek veri bağlama
