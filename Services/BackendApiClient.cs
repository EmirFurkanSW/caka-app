using System.Net;
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
            var req = new
            {
                UserName = SecurityConstants.Truncate(userName, SecurityConstants.MaxUserNameLength),
                Password = SecurityConstants.Truncate(password, SecurityConstants.MaxPasswordLength)
            };
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
            var req = new
            {
                CurrentPassword = SecurityConstants.Truncate(currentPassword, SecurityConstants.MaxPasswordLength),
                NewPassword = SecurityConstants.Truncate(newPassword, SecurityConstants.MaxPasswordLength)
            };
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
            var req = new
            {
                CurrentPassword = SecurityConstants.Truncate(currentPassword, SecurityConstants.MaxPasswordLength),
                NewPassword = SecurityConstants.Truncate(newPassword, SecurityConstants.MaxPasswordLength)
            };
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

    /// <summary>Aktif işler (personel dropdown) veya tümü (admin yönetimi).</summary>
    public IReadOnlyList<Job> GetJobs(bool activeOnly = false)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var url = activeOnly ? "api/jobs?activeOnly=true" : "api/jobs";
            var res = await _http.GetAsync(url).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException(FormatApiError(res, json));
            var list = JsonSerializer.Deserialize<List<Job>>(json, JsonOptions);
            return list ?? new List<Job>();
        }) ?? new List<Job>();
    }

    /// <summary>Tek işin aşama / çalışan / plan detayı.</summary>
    public JobDetail? GetJobDetail(Guid id)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var res = await _http.GetAsync($"api/jobs/{id}").ConfigureAwait(false);
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException(FormatApiError(res, json));
            return JsonSerializer.Deserialize<JobDetail>(json, JsonOptions);
        });
    }

    public (bool Success, string? Error) AddJob(Job job)
    {
        try
        {
            CallAsync(async () =>
            {
                SetBearer();
                var dto = new { job.Code, job.Description };
                var body = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
                var res = await _http.PostAsync("api/jobs", body).ConfigureAwait(false);
                var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                    throw new InvalidOperationException(FormatApiError(res, json));
                var created = JsonSerializer.Deserialize<Job>(json, JsonOptions);
                if (created != null) { job.Id = created.Id; job.IsActive = created.IsActive; }
            });
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Aşamalar, çalışan ücretleri ve plan saatleri ile iş oluşturur.</summary>
    public (bool Success, string? Error) AddJobDetail(JobDetail detail)
    {
        try
        {
            CallAsync(async () =>
            {
                SetBearer();
                var body = new StringContent(JsonSerializer.Serialize(detail, JsonOptions), Encoding.UTF8, "application/json");
                var res = await _http.PostAsync("api/jobs", body).ConfigureAwait(false);
                var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                    throw new InvalidOperationException(FormatApiError(res, json));
                var created = JsonSerializer.Deserialize<JobDetail>(json, JsonOptions);
                if (created != null)
                    detail.Id = created.Id;
            });
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Success, string? Error) UpdateJob(Job job)
    {
        try
        {
            CallAsync(async () =>
            {
                SetBearer();
                var dto = new { job.Code, job.Description, job.IsActive };
                var body = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
                var res = await _http.PutAsync($"api/jobs/{job.Id}", body).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(FormatApiError(res, json));
                }
            });
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Aşamalar ve planları da günceller (tam gövde).</summary>
    public (bool Success, string? Error) UpdateJobDetail(JobDetail detail)
    {
        try
        {
            CallAsync(async () =>
            {
                SetBearer();
                var body = new StringContent(JsonSerializer.Serialize(detail, JsonOptions), Encoding.UTF8, "application/json");
                var res = await _http.PutAsync($"api/jobs/{detail.Id}", body).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(FormatApiError(res, json));
                }
            });
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public bool DeleteJob(Guid id)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var res = await _http.DeleteAsync($"api/jobs/{id}").ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        });
    }

    public (bool Success, string? Error) AddUser(StoredUser user)
    {
        var dto = new
        {
            UserName = SecurityConstants.Truncate(user.UserName, SecurityConstants.MaxUserNameLength),
            Password = SecurityConstants.Truncate(user.Password, SecurityConstants.MaxPasswordLength),
            DisplayName = SecurityConstants.Truncate(user.DisplayName, SecurityConstants.MaxDisplayNameLength),
            Department = SecurityConstants.Truncate(user.Department, SecurityConstants.MaxDepartmentLength),
            user.HourlyRate,
            user.IsSuspended,
            Role = string.IsNullOrWhiteSpace(user.Role) ? "Personel" : user.Role.Trim()
        };
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

    public (bool Success, string? Error) UpdateUser(string userName, string displayName, string department, decimal hourlyRate, string? newPassword, string? role = null)
    {
        var dto = new
        {
            UserName = SecurityConstants.Truncate(userName, SecurityConstants.MaxUserNameLength),
            Password = SecurityConstants.Truncate(newPassword ?? "", SecurityConstants.MaxPasswordLength),
            DisplayName = SecurityConstants.Truncate(displayName, SecurityConstants.MaxDisplayNameLength),
            Department = SecurityConstants.Truncate(department, SecurityConstants.MaxDepartmentLength),
            HourlyRate = hourlyRate < 0 ? 0 : hourlyRate,
            IsSuspended = false,
            Role = role
        };
        var resp = CallAsync(async () =>
        {
            SetBearer();
            var body = new StringContent(JsonSerializer.Serialize(dto, JsonOptions), Encoding.UTF8, "application/json");
            var res = await _http.PutAsync($"api/users/{Uri.EscapeDataString(userName)}", body).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var err = TryGetErrorMessage(json)
                          ?? (json.Length > 200 ? json[..200] + "…" : json)
                          ?? res.ReasonPhrase
                          ?? $"HTTP {(int)res.StatusCode}";
                return new LoginResponseDto { Success = false, Error = err };
            }

            return JsonSerializer.Deserialize<LoginResponseDto>(json, JsonOptions)
                   ?? new LoginResponseDto { Success = true };
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
            var dto = new
            {
                workLog.Id,
                Date = workLog.Date.ToString("yyyy-MM-dd"),
                JobId = workLog.JobId,
                JobStageId = workLog.JobStageId,
                Description = SecurityConstants.Truncate(workLog.Description, SecurityConstants.MaxDescriptionLength),
                workLog.Hours,
                UserName = SecurityConstants.Truncate(workLog.UserName, SecurityConstants.MaxUserNameLength)
            };
            var body = new StringContent(JsonSerializer.Serialize(dto, JsonOptions), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("api/worklogs", body).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var errBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                var msg = TryGetErrorMessage(errBody) ?? errBody ?? "Sunucu hatası.";
                throw new InvalidOperationException(msg);
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
            var dto = new
            {
                Date = workLog.Date.ToString("yyyy-MM-dd"),
                JobId = workLog.JobId,
                JobStageId = workLog.JobStageId,
                Description = SecurityConstants.Truncate(workLog.Description, SecurityConstants.MaxDescriptionLength),
                workLog.Hours
            };
            var body = new StringContent(JsonSerializer.Serialize(dto, JsonOptions), Encoding.UTF8, "application/json");
            var res = await _http.PutAsync($"api/worklogs/{workLog.Id}", body).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                var msg = TryGetErrorMessage(err) ?? res.ReasonPhrase ?? "Güncelleme reddedildi.";
                throw new InvalidOperationException(msg);
            }
            return true;
        });
    }

    public bool DeleteWorkLog(Guid id)
    {
        return CallAsync(async () =>
        {
            SetBearer();
            var res = await _http.DeleteAsync($"api/worklogs/{id}").ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                var msg = TryGetErrorMessage(err) ?? res.ReasonPhrase ?? "Silme reddedildi.";
                throw new InvalidOperationException(msg);
            }
            return true;
        });
    }

    private static string? TryGetErrorMessage(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var e))
            {
                if (e.ValueKind == JsonValueKind.String) return e.GetString();
            }
            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                var t = title.GetString();
                if (doc.RootElement.TryGetProperty("detail", out var det) && det.ValueKind == JsonValueKind.String)
                {
                    var d = det.GetString();
                    if (!string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(d)) return $"{t}: {d}";
                }
                if (!string.IsNullOrEmpty(t)) return t;
            }
            if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString();
            if (doc.RootElement.TryGetProperty("detail", out var d2) && d2.ValueKind == JsonValueKind.String) return d2.GetString();
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            parts.Add($"{prop.Name}: {item.GetString()}");
                    }
                }
                if (parts.Count > 0) return string.Join("; ", parts);
            }
            if (doc.RootElement.ValueKind == JsonValueKind.String) return doc.RootElement.GetString();
        }
        catch { }
        return null;
    }

    private static string FormatApiError(HttpResponseMessage response, string body)
    {
        var code = (int)response.StatusCode;
        var summary = TryGetErrorMessage(body);
        if (string.IsNullOrWhiteSpace(summary) && !string.IsNullOrWhiteSpace(body))
        {
            var t = body.Trim();
            if (t.Length > 420) t = t[..420] + "…";
            summary = t;
        }
        if (string.IsNullOrWhiteSpace(summary))
            summary = response.ReasonPhrase ?? "Bilinmeyen hata";

        var hint = code switch
        {
            (int)HttpStatusCode.Unauthorized => " Oturum süresi dolmuş olabilir; çıkış yapıp yeniden giriş yapın.",
            (int)HttpStatusCode.Forbidden => " Bu işlem için Admin veya Yönetici hesabı gerekir.",
            (int)HttpStatusCode.NotFound => " API uç noktası bulunamadı; CAKA.config.json içindeki ApiBaseUrl adresini kontrol edin.",
            (int)HttpStatusCode.BadGateway or (int)HttpStatusCode.ServiceUnavailable or 504 =>
                " Sunucu geçici olarak yanıt vermiyor (ör. Render uyku / yoğunluk). Bir dakika sonra yenileyin.",
            _ => ""
        };
        return $"{code} — {summary}{hint}";
    }

    public decimal GetTotalHoursForUser(string? userName, DateTime from, DateTime to)
    {
        var url = "api/worklogs/totals?from=" + Uri.EscapeDataString(from.ToString("yyyy-MM-dd"))
                  + "&to=" + Uri.EscapeDataString(to.ToString("yyyy-MM-dd"));
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
        var url = "api/worklogs/totals-all?from=" + Uri.EscapeDataString(from.ToString("yyyy-MM-dd"))
                  + "&to=" + Uri.EscapeDataString(to.ToString("yyyy-MM-dd"));
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
            var m = ex.Message ?? "";
            var is403 = m.Contains("403", StringComparison.Ordinal) || m.Contains("Forbidden", StringComparison.OrdinalIgnoreCase);
            if (is403)
                throw new InvalidOperationException(
                    "Yetki hatası (403). Çıkış yapıp yeniden giriş yapın (yeni oturum JWT’si gerekir). Sunucu güncel kodu dağıttıysanız bu genelde düzelir. " + m, ex);
            throw new InvalidOperationException("Sunucuya bağlanılamadı. API adresini ve internet bağlantınızı kontrol edin. " + m, ex);
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
