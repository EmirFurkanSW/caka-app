namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Admin şifresinin güncellenmesi (web API üzerinden).
/// </summary>
public interface IAdminPasswordStore
{
    /// <summary>
    /// Mevcut şifreyi doğrular ve yeni şifreyi kaydeder.
    /// </summary>
    (bool Success, string? ErrorMessage) SetPassword(string currentPassword, string newPassword);
}
