namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Admin şifresi web API üzerinden güncellenir (mevcut şifre doğrulanır).
/// </summary>
public class ApiAdminPasswordStore : IAdminPasswordStore
{
    private readonly BackendApiClient _api;

    public ApiAdminPasswordStore(BackendApiClient api)
    {
        _api = api;
    }

    public (bool Success, string? ErrorMessage) SetPassword(string currentPassword, string newPassword)
    {
        var response = _api.ChangeAdminPassword(currentPassword, newPassword);
        return (response?.Success ?? false, response?.Error);
    }
}
