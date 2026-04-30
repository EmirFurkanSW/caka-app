using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

/// <summary>
/// Kayıtlı işleri yönetme ekranı: seçme, güncelleme, kapat/aç, sil.
/// İş mantığı AdminJobsViewModel tabanında tutulur.
/// </summary>
public class AdminJobManagementViewModel : AdminJobsViewModel
{
    public AdminJobManagementViewModel(BackendApiClient api)
        : base(api)
    {
    }
}
