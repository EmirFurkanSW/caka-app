using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Admin tarafından oluşturulan kullanıcıların saklanması. Dosya tabanlı; ileride veritabanı ile değiştirilebilir.
/// </summary>
public interface IUserStore
{
    IReadOnlyList<StoredUser> GetAll();
    void Add(StoredUser user);
    void Delete(string userName);
    void SetSuspended(string userName, bool suspended);
    void UpdatePassword(string userName, string newPassword);
    /// <param name="role">Yalnızca ana admin güncelleyebilir; API tarafında doğrulanır.</param>
    void UpdateUserInfo(string userName, string displayName, string department, decimal hourlyRate, string? newPassword = null, string? role = null);
    bool Exists(string userName);
}
