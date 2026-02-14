using System.Net.Http;
using CAKA.PerformanceApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CAKA.PerformanceApp;

/// <summary>
/// Uygulama başlangıcında servisleri kaydeder. Kullanıcılar ve iş kayıtları web API üzerinden tutulur.
/// </summary>
public static class AppBootstrapper
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // API yapılandırması (CAKA.config.json veya appsettings.json)
        var apiOptions = ApiOptionsLoader.Load();
        services.AddSingleton(apiOptions);

        // HTTP client (API base URL ile)
        services.AddSingleton(sp =>
        {
            var opt = sp.GetRequiredService<ApiOptions>();
            var baseUrl = (opt.BaseUrl ?? "").TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = "https://localhost:5001";
            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl + "/"),
                Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds > 0 ? opt.TimeoutSeconds : 30)
            };
            return client;
        });

        services.AddSingleton<IApiTokenHolder, ApiTokenHolder>();
        services.AddSingleton<BackendApiClient>();

        // Auth, kullanıcılar ve iş kayıtları web API üzerinden
        services.AddSingleton<IAuthService, ApiAuthService>();
        services.AddSingleton<IUserStore, ApiUserStore>();
        services.AddSingleton<IWorkLogService, ApiWorkLogService>();
        services.AddSingleton<IAdminPasswordStore, ApiAdminPasswordStore>();

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDashboardDataService, DashboardDataService>();
        services.AddSingleton<IReportPdfService, ReportPdfService>();
        services.AddSingleton<IReportExcelService, ReportExcelService>();
        services.AddSingleton<ILastLoginStore, LastLoginStore>();

        // ViewModels (transient: her sayfa için yeni instance)
        services.AddTransient<ViewModels.LoginViewModel>();
        services.AddTransient<ViewModels.Admin.AdminShellViewModel>();
        services.AddTransient<ViewModels.Admin.AdminDashboardViewModel>();
        services.AddTransient<ViewModels.Admin.AdminEmployeesViewModel>();
        services.AddTransient<ViewModels.Admin.AdminReportsViewModel>();
        services.AddTransient<ViewModels.Admin.AdminSettingsViewModel>();
        services.AddTransient<ViewModels.Personel.PersonelShellViewModel>();
        services.AddTransient<ViewModels.Personel.PersonelDashboardViewModel>();
        services.AddTransient<ViewModels.Personel.PersonelAddWorkViewModel>();
        services.AddTransient<ViewModels.Personel.PersonelHistoryViewModel>();
        services.AddTransient<ViewModels.Personel.PersonelProfileViewModel>();

        // Windows (View + ViewModel constructor injection)
        services.AddTransient<Views.LoginWindow>();
        services.AddTransient<Views.Admin.AdminShellWindow>();
        services.AddTransient<Views.Personel.PersonelShellWindow>();

        return services.BuildServiceProvider();
    }
}
