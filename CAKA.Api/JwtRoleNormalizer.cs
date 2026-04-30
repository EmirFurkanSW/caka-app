using System.Linq;
using System.Security.Claims;

namespace CAKA.Api;

/// <summary>
/// Veritabanında veya arayüzde "Yönetici" (ö ile) yazılsa bile JWT ve [Authorize] politikalarında
/// tutarlı <see cref="Yonetici"/> (ASCII) kullanılır.
/// JwtBearer'in <see cref="RoleClaimType"/> ayarıyla sınırlı kalmayıp tüm yaygın rol claim türleri okunur
/// (<c>role</c>, <see cref="ClaimTypes.Role"/> vb.) böylece 403 kaybolur.
/// </summary>
public static class JwtRoleNormalizer
{
    public const string Admin = "Admin";
    public const string Yonetici = "Yonetici";
    public const string Personel = "Personel";

    public static bool IsRoleClaimType(string? claimType)
    {
        if (string.IsNullOrEmpty(claimType)) return false;
        if (claimType == ClaimTypes.Role) return true;
        if (string.Equals(claimType, "role", StringComparison.Ordinal)) return true;
        return claimType.EndsWith("/role", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Principal üzerinden ham rol stringlerini politikaya göre sıkıştırır (tekilleştirmez).</summary>
    public static IEnumerable<string> EnumerateNormalizedRoles(ClaimsPrincipal? user)
    {
        if (user?.Claims == null)
            yield break;
        foreach (var c in user.Claims.Where(c => IsRoleClaimType(c.Type)))
        {
            var n = ToPolicyRole(c.Value);
            yield return n;
        }
    }

    public static bool HasAdmin(ClaimsPrincipal? user) =>
        EnumerateNormalizedRoles(user).Any(r => string.Equals(r, Admin, StringComparison.Ordinal));

    public static bool HasAdminOrYonetici(ClaimsPrincipal? user) =>
        EnumerateNormalizedRoles(user).Any(r =>
            string.Equals(r, Admin, StringComparison.Ordinal) ||
            string.Equals(r, Yonetici, StringComparison.Ordinal));

    public static bool HasAdminPersonelOrYonetici(ClaimsPrincipal? user) =>
        EnumerateNormalizedRoles(user).Any(r =>
            string.Equals(r, Admin, StringComparison.Ordinal) ||
            string.Equals(r, Personel, StringComparison.Ordinal) ||
            string.Equals(r, Yonetici, StringComparison.Ordinal));

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
