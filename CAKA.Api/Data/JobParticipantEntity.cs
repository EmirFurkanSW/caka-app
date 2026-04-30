namespace CAKA.Api.Data;

/// <summary>İşe atanmış çalışan ve bu iş için saatlik ücret.</summary>
public class JobParticipantEntity
{
    public Guid JobId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    /// <summary>TRY veya USD.</summary>
    public string HourlyRateCurrency { get; set; } = "TRY";
}
