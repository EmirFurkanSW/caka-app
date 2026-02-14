namespace CAKA.PerformanceApp.Models;

/// <summary>
/// Grafik çubuğu için etiket ve değer (Dashboard haftalık saat dağılımı).
/// </summary>
public class ChartBarItem
{
    public string Label { get; init; } = string.Empty;
    public double Value { get; init; }
}
