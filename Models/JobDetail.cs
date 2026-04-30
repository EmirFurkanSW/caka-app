namespace CAKA.PerformanceApp.Models;

/// <summary>API ile uyumlu tam iş tanımı (aşamalar, çalışan ücretleri, plan saatleri).</summary>
public class JobStageItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class JobParticipantItem
{
    public string UserName { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    /// <summary>TRY veya USD.</summary>
    public string HourlyRateCurrency { get; set; } = "TRY";
}

public class JobStagePlanItem
{
    public int StageIndex { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal PlannedHours { get; set; }
}

public class JobDetail
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<JobStageItem> Stages { get; set; } = new();
    public List<JobParticipantItem> Participants { get; set; } = new();
    public List<JobStagePlanItem> StagePlans { get; set; } = new();
}
