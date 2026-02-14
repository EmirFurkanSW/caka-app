using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private string _userName = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    private readonly IAuthService _authService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILastLoginStore _lastLoginStore;
    private readonly ObservableCollection<string> _savedUserNames = new();
    private readonly Dictionary<string, string> _savedPasswords = new(StringComparer.OrdinalIgnoreCase);

    public LoginViewModel(IAuthService authService, IServiceProvider serviceProvider, ILastLoginStore lastLoginStore)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;
        _lastLoginStore = lastLoginStore;
        LoginCommand = new RelayCommand(_ => DoLogin(), _ => !IsBusy && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password));
        RemoveSavedUserCommand = new RelayCommand(_ => DoRemoveSavedUser(), _ => CanRemoveSavedUser());

        var (logins, lastUsed) = _lastLoginStore.GetAllLogins();
        // Bu bilgisayarda daha önce giriş yapmış tüm kullanıcı adları listelenir; şifre saklanmaz.
        foreach (var (u, _) in logins)
            if (!string.IsNullOrEmpty(u) && !_savedUserNames.Contains(u))
                _savedUserNames.Add(u);
        var preSelect = lastUsed ?? _savedUserNames.FirstOrDefault();
        if (!string.IsNullOrEmpty(preSelect))
        {
            _userName = preSelect;
            _password = _savedPasswords.TryGetValue(preSelect, out var pass) ? pass : "";
        }
    }

    /// <summary>Kaydedilmiş kullanıcı adları listesi (ComboBox için).</summary>
    public ObservableCollection<string> SavedUserNames => _savedUserNames;

    /// <summary>Listeden seçilen kullanıcı için kaydedilmiş şifreyi döner.</summary>
    public string? GetPasswordForUser(string? userName)
    {
        return userName != null && _savedPasswords.TryGetValue(userName, out var p) ? p : null;
    }

    public string UserName
    {
        get => _userName;
        set
        {
            if (SetProperty(ref _userName, value))
            {
                ClearError();
                ((RelayCommand)RemoveSavedUserCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) { ClearError(); } }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand RemoveSavedUserCommand { get; }

    private bool CanRemoveSavedUser()
    {
        var trimmed = UserName?.Trim();
        return !string.IsNullOrEmpty(trimmed) && _savedUserNames.Contains(trimmed);
    }

    private void DoRemoveSavedUser()
    {
        var trimmed = UserName?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !_savedUserNames.Contains(trimmed)) return;
        _lastLoginStore.RemoveUserName(trimmed);
        _savedUserNames.Remove(trimmed);
        if (UserName == trimmed)
            UserName = _savedUserNames.FirstOrDefault() ?? string.Empty;
        ((RelayCommand)RemoveSavedUserCommand).RaiseCanExecuteChanged();
    }

    private void ClearError() => ErrorMessage = string.Empty;

    private void DoLogin()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            try
            {
                if (!_authService.Login(UserName.Trim(), Password))
                {
                    ErrorMessage = "Kullanıcı adı veya şifre hatalı.";
                    return;
                }
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                return;
            }

            _lastLoginStore.SaveLogin(UserName.Trim(), Password);

            var currentUser = _authService.CurrentUser!;
            Window? targetWindow = null;

            if (currentUser.Role == UserRole.Admin)
            {
                var adminShell = _serviceProvider.GetService(typeof(Views.Admin.AdminShellWindow)) as Window;
                targetWindow = adminShell;
            }
            else
            {
                var personelShell = _serviceProvider.GetService(typeof(Views.Personel.PersonelShellWindow)) as Window;
                targetWindow = personelShell;
            }

            if (targetWindow != null)
            {
                targetWindow.Show();
                Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.LoginWindow)?.Close();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
