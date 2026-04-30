namespace CAKA.PerformanceApp.Models;

/// <summary>Personel iş kaydı formunda aşama açılır listesi satırı.</summary>
public class JobStagePickItem
{
    public Guid StageId { get; set; }
    public string Label { get; set; } = string.Empty;
}
