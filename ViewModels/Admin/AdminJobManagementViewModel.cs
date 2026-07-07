using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

/// <summary>
/// Kayıtlı işleri yönetme ekranı: seçme, güncelleme, kapat/aç, sil.
/// Proje müdürü yalnızca kendi işlerini görür ve düzenler.
/// </summary>
public class AdminJobManagementViewModel : AdminJobsViewModel
{
    public AdminJobManagementViewModel(BackendApiClient api, IAuthService authService)
        : base(api, authService)
    {
    }
}
