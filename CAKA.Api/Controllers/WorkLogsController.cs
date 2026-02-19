using System.Security.Claims;
using CAKA.Api.Data;
using CAKA.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOrPersonel")]
public class WorkLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public WorkLogsController(AppDbContext db)
    {
        _db = db;
    }

    private string? CurrentUserName => User.FindFirstValue(ClaimTypes.Name);
    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>Sunucu saati (UTC) ile cari haftanın Pazartesi ve Pazar günlerini döner; tarih manipülasyonu engellenir.</summary>
    private static (DateTime WeekStart, DateTime WeekEnd) GetCurrentWeekUtc()
    {
        var today = DateTime.UtcNow.Date;
        var daysToMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var weekStart = today.AddDays(-daysToMonday);
        var weekEnd = weekStart.AddDays(6);
        return (weekStart, weekEnd);
    }

    private static bool IsDateInCurrentWeek(DateTime dateUtc)
    {
        var (weekStart, weekEnd) = GetCurrentWeekUtc();
        var d = dateUtc.Date;
        return d >= weekStart && d <= weekEnd;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkLogDto>>> Get([FromQuery] string? userName = null)
    {
        var current = CurrentUserName;
        if (string.IsNullOrEmpty(current)) return Unauthorized();

        IQueryable<WorkLogEntity> query = _db.WorkLogs.AsNoTracking();
        if (!IsAdmin || string.IsNullOrEmpty(userName))
            query = query.Where(w => w.UserName == current);
        else
            query = query.Where(w => w.UserName == userName);

        var list = await query
            .OrderByDescending(w => w.Date)
            .ThenByDescending(w => w.CreatedAt)
            .Select(w => new WorkLogDto
            {
                Id = w.Id,
                Date = w.Date,
                Description = w.Description,
                Hours = w.Hours,
                UserName = w.UserName,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync();
        return list;
    }

    [HttpGet("all")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<List<WorkLogDto>>> GetAll()
    {
        var list = await _db.WorkLogs
            .AsNoTracking()
            .OrderByDescending(w => w.Date)
            .ThenByDescending(w => w.CreatedAt)
            .Select(w => new WorkLogDto
            {
                Id = w.Id,
                Date = w.Date,
                Description = w.Description,
                Hours = w.Hours,
                UserName = w.UserName,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync();
        return list;
    }

    [HttpPost]
    public async Task<ActionResult<WorkLogDto>> Add([FromBody] WorkLogDto? dto)
    {
        var current = CurrentUserName;
        if (string.IsNullOrEmpty(current)) return Unauthorized();
        if (dto == null) return BadRequest("İş kaydı verisi eksik.");

        try
        {
            // Kullanıcının seçtiği takvim günü (yıl/ay/gün) aynen saklanır; timezone kayması olmaz.
            var logDate = dto.Date;
            if (logDate == default) logDate = DateTime.UtcNow;
            var dateUtc = new DateTime(logDate.Year, logDate.Month, logDate.Day, 0, 0, 0, DateTimeKind.Utc);

            // Personel sadece bu hafta (sunucu saati) için kayıt ekleyebilir; bilgisayar tarihi değiştirilse bile geçersiz.
            if (!IsAdmin && !IsDateInCurrentWeek(dateUtc))
                return BadRequest("Sadece bu haftanın iş kayıtları eklenebilir. Geçmiş veya gelecek hafta için kayıt eklenemez.");

            var entity = new WorkLogEntity
            {
                Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                Date = dateUtc,
                Description = dto.Description ?? "",
                Hours = dto.Hours,
                UserName = IsAdmin && !string.IsNullOrEmpty(dto.UserName) ? dto.UserName : current,
                CreatedAt = DateTime.UtcNow
            };
            _db.WorkLogs.Add(entity);
            await _db.SaveChangesAsync();
            return Ok(new WorkLogDto
            {
                Id = entity.Id,
                Date = entity.Date,
                Description = entity.Description,
                Hours = entity.Hours,
                UserName = entity.UserName,
                CreatedAt = entity.CreatedAt
            });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { error = "İş kaydı eklenemedi.", detail = msg });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] WorkLogDto dto)
    {
        var entity = await _db.WorkLogs.FindAsync(id);
        if (entity == null) return NotFound();
        if (!IsAdmin && entity.UserName != CurrentUserName) return Forbid();

        // Personel sadece bu haftanın kayıtlarını düzenleyebilir (sunucu saati).
        if (!IsAdmin && !IsDateInCurrentWeek(entity.Date))
            return BadRequest("Sadece bu haftanın iş kayıtları düzenlenebilir.");

        // Takvim günü aynen korunur (timezone kayması önlenir).
        var logDate = dto.Date == default ? DateTime.UtcNow : dto.Date;
        entity.Date = new DateTime(logDate.Year, logDate.Month, logDate.Day, 0, 0, 0, DateTimeKind.Utc);
        entity.Description = dto.Description ?? "";
        entity.Hours = dto.Hours;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var entity = await _db.WorkLogs.FindAsync(id);
        if (entity == null) return NotFound();
        if (!IsAdmin && entity.UserName != CurrentUserName) return Forbid();

        // Personel sadece bu haftanın kayıtlarını silebilir (sunucu saati).
        if (!IsAdmin && !IsDateInCurrentWeek(entity.Date))
            return BadRequest("Sadece bu haftanın iş kayıtları silinebilir.");

        _db.WorkLogs.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("totals")]
    public async Task<ActionResult<object>> GetTotals(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? userName = null)
    {
        var current = CurrentUserName;
        if (string.IsNullOrEmpty(current)) return Unauthorized();

        IQueryable<WorkLogEntity> query = _db.WorkLogs.Where(w => w.Date >= from && w.Date <= to);
        if (!IsAdmin || string.IsNullOrEmpty(userName))
            query = query.Where(w => w.UserName == current);
        else
            query = query.Where(w => w.UserName == userName);

        var total = await query.SumAsync(w => w.Hours);
        return Ok(new { TotalHours = total });
    }

    [HttpGet("totals-all")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<object>> GetTotalsAll([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var total = await _db.WorkLogs
            .Where(w => w.Date >= from && w.Date <= to)
            .SumAsync(w => w.Hours);
        return Ok(new { TotalHours = total });
    }
}
