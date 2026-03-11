using CAKA.Api.Data;
using CAKA.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOrPersonel")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _db;

    public JobsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<JobDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var query = _db.Jobs.AsNoTracking();
        if (activeOnly)
            query = query.Where(j => j.IsActive);
        var list = await query
            .OrderBy(j => j.Code)
            .Select(j => new JobDto
            {
                Id = j.Id,
                Code = j.Code,
                Description = j.Description,
                IsActive = j.IsActive
            })
            .ToListAsync();
        return list;
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<JobDto>> Create([FromBody] JobDto? dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest("İş kodu gerekli.");
        var code = dto.Code.Trim();
        if (await _db.Jobs.AnyAsync(j => j.Code == code))
            return BadRequest("Bu iş kodu zaten kayıtlı.");
        var entity = new JobEntity
        {
            Id = Guid.NewGuid(),
            Code = code,
            Description = (dto.Description ?? "").Trim(),
            IsActive = true
        };
        _db.Jobs.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new JobDto { Id = entity.Id, Code = entity.Code, Description = entity.Description, IsActive = entity.IsActive });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Update(Guid id, [FromBody] JobDto dto)
    {
        var entity = await _db.Jobs.FindAsync(id);
        if (entity == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var code = dto.Code.Trim();
            if (code != entity.Code && await _db.Jobs.AnyAsync(j => j.Code == code))
                return BadRequest("Bu iş kodu zaten kayıtlı.");
            entity.Code = code;
        }
        entity.Description = (dto.Description ?? "").Trim();
        if (dto.IsActive != entity.IsActive)
            entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var entity = await _db.Jobs.FindAsync(id);
        if (entity == null) return NotFound();
        // İlişkili work log'ların JobId'si SetNull olacak (Description eski değeri taşır veya boş kalır)
        _db.Jobs.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
