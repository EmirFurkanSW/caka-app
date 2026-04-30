namespace CAKA.Api.Models;

public class StoredUserDto
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public bool IsSuspended { get; set; }

    /// <summary>Yeni kullanıcı veya (sadece ana admin) güncellemede: Personel | Yonetici. PUT'ta null ise rol değişmez.</summary>
    public string? Role { get; set; }
}
