using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Personel;

public class PersonelAddWorkViewModel : ViewModelBase
{
    private DateTime? _selectedDate = DateTime.Today;
    private string _statusMessage = string.Empty;
    private readonly Dictionary<Guid, JobDetail?> _jobDetailCache = new();

    public PersonelAddWorkViewModel(IAuthService authService, IWorkLogService workLogService, BackendApiClient api)
    {
        _authService = authService;
        _workLogService = workLogService;
        _api = api;
        Entries = new ObservableCollection<WorkEntryRow>();
        Jobs = new ObservableCollection<Job>();
        var (start, end) = GetCurrentWeekRange();
        WeekStart = start;
        WeekEnd = end;
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

    /// <summary>
    /// Haftalık periyot: Pazartesi 00:00 - Pazar 23:59 (içinde bulunulan hafta).
    /// </summary>
    private static (DateTime WeekStart, DateTime WeekEnd) GetCurrentWeekRange()
    {
        var today = DateTime.Today;
        var daysToMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var weekStart = today.AddDays(-daysToMonday);
        var weekEnd = weekStart.AddDays(6);
        return (weekStart, weekEnd);
    }

    private readonly IAuthService _authService;
    private readonly IWorkLogService _workLogService;

    /// <summary>Admin tarafından tanımlı aktif işler (çoktan seçmeli liste).</summary>
    public ObservableCollection<Job> Jobs { get; }

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value);
    }

    public DateTime WeekStart { get; }
    public DateTime WeekEnd { get; }
    public string WeekRangeText => $"{WeekStart:dd.MM.yyyy} - {WeekEnd:dd.MM.yyyy}";

    public ObservableCollection<WorkEntryRow> Entries { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand AddRowCommand { get; }
    public ICommand RemoveRowCommand { get; }
    public ICommand SaveCommand { get; }

    private JobDetail? GetJobDetailCached(Guid jobId)
    {
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
        foreach (var j in _api.GetJobs(activeOnly: true))
            Jobs.Add(j);
    }

    /// <summary>Sayfa her açıldığında iş listesini yenile (admin yeni iş eklemiş olabilir).</summary>
    public void Refresh()
    {
        _jobDetailCache.Clear();
        LoadJobs();
    }

    private void AddRow()
    {
        Entries.Add(new WorkEntryRow(GetJobDetailCached));
    }

    private void SaveAll()
    {
        var date = SelectedDate ?? DateTime.Today;
        var dateOnly = date.Date;

        if (dateOnly < WeekStart || dateOnly > WeekEnd)
        {
            StatusMessage = "Sadece bu hafta (Pazartesi–Pazar) için iş girişi yapabilirsiniz. Geçmiş veya gelecek hafta seçilemez.";
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
            var detail = GetJobDetailCached(row.SelectedJob!.Id);
            var needsStage = detail?.Stages is { Count: > 0 };
            if (needsStage == true && (row.SelectedStageId == null || row.SelectedStageId == Guid.Empty))
            {
                StatusMessage = "Aşaması tanımlı işlerde önce işi, sonra ilgili aşamayı seçin.";
                return;
            }
        }

        try
        {
            foreach (var row in validRows)
            {
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
