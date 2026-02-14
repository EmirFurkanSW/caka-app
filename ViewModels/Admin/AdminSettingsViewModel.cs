using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class AdminSettingsViewModel : ViewModelBase
{
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = string.Empty;

    public AdminSettingsViewModel(IAdminPasswordStore adminPasswordStore)
    {
        _adminPasswordStore = adminPasswordStore;
        ChangePasswordCommand = new RelayCommand(_ => ChangePassword());
    }

    private readonly IAdminPasswordStore _adminPasswordStore;

    public string CurrentPassword
    {
        get => _currentPassword;
        set { if (SetProperty(ref _currentPassword, value ?? "")) ClearStatus(); }
    }

    public string NewPassword
    {
        get => _newPassword;
        set { if (SetProperty(ref _newPassword, value ?? "")) ClearStatus(); }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { if (SetProperty(ref _confirmPassword, value ?? "")) ClearStatus(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand ChangePasswordCommand { get; }

    private void ClearStatus() => StatusMessage = string.Empty;

    private void ChangePassword()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            StatusMessage = "Mevcut şifrenizi girin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "Yeni şifreyi girin.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = "Yeni şifre ile tekrarı eşleşmiyor.";
            return;
        }

        var (success, errorMessage) = _adminPasswordStore.SetPassword(CurrentPassword, NewPassword);
        if (!success)
        {
            StatusMessage = errorMessage ?? "Şifre güncellenemedi.";
            return;
        }

        CurrentPassword = "";
        NewPassword = "";
        ConfirmPassword = "";
        StatusMessage = "Admin şifresi güncellendi.";
    }
}
