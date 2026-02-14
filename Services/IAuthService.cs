using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Kimlik doğrulama servisi arayüzü.
/// Şu an in-memory; ileride API/veritabanı ile değiştirilebilir.
/// </summary>
public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    bool Login(string userName, string password);
    void Logout();
    /// <summary>Giriş yapmış kullanıcının kendi şifresini değiştirmesi (API üzerinden).</summary>
    (bool Success, string? Error) ChangeMyPassword(string currentPassword, string newPassword);
}
