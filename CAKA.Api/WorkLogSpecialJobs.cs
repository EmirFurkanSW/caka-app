namespace CAKA.Api;

public static class WorkLogSpecialJobs
{
    public const string OfficeTripDescription = "Ofis Dışı Gezi";

    public static bool IsOfficeTripDescription(string? description) =>
        string.Equals(description?.Trim(), OfficeTripDescription, StringComparison.OrdinalIgnoreCase);
}
