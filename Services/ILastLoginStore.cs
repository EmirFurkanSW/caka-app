namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Başarılı giriş yapan tüm kullanıcıları saklar; liste açıldığında gösterilir, seçileni doldurur.
/// </summary>
public interface ILastLoginStore
{
    /// <summary>
    /// Kaydedilmiş tüm girişleri ve son kullanılan kullanıcı adını döner.
    /// </summary>
    (IReadOnlyList<(string UserName, string Password)> Logins, string? LastUsedUserName) GetAllLogins();

    /// <summary>
    /// Bu kullanıcıyı ekler veya şifresini günceller; son kullanılan olarak işaretler.
    /// </summary>
    void SaveLogin(string userName, string password);

    /// <summary>
    /// Kayıtlı kullanıcı adları listesinden verilen kullanıcıyı siler.
    /// </summary>
    void RemoveUserName(string userName);
}
