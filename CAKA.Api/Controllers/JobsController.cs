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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobDetailDto>> GetById(Guid id)
    {
        var detail = await MapJobToDetailDtoAsync(id);
        if (detail == null) return NotFound();
        return detail;
    }

    [HttpPost]
    [Authorize(Policy = "AdminOrYonetici")]
    public async Task<ActionResult<JobDetailDto>> Create([FromBody] JobDetailDto? dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest("İş kodu gerekli.");
        var code = dto.Code.Trim();
        if (await _db.Jobs.AnyAsync(j => j.Code == code))
            return BadRequest("Bu iş kodu zaten kayıtlı.");

        dto.Stages ??= new List<JobStageDto>();
        dto.Participants ??= new List<JobParticipantDto>();
        dto.StagePlans ??= new List<JobStagePlanDto>();

        var err = ValidatePlanning(dto);
        if (err != null) return BadRequest(err);

        JobEntity entity;
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            try
            {
                entity = new JobEntity
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    Description = (dto.Description ?? "").Trim(),
                    IsActive = dto.IsActive
                };
                _db.Jobs.Add(entity);
                await _db.SaveChangesAsync();

                await ReplaceJobChildrenAsync(entity.Id, dto);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        var created = await MapJobToDetailDtoAsync(entity.Id);
        return Ok(created!);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOrYonetici")]
    public async Task<ActionResult> Update(Guid id, [FromBody] JobDetailDto dto)
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
        entity.IsActive = dto.IsActive;

        if (dto.Stages != null)
        {
            dto.Participants ??= new List<JobParticipantDto>();
            dto.StagePlans ??= new List<JobStagePlanDto>();
            var err = ValidatePlanning(dto);
            if (err != null) return BadRequest(err);

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                await ReplaceJobChildrenAsync(id, dto);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        else
            await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOrYonetici")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var entity = await _db.Jobs.FindAsync(id);
        if (entity == null) return NotFound();
        _db.Jobs.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? ValidatePlanning(JobDetailDto dto)
    {
        var stages = dto.Stages ?? new List<JobStageDto>();
        var ordered = stages
            .Select((s, i) => (s, i))
            .OrderBy(x => x.s.SortOrder)
            .ThenBy(x => x.i)
            .Select(x => x.s)
            .ToList();

        foreach (var s in ordered)
        {
            if (string.IsNullOrWhiteSpace(s.Name))
                return "Her aşamanın adı dolu olmalıdır.";
        }

        var stageCount = ordered.Count;
        if (stageCount == 0 && (dto.StagePlans?.Count ?? 0) > 0)
            return "Aşama tanımlanmadan plan saati girilemez.";

        foreach (var p in dto.StagePlans ?? new List<JobStagePlanDto>())
        {
            if (p.StageIndex < 0 || p.StageIndex >= stageCount)
                return $"Geçersiz aşama indeksi: {p.StageIndex}.";
            if (string.IsNullOrWhiteSpace(p.UserName))
                return "Plan satırında kullanıcı adı gerekli.";
            if (p.PlannedHours < 0)
                return "Planlanan saat negatif olamaz.";
        }

        foreach (var part in dto.Participants ?? new List<JobParticipantDto>())
        {
            if (string.IsNullOrWhiteSpace(part.UserName))
                return "Çalışan satırında kullanıcı adı gerekli.";
            if (part.HourlyRate < 0)
                return "Saatlik ücret negatif olamaz.";
            var c = NormalizeCurrency(part.HourlyRateCurrency);
            if (c == null)
                return "Para birimi TRY veya USD olmalıdır.";
        }

        return null;
    }

    private static string? NormalizeCurrency(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "TRY";
        var u = raw.Trim().ToUpperInvariant();
        if (u is "TRY" or "USD")
            return u;
        return null;
    }

    private async Task ReplaceJobChildrenAsync(Guid jobId, JobDetailDto dto)
    {
        var existingStages = await _db.JobStages.Where(s => s.JobId == jobId).ToListAsync();
        _db.JobStages.RemoveRange(existingStages);

        var existingParts = await _db.JobParticipants.Where(p => p.JobId == jobId).ToListAsync();
        _db.JobParticipants.RemoveRange(existingParts);

        var stagesInput = dto.Stages ?? new List<JobStageDto>();
        var orderedInputs = stagesInput
            .Select((s, i) => (s, i))
            .OrderBy(x => x.s.SortOrder)
            .ThenBy(x => x.i)
            .Select(x => x.s)
            .ToList();

        var newStages = new List<JobStageEntity>();
        for (var i = 0; i < orderedInputs.Count; i++)
        {
            var s = orderedInputs[i];
            newStages.Add(new JobStageEntity
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Name = (s.Name ?? "").Trim(),
                Description = (s.Description ?? "").Trim(),
                SortOrder = i
            });
        }
        _db.JobStages.AddRange(newStages);

        var participants = MergeParticipants(jobId, dto.Participants);
        _db.JobParticipants.AddRange(participants);

        var stageIdsInOrder = newStages.Select(s => s.Id).ToList();
        var planRows = MergeStagePlans(stageIdsInOrder, dto.StagePlans);
        foreach (var row in planRows)
        {
            _db.JobStagePlans.Add(new JobStagePlanEntity
            {
                Id = Guid.NewGuid(),
                JobStageId = row.StageId,
                UserName = row.UserName,
                PlannedHours = row.Hours
            });
        }
    }

    private static List<JobParticipantEntity> MergeParticipants(Guid jobId, IEnumerable<JobParticipantDto>? dtos)
    {
        var map = new Dictionary<string, (decimal Rate, string Currency)>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in dtos ?? Enumerable.Empty<JobParticipantDto>())
        {
            var u = (p.UserName ?? "").Trim();
            if (string.IsNullOrEmpty(u)) continue;
            var cur = NormalizeCurrency(p.HourlyRateCurrency) ?? "TRY";
            map[u] = (p.HourlyRate, cur);
        }
        return map.Select(kv => new JobParticipantEntity
        {
            JobId = jobId,
            UserName = kv.Key,
            HourlyRate = kv.Value.Rate,
            HourlyRateCurrency = kv.Value.Currency
        }).ToList();
    }

    private static List<(Guid StageId, string UserName, decimal Hours)> MergeStagePlans(
        IReadOnlyList<Guid> stageIdsInOrder,
        IEnumerable<JobStagePlanDto>? plans)
    {
        var dict = new Dictionary<(int StageIndex, string UserName), decimal>();
        foreach (var p in plans ?? Enumerable.Empty<JobStagePlanDto>())
        {
            var u = (p.UserName ?? "").Trim();
            if (string.IsNullOrEmpty(u)) continue;
            var key = (p.StageIndex, u);
            dict[key] = dict.GetValueOrDefault(key, 0) + p.PlannedHours;
        }

        var result = new List<(Guid, string, decimal)>();
        foreach (var kv in dict)
        {
            var idx = kv.Key.StageIndex;
            if (idx < 0 || idx >= stageIdsInOrder.Count) continue;
            result.Add((stageIdsInOrder[idx], kv.Key.UserName, kv.Value));
        }
        return result;
    }

    private async Task<JobDetailDto?> MapJobToDetailDtoAsync(Guid entityId)
    {
        var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == entityId);
        if (job == null) return null;

        var stages = await _db.JobStages.AsNoTracking()
            .Where(s => s.JobId == entityId)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

        var participants = await _db.JobParticipants.AsNoTracking()
            .Where(p => p.JobId == entityId)
            .OrderBy(p => p.UserName)
            .ToListAsync();

        var stageIds = stages.Select(s => s.Id).ToList();
        var plans = await _db.JobStagePlans.AsNoTracking()
            .Where(p => stageIds.Contains(p.JobStageId))
            .ToListAsync();

        var stageIndexById = stages.Select((s, i) => (s.Id, i)).ToDictionary(t => t.Id, t => t.i);

        return new JobDetailDto
        {
            Id = job.Id,
            Code = job.Code,
            Description = job.Description,
            IsActive = job.IsActive,
            Stages = stages.Select(s => new JobStageDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                SortOrder = s.SortOrder
            }).ToList(),
            Participants = participants.Select(p => new JobParticipantDto
            {
                UserName = p.UserName,
                HourlyRate = p.HourlyRate,
                HourlyRateCurrency = string.IsNullOrWhiteSpace(p.HourlyRateCurrency) ? "TRY" : p.HourlyRateCurrency
            }).ToList(),
            StagePlans = plans
                .Where(p => stageIndexById.ContainsKey(p.JobStageId))
                .Select(p => new JobStagePlanDto
                {
                    StageIndex = stageIndexById[p.JobStageId],
                    UserName = p.UserName,
                    PlannedHours = p.PlannedHours
                })
                .OrderBy(p => p.StageIndex)
                .ThenBy(p => p.UserName)
                .ToList()
        };
    }
}
