using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class AdminShellViewModel : ViewModelBase
{
    private ViewModelBase? _currentPage;
    private string _pageTitle = "Dashboard";

    public AdminShellViewModel(
        IAuthService authService,
        IServiceProvider serviceProvider,
        AdminDashboardViewModel dashboardVm,
        AdminEmployeesViewModel employeesVm,
        AdminReportsViewModel reportsVm,
        AdminJobsViewModel jobsVm,
        AdminSettingsViewModel settingsVm)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;

        MenuItems = new ObservableCollection<AdminMenuItem>
        {
            new("Dashboard", "ViewDashboard", () => NavigateTo(dashboardVm)),
            new("Çalışanlar", "AccountGroup", () => NavigateTo(employeesVm)),
            new("İş Ekleme", "Briefcase", () => NavigateTo(jobsVm)),
            new("Raporlar", "ChartBar", () => NavigateTo(reportsVm)),
            new("Ayarlar", "Cog", () => NavigateTo(settingsVm))
        };

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is AdminMenuItem item)
                item.Action();
        });

        LogoutCommand = new RelayCommand(_ => DoLogout());

        NavigateTo(dashboardVm);
    }

    private readonly IAuthService _authService;
    private readonly IServiceProvider _serviceProvider;

    public ObservableCollection<AdminMenuItem> MenuItems { get; }
    public ICommand NavigateCommand { get; }
    public ICommand LogoutCommand { get; }

    public string UserDisplayName => _authService.CurrentUser?.DisplayName ?? "Kullanıcı";

    public ViewModelBase? CurrentPage
    {
        get => _currentPage;
        set
        {
            if (value == null)
            {
                SetProperty(ref _currentPage, null);
                return;
            }
            NavigateTo(value);
        }
    }

    private void NavigateTo(ViewModelBase page)
    {
        // CallerMemberName burada NavigateTo olurdu; bağlama doğru bildirimi için adı veriyoruz.
        SetProperty(ref _currentPage, page, nameof(CurrentPage));
        PageTitle = page is AdminDashboardViewModel ? "Dashboard"
            : page is AdminEmployeesViewModel ? "Çalışanlar"
            : page is AdminJobsViewModel ? "İş Ekleme"
            : page is AdminReportsViewModel ? "Raporlar"
            : "Ayarlar";
        if (page is INavigationRefresh nav)
            nav.RefreshOnNavigate();
    }

    public string PageTitle
    {
        get => _pageTitle;
        set => SetProperty(ref _pageTitle, value);
    }

    private void DoLogout()
    {
        _authService.Logout();
        var loginWindow = _serviceProvider.GetService(typeof(Views.LoginWindow)) as Window;
        loginWindow?.Show();
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w?.DataContext == this)?.Close();
    }
}

public class AdminMenuItem
{
    public string Title { get; }
    public string IconName { get; }
    public Action Action { get; }

    public AdminMenuItem(string title, string iconName, Action action)
    {
        Title = title;
        IconName = iconName;
        Action = action;
    }
}
