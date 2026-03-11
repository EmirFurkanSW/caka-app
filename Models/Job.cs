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

    /// <summary>ComboBox/lista gösterim: TRCK-0064 - DemirExport</summary>
    public string DisplayText => string.IsNullOrWhiteSpace(Description) ? Code : $"{Code} - {Description}";
}
