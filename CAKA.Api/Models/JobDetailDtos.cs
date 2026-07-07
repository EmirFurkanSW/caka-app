namespace CAKA.Api.Models;

public class JobStageDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class JobParticipantDto
{
    public string UserName { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    /// <summary>TRY veya USD.</summary>
    public string HourlyRateCurrency { get; set; } = "TRY";
}

/// <summary>
/// Plan: çalışan + aşama indeksi (Stages SortOrder sıralı listede 0 tabanlı) + planlanan saat.
/// </summary>
public class JobStagePlanDto
{
    /// <summary>Veritabanındaki aşama kimliği (rapor eşlemesi için).</summary>
    public Guid? StageId { get; set; }
    /// <summary>Stages listesi SortOrder'a göre sıralandıktan sonraki indeks.</summary>
    public int StageIndex { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal PlannedHours { get; set; }
}

/// <summary>Tam iş tanımı: temel alanlar + aşamalar + çalışan ücretleri + aşama bazlı plan saatleri.</summary>
public class JobDetailDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? ProjectManagerUserName { get; set; }
    public List<JobStageDto> Stages { get; set; } = new();
    public List<JobParticipantDto> Participants { get; set; } = new();
    public List<JobStagePlanDto> StagePlans { get; set; } = new();
}
