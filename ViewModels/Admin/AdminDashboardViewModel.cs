using System.Collections.ObjectModel;
using System.Linq;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class AdminDashboardViewModel : ViewModelBase
{
    public AdminDashboardViewModel(IWorkLogService workLogService, IUserStore userStore)
    {
        _workLogService = workLogService;
        _userStore = userStore;
        ChartData = new ObservableCollection<ChartBarItem>();
        RecentActivities = new ObservableCollection<ActivityItem>();
        Refresh();
    }

    private readonly IWorkLogService _workLogService;
    private readonly IUserStore _userStore;

    private int _totalEmployees;
    private decimal _weeklyTotalHours;

    public int TotalEmployees
    {
        get => _totalEmployees;
        private set => SetProperty(ref _totalEmployees, value);
    }

    public decimal WeeklyTotalHours
    {
        get => _weeklyTotalHours;
        private set => SetProperty(ref _weeklyTotalHours, value);
    }

    public ObservableCollection<ChartBarItem> ChartData { get; }
    public ObservableCollection<ActivityItem> RecentActivities { get; }

    public void Refresh()
    {
        var users = _userStore.GetAll();
        TotalEmployees = users.Count;

        var today = DateTime.Today;
        var daysToMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var weekStart = today.AddDays(-daysToMonday);
        var weekEnd = weekStart.AddDays(6);

        WeeklyTotalHours = _workLogService.GetTotalHoursAll(weekStart, weekEnd);

        ChartData.Clear();
        foreach (var u in users.OrderByDescending(u => _workLogService.GetTotalHoursForUser(u.UserName, weekStart, weekEnd)))
            ChartData.Add(new ChartBarItem
            {
                Label = string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName,
                Value = (double)_workLogService.GetTotalHoursForUser(u.UserName, weekStart, weekEnd)
            });

        RecentActivities.Clear();
        foreach (var log in _workLogService.GetAll()
            .OrderByDescending(w => w.CreatedAt)
            .Take(10))
        {
            var desc = string.IsNullOrEmpty(log.Description) ? log.UserName ?? "" : $"{log.Description} ({log.UserName})";
            RecentActivities.Add(new ActivityItem
            {
                Title = "İş kaydı",
                Description = $"{desc} · {log.Hours:N1} sa",
                Time = log.CreatedAt
            });
        }
    }
}
