using System.Text.Json;
using System.Text.Json.Serialization;

namespace CAKA.Api.Json;

/// <summary>
/// İstemciden gelen "yyyy-MM-dd" string'ini DateTime olarak okur; timezone kayması olmaz.
/// </summary>
public class DateOnlyDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) return default;
        var s = reader.GetString();
        if (string.IsNullOrEmpty(s)) return default;
        if (DateTime.TryParseExact(s, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
            return date;
        if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            return parsed.Date;
        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}
