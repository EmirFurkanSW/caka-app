namespace CAKA.Api.Data;

/// <summary>
/// Admin tarafından tanımlanan iş (iş kodu + açıklama). Çalışanlar bu işlerden seçip saat girer.
/// </summary>
public class JobEntity
{
    public Guid Id { get; set; }
    /// <summary>İş kodu, örn. TRCK-0064</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Açıklama, örn. DemirExport</summary>
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
