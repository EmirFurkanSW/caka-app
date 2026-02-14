using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Personel;

public class PersonelProfileViewModel : ViewModelBase
{
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = string.Empty;

    public PersonelProfileViewModel(IAuthService authService)
    {
        _authService = authService;
        UserName = authService.CurrentUser?.UserName ?? "";
        DisplayName = authService.CurrentUser?.DisplayName ?? "";
        Department = authService.CurrentUser?.Department ?? "";
        IsPersonel = authService.CurrentUser?.Role == UserRole.Personel;
        ChangePasswordCommand = new RelayCommand(_ => ChangePassword());
    }

    private readonly IAuthService _authService;

    public string UserName { get; }
    public string DisplayName { get; }
    public string Department { get; }
    public bool IsPersonel { get; }

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
        if (!IsPersonel)
        {
            StatusMessage = "Admin şifresi Ayarlar ekranından değiştirilir.";
            return;
        }

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

        var (success, error) = _authService.ChangeMyPassword(CurrentPassword, NewPassword);
        if (!success)
        {
            StatusMessage = error ?? "Şifre güncellenemedi.";
            return;
        }

        CurrentPassword = "";
        NewPassword = "";
        ConfirmPassword = "";
        StatusMessage = "Şifreniz güncellendi.";
    }
}
