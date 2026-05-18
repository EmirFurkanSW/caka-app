namespace CAKA.PerformanceApp.Models;

/// <summary>İş listesinde her zaman görünen özel kayıt türleri (veritabanında iş tanımı yok).</summary>
public static class WorkLogSpecialJobs
{
    public static readonly Guid OfficeTripJobId = new("c4a8e210-7f3b-4d2a-9e61-0f1a2b3c4d5e");

  public const string OfficeTripDisplayText = "Ofis Dışı Gezi";

  public static bool IsOfficeTrip(Job? job) =>
      job != null && job.Id == OfficeTripJobId;

  public static bool IsOfficeTripDescription(string? description) =>
      string.Equals(description?.Trim(), OfficeTripDisplayText, StringComparison.OrdinalIgnoreCase);

  public static Job CreateOfficeTripJob() => new()
  {
      Id = OfficeTripJobId,
      Code = "ODY",
      Description = "Ofis Dışı Gezi",
      IsActive = true
  };
}
