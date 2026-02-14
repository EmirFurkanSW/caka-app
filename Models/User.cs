namespace CAKA.PerformanceApp.Models;

/// <summary>
/// Giriş yapan kullanıcı bilgisi. İleride gerçek entity ile değiştirilebilir.
/// </summary>
public class User
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
