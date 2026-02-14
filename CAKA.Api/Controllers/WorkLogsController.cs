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

        var logDate = dto.Date;
        if (logDate == default) logDate = DateTime.UtcNow.Date;
        var dateOnly = DateTime.SpecifyKind(logDate.Date, DateTimeKind.Unspecified);

        var entity = new WorkLogEntity
        {
            Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
            Date = dateOnly,
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] WorkLogDto dto)
    {
        var entity = await _db.WorkLogs.FindAsync(id);
        if (entity == null) return NotFound();
        if (!IsAdmin && entity.UserName != CurrentUserName) return Forbid();

        entity.Date = dto.Date;
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
