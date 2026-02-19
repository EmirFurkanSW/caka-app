namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Güvenlik ve girdi doğrulama sabitleri. Aşırı uzun girdiler API/sunucu yükü ve olası açıkları sınırlar.
/// </summary>
public static class SecurityConstants
{
    public const int MaxDescriptionLength = 2000;
    public const int MaxUserNameLength = 100;
    public const int MaxDisplayNameLength = 200;
    public const int MaxDepartmentLength = 200;
    public const int MaxPasswordLength = 256;

    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
