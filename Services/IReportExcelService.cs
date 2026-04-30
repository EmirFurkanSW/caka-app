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

    /// <summary>
    /// Seçilen iş için maliyet matrisi. <paramref name="columnUserNames"/> dolu ise sütunlar bu sırayla (ör. sistemdeki tüm çalışanlar);
    /// boşsa yalnızca kaydı olan kullanıcılar listelenir. Kayıtsız ihracatta iş kimliği için <paramref name="explicitJobId"/> verin.
    /// </summary>
    void GenerateJobPerformanceReport(string filePath, string jobCode, string jobDescription,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName,
        JobDetail? jobDetail = null,
        IReadOnlyList<string>? columnUserNames = null,
        Guid? explicitJobId = null);
}
