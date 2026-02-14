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
}
