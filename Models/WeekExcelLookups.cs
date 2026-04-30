namespace CAKA.PerformanceApp.Models;

/// <summary>
/// Haftalık Excel çıktısında iş kodu/aşama adı/saatlik ücret için API'den toplanan veri.
/// </summary>
public sealed class WeekExcelLookups
{
    /// <summary>İş listesinden doldurulur; GetJobDetail başarısız olsa bile kod/açıklama kalır.</summary>
    public Dictionary<Guid, (string Code, string Description)> JobBasics { get; } = new();

    /// <summary>İş kimliği → tam iş tanımı (aşama, katılımcı ücretleri, plan).</summary>
    public Dictionary<Guid, JobDetail> JobDetails { get; } = new();

    public (string Code, string Desc) ResolveJob(Guid jobId)
    {
        if (JobDetails.TryGetValue(jobId, out var d))
            return (d.Code ?? "?", string.IsNullOrWhiteSpace(d.Description) ? "" : d.Description);
        if (JobBasics.TryGetValue(jobId, out var b))
            return (string.IsNullOrWhiteSpace(b.Code) ? "?" : b.Code, b.Description ?? "");
        return ("?", "");
    }

    public string ResolveStage(Guid jobId, Guid? stageId)
    {
        if (!stageId.HasValue || stageId.Value == Guid.Empty)
            return "Aşamasız / genel";
        var shortId = stageId.Value.ToString("N")[..8] + "…";
        if (!JobDetails.TryGetValue(jobId, out var d))
            return $"Aşama ({shortId})";
        var st = d.Stages.FirstOrDefault(s => s.Id == stageId.Value);
        if (st == null)
            return $"Aşama ({shortId})";
        var sorted = d.Stages.OrderBy(x => x.SortOrder).ToList();
        var ix = sorted.FindIndex(x => x.Id == st.Id);
        var orderNum = ix >= 0 ? ix + 1 : (st.SortOrder > 0 ? st.SortOrder : 1);
        var label = string.IsNullOrWhiteSpace(st.Name)
            ? $"Aşama {orderNum}"
            : $"{orderNum}. {st.Name.Trim()}";
        if (!string.IsNullOrWhiteSpace(st.Description))
            label += $" — {st.Description.Trim()}";
        return label;
    }

    public decimal? ParticipantHourly(string? userName, Guid jobId, out bool isUsd)
    {
        isUsd = false;
        if (string.IsNullOrWhiteSpace(userName) || !JobDetails.TryGetValue(jobId, out var d))
            return null;
        var p = d.Participants.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.UserName) &&
            string.Equals(x.UserName.Trim(), userName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (p == null || string.IsNullOrWhiteSpace(p.UserName))
            return null;
        isUsd = string.Equals(p.HourlyRateCurrency?.Trim(), "USD", StringComparison.OrdinalIgnoreCase);
        return p.HourlyRate;
    }
}
