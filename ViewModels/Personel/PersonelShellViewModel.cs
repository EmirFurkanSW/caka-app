using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Personel;

public class PersonelShellViewModel : ViewModelBase
{
    private ViewModelBase? _currentPage;
    private string _pageTitle = "Dashboard";

    public PersonelShellViewModel(
        IAuthService authService,
        IServiceProvider serviceProvider,
        PersonelDashboardViewModel dashboardVm,
        PersonelAddWorkViewModel addWorkVm,
        PersonelHistoryViewModel historyVm,
        PersonelProfileViewModel profileVm)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;

        MenuItems = new ObservableCollection<PersonelMenuItem>
        {
            new("Dashboard", "ViewDashboard", () => CurrentPage = dashboardVm),
            new("İş Ekle", "PlusCircle", () => CurrentPage = addWorkVm),
            new("Geçmiş", "History", () => CurrentPage = historyVm),
            new("Profil", "Account", () => CurrentPage = profileVm)
        };

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is PersonelMenuItem item)
                item.Action();
        });

        LogoutCommand = new RelayCommand(_ => DoLogout());

        CurrentPage = dashboardVm;
    }

    private readonly IAuthService _authService;
    private readonly IServiceProvider _serviceProvider;

    public ObservableCollection<PersonelMenuItem> MenuItems { get; }
    public ICommand NavigateCommand { get; }
    public ICommand LogoutCommand { get; }

    public string UserDisplayName => _authService.CurrentUser?.DisplayName ?? "Personel";

    public ViewModelBase? CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value) && value != null)
            {
                if (value is PersonelDashboardViewModel dashboard)
                    dashboard.Refresh();
                if (value is PersonelHistoryViewModel history)
                    history.Refresh();
                PageTitle = value is PersonelDashboardViewModel ? "Dashboard" : value is PersonelAddWorkViewModel ? "İş Ekle" : value is PersonelHistoryViewModel ? "Geçmiş" : "Profil";
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

public class PersonelMenuItem
{
    public string Title { get; }
    public string IconName { get; }
    public Action Action { get; }

    public PersonelMenuItem(string title, string iconName, Action action)
    {
        Title = title;
        IconName = iconName;
        Action = action;
    }
}
