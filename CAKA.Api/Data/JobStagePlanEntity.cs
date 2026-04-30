namespace CAKA.Api.Data;

/// <summary>Çalışanın belirli bir aşamada planlanan saati.</summary>
public class JobStagePlanEntity
{
    public Guid Id { get; set; }
    public Guid JobStageId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal PlannedHours { get; set; }
}
