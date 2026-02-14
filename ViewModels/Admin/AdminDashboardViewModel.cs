using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
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
    }

    private readonly IWorkLogService _workLogService;
    private readonly IUserStore _userStore;

    private int _totalEmployees;
    private decimal _weeklyTotalHours;
    private bool _isLoading;

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

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<ChartBarItem> ChartData { get; }
    public ObservableCollection<ActivityItem> RecentActivities { get; }

    /// <summary>Veriyi arka planda yükler; UI hemen yanıt verir, veri gelince güncellenir.</summary>
    public void RefreshAsync()
    {
        var dispatcher = Application.Current.Dispatcher;
        IsLoading = true;

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var users = _userStore.GetAll();
                var today = DateTime.Today;
                var daysToMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
                var weekStart = today.AddDays(-daysToMonday);
                var weekEnd = weekStart.AddDays(6);

                var totalEmployees = users.Count;
                var weeklyTotalHours = _workLogService.GetTotalHoursAll(weekStart, weekEnd);
                var chartItems = users
                    .OrderByDescending(u => _workLogService.GetTotalHoursForUser(u.UserName, weekStart, weekEnd))
                    .Select(u => new ChartBarItem
                    {
                        Label = string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName,
                        Value = (double)_workLogService.GetTotalHoursForUser(u.UserName, weekStart, weekEnd)
                    })
                    .ToList();
                var activities = _workLogService.GetAll()
                    .OrderByDescending(w => w.CreatedAt)
                    .Take(10)
                    .Select(log =>
                    {
                        var desc = string.IsNullOrEmpty(log.Description) ? log.UserName ?? "" : $"{log.Description} ({log.UserName})";
                        return new ActivityItem
                        {
                            Title = "İş kaydı",
                            Description = $"{desc} · {log.Hours:N1} sa",
                            Time = log.CreatedAt
                        };
                    })
                    .ToList();

                dispatcher.InvokeAsync(() =>
                {
                    TotalEmployees = totalEmployees;
                    WeeklyTotalHours = weeklyTotalHours;
                    ChartData.Clear();
                    foreach (var item in chartItems)
                        ChartData.Add(item);
                    RecentActivities.Clear();
                    foreach (var item in activities)
                        RecentActivities.Add(item);
                }, DispatcherPriority.Normal);
            }
            finally
            {
                dispatcher.InvokeAsync(() => IsLoading = false, DispatcherPriority.Normal);
            }
        });
    }

    /// <summary>Senkron yenileme (bloklar); mümkünse RefreshAsync kullanın.</summary>
    public void Refresh()
    {
        RefreshAsync();
    }
}
