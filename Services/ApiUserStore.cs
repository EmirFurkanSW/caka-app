using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Kullanıcı listesi ve CRUD işlemleri web API üzerinden.
/// </summary>
public class ApiUserStore : IUserStore
{
    private readonly BackendApiClient _api;

    public ApiUserStore(BackendApiClient api)
    {
        _api = api;
    }

    public IReadOnlyList<StoredUser> GetAll() => _api.GetUsers();

    public void Add(StoredUser user)
    {
        var (success, error) = _api.AddUser(user);
        if (!success)
            throw new InvalidOperationException(error ?? "Kullanıcı eklenemedi.");
    }

    public void Delete(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return;
        if (!_api.DeleteUser(userName.Trim()))
            throw new InvalidOperationException("Kullanıcı silinemedi.");
    }

    public void SetSuspended(string userName, bool suspended)
    {
        if (string.IsNullOrWhiteSpace(userName)) return;
        _api.SetSuspended(userName.Trim(), suspended);
    }

    public void UpdatePassword(string userName, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(userName)) return;
        var (success, _) = _api.UpdateUser(userName.Trim(), "", "", newPassword);
        if (!success)
            throw new InvalidOperationException("Şifre güncellenemedi.");
    }

    public void UpdateUserInfo(string userName, string displayName, string department, string? newPassword = null)
    {
        if (string.IsNullOrWhiteSpace(userName)) return;
        var (success, error) = _api.UpdateUser(userName.Trim(), displayName, department, newPassword);
        if (!success)
            throw new InvalidOperationException(error ?? "Bilgiler güncellenemedi.");
    }

    public bool Exists(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return false;
        return _api.GetUsers().Any(u => string.Equals(u.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
