using System.IO;
using System.Text.Json;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// API adresini okur: önce AppData\CAKA\CAKA.config.json, sonra EXE yanındaki config, yoksa gömülü varsayılan kullanılır.
/// Config dosyası zorunlu değildir; sadece EXE dağıtılabilir.
/// </summary>
public static class ApiOptionsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Config dosyası yoksa kullanılan varsayılan API adresi (EXE içine gömülü).</summary>
    private const string DefaultBaseUrl = "https://caka-api.onrender.com";
    private const int DefaultTimeoutSeconds = 30;

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
                var json = File.ReadAllText(path);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var url = root.TryGetProperty("ApiBaseUrl", out var u) ? u.GetString()
                    : root.TryGetProperty("Api", out var a) && a.TryGetProperty("BaseUrl", out var bu) ? bu.GetString()
                    : null;
                var timeout = DefaultTimeoutSeconds;
                if (root.TryGetProperty("ApiTimeoutSeconds", out var t))
                    timeout = t.TryGetInt32(out var sec) ? sec : DefaultTimeoutSeconds;
                if (root.TryGetProperty("Api", out var a2) && a2.TryGetProperty("TimeoutSeconds", out var ts))
                    timeout = ts.TryGetInt32(out var sec2) ? sec2 : DefaultTimeoutSeconds;

                if (!string.IsNullOrWhiteSpace(url))
                    return new ApiOptions { BaseUrl = url.TrimEnd('/'), TimeoutSeconds = timeout };
            }
            catch
            {
                // Sonraki dosyayı dene
            }
        }

        return new ApiOptions { BaseUrl = DefaultBaseUrl, TimeoutSeconds = DefaultTimeoutSeconds };
    }
}
