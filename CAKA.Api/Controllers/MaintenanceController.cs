using CAKA.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CAKA.Api.Controllers;

/// <summary>
/// Swagger’da gizli bakım uçları. Üretimde kısa süreli sıfırlama anahtarıyla kullanın, sonra anahtarı kaldırın.
/// </summary>
[ApiController]
[Route("api/maintenance")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class MaintenanceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public MaintenanceController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Tüm kullanıcı verilerini siler; yalnızca <see cref="SeedData.AdminUserName"/> hesabını korur / oluşturur.</summary>
    [HttpPost("factory-reset")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> FactoryReset()
    {
        var secret =
            Environment.GetEnvironmentVariable("CAKA_FACTORY_RESET_SECRET")?.Trim()
            ?? _config["CAKA_FACTORY_RESET_SECRET"]?.Trim();

        if (string.IsNullOrEmpty(secret))
        {
            return NotFound(new
            {
                ok = false,
                message =
                    "Sıfırlama kapalı. Render’da Environment → CAKA_FACTORY_RESET_SECRET ayarlayın veya ilk açılışta CAKA_FACTORY_RESET=1 kullanın."
            });
        }

        var key =
            Request.Headers["X-CAKA-FACTORY-KEY"].FirstOrDefault()?.Trim()
            ?? Request.Query["key"].FirstOrDefault()?.Trim();

        if (string.IsNullOrEmpty(key))
        {
            return Unauthorized(new { ok = false, message = "X-CAKA-FACTORY-KEY başlığı veya ?key= sorgusu gerekli." });
        }

        if (!string.Equals(secret, key, StringComparison.Ordinal))
            return Unauthorized(new { ok = false, message = "Geçersiz sıfırlama anahtarı." });

        await SeedData.WipeAllUserGeneratedDataKeepingAdminAsync(_db);
        await SeedData.EnsureAdminAsync(_db);

        Console.WriteLine(
            $"[{DateTime.UtcNow:u}] api/maintenance/factory-reset: veri sıfırlandı (admin korundu).");
        return Ok(new
        {
            ok = true,
            keptAdminLogin = SeedData.AdminUserName,
            message =
                $"Tamamlandı. Yalnızca kullanıcı adı '{SeedData.AdminUserName}' kullanılabilir. Varsayılan şifreyi daha önce değiştirmediyseniz 1234 olabilir.",
            reminder =
                "Güvenlik: CAKA_FACTORY_RESET_SECRET anahtarını şimdi kaldırın (Render Environment / yerel işletim)."
        });
    }
}
