namespace CAKA.Api.Data;

public class WorkLogEntity
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    /// <summary>Hangi iş (admin tanımlı). Null ise eski serbest metin kayıt.</summary>
    public Guid? JobId { get; set; }
    public JobEntity? Job { get; set; }
    /// <summary>Gösterim için: Job varsa Job.Code + Job.Description, yoksa eski serbest metin.</summary>
    public string Description { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
}
