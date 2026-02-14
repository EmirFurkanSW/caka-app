using System.Collections.ObjectModel;
using System.Linq;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Personel;

public class PersonelDashboardViewModel : ViewModelBase
{
    private decimal _weeklyTotalHours;

    public PersonelDashboardViewModel(IAuthService authService, IWorkLogService workLogService)
    {
        _authService = authService;
        _workLogService = workLogService;
        RecentWorkLogs = new ObservableCollection<WorkLog>();
        Refresh();
    }

    private readonly IAuthService _authService;
    private readonly IWorkLogService _workLogService;

    public decimal WeeklyTotalHours
    {
        get => _weeklyTotalHours;
        private set => SetProperty(ref _weeklyTotalHours, value);
    }

    public ObservableCollection<WorkLog> RecentWorkLogs { get; }

    /// <summary>Haftalık toplam ve son işleri yeniler (Geçmiş/İş Ekle sonrası Dashboard'a dönünce güncel veri görünsün).</summary>
    public void Refresh()
    {
        var userName = _authService.CurrentUser?.UserName;
        var today = DateTime.Today;
        var daysToMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var weekStart = today.AddDays(-daysToMonday);
        var weekEnd = weekStart.AddDays(6);
        WeeklyTotalHours = _workLogService.GetTotalHoursForUser(userName, weekStart, weekEnd);
        RecentWorkLogs.Clear();
        foreach (var log in _workLogService.GetByUser(userName)
            .OrderByDescending(w => w.CreatedAt)
            .Take(10))
            RecentWorkLogs.Add(log);
    }
}
