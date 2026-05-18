using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Personel;

public class PersonelAddWorkViewModel : ViewModelBase, INavigationRefresh
{
    private DateTime? _selectedDate = DateTime.Today;
    private string _statusMessage = string.Empty;
    private readonly Dictionary<Guid, JobDetail?> _jobDetailCache = new();
    private DateTime _entryMinDate;
    private DateTime _entryMaxDate;
    private string _allowedPeriodHint = "";

    public PersonelAddWorkViewModel(IAuthService authService, IWorkLogService workLogService, BackendApiClient api)
    {
        _authService = authService;
        _workLogService = workLogService;
        _api = api;
        Entries = new ObservableCollection<WorkEntryRow>();
        Jobs = new ObservableCollection<Job>();
        RefreshAllowedDateRange();
        AddRowCommand = new RelayCommand(_ => AddRow());
        RemoveRowCommand = new RelayCommand(param =>
        {
            if (param is WorkEntryRow row)
                Entries.Remove(row);
        });
        SaveCommand = new RelayCommand(_ => SaveAll());
        LoadJobs();
        AddRow();
    }

    private readonly BackendApiClient _api;
    private readonly IAuthService _authService;
    private readonly IWorkLogService _workLogService;

    public ObservableCollection<Job> Jobs { get; }
    public ObservableCollection<WorkEntryRow> Entries { get; }

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value);
    }

    public DateTime EntryMinDate
    {
        get => _entryMinDate;
        private set => SetProperty(ref _entryMinDate, value);
    }

    public DateTime EntryMaxDate
    {
        get => _entryMaxDate;
        private set => SetProperty(ref _entryMaxDate, value);
    }

    public string AllowedPeriodHint
    {
        get => _allowedPeriodHint;
        private set => SetProperty(ref _allowedPeriodHint, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand AddRowCommand { get; }
    public ICommand RemoveRowCommand { get; }
    public ICommand SaveCommand { get; }

    private void RefreshAllowedDateRange()
    {
        var today = DateTime.Today;
        var (min, max) = WorkLogEntryPeriod.GetSelectableDateRange(today);
        EntryMinDate = min;
        EntryMaxDate = max;
        AllowedPeriodHint = WorkLogEntryPeriod.FormatAllowedPeriodHint(today);
        if (SelectedDate < min || SelectedDate > max)
            SelectedDate = today <= max ? today : max;
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

    public void Refresh()
    {
        _jobDetailCache.Clear();
        RefreshAllowedDateRange();
        LoadJobs();
    }

    public void RefreshOnNavigate()
    {
        _jobDetailCache.Clear();
        RefreshAllowedDateRange();
        LoadJobs();
        Entries.Clear();
        SelectedDate = DateTime.Today <= EntryMaxDate ? DateTime.Today : EntryMaxDate;
        StatusMessage = "";
        AddRow();
    }

    private void AddRow() => Entries.Add(new WorkEntryRow(GetJobDetailCached));

    private void SaveAll()
    {
        var date = SelectedDate ?? DateTime.Today;
        var dateOnly = date.Date;

        if (!WorkLogEntryPeriod.CanPersonnelEnterLogForDate(dateOnly, DateTime.Today))
        {
            StatusMessage =
                "Seçilen tarih için giriş süresi dolmuş veya gelecek hafta seçildi. Kayıtlar, ilgili haftayı izleyen Çarşamba gününe kadar girilebilir.";
            return;
        }

        var validRows = Entries
            .Where(e => e.SelectedJob != null && e.Hours.HasValue && e.Hours.Value >= 0 && e.Hours.Value <= 24)
            .ToList();

        if (validRows.Count == 0)
        {
            StatusMessage = "En az bir satırda iş seçin ve saat 0–24 arasında girin.";
            return;
        }

        foreach (var row in validRows)
        {
            if (WorkLogSpecialJobs.IsOfficeTrip(row.SelectedJob))
                continue;

            var detail = GetJobDetailCached(row.SelectedJob!.Id);
            var needsStage = detail?.Stages is { Count: > 0 };
            if (needsStage == true && (row.SelectedStageId == null || row.SelectedStageId == Guid.Empty))
            {
                StatusMessage = "Aşaması tanımlı işlerde ilgili aşamayı seçin.";
                return;
            }
        }

        try
        {
            foreach (var row in validRows)
            {
                if (WorkLogSpecialJobs.IsOfficeTrip(row.SelectedJob))
                {
                    _workLogService.Add(new WorkLog
                    {
                        Date = date,
                        JobId = null,
                        JobStageId = null,
                        Description = WorkLogSpecialJobs.OfficeTripDisplayText,
                        Hours = row.Hours!.Value,
                        UserName = _authService.CurrentUser?.UserName
                    });
                    continue;
                }

                var detail = GetJobDetailCached(row.SelectedJob!.Id);
                var hasStages = detail?.Stages is { Count: > 0 };
                _workLogService.Add(new WorkLog
                {
                    Date = date,
                    JobId = row.SelectedJob!.Id,
                    JobStageId = hasStages == true ? row.SelectedStageId : null,
                    Description = row.SelectedJob.DisplayText,
                    Hours = row.Hours!.Value,
                    UserName = _authService.CurrentUser?.UserName
                });
            }

            foreach (var row in validRows)
                Entries.Remove(row);

            StatusMessage = $"{validRows.Count} iş kaydı eklendi.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
