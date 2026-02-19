using System.IO;
using System.Text.Json;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// API adresini okur: önce AppData\CAKA\CAKA.config.json, sonra EXE yanındaki config, yoksa gömülü varsayılan kullanılır.
/// Config dosyası zorunlu değildir; sadece EXE dağıtılabilir.
/// Güvenlik: Sadece HTTPS veya localhost HTTP kabul edilir; config dosya boyutu sınırlıdır.
/// </summary>
public static class ApiOptionsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Config dosyası yoksa kullanılan varsayılan API adresi (EXE içine gömülü).</summary>
    private const string DefaultBaseUrl = "https://caka-api.onrender.com";
    private const int DefaultTimeoutSeconds = 30;
    /// <summary>Config dosyası maksimum boyut (bayt) - büyük dosya ile DoS önlenir.</summary>
    private const int MaxConfigFileSize = 64 * 1024;
    private const int MinTimeoutSeconds = 5;
    private const int MaxTimeoutSeconds = 300;

    /// <summary>Güvenlik: Sadece HTTPS veya localhost/127.0.0.1 HTTP kabul edilir; başka http adresleri kabul edilmez.</summary>
    public static bool IsAllowedApiBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var u = url.TrimEnd('/');
        if (u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return true;
        if (u.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ||
            u.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    public static ApiOptions Load()
    {
        var baseDir = AppContext.BaseDirectory;
        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAKA");
        var candidates = new[]
        {
            Path.Combine(appDataDir, "CAKA.config.json"),
            Path.Combine(baseDir, "CAKA.config.json"),
            Path.Combine(baseDir, "appsettings.json"),
            Path.Combine(baseDir, "appsettings.Production.json")
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var fi = new FileInfo(path);
                if (fi.Length > MaxConfigFileSize || fi.Length <= 0) continue;
                var json = File.ReadAllText(path);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var url = root.TryGetProperty("ApiBaseUrl", out var u) ? u.GetString()
                    : root.TryGetProperty("Api", out var a) && a.TryGetProperty("BaseUrl", out var bu) ? bu.GetString()
                    : null;
                var timeout = DefaultTimeoutSeconds;
                if (root.TryGetProperty("ApiTimeoutSeconds", out var t))
                    timeout = t.TryGetInt32(out var sec) ? Math.Clamp(sec, MinTimeoutSeconds, MaxTimeoutSeconds) : DefaultTimeoutSeconds;
                if (root.TryGetProperty("Api", out var a2) && a2.TryGetProperty("TimeoutSeconds", out var ts))
                    timeout = ts.TryGetInt32(out var sec2) ? Math.Clamp(sec2, MinTimeoutSeconds, MaxTimeoutSeconds) : timeout;

                if (!string.IsNullOrWhiteSpace(url))
                {
                    var trimmed = url.TrimEnd('/');
                    if (IsAllowedApiBaseUrl(trimmed))
                        return new ApiOptions { BaseUrl = trimmed, TimeoutSeconds = timeout };
                }
            }
            catch
            {
                // Sonraki dosyayı dene
            }
        }

        return new ApiOptions { BaseUrl = DefaultBaseUrl, TimeoutSeconds = DefaultTimeoutSeconds };
    }
}
