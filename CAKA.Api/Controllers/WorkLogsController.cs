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
    private bool IsAdminOrYonetici => User.IsInRole("Admin") || User.IsInRole("Yonetici");

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
        if (!IsAdminOrYonetici || string.IsNullOrEmpty(userName))
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
                JobId = w.JobId,
                JobStageId = w.JobStageId,
                Description = w.Description,
                Hours = w.Hours,
                UserName = w.UserName,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync();
        return list;
    }

    [HttpGet("all")]
    [Authorize(Policy = "AdminOrYonetici")]
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
                JobId = w.JobId,
                JobStageId = w.JobStageId,
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
            if (!IsAdminOrYonetici && !IsDateInCurrentWeek(dateUtc))
                return BadRequest("Sadece bu haftanın iş kayıtları eklenebilir. Geçmiş veya gelecek hafta için kayıt eklenemez.");

            string displayDescription = dto.Description ?? "";
            Guid? resolvedStageId = null;
            if (dto.JobId.HasValue && dto.JobId.Value != Guid.Empty)
            {
                var job = await _db.Jobs.FindAsync(dto.JobId.Value);
                if (job == null || !job.IsActive)
                    return BadRequest("Seçilen iş bulunamadı veya artık aktif değil.");

                var stageCount = await _db.JobStages.CountAsync(s => s.JobId == job.Id);
                if (stageCount > 0)
                {
                    if (!dto.JobStageId.HasValue || dto.JobStageId.Value == Guid.Empty)
                        return BadRequest("Bu iş için aşama seçimi zorunludur.");
                    var stage = await _db.JobStages.FirstOrDefaultAsync(s => s.Id == dto.JobStageId.Value && s.JobId == job.Id);
                    if (stage == null)
                        return BadRequest("Seçilen aşama bu işe ait değil.");
                    resolvedStageId = stage.Id;
                    displayDescription = $"{job.Code} - {job.Description} · {stage.Name}";
                }
                else
                {
                    if (dto.JobStageId.HasValue && dto.JobStageId.Value != Guid.Empty)
                        return BadRequest("Bu işte aşama tanımlı değil.");
                    displayDescription = $"{job.Code} - {job.Description}";
                }
            }
            else if (!IsAdminOrYonetici)
                return BadRequest("Lütfen listeden bir iş seçin.");

            var entity = new WorkLogEntity
            {
                Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                Date = dateUtc,
                JobId = dto.JobId,
                JobStageId = resolvedStageId,
                Description = displayDescription,
                Hours = dto.Hours,
                UserName = IsAdminOrYonetici && !string.IsNullOrEmpty(dto.UserName) ? dto.UserName : current,
                CreatedAt = DateTime.UtcNow
            };
            _db.WorkLogs.Add(entity);
            await _db.SaveChangesAsync();
            return Ok(new WorkLogDto
            {
                Id = entity.Id,
                Date = entity.Date,
                JobId = entity.JobId,
                JobStageId = entity.JobStageId,
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
        if (!IsAdminOrYonetici && entity.UserName != CurrentUserName) return Forbid();

        // Personel sadece bu haftanın kayıtlarını düzenleyebilir (sunucu saati).
        if (!IsAdminOrYonetici && !IsDateInCurrentWeek(entity.Date))
            return BadRequest("Sadece bu haftanın iş kayıtları düzenlenebilir.");

        // Takvim günü aynen korunur (timezone kayması önlenir).
        var logDate = dto.Date == default ? DateTime.UtcNow : dto.Date;
        entity.Date = new DateTime(logDate.Year, logDate.Month, logDate.Day, 0, 0, 0, DateTimeKind.Utc);
        entity.Hours = dto.Hours;
        if (dto.JobId.HasValue && dto.JobId.Value != Guid.Empty)
        {
            var job = await _db.Jobs.FindAsync(dto.JobId.Value);
            if (job != null && job.IsActive)
            {
                entity.JobId = dto.JobId;
                var stageCount = await _db.JobStages.CountAsync(s => s.JobId == job.Id);
                if (stageCount > 0)
                {
                    if (!dto.JobStageId.HasValue || dto.JobStageId.Value == Guid.Empty)
                        return BadRequest("Bu iş için aşama seçimi zorunludur.");
                    var stage = await _db.JobStages.FirstOrDefaultAsync(s => s.Id == dto.JobStageId.Value && s.JobId == job.Id);
                    if (stage == null)
                        return BadRequest("Seçilen aşama bu işe ait değil.");
                    entity.JobStageId = stage.Id;
                    entity.Description = $"{job.Code} - {job.Description} · {stage.Name}";
                }
                else
                {
                    entity.JobStageId = null;
                    entity.Description = $"{job.Code} - {job.Description}";
                }
            }
        }
        else
        {
            entity.JobStageId = null;
            entity.Description = dto.Description ?? "";
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var entity = await _db.WorkLogs.FindAsync(id);
        if (entity == null) return NotFound();
        if (!IsAdminOrYonetici && entity.UserName != CurrentUserName) return Forbid();

        // Personel sadece bu haftanın kayıtlarını silebilir (sunucu saati).
        if (!IsAdminOrYonetici && !IsDateInCurrentWeek(entity.Date))
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
        if (!IsAdminOrYonetici || string.IsNullOrEmpty(userName))
            query = query.Where(w => w.UserName == current);
        else
            query = query.Where(w => w.UserName == userName);

        var total = await query.SumAsync(w => w.Hours);
        return Ok(new { TotalHours = total });
    }

    [HttpGet("totals-all")]
    [Authorize(Policy = "AdminOrYonetici")]
    public async Task<ActionResult<object>> GetTotalsAll([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var total = await _db.WorkLogs
            .Where(w => w.Date >= from && w.Date <= to)
            .SumAsync(w => w.Hours);
        return Ok(new { TotalHours = total });
    }
}
