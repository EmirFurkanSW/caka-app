using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Haftalık raporu Excel (.xlsx) olarak oluşturur.
/// </summary>
public interface IReportExcelService
{
    void GenerateWeekReport(string filePath, DateTime weekStart, DateTime weekEnd,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName);
}
