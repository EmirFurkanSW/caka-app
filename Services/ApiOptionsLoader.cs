using System.IO;
using System.Text.Json;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// appsettings.json veya CAKA.config.json dosyasından API adresini okur.
/// EXE ile aynı klasörde veya uygulama kökünde aranır.
/// </summary>
public static class ApiOptionsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static ApiOptions Load()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
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
                var timeout = 30;
                if (root.TryGetProperty("ApiTimeoutSeconds", out var t))
                    timeout = t.TryGetInt32(out var sec) ? sec : 30;
                if (root.TryGetProperty("Api", out var a2) && a2.TryGetProperty("TimeoutSeconds", out var ts))
                    timeout = ts.TryGetInt32(out var sec2) ? sec2 : 30;

                if (!string.IsNullOrWhiteSpace(url))
                    return new ApiOptions { BaseUrl = url.TrimEnd('/'), TimeoutSeconds = timeout };
            }
            catch
            {
                // Sonraki dosyayı dene
            }
        }

        return new ApiOptions { BaseUrl = "https://localhost:5001", TimeoutSeconds = 30 };
    }
}
