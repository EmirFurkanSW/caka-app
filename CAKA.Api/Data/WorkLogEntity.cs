namespace CAKA.Api.Data;

public class WorkLogEntity
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
}
