namespace CAKA.PerformanceApp.Models;

/// <summary>
/// Kullanıcı rolleri. İleride veritabanı/API ile genişletilebilir.
/// </summary>
public enum UserRole
{
    Admin,
    /// <summary>Patron paneli: kullanıcı oluşturamaz; diğer yönetim işlevleri açık.</summary>
    Yonetici,
    Personel
}
