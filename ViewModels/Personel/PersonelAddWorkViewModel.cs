using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Personel;

public class PersonelAddWorkViewModel : ViewModelBase, INavigationRefresh
{
    private DateTime _selectedWeekStart;
    private string _statusMessage = string.Empty;
    private readonly Dictionary<Guid, JobDetail?> _jobDetailCache = new();
    private string _allowedPeriodHint = "";
    private bool _isWeekEditable;

    public PersonelAddWorkViewModel(IAuthService authService, IWorkLogService workLogService, BackendApiClient api)
    {
        _authService = authService;
        _workLogService = workLogService;
        _api = api;
        Entries = new ObservableCollection<WeeklyWorkEntryRow>();
        Jobs = new ObservableCollection<Job>();
        AvailableWeeks = new ObservableCollection<WeekPickerOption>();
        DayHeaders = new ObservableCollection<string>();

        AddRowCommand = new RelayCommand(_ => AddRow());
        RemoveRowCommand = new RelayCommand(param =>
        {
            if (param is WeeklyWorkEntryRow row)
                Entries.Remove(row);
        });
        SaveCommand = new RelayCommand(_ => SaveWeek(), _ => IsWeekEditable);

        RefreshWeekOptions();
        LoadJobs();
        LoadWeekFromStorage();
    }

    private readonly BackendApiClient _api;
    private readonly IAuthService _authService;
    private readonly IWorkLogService _workLogService;

    public ObservableCollection<Job> Jobs { get; }
    public ObservableCollection<WeeklyWorkEntryRow> Entries { get; }
    public ObservableCollection<WeekPickerOption> AvailableWeeks { get; }
    public ObservableCollection<string> DayHeaders { get; }

    public decimal TargetWeekdayHours => 8m;
    public decimal TargetWeekendHours => 0m;
    public decimal TargetWeekTotal => 40m;

    public DateTime SelectedWeekStart
    {
        get => _selectedWeekStart;
        set
        {
            if (!SetProperty(ref _selectedWeekStart, value.Date)) return;
            RefreshDayHeaders();
            RefreshWeekEditability();
            LoadWeekEntries();
        }
    }

    public string AllowedPeriodHint
    {
        get => _allowedPeriodHint;
        private set => SetProperty(ref _allowedPeriodHint, value);
    }

    public bool IsWeekEditable
    {
        get => _isWeekEditable;
        private set
        {
            if (SetProperty(ref _isWeekEditable, value))
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public decimal TotalMonday => SumDay(0);
    public decimal TotalTuesday => SumDay(1);
    public decimal TotalWednesday => SumDay(2);
    public decimal TotalThursday => SumDay(3);
    public decimal TotalFriday => SumDay(4);
    public decimal TotalSaturday => SumDay(5);
    public decimal TotalSunday => SumDay(6);
    public decimal TotalAll => TotalMonday + TotalTuesday + TotalWednesday + TotalThursday
        + TotalFriday + TotalSaturday + TotalSunday;

    public ICommand AddRowCommand { get; }
    public ICommand RemoveRowCommand { get; }
    public ICommand SaveCommand { get; }

    private decimal SumDay(int dayIndex) =>
        Entries.Sum(r => r.GetHours(dayIndex) ?? 0);

    private void OnEntryHoursChanged()
    {
        OnPropertyChanged(nameof(TotalMonday));
        OnPropertyChanged(nameof(TotalTuesday));
        OnPropertyChanged(nameof(TotalWednesday));
        OnPropertyChanged(nameof(TotalThursday));
        OnPropertyChanged(nameof(TotalFriday));
        OnPropertyChanged(nameof(TotalSaturday));
        OnPropertyChanged(nameof(TotalSunday));
        OnPropertyChanged(nameof(TotalAll));
    }

    private void RefreshWeekOptions()
    {
        var today = DateTime.Today;
        AllowedPeriodHint = WorkLogEntryPeriod.FormatAllowedPeriodHint(today);
        AvailableWeeks.Clear();
        foreach (var start in WorkLogEntryPeriod.GetSelectableWeekStarts(today))
            AvailableWeeks.Add(new WeekPickerOption { WeekStart = start });
    }

    private void LoadWeekFromStorage()
    {
        var today = DateTime.Today;
        var defaultStart = WorkLogEntryPeriod.GetSelectableWeekStarts(today).FirstOrDefault();
        if (defaultStart == default)
            defaultStart = WorkLogEntryPeriod.GetWeekRange(today).WeekStart;
        SelectedWeekStart = defaultStart;
    }

    private void RefreshDayHeaders()
    {
        var labels = new[] { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
        DayHeaders.Clear();
        for (var i = 0; i < 7; i++)
            DayHeaders.Add($"{labels[i]}\n{SelectedWeekStart.AddDays(i):dd.MM}");
        OnPropertyChanged(nameof(Day1Header));
        OnPropertyChanged(nameof(Day2Header));
        OnPropertyChanged(nameof(Day3Header));
        OnPropertyChanged(nameof(Day4Header));
        OnPropertyChanged(nameof(Day5Header));
        OnPropertyChanged(nameof(Day6Header));
        OnPropertyChanged(nameof(Day7Header));
        OnPropertyChanged(nameof(Day1Date));
        OnPropertyChanged(nameof(Day2Date));
        OnPropertyChanged(nameof(Day3Date));
        OnPropertyChanged(nameof(Day4Date));
        OnPropertyChanged(nameof(Day5Date));
        OnPropertyChanged(nameof(Day6Date));
        OnPropertyChanged(nameof(Day7Date));
    }

    public string Day1Header => DayHeaders.Count > 0 ? DayHeaders[0] : "Pzt";
    public string Day2Header => DayHeaders.Count > 1 ? DayHeaders[1] : "Sal";
    public string Day3Header => DayHeaders.Count > 2 ? DayHeaders[2] : "Çar";
    public string Day4Header => DayHeaders.Count > 3 ? DayHeaders[3] : "Per";
    public string Day5Header => DayHeaders.Count > 4 ? DayHeaders[4] : "Cum";
    public string Day6Header => DayHeaders.Count > 5 ? DayHeaders[5] : "Cmt";
    public string Day7Header => DayHeaders.Count > 6 ? DayHeaders[6] : "Paz";

    public string Day1Date => SelectedWeekStart.ToString("dd.MM");
    public string Day2Date => SelectedWeekStart.AddDays(1).ToString("dd.MM");
    public string Day3Date => SelectedWeekStart.AddDays(2).ToString("dd.MM");
    public string Day4Date => SelectedWeekStart.AddDays(3).ToString("dd.MM");
    public string Day5Date => SelectedWeekStart.AddDays(4).ToString("dd.MM");
    public string Day6Date => SelectedWeekStart.AddDays(5).ToString("dd.MM");
    public string Day7Date => SelectedWeekStart.AddDays(6).ToString("dd.MM");

    private void RefreshWeekEditability()
    {
        IsWeekEditable = WorkLogEntryPeriod.IsWeekEditable(SelectedWeekStart, DateTime.Today);
    }

    private JobDetail? GetJobDetailCached(Guid jobId)
    {
        if (WorkLogSpecialJobs.IsOfficeTrip(new Job { Id = jobId }))
            return null;
        if (_jobDetailCache.TryGetValue(jobId, out var cached))
            return cached;
        try
        {
            var d = _api.GetJobDetail(jobId);
            _jobDetailCache[jobId] = d;
            return d;
        }
        catch
        {
            _jobDetailCache[jobId] = null;
            return null;
        }
    }

    private void LoadJobs()
    {
        Jobs.Clear();
        Jobs.Add(WorkLogSpecialJobs.CreateOfficeTripJob());
        foreach (var j in _api.GetJobs(activeOnly: true))
            Jobs.Add(j);
    }

    private void LoadWeekEntries()
    {
        Entries.Clear();
        var userName = _authService.CurrentUser?.UserName;
        var weekEnd = SelectedWeekStart.AddDays(6);
        var logs = _workLogService.GetByUser(userName)
            .Where(l => l.Date.Date >= SelectedWeekStart && l.Date.Date <= weekEnd)
            .ToList();

        var groups = logs
            .GroupBy(l => GetLogRowKey(l))
            .OrderBy(g => g.Key.SortKey)
            .ToList();

        foreach (var group in groups)
        {
            var sample = group.First();
            EnsureJobInList(group.Key.Job, sample);
            var row = new WeeklyWorkEntryRow(GetJobDetailCached, OnEntryHoursChanged);
            row.InitializeJobAndStage(group.Key.Job, group.Key.StageId);
            foreach (var log in group)
            {
                var dayIndex = (int)(log.Date.Date - SelectedWeekStart).TotalDays;
                if (dayIndex is >= 0 and <= 6)
                    row.SetDayFromLog(dayIndex, log.Hours, log.Id);
            }
            Entries.Add(row);
        }

        if (Entries.Count == 0)
            AddRow();

        OnEntryHoursChanged();
    }

    private void EnsureJobInList(Job? job, WorkLog sampleLog)
    {
        if (job == null || WorkLogSpecialJobs.IsOfficeTrip(job)) return;
        if (Jobs.Any(j => j.Id == job.Id)) return;
        Jobs.Add(new Job
        {
            Id = job.Id,
            Code = job.Code,
            Description = string.IsNullOrWhiteSpace(job.Description) ? sampleLog.Description : job.Description,
            IsActive = false
        });
    }

    private (string SortKey, Job? Job, Guid? StageId) GetLogRowKey(WorkLog log)
    {
        if (log.JobId == null && WorkLogSpecialJobs.IsOfficeTripDescription(log.Description))
            return ("0|office", WorkLogSpecialJobs.CreateOfficeTripJob(), null);

        var job = Jobs.FirstOrDefault(j => j.Id == log.JobId);
        if (job == null && log.JobId.HasValue)
        {
            job = new Job
            {
                Id = log.JobId.Value,
                Code = "",
                Description = log.Description,
                IsActive = true
            };
        }

        return ($"1|{log.JobId}|{log.JobStageId}", job, log.JobStageId);
    }

    public void Refresh()
    {
        _jobDetailCache.Clear();
        RefreshWeekOptions();
        LoadJobs();
        LoadWeekEntries();
    }

    public void RefreshOnNavigate()
    {
        _jobDetailCache.Clear();
        RefreshWeekOptions();
        LoadJobs();
        StatusMessage = "";
        LoadWeekFromStorage();
    }

    private WeeklyWorkEntryRow AddRow()
    {
        var row = new WeeklyWorkEntryRow(GetJobDetailCached, OnEntryHoursChanged);
        Entries.Add(row);
        return row;
    }

    private void SaveWeek()
    {
        if (!IsWeekEditable)
        {
            StatusMessage = "Bu hafta için giriş süresi dolmuş. Kayıtlar, haftayı izleyen Çarşamba gününe kadar girilebilir.";
            return;
        }

        var rowsWithData = Entries.Where(r => r.HasAnyHours()).ToList();
        if (rowsWithData.Count == 0)
        {
            StatusMessage = "Kaydetmek için en az bir satırda saat girin.";
            return;
        }

        foreach (var row in rowsWithData)
        {
            if (row.SelectedJob == null)
            {
                StatusMessage = "Saat girilen her satırda iş seçin.";
                return;
            }

            if (WorkLogSpecialJobs.IsOfficeTrip(row.SelectedJob))
                continue;

            var detail = GetJobDetailCached(row.SelectedJob.Id);
            var needsStage = detail?.Stages is { Count: > 0 };
            if (needsStage == true && (row.SelectedStageId == null || row.SelectedStageId == Guid.Empty))
            {
                StatusMessage = "Aşaması tanımlı işlerde ilgili aşamayı seçin.";
                return;
            }
        }

        foreach (var row in rowsWithData)
        {
            for (var d = 0; d < 7; d++)
            {
                var hours = row.GetHours(d);
                if (hours is < 0 or > 24)
                {
                    StatusMessage = "Günlük saat 0–24 arasında olmalıdır.";
                    return;
                }
            }
        }

        try
        {
            var saved = 0;
            var deleted = 0;

            foreach (var row in Entries)
            {
                for (var d = 0; d < 7; d++)
                {
                    var hours = row.GetHours(d);
                    var logId = row.GetLogId(d);
                    var date = SelectedWeekStart.AddDays(d);

                    if (hours is null or 0)
                    {
                        if (logId.HasValue)
                        {
                            _workLogService.Delete(logId.Value);
                            row.ClearLogId(d);
                            deleted++;
                        }
                        continue;
                    }

                    if (row.SelectedJob == null) continue;

                    if (WorkLogSpecialJobs.IsOfficeTrip(row.SelectedJob))
                    {
                        SaveLog(logId, date, null, null, WorkLogSpecialJobs.OfficeTripDisplayText, hours.Value, row, d, ref saved);
                        continue;
                    }

                    var detail = GetJobDetailCached(row.SelectedJob.Id);
                    var hasStages = detail?.Stages is { Count: > 0 };
                    SaveLog(logId, date, row.SelectedJob.Id,
                        hasStages == true ? row.SelectedStageId : null,
                        row.SelectedJob.DisplayText, hours.Value, row, d, ref saved);
                }
            }

            StatusMessage = deleted > 0
                ? $"{saved} kayıt güncellendi/eklendi, {deleted} kayıt silindi."
                : $"{saved} kayıt kaydedildi.";
            LoadWeekEntries();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void SaveLog(Guid? logId, DateTime date, Guid? jobId, Guid? stageId, string description,
        decimal hours, WeeklyWorkEntryRow row, int dayIndex, ref int saved)
    {
        var userName = _authService.CurrentUser?.UserName;
        if (logId.HasValue)
        {
            _workLogService.Update(new WorkLog
            {
                Id = logId.Value,
                Date = date,
                JobId = jobId,
                JobStageId = stageId,
                Description = description,
                Hours = hours,
                UserName = userName
            });
        }
        else
        {
            var created = new WorkLog
            {
                Date = date,
                JobId = jobId,
                JobStageId = stageId,
                Description = description,
                Hours = hours,
                UserName = userName
            };
            _workLogService.Add(created);
            if (created.Id != Guid.Empty)
                row.SetDayFromLog(dayIndex, hours, created.Id);
        }
        saved++;
    }
}
