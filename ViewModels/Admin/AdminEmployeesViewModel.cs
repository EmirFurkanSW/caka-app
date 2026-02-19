using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class AdminEmployeesViewModel : ViewModelBase
{
    private string _newUserName = string.Empty;
    private string _newPassword = string.Empty;
    private string _newDisplayName = string.Empty;
    private string _newDepartment = string.Empty;
    private string _statusMessage = string.Empty;
    private StoredUser? _selectedUser;
    private bool _isEditMode;
    private string _editingUserName = string.Empty;

    public AdminEmployeesViewModel(IUserStore userStore)
    {
        _userStore = userStore;
        Users = new ObservableCollection<StoredUser>();
        RefreshCommand = new RelayCommand(_ => Refresh());
        AddUserCommand = new RelayCommand(_ => AddUser(), _ => !IsEditMode && !string.IsNullOrWhiteSpace(NewUserName) && !string.IsNullOrWhiteSpace(NewPassword));
        DeleteUserCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedUser != null);
        ToggleSuspendCommand = new RelayCommand(_ => ToggleSuspendSelected(), _ => SelectedUser != null && !IsEditMode);
        StartEditCommand = new RelayCommand(_ => StartEdit(), _ => SelectedUser != null && !IsEditMode);
        SaveEditCommand = new RelayCommand(_ => SaveEdit());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        Refresh();
    }

    private readonly IUserStore _userStore;

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

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
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

    private void Refresh()
    {
        Users.Clear();
        foreach (var u in _userStore.GetAll())
            Users.Add(u);
        StatusMessage = string.Empty;
    }

    private void AddUser()
    {
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

        _userStore.Add(new StoredUser
        {
            UserName = userName,
            Password = NewPassword,
            DisplayName = NewDisplayName.Trim(),
            Department = NewDepartment.Trim(),
            IsSuspended = false
        });
        Refresh();
        NewUserName = "";
        NewPassword = "";
        NewDisplayName = "";
        NewDepartment = "";
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
        NewPassword = "";
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
        _userStore.UpdateUserInfo(
            EditingUserName,
            NewDisplayName.Trim(),
            NewDepartment.Trim(),
            string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword);
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
    }

    /// <summary>Sayfa her açıldığında sıfırdan açılsın; seçim ve düzenleme modu temizlenir.</summary>
    public void Reset()
    {
        ClearEdit();
        SelectedUser = null;
        StatusMessage = string.Empty;
    }
}
