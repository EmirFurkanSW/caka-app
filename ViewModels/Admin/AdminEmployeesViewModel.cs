using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class AdminEmployeesViewModel : ViewModelBase, INavigationRefresh
{
    /// <summary>Arayüzdeki rol metnini API değerine çevirir (Yönetici / Yonetici karışıklığına dayanıklı).</summary>
    private static string MapUiRoleToApi(string? uiChoice)
    {
        if (string.IsNullOrWhiteSpace(uiChoice)) return "Personel";
        var t = uiChoice.Trim();
        if (string.Equals(t, "Yonetici", StringComparison.OrdinalIgnoreCase)) return "Yonetici";
        if (string.Equals(t, "Yönetici", StringComparison.OrdinalIgnoreCase)) return "Yonetici";
        return "Personel";
    }

    private string _newUserName = string.Empty;
    private string _newPassword = string.Empty;
    private string _newDisplayName = string.Empty;
    private string _newDepartment = string.Empty;
    private string _newHourlyRate = "0";
    private string _newUserRole = "Personel";
    private string _editingRole = "Personel";
    private string _statusMessage = string.Empty;
    private StoredUser? _selectedUser;
    private bool _isEditMode;
    private string _editingUserName = string.Empty;

    public AdminEmployeesViewModel(IUserStore userStore, IAuthService authService)
    {
        _userStore = userStore;
        _authService = authService;
        Users = new ObservableCollection<StoredUser>();
        RefreshCommand = new RelayCommand(_ => Refresh());
        AddUserCommand = new RelayCommand(_ => AddUser(),
            _ => CanCreateUsers && !IsEditMode && !string.IsNullOrWhiteSpace(NewUserName) && !string.IsNullOrWhiteSpace(NewPassword));
        DeleteUserCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedUser != null);
        ToggleSuspendCommand = new RelayCommand(_ => ToggleSuspendSelected(), _ => SelectedUser != null && !IsEditMode);
        StartEditCommand = new RelayCommand(_ => StartEdit(), _ => SelectedUser != null && !IsEditMode);
        SaveEditCommand = new RelayCommand(_ => SaveEdit());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
    }

    private readonly IUserStore _userStore;
    private readonly IAuthService _authService;

    /// <summary>Yalnız ana admin (Admin) yeni kullanıcı oluşturabilir.</summary>
    public bool CanCreateUsers => _authService.CurrentUser?.Role == UserRole.Admin;

    /// <summary>Rol atamasını yalnız ana admin değiştirebilir.</summary>
    public bool CanChangeUserRoles => _authService.CurrentUser?.Role == UserRole.Admin;

    public IReadOnlyList<string> RoleChoices { get; } = new[] { "Personel", "Yönetici" };

    public bool ShowBrowseOnlyNotice => !CanCreateUsers && !IsEditMode;

    public bool ShowEmployeeFormCard => CanCreateUsers || IsEditMode;

    public bool ShowNewUserRoleCombo => CanCreateUsers && !IsEditMode;

    public bool ShowEditRoleCombo => IsEditMode && CanChangeUserRoles;

    public ObservableCollection<StoredUser> Users { get; }

    public StoredUser? SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    public string NewUserName
    {
        get => _newUserName;
        set { if (SetProperty(ref _newUserName, value ?? "")) ClearStatus(); }
    }

    public string NewPassword
    {
        get => _newPassword;
        set { if (SetProperty(ref _newPassword, value ?? "")) ClearStatus(); }
    }

    public string NewDisplayName
    {
        get => _newDisplayName;
        set { if (SetProperty(ref _newDisplayName, value ?? "")) ClearStatus(); }
    }

    public string NewDepartment
    {
        get => _newDepartment;
        set { if (SetProperty(ref _newDepartment, value ?? "")) ClearStatus(); }
    }

    public string NewHourlyRate
    {
        get => _newHourlyRate;
        set { if (SetProperty(ref _newHourlyRate, value ?? "")) ClearStatus(); }
    }

    public string NewUserRole
    {
        get => _newUserRole;
        set => SetProperty(ref _newUserRole, string.IsNullOrWhiteSpace(value) ? "Personel" : value);
    }

    public string EditingRole
    {
        get => _editingRole;
        set => SetProperty(ref _editingRole, string.IsNullOrWhiteSpace(value) ? "Personel" : value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(ShowBrowseOnlyNotice));
                OnPropertyChanged(nameof(ShowEmployeeFormCard));
                OnPropertyChanged(nameof(ShowNewUserRoleCombo));
                OnPropertyChanged(nameof(ShowEditRoleCombo));
                ((RelayCommand)AddUserCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string EditingUserName
    {
        get => _editingUserName;
        set => SetProperty(ref _editingUserName, value ?? "");
    }

    public ICommand RefreshCommand { get; }
    public ICommand AddUserCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand ToggleSuspendCommand { get; }
    public ICommand StartEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }

    private void ClearStatus() => StatusMessage = string.Empty;

    /// <summary>API listesini yükler. Hata oluşursa yakalanır — giriş sırasında pencere açılışı çökmez.</summary>
    public void Refresh()
    {
        Users.Clear();
        try
        {
            foreach (var u in _userStore.GetAll())
                Users.Add(u);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = "Kullanıcı listesi alınamadı: " + ex.Message;
        }
    }

    private void AddUser()
    {
        if (!CanCreateUsers)
            return;

        var userName = NewUserName.Trim();
        if (string.IsNullOrEmpty(userName))
        {
            StatusMessage = "Kullanıcı adı girin.";
            return;
        }
        if (_userStore.Exists(userName))
        {
            StatusMessage = "Bu kullanıcı adı zaten kayıtlı. Farklı bir kullanıcı adı girin.";
            MessageBox.Show(
                $"'{userName}' kullanıcı adı zaten sistemde kayıtlı.\nFarklı bir kullanıcı adı girin.",
                "Kullanıcı adı kullanımda",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "Şifre girin.";
            return;
        }
        if (userName.Length > SecurityConstants.MaxUserNameLength)
        {
            StatusMessage = $"Kullanıcı adı en fazla {SecurityConstants.MaxUserNameLength} karakter olabilir.";
            return;
        }
        if (NewDisplayName.Trim().Length > SecurityConstants.MaxDisplayNameLength)
        {
            StatusMessage = $"Ad soyad en fazla {SecurityConstants.MaxDisplayNameLength} karakter olabilir.";
            return;
        }
        if (NewDepartment.Trim().Length > SecurityConstants.MaxDepartmentLength)
        {
            StatusMessage = $"Departman en fazla {SecurityConstants.MaxDepartmentLength} karakter olabilir.";
            return;
        }

        if (!decimal.TryParse((NewHourlyRate ?? "").Trim().Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var hourlyRate))
        {
            StatusMessage = "Saatlik ücret için geçerli bir sayı girin (örn: 250 veya 250,5).";
            return;
        }
        if (hourlyRate < 0)
        {
            StatusMessage = "Saatlik ücret negatif olamaz.";
            return;
        }

        _userStore.Add(new StoredUser
        {
            UserName = userName,
            Password = NewPassword,
            DisplayName = NewDisplayName.Trim(),
            Department = NewDepartment.Trim(),
            HourlyRate = hourlyRate,
            IsSuspended = false,
            Role = MapUiRoleToApi(NewUserRole)
        });
        Refresh();
        NewUserName = "";
        NewPassword = "";
        NewDisplayName = "";
        NewDepartment = "";
        NewHourlyRate = "0";
        NewUserRole = "Personel";
        StatusMessage = "Kullanıcı eklendi.";
    }

    private void DeleteSelected()
    {
        if (SelectedUser == null) return;
        if (MessageBox.Show(
                $"'{SelectedUser.UserName}' kullanıcısını silmek istediğinize emin misiniz?",
                "Kullanıcı Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _userStore.Delete(SelectedUser.UserName);
        Refresh();
        StatusMessage = "Kullanıcı silindi.";
    }

    private void ToggleSuspendSelected()
    {
        if (SelectedUser == null) return;
        var newState = !SelectedUser.IsSuspended;
        var action = newState ? "askıya almak" : "tekrar aktifleştirmek";
        if (MessageBox.Show(
                $"'{SelectedUser.UserName}' kullanıcısını {action} istiyor musunuz?",
                newState ? "Askıya Al" : "Aktifleştir",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _userStore.SetSuspended(SelectedUser.UserName, newState);
        Refresh();
        StatusMessage = newState ? "Kullanıcı askıya alındı." : "Kullanıcı aktifleştirildi.";
    }

    private void StartEdit()
    {
        if (SelectedUser == null) return;
        EditingUserName = SelectedUser.UserName;
        NewUserName = SelectedUser.UserName;
        NewDisplayName = SelectedUser.DisplayName;
        NewDepartment = SelectedUser.Department;
        NewHourlyRate = SelectedUser.HourlyRate.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        NewPassword = "";
        EditingRole = SelectedUser.Role == "Yonetici" ? "Yönetici" : "Personel";
        IsEditMode = true;
        StatusMessage = "";
    }

    private void SaveEdit()
    {
        if (string.IsNullOrWhiteSpace(EditingUserName)) return;
        if (NewDisplayName.Trim().Length > SecurityConstants.MaxDisplayNameLength)
        {
            StatusMessage = $"Ad soyad en fazla {SecurityConstants.MaxDisplayNameLength} karakter olabilir.";
            return;
        }
        if (NewDepartment.Trim().Length > SecurityConstants.MaxDepartmentLength)
        {
            StatusMessage = $"Departman en fazla {SecurityConstants.MaxDepartmentLength} karakter olabilir.";
            return;
        }
        if (!decimal.TryParse((NewHourlyRate ?? "").Trim().Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var hourlyRate))
        {
            StatusMessage = "Saatlik ücret için geçerli bir sayı girin (örn: 250 veya 250,5).";
            return;
        }
        if (hourlyRate < 0)
        {
            StatusMessage = "Saatlik ücret negatif olamaz.";
            return;
        }
        var roleApi = CanChangeUserRoles ? MapUiRoleToApi(EditingRole) : null;

        _userStore.UpdateUserInfo(
            EditingUserName,
            NewDisplayName.Trim(),
            NewDepartment.Trim(),
            hourlyRate,
            string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword,
            roleApi);
        ClearEdit();
        Refresh();
        StatusMessage = "Kullanıcı bilgileri güncellendi.";
    }

    private void CancelEdit()
    {
        ClearEdit();
        StatusMessage = "";
    }

    private void ClearEdit()
    {
        IsEditMode = false;
        EditingUserName = "";
        NewUserName = "";
        NewPassword = "";
        NewDisplayName = "";
        NewDepartment = "";
        NewHourlyRate = "0";
        NewUserRole = "Personel";
        EditingRole = "Personel";
    }

    /// <summary>Sayfa her açıldığında sıfırdan açılsın; seçim ve düzenleme modu temizlenir.</summary>
    public void Reset()
    {
        ClearEdit();
        SelectedUser = null;
        StatusMessage = string.Empty;
    }

    public void RefreshOnNavigate()
    {
        Reset();
        Refresh();
    }
}
