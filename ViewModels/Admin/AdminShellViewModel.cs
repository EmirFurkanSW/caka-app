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
        AdminSettingsViewModel settingsVm)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;

        MenuItems = new ObservableCollection<AdminMenuItem>
        {
            new("Dashboard", "ViewDashboard", () => CurrentPage = dashboardVm),
            new("Çalışanlar", "AccountGroup", () => CurrentPage = employeesVm),
            new("Raporlar", "ChartBar", () => CurrentPage = reportsVm),
            new("Ayarlar", "Cog", () => CurrentPage = settingsVm)
        };

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is AdminMenuItem item)
                item.Action();
        });

        LogoutCommand = new RelayCommand(_ => DoLogout());

        CurrentPage = dashboardVm;
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
            if (SetProperty(ref _currentPage, value) && value != null)
            {
                if (value is AdminDashboardViewModel dashboard)
                    dashboard.RefreshAsync();
                if (value is AdminEmployeesViewModel employees)
                    employees.Reset();
                PageTitle = value is AdminDashboardViewModel ? "Dashboard" : value is AdminEmployeesViewModel ? "Çalışanlar" : value is AdminReportsViewModel ? "Raporlar" : "Ayarlar";
            }
        }
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
