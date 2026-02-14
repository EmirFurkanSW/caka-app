namespace CAKA.PerformanceApp.Models;

/// <summary>
/// Son aktivite öğesi (admin dashboard için).
/// </summary>
public class ActivityItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Time { get; set; }
}
