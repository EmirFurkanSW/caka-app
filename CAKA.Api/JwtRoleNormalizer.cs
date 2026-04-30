namespace CAKA.Api;

/// <summary>
/// Veritabanında veya arayüzde "Yönetici" (ö ile) yazılsa bile JWT ve [Authorize] politikalarında
/// tutarlı <see cref="Yonetici"/> (ASCII) kullanılır; aksi halde RequireRole ile 403 oluşur.
/// </summary>
public static class JwtRoleNormalizer
{
    public const string Admin = "Admin";
    public const string Yonetici = "Yonetici";
    public const string Personel = "Personel";

    public static string ToPolicyRole(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Personel;
        var r = raw.Trim();
        if (string.Equals(r, Admin, StringComparison.OrdinalIgnoreCase))
            return Admin;
        if (string.Equals(r, Personel, StringComparison.OrdinalIgnoreCase))
            return Personel;
        if (LooksLikeYonetici(r))
            return Yonetici;
        return Personel;
    }

    /// <summary>Admin kontrolleri için (ham DB rolü).</summary>
    public static bool IsAdminRole(string? raw) =>
        string.Equals(ToPolicyRole(raw), Admin, StringComparison.Ordinal);

    private static bool LooksLikeYonetici(string r)
    {
        if (string.Equals(r, Yonetici, StringComparison.OrdinalIgnoreCase))
            return true;
        var folded = r
            .Replace('ö', 'o')
            .Replace('Ö', 'O');
        return string.Equals(folded, Yonetici, StringComparison.OrdinalIgnoreCase);
    }
}
