using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class AdminDashboardViewModel : ViewModelBase, INavigationRefresh
{
    public AdminDashboardViewModel(IWorkLogService workLogService, IUserStore userStore, BackendApiClient api)
    {
        _workLogService = workLogService;
        _userStore = userStore;
        _api = api;
        ChartData = new ObservableCollection<ChartBarItem>();
        JobChartData = new ObservableCollection<ChartBarItem>();
        RecentActivities = new ObservableCollection<ActivityItem>();

        var (weekStart, weekEnd) = GetCurrentWeekRange();
        _personnelFilterFrom = weekStart;
        _personnelFilterTo = weekEnd;

        var (monthStart, monthEnd) = GetCurrentMonthRange();
        _jobFilterFrom = monthStart;
        _jobFilterTo = monthEnd;

        ApplyPersonnelFilterCommand = new RelayCommand(_ => RefreshPersonnelChartAsync());
        ApplyJobFilterCommand = new RelayCommand(_ => RefreshJobChartAsync());
        ResetPersonnelFilterCommand = new RelayCommand(_ =>
        {
            var (ws, we) = GetCurrentWeekRange();
            PersonnelFilterFrom = ws;
            PersonnelFilterTo = we;
            RefreshPersonnelChartAsync();
        });
        ResetJobFilterCommand = new RelayCommand(_ =>
        {
            var (ms, me) = GetCurrentMonthRange();
            JobFilterFrom = ms;
            JobFilterTo = me;
            RefreshJobChartAsync();
        });
    }

    private readonly IWorkLogService _workLogService;
    private readonly IUserStore _userStore;
    private readonly BackendApiClient _api;

    private int _totalEmployees;
    private decimal _periodTotalHours;
    private bool _isLoading;
    private DateTime _personnelFilterFrom;
    private DateTime _personnelFilterTo;
    private DateTime _jobFilterFrom;
    private DateTime _jobFilterTo;
    private string _personnelFilterSummary = "";
    private string _jobFilterSummary = "";

    public const string PersonnelChartTitle = "Personel Çalışma Saatleri";
    public const string JobChartTitle = "İş Bazında Çalışma Saatleri";
    public const string PeriodTotalHoursLabel = "Seçili dönem toplam saat";

    public int TotalEmployees
    {
        get => _totalEmployees;
        private set => SetProperty(ref _totalEmployees, value);
    }

    public decimal PeriodTotalHours
    {
        get => _periodTotalHours;
        private set => SetProperty(ref _periodTotalHours, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public DateTime PersonnelFilterFrom
    {
        get => _personnelFilterFrom;
        set => SetProperty(ref _personnelFilterFrom, value.Date);
    }

    public DateTime PersonnelFilterTo
    {
        get => _personnelFilterTo;
        set => SetProperty(ref _personnelFilterTo, value.Date);
    }

    public DateTime JobFilterFrom
    {
        get => _jobFilterFrom;
        set => SetProperty(ref _jobFilterFrom, value.Date);
    }

    public DateTime JobFilterTo
    {
        get => _jobFilterTo;
        set => SetProperty(ref _jobFilterTo, value.Date);
    }

    public string PersonnelFilterSummary
    {
        get => _personnelFilterSummary;
        private set => SetProperty(ref _personnelFilterSummary, value);
    }

    public string JobFilterSummary
    {
        get => _jobFilterSummary;
        private set => SetProperty(ref _jobFilterSummary, value);
    }

    public ObservableCollection<ChartBarItem> ChartData { get; }
    public ObservableCollection<ChartBarItem> JobChartData { get; }
    public ObservableCollection<ActivityItem> RecentActivities { get; }

    public ICommand ApplyPersonnelFilterCommand { get; }
    public ICommand ApplyJobFilterCommand { get; }
    public ICommand ResetPersonnelFilterCommand { get; }
    public ICommand ResetJobFilterCommand { get; }

    public void RefreshAsync()
    {
        var dispatcher = Application.Current.Dispatcher;
        var pFrom = PersonnelFilterFrom;
        var pTo = PersonnelFilterTo;
        var jFrom = JobFilterFrom;
        var jTo = JobFilterTo;
        IsLoading = true;

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var users = _userStore.GetAll();
                var totalEmployees = users.Count;
                var allLogs = _workLogService.GetAll();

                var activities = allLogs
                    .OrderByDescending(w => w.CreatedAt)
                    .Take(50)
                    .Select(log =>
                    {
                        var desc = string.IsNullOrEmpty(log.Description)
                            ? log.UserName ?? ""
                            : $"{log.Description} ({log.UserName})";
                        return new ActivityItem
                        {
                            Title = "İş kaydı",
                            Description = $"{desc} · {log.Hours:N1} sa",
                            Time = log.CreatedAt
                        };
                    })
                    .ToList();

                var (pFromN, pToN) = NormalizeRange(pFrom, pTo);
                var personnelChart = BuildPersonnelChart(users, pFromN, pToN, allLogs);
                var periodTotal = FilterLogs(allLogs, pFromN, pToN).Sum(l => l.Hours);

                var (jFromN, jToN) = NormalizeRange(jFrom, jTo);
                var jobChart = BuildJobChart(jFromN, jToN, allLogs);

                dispatcher.InvokeAsync(() =>
                {
                    TotalEmployees = totalEmployees;
                    PeriodTotalHours = periodTotal;
                    ChartData.Clear();
                    foreach (var item in personnelChart)
                        ChartData.Add(item);
                    JobChartData.Clear();
                    foreach (var item in jobChart)
                        JobChartData.Add(item);
                    RecentActivities.Clear();
                    foreach (var item in activities)
                        RecentActivities.Add(item);
                    PersonnelFilterSummary = FormatRangeSummary(pFromN, pToN);
                    JobFilterSummary = FormatRangeSummary(jFromN, jToN);
                }, DispatcherPriority.Normal);
            }
            catch
            {
                dispatcher.InvokeAsync(() =>
                {
                    TotalEmployees = 0;
                    PeriodTotalHours = 0m;
                    ChartData.Clear();
                    JobChartData.Clear();
                    RecentActivities.Clear();
                }, DispatcherPriority.Normal);
            }
            finally
            {
                dispatcher.InvokeAsync(() => IsLoading = false, DispatcherPriority.Normal);
            }
        });
    }

    private void RefreshPersonnelChartAsync()
    {
        var dispatcher = Application.Current.Dispatcher;
        var from = PersonnelFilterFrom;
        var to = PersonnelFilterTo;
        var (fromNorm, toNorm) = NormalizeRange(from, to);

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var users = _userStore.GetAll();
                var allLogs = _workLogService.GetAll();
                var chartItems = BuildPersonnelChart(users, fromNorm, toNorm, allLogs);
                var total = FilterLogs(allLogs, fromNorm, toNorm).Sum(l => l.Hours);

                dispatcher.InvokeAsync(() =>
                {
                    PeriodTotalHours = total;
                    ChartData.Clear();
                    foreach (var item in chartItems)
                        ChartData.Add(item);
                    PersonnelFilterSummary = FormatRangeSummary(fromNorm, toNorm);
                }, DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                dispatcher.InvokeAsync(() =>
                    MessageBox.Show(ex.Message, "Personel grafiği yüklenemedi", MessageBoxButton.OK,
                        MessageBoxImage.Warning), DispatcherPriority.Normal);
            }
        });
    }

    private void RefreshJobChartAsync()
    {
        var dispatcher = Application.Current.Dispatcher;
        var from = JobFilterFrom;
        var to = JobFilterTo;
        var (fromNorm, toNorm) = NormalizeRange(from, to);

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var allLogs = _workLogService.GetAll();
                var chartItems = BuildJobChart(fromNorm, toNorm, allLogs);
                dispatcher.InvokeAsync(() =>
                {
                    JobChartData.Clear();
                    foreach (var item in chartItems)
                        JobChartData.Add(item);
                    JobFilterSummary = FormatRangeSummary(fromNorm, toNorm);
                }, DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                dispatcher.InvokeAsync(() =>
                    MessageBox.Show(ex.Message, "İş grafiği yüklenemedi", MessageBoxButton.OK,
                        MessageBoxImage.Warning), DispatcherPriority.Normal);
            }
        });
    }

    private static List<WorkLog> FilterLogs(IReadOnlyList<WorkLog> allLogs, DateTime from, DateTime to) =>
        allLogs.Where(l =>
        {
            var d = l.Date.Date;
            return d >= from && d <= to;
        }).ToList();

    private static List<ChartBarItem> BuildPersonnelChart(
        IReadOnlyList<StoredUser> users, DateTime from, DateTime to, IReadOnlyList<WorkLog> allLogs)
    {
        var logs = FilterLogs(allLogs, from, to);
        return users
            .Select(u =>
            {
                var hours = logs
                    .Where(l => SameUser(l.UserName, u.UserName))
                    .Sum(l => l.Hours);
                return new ChartBarItem
                {
                    Label = string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName,
                    Value = (double)hours
                };
            })
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<ChartBarItem> BuildJobChart(DateTime from, DateTime to, IReadOnlyList<WorkLog> allLogs)
    {
        Dictionary<Guid, string> jobLabels;
        try
        {
            jobLabels = _api.GetJobs(activeOnly: false)
                .ToDictionary(j => j.Id, j => j.DisplayText);
        }
        catch
        {
            jobLabels = new Dictionary<Guid, string>();
        }

        return FilterLogs(allLogs, from, to)
            .Where(l => l.JobId.HasValue && l.JobId.Value != Guid.Empty)
            .GroupBy(l => l.JobId!.Value)
            .Select(g => new ChartBarItem
            {
                Label = jobLabels.TryGetValue(g.Key, out var name) ? name : "Bilinmeyen iş",
                Value = (double)g.Sum(x => x.Hours)
            })
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool SameUser(string? logUser, string colUser) =>
        string.Equals(logUser?.Trim(), colUser, StringComparison.OrdinalIgnoreCase);

    private static string FormatRangeSummary(DateTime from, DateTime to) =>
        $"{from:dd.MM.yyyy} – {to:dd.MM.yyyy}";

    private static (DateTime From, DateTime To) NormalizeRange(DateTime from, DateTime to)
    {
        from = from.Date;
        to = to.Date;
        if (from > to)
            (from, to) = (to, from);
        return (from, to);
    }

    private static (DateTime WeekStart, DateTime WeekEnd) GetCurrentWeekRange()
    {
        var today = DateTime.Today;
        var daysToMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var weekStart = today.AddDays(-daysToMonday);
        return (weekStart, weekStart.AddDays(6));
    }

    private static (DateTime MonthStart, DateTime MonthEnd) GetCurrentMonthRange()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        return (monthStart, monthStart.AddMonths(1).AddDays(-1));
    }

    public void Refresh() => RefreshAsync();

    public void RefreshOnNavigate() => RefreshAsync();
}
