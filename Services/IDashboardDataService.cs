using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Dashboard için özet veri servisi. İleride API/raporlama servisi ile değiştirilebilir.
/// </summary>
public interface IDashboardDataService
{
    int GetTotalEmployeeCount();
    IReadOnlyList<ActivityItem> GetRecentActivities(int count = 10);
    IReadOnlyList<(string Label, double Value)> GetWeeklyChartData();
}
