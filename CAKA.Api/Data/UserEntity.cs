namespace CAKA.Api.Data;

public class UserEntity
{
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool IsSuspended { get; set; }
    public string Role { get; set; } = "Personel"; // Admin | Personel
}
