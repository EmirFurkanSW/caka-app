using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Haftalık raporu PDF olarak oluşturur.
/// </summary>
public interface IReportPdfService
{
    /// <summary>
    /// Seçilen haftanın tüm çalışan ve iş kayıtlarını tablo formatında PDF'e yazar.
    /// </summary>
    /// <param name="filePath">Kaydedilecek dosya yolu</param>
    /// <param name="weekStart">Hafta başı (Pazartesi)</param>
    /// <param name="weekEnd">Hafta sonu (Pazar)</param>
    /// <param name="entries">O haftaya ait tüm iş kayıtları</param>
    /// <param name="userNameToDisplayName">Kullanıcı adı -> Görünen ad eşlemesi</param>
    void GenerateWeekReport(string filePath, DateTime weekStart, DateTime weekEnd,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName);
}
