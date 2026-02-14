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
    void UpdateUserInfo(string userName, string displayName, string department, string? newPassword = null);
    bool Exists(string userName);
}
