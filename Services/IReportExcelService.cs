using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Haftalık raporu ve iş performans raporunu Excel (.xlsx) olarak oluşturur.
/// </summary>
public interface IReportExcelService
{
    void GenerateWeekReport(string filePath, DateTime weekStart, DateTime weekEnd,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName);

    /// <summary>Seçilen iş için çalışan bazlı profesyonel performans raporu.</summary>
    void GenerateJobPerformanceReport(string filePath, string jobCode, string jobDescription,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName,
        IReadOnlyDictionary<string, decimal> hourlyRateByUser,
        decimal patronTargetHoursPerEmployee);
}
