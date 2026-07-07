using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Services;
using CAKA.PerformanceApp.ViewModels.Admin;

namespace CAKA.PerformanceApp.ViewModels.Personel;

public class PersonelShellViewModel : ViewModelBase
{
    private ViewModelBase? _currentPage;
    private string _pageTitle = "Dashboard";

    public PersonelShellViewModel(
        IAuthService authService,
        IServiceProvider serviceProvider,
        BackendApiClient api,
        PersonelDashboardViewModel dashboardVm,
        PersonelAddWorkViewModel addWorkVm,
        PersonelHistoryViewModel historyVm,
        PersonelProfileViewModel profileVm,
        AdminJobManagementViewModel jobManagementVm)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;

        MenuItems = new ObservableCollection<PersonelMenuItem>
        {
            new("Dashboard", "ViewDashboard", () => NavigateTo(dashboardVm)),
            new("İş Kaydı Gir", "PlusCircle", () => NavigateTo(addWorkVm)),
            new("Geçmiş", "History", () => NavigateTo(historyVm)),
            new("Profil", "Account", () => NavigateTo(profileVm))
        };

        if (IsProjectManagerForAnyJob(api, authService))
        {
            MenuItems.Insert(1, new PersonelMenuItem("İş Yönetimi", "BriefcaseEdit", () => NavigateTo(jobManagementVm)));
        }

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is PersonelMenuItem item)
                item.Action();
        });

        LogoutCommand = new RelayCommand(_ => DoLogout());

        NavigateTo(dashboardVm);
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
            if (value == null)
            {
                SetProperty(ref _currentPage, null);
                return;
            }
            NavigateTo(value);
        }
    }

    private static bool IsProjectManagerForAnyJob(BackendApiClient api, IAuthService authService)
    {
        var me = authService.CurrentUser?.UserName;
        if (string.IsNullOrWhiteSpace(me)) return false;
        try
        {
            return api.GetJobs(activeOnly: false)
                .Any(j => string.Equals(j.ProjectManagerUserName, me, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private void NavigateTo(ViewModelBase page)
    {
        SetProperty(ref _currentPage, page, nameof(CurrentPage));
        PageTitle = page is PersonelDashboardViewModel ? "Dashboard"
            : page is PersonelAddWorkViewModel ? "İş Kaydı Gir"
            : page is AdminJobManagementViewModel ? "İş Yönetimi"
            : page is PersonelHistoryViewModel ? "Geçmiş"
            : "Profil";
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
