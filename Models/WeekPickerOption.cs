namespace CAKA.PerformanceApp.Models;

public class WeekPickerOption
{
    public DateTime WeekStart { get; init; }
    public string Label => WorkLogEntryPeriod.FormatWeekLabel(WeekStart);
}
