using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Haftalık raporu ve iş performans raporunu Excel (.xlsx) olarak oluşturur.
/// </summary>
public interface IReportExcelService
{
    void GenerateWeekReport(string filePath, DateTime weekStart, DateTime weekEnd,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName,
        WeekExcelLookups? lookups = null);

    /// <summary>Seçilen iş için çalışan bazlı performans raporu. İsteğe bağlı <paramref name="jobDetail"/> ile aşama/plan/ücret bilgisi ikinci sayfada ve saatlik ücrette otomatik doldurulur.</summary>
    void GenerateJobPerformanceReport(string filePath, string jobCode, string jobDescription,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName,
        JobDetail? jobDetail = null);
}
