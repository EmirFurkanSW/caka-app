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

    public int TotalEmployees
    {
        get => _totalEmployees;
        private set => SetProperty(ref _totalEmployees, value);
    }

    /// <summary>Personel grafiği tarih aralığındaki toplam saat.</summary>
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

    public string PersonnelChartTitle =>
        $"Personel Çalışma Saatleri ({PersonnelFilterFrom:dd.MM.yyyy} – {PersonnelFilterTo:dd.MM.yyyy})";

    public string JobChartTitle =>
        $"İş Bazında Çalışma Saatleri ({JobFilterFrom:dd.MM.yyyy} – {JobFilterTo:dd.MM.yyyy})";

    public string PeriodTotalHoursLabel =>
        $"Toplam Saat ({PersonnelFilterFrom:dd.MM.yyyy} – {PersonnelFilterTo:dd.MM.yyyy})";

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
        IsLoading = true;

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var users = _userStore.GetAll();
                var totalEmployees = users.Count;
                var activities = _workLogService.GetAll()
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

                var (pFrom, pTo) = NormalizeRange(PersonnelFilterFrom, PersonnelFilterTo);
                var personnelChart = BuildPersonnelChart(users, pFrom, pTo);
                var periodTotal = _workLogService.GetTotalHoursAll(pFrom, pTo);

                var (jFrom, jTo) = NormalizeRange(JobFilterFrom, JobFilterTo);
                var jobChart = BuildJobChart(jFrom, jTo);

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
                    OnPropertyChanged(nameof(PersonnelChartTitle));
                    OnPropertyChanged(nameof(JobChartTitle));
                    OnPropertyChanged(nameof(PeriodTotalHoursLabel));
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
        var (from, to) = NormalizeRange(PersonnelFilterFrom, PersonnelFilterTo);

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var users = _userStore.GetAll();
                var chartItems = BuildPersonnelChart(users, from, to);
                var total = _workLogService.GetTotalHoursAll(from, to);

                dispatcher.InvokeAsync(() =>
                {
                    PeriodTotalHours = total;
                    ChartData.Clear();
                    foreach (var item in chartItems)
                        ChartData.Add(item);
                    OnPropertyChanged(nameof(PersonnelChartTitle));
                    OnPropertyChanged(nameof(PeriodTotalHoursLabel));
                }, DispatcherPriority.Normal);
            }
            catch
            {
                dispatcher.InvokeAsync(() => ChartData.Clear(), DispatcherPriority.Normal);
            }
        });
    }

    private void RefreshJobChartAsync()
    {
        var dispatcher = Application.Current.Dispatcher;
        var (from, to) = NormalizeRange(JobFilterFrom, JobFilterTo);

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var chartItems = BuildJobChart(from, to);
                dispatcher.InvokeAsync(() =>
                {
                    JobChartData.Clear();
                    foreach (var item in chartItems)
                        JobChartData.Add(item);
                    OnPropertyChanged(nameof(JobChartTitle));
                }, DispatcherPriority.Normal);
            }
            catch
            {
                dispatcher.InvokeAsync(() => JobChartData.Clear(), DispatcherPriority.Normal);
            }
        });
    }

    private List<ChartBarItem> BuildPersonnelChart(IReadOnlyList<StoredUser> users, DateTime from, DateTime to) =>
        users
            .OrderByDescending(u => _workLogService.GetTotalHoursForUser(u.UserName, from, to))
            .Select(u => new ChartBarItem
            {
                Label = string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName,
                Value = (double)_workLogService.GetTotalHoursForUser(u.UserName, from, to)
            })
            .ToList();

    private List<ChartBarItem> BuildJobChart(DateTime from, DateTime to)
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

        return _workLogService.GetAll()
            .Where(l => l.JobId.HasValue && l.JobId.Value != Guid.Empty)
            .Where(l =>
            {
                var d = l.Date.Date;
                return d >= from && d <= to;
            })
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
