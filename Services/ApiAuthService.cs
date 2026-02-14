using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Giriş işlemini web API üzerinden yapar; token ve mevcut kullanıcıyı tutar.
/// </summary>
public class ApiAuthService : IAuthService
{
    private readonly BackendApiClient _api;
    private readonly IApiTokenHolder _tokenHolder;

    public ApiAuthService(BackendApiClient api, IApiTokenHolder tokenHolder)
    {
        _api = api;
        _tokenHolder = tokenHolder;
    }

    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null;

    public bool Login(string userName, string password)
    {
        var response = _api.Login(userName, password);
        if (response == null || !response.Success)
            return false;

        _tokenHolder.Token = response.Token;
        CurrentUser = new User
        {
            UserName = response.UserName ?? userName,
            DisplayName = string.IsNullOrWhiteSpace(response.DisplayName) ? response.UserName ?? userName : response.DisplayName,
            Department = response.Department ?? "",
            Role = response.Role == "Admin" ? UserRole.Admin : UserRole.Personel
        };
        return true;
    }

    public void Logout()
    {
        _tokenHolder.Token = null;
        CurrentUser = null;
    }

    public (bool Success, string? Error) ChangeMyPassword(string currentPassword, string newPassword)
    {
        return _api.ChangeMyPassword(currentPassword, newPassword);
    }
}
