using System.Text.Json.Serialization;
using CAKA.Api.Json;

namespace CAKA.Api.Models;

public class WorkLogDto
{
    public Guid Id { get; set; }

    [JsonConverter(typeof(DateOnlyDateTimeConverter))]
    public DateTime Date { get; set; }
    /// <summary>Hangi iş (admin tanımlı). Yeni kayıtlarda zorunlu.</summary>
    public Guid? JobId { get; set; }
    /// <summary>Gösterim: iş kodu + açıklama veya eski serbest metin.</summary>
    public string Description { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
}
