using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Bu bilgisayarda daha önce giriş yapmış tüm kullanıcı adlarını saklar (şifre asla saklanmaz).
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
        if (!File.Exists(_filePath))
            return (new List<(string, string)>(), null);
        try
        {
            var json = File.ReadAllText(_filePath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var lastUsed = root.TryGetProperty("LastUsedUserName", out var lu) ? lu.GetString() : null;
            var userNames = new List<(string, string)>();
            if (root.TryGetProperty("UserNames", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var u = item.GetString();
                    if (!string.IsNullOrWhiteSpace(u))
                        userNames.Add((u.Trim(), "")); // Şifre saklanmaz
                }
            }
            return (userNames, lastUsed);
        }
        catch
        {
            return (new List<(string, string)>(), null);
        }
    }

    public void SaveLogin(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName)) return;
        try
        {
            var (logins, _) = GetAllLogins();
            var list = logins.Select(x => x.UserName).ToList();
            var trimmed = userName.Trim();
            if (!list.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                list.Add(trimmed);
            var obj = new { LastUsedUserName = trimmed, UserNames = list };
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Kaydetme hatasında sessizce devam et
        }
    }

    public void RemoveUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return;
        try
        {
            var (logins, lastUsed) = GetAllLogins();
            var list = logins.Select(x => x.UserName)
                .Where(u => !string.Equals(u, userName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            var obj = new { LastUsedUserName = lastUsed == userName.Trim() ? list.FirstOrDefault() : lastUsed, UserNames = list };
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Silme hatasında sessizce devam et
        }
    }
}
