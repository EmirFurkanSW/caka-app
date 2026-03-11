namespace CAKA.PerformanceApp.Models;

/// <summary>
/// İş kaydı (personel tarafından girilen). İleride veritabanı entity'si ile eşleştirilebilir.
/// </summary>
public class WorkLog
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    /// <summary>Seçilen iş (admin tanımlı). Null ise eski serbest metin kayıt.</summary>
    public Guid? JobId { get; set; }
    /// <summary>Gösterim metni: iş kodu - açıklama veya eski açıklama.</summary>
    public string Description { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
}
