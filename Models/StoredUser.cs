namespace CAKA.PerformanceApp.Models;

/// <summary>
/// Admin tarafından oluşturulan, sistemde saklanan kullanıcı (personel).
/// </summary>
public class StoredUser
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    /// <summary>Saatlik ücret (TRY).</summary>
    public decimal HourlyRate { get; set; }
    public bool IsSuspended { get; set; }

    /// <summary>Personel | Yonetici (API ile uyumlu).</summary>
    public string Role { get; set; } = "Personel";

    /// <summary>Arayüzde gösterim (Personel / Yönetici).</summary>
    public string RoleLabel => Role == "Yonetici" ? "Yönetici" : "Personel";
}
