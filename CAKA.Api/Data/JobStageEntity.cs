namespace CAKA.Api.Data;

/// <summary>İşe bağlı aşama (stage).</summary>
public class JobStageEntity
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
