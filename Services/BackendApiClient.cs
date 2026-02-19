using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Models.Api;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Web API ile iletişim. Tüm çağrılar senkron sarmalayıcı kullanır (mevcut ViewModel arayüzüyle uyum için).
/// </summary>
public class BackendApiClient
{
    private readonly HttpClient _http;
    private readonly IApiTokenHolder _tokenHolder;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BackendApiClient(HttpClient http, IApiTokenHolder tokenHolder)
    {
        _http = http;
        _tokenHolder = tokenHolder;
    }

    private void SetBearer()
    {
        var t = _tokenHolder.Token;
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(t)
            ? null
            : new AuthenticationHeaderValue("Bearer", t);
    }

    private static string EnsureEnd(string baseUrl) => baseUrl?.TrimEnd('/') ?? "";

    public void Configure(string baseUrl, int timeoutSeconds = 30)
    {
        _http.BaseAddress = new Uri(EnsureEnd(baseUrl) + "/");
        _http.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public LoginResponseDto? Login(string userName, string password)
    {
        return CallAsync(async () =>
        {
            var req = new { UserName = userName, Password = password };
            var body = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("api/auth/login", body).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<LoginResponseDto>(json, JsonOptions);
        });
    }

    public LoginResponseDto? ChangeAdminPassword(string currentPassword, string newPassword)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var req = new { CurrentPassword = currentPassword, NewPassword = newPassword };
            var body = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("api/auth/change-admin-password", body).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<LoginResponseDto>(json, JsonOptions);
        });
    }

    /// <summary>Giriş yapmış kullanıcının kendi şifresini değiştirmesi (admin veya personel).</summary>
    public (bool Success, string? Error) ChangeMyPassword(string currentPassword, string newPassword)
    {
        var resp = CallAsync(async () =>
        {
            SetBearer();
            var req = new { CurrentPassword = currentPassword, NewPassword = newPassword };
            var body = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("api/auth/change-my-password", body).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<LoginResponseDto>(json, JsonOptions);
        });
        return (resp?.Success ?? false, resp?.Error);
    }

    public IReadOnlyList<StoredUser> GetUsers()
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var res = await _http.GetAsync("api/users").ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<StoredUser>>(json, JsonOptions);
            return list ?? new List<StoredUser>();
        }) ?? new List<StoredUser>();
    }

    public (bool Success, string? Error) AddUser(StoredUser user)
    {
        var dto = new { user.UserName, user.Password, user.DisplayName, user.Department, user.IsSuspended };
        var resp = CallAsync(async () =>
        {
            SetBearer();
            var body = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("api/users", body).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<LoginResponseDto>(json, JsonOptions);
        });
        return (resp?.Success ?? false, resp?.Error);
    }

    public bool DeleteUser(string userName)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var res = await _http.DeleteAsync($"api/users/{Uri.EscapeDataString(userName)}").ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        });
    }

    public bool SetSuspended(string userName, bool suspended)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var body = new StringContent(JsonSerializer.Serialize(suspended), Encoding.UTF8, "application/json");
            var res = await _http.PutAsync($"api/users/{Uri.EscapeDataString(userName)}/suspended", body).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        });
    }

    public (bool Success, string? Error) UpdateUser(string userName, string displayName, string department, string? newPassword)
    {
        var dto = new { UserName = userName, Password = newPassword ?? "", DisplayName = displayName, Department = department, IsSuspended = false };
        var resp = CallAsync(async () =>
        {
            SetBearer();
            var body = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
            var res = await _http.PutAsync($"api/users/{Uri.EscapeDataString(userName)}", body).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<LoginResponseDto>(json, JsonOptions);
        });
        return (resp?.Success ?? false, resp?.Error);
    }

    public IReadOnlyList<WorkLog> GetWorkLogs(string? userName = null)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var url = string.IsNullOrEmpty(userName) ? "api/worklogs" : "api/worklogs?userName=" + Uri.EscapeDataString(userName);
            var res = await _http.GetAsync(url).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<WorkLog>>(json, JsonOptions);
            return list ?? new List<WorkLog>();
        }) ?? new List<WorkLog>();
    }

    public IReadOnlyList<WorkLog> GetAllWorkLogs()
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var res = await _http.GetAsync("api/worklogs/all").ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<WorkLog>>(json, JsonOptions);
            return list ?? new List<WorkLog>();
        }) ?? new List<WorkLog>();
    }

    public void AddWorkLog(WorkLog workLog)
    {
        CallAsync(async () =>
        {
            SetBearer();
            // Tarihi sadece gün olarak (yyyy-MM-dd) gonderiyoruz; timezone kayması olmaz.
            var dto = new { workLog.Id, Date = workLog.Date.ToString("yyyy-MM-dd"), workLog.Description, workLog.Hours, workLog.UserName };
            var body = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("api/worklogs", body).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var errBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException("Sunucu hatası: " + errBody);
            }
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var created = JsonSerializer.Deserialize<WorkLog>(json, JsonOptions);
            if (created != null)
            {
                workLog.Id = created.Id;
                workLog.CreatedAt = created.CreatedAt;
            }
        });
    }

    public bool UpdateWorkLog(WorkLog workLog)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var dto = new { Date = workLog.Date.ToString("yyyy-MM-dd"), workLog.Description, workLog.Hours };
            var body = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
            var res = await _http.PutAsync($"api/worklogs/{workLog.Id}", body).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        });
    }

    public bool DeleteWorkLog(Guid id)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var res = await _http.DeleteAsync($"api/worklogs/{id}").ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        });
    }

    public decimal GetTotalHoursForUser(string? userName, DateTime from, DateTime to)
    {
        var url = "api/worklogs/totals?from=" + Uri.EscapeDataString(from.ToString("O")) + "&to=" + Uri.EscapeDataString(to.ToString("O"));
        if (!string.IsNullOrEmpty(userName))
            url += "&userName=" + Uri.EscapeDataString(userName);
        var v = CallAsync(async () =>
        {
            SetBearer();
            var res = await _http.GetAsync(url).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var obj = JsonSerializer.Deserialize<TotalsResponseDto>(json, JsonOptions);
            return obj?.TotalHours ?? 0m;
        });
        return v;
    }

    public decimal GetTotalHoursAll(DateTime from, DateTime to)
    {
        var url = "api/worklogs/totals-all?from=" + Uri.EscapeDataString(from.ToString("O")) + "&to=" + Uri.EscapeDataString(to.ToString("O"));
        var v = CallAsync(async () =>
        {
            SetBearer();
            var res = await _http.GetAsync(url).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var obj = JsonSerializer.Deserialize<TotalsResponseDto>(json, JsonOptions);
            return obj?.TotalHours ?? 0m;
        });
        return v;
    }

    private static T? CallAsync<T>(Func<Task<T>> fn)
    {
        try
        {
            return fn().GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Sunucuya bağlanılamadı. API adresini ve internet bağlantınızı kontrol edin. " + ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new InvalidOperationException("İstek zaman aşımına uğradı.", ex);
        }
    }

    private static void CallAsync(Func<Task> fn)
    {
        CallAsync(async () => { await fn().ConfigureAwait(false); return 0; });
    }
}
