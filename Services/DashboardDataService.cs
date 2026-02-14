using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Mock dashboard verisi. İleride gerçek veri kaynağına bağlanabilir.
/// </summary>
public class DashboardDataService : IDashboardDataService
{
    public int GetTotalEmployeeCount() => 24;

    public IReadOnlyList<ActivityItem> GetRecentActivities(int count = 10)
    {
        var items = new List<ActivityItem>
        {
            new() { Title = "Yeni iş kaydı", Description = "Ahmet Y. - Proje X", Time = DateTime.Now.AddMinutes(-15) },
            new() { Title = "Rapor onaylandı", Description = "Haftalık rapor", Time = DateTime.Now.AddHours(-1) },
            new() { Title = "İş kaydı güncellendi", Description = "Ayşe K.", Time = DateTime.Now.AddHours(-2) },
            new() { Title = "Yeni iş kaydı", Description = "Mehmet D. - Analiz", Time = DateTime.Now.AddHours(-3) },
            new() { Title = "Sistem girişi", Description = "Personel paneli", Time = DateTime.Now.AddHours(-4) }
        };
        return items.Take(count).ToList();
    }

    public IReadOnlyList<(string Label, double Value)> GetWeeklyChartData()
    {
        return new List<(string, double)>
        {
            ("Pzt", 32),
            ("Sal", 28),
            ("Çar", 35),
            ("Per", 40),
            ("Cum", 38),
            ("Cmt", 12),
            ("Paz", 8)
        };
    }
}
