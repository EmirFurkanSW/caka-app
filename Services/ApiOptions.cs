namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Web API bağlantı ayarları. appsettings.json veya CAKA.config.json ile yapılandırılır.
/// </summary>
public class ApiOptions
{
    /// <summary>
    /// API base URL (örn: https://localhost:5001 veya https://your-server.com/caka-api). Sonunda / olmamalı.
    /// </summary>
    public string BaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>
    /// İstek zaman aşımı (saniye).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
