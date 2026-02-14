using System.IO;
using System.Text.Json;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Sadece son kullanılan kullanıcı adını saklar (şifre asla saklanmaz - güvenlik).
/// Yerel dosya: LocalApplicationData\CAKA\lastuser.json
/// </summary>
public class LastLoginStore : ILastLoginStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public LastLoginStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CAKA");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "lastuser.json");
    }

    public (IReadOnlyList<(string UserName, string Password)> Logins, string? LastUsedUserName) GetAllLogins()
    {
        // Şifre saklanmaz; sadece son kullanıcı adı döner, liste boş.
        if (!File.Exists(_filePath))
            return (new List<(string, string)>(), null);
        try
        {
            var json = File.ReadAllText(_filePath);
            var doc = JsonDocument.Parse(json);
            var lastUsed = doc.RootElement.TryGetProperty("LastUsedUserName", out var lu) ? lu.GetString() : null;
            return (new List<(string, string)>(), lastUsed);
        }
        catch
        {
            return (new List<(string, string)>(), null);
        }
    }

    public void SaveLogin(string userName, string password)
    {
        // Sadece son kullanıcı adı yazılır; şifre yazılmaz.
        if (string.IsNullOrWhiteSpace(userName)) return;
        try
        {
            var obj = new { LastUsedUserName = userName.Trim() };
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Kaydetme hatasında sessizce devam et
        }
    }
}
