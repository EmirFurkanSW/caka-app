namespace CAKA.PerformanceApp.Models;

/// <summary>
/// Personel iş kaydı: hafta Pazartesi–Pazar; her hafta için Pazar + 3 gün (Çarşamba) ek süre.
/// </summary>
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

    /// <summary>Seçilen güne ait hafta için bugün hâlâ giriş yapılabilir mi?</summary>
    public static bool CanPersonnelEnterLogForDate(DateTime logDate, DateTime today)
    {
        var (weekStart, weekEnd) = GetWeekRange(logDate);
        var d = logDate.Date;
        if (d < weekStart || d > weekEnd)
            return false;
        return today.Date <= GetEntryDeadline(weekEnd);
    }

    /// <summary>DatePicker: önceki hafta (süre dolmadıysa) + cari hafta.</summary>
    public static (DateTime MinDate, DateTime MaxDate) GetSelectableDateRange(DateTime today)
    {
        var (curStart, curEnd) = GetWeekRange(today);
        var prevStart = curStart.AddDays(-7);
        var prevEnd = curStart.AddDays(-1);
        var min = CanPersonnelEnterLogForDate(prevStart, today) ? prevStart : curStart;
        return (min, curEnd);
    }

    /// <summary>Geçmiş sayfasında hafta düzenlenebilir / silinebilir mi?</summary>
    public static bool IsWeekEditable(DateTime weekStart, DateTime today)
    {
        var weekEnd = weekStart.AddDays(6);
        return today.Date <= GetEntryDeadline(weekEnd);
    }

    public static string FormatAllowedPeriodHint(DateTime today)
    {
        var (min, max) = GetSelectableDateRange(today);
        var deadline = GetEntryDeadline(max);
        return $"Giriş yapılabilir tarihler: {min:dd.MM.yyyy} – {max:dd.MM.yyyy} · Son teslim: {deadline:dd.MM.yyyy} (Çarşamba)";
    }

    /// <summary>DatePicker yerine hafta seçimi: düzenlenebilir haftaların Pazartesi tarihleri.</summary>
    public static IReadOnlyList<DateTime> GetSelectableWeekStarts(DateTime today)
    {
        var (curStart, _) = GetWeekRange(today);
        var prevStart = curStart.AddDays(-7);
        var list = new List<DateTime>();
        if (IsWeekEditable(prevStart, today))
            list.Add(prevStart);
        if (IsWeekEditable(curStart, today))
            list.Add(curStart);
        return list.OrderByDescending(w => w).ToList();
    }

    public static string FormatWeekLabel(DateTime weekStart) =>
        $"{weekStart:dd.MM.yyyy} – {weekStart.AddDays(6):dd.MM.yyyy}";
}
