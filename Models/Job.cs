namespace CAKA.PerformanceApp.Models;

/// <summary>
/// Admin tarafından tanımlanan iş (iş kodu + açıklama). Çalışanlar listeden seçip saat girer.
/// </summary>
public class Job
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    /// <summary>Proje müdürü kullanıcı adı.</summary>
    public string? ProjectManagerUserName { get; set; }
    /// <summary>Liste gösterimi için doldurulur.</summary>
    public string ProjectManagerDisplay { get; set; } = string.Empty;

    /// <summary>ComboBox/lista gösterim: TRCK-0064 - DemirExport</summary>
    public string DisplayText => WorkLogSpecialJobs.IsOfficeTrip(this)
        ? WorkLogSpecialJobs.OfficeTripDisplayText
        : string.IsNullOrWhiteSpace(Description) ? Code : $"{Code} - {Description}";

    /// <summary>Admin listesinde durum: Aktif / Kapatıldı</summary>
    public string StatusText => IsActive ? "Aktif" : "Kapatıldı";
}
