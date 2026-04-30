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
            Role = MapApiRole(response.Role)
        };
        return true;
    }

    public void Logout()
    {
        _tokenHolder.Token = null;
        CurrentUser = null;
    }

    /// <summary>API / DB'de yazım farkı (Yönetic vs Yonetici) için.</summary>
    private static UserRole MapApiRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return UserRole.Personel;
        var r = role.Trim();
        if (string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)) return UserRole.Admin;
        if (string.Equals(r, "Yonetici", StringComparison.OrdinalIgnoreCase)) return UserRole.Yonetici;
        var folded = r.Replace('ö', 'o').Replace('Ö', 'O');
        if (string.Equals(folded, "Yonetici", StringComparison.OrdinalIgnoreCase)) return UserRole.Yonetici;
        if (string.Equals(r, "Personel", StringComparison.OrdinalIgnoreCase)) return UserRole.Personel;
        return UserRole.Personel;
    }

    public (bool Success, string? Error) ChangeMyPassword(string currentPassword, string newPassword)
    {
        return _api.ChangeMyPassword(currentPassword, newPassword);
    }
}
