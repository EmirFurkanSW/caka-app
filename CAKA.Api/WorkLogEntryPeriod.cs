namespace CAKA.Api;

/// <summary>Personel iş kaydı: hafta bitiminden sonra 3 gün (Çarşamba) ek süre.</summary>
public static class WorkLogEntryPeriod
{
    public const int GraceDaysAfterWeekEnd = 3;

    public static (DateTime WeekStart, DateTime WeekEnd) GetWeekRange(DateTime date)
    {
        var d = date.Date;
        var daysToMonday = d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1;
        var weekStart = d.AddDays(-daysToMonday);
        return (weekStart, weekStart.AddDays(6));
    }

    public static DateTime GetEntryDeadline(DateTime weekEnd) =>
        weekEnd.Date.AddDays(GraceDaysAfterWeekEnd);

    public static bool CanPersonnelEnterLogForDate(DateTime logDateUtc, DateTime todayUtc)
    {
        var (weekStart, weekEnd) = GetWeekRange(logDateUtc);
        var d = logDateUtc.Date;
        if (d < weekStart || d > weekEnd)
            return false;
        return todayUtc.Date <= GetEntryDeadline(weekEnd);
    }
}
