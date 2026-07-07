using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CAKA.PerformanceApp.Models;

/// <summary>Haftalık iş kaydı tablosunda tek satır: iş + aşama + 7 günlük saat.</summary>
public class WeeklyWorkEntryRow : INotifyPropertyChanged
{
    private readonly Func<Guid, JobDetail?> _resolveJobDetail;
    private readonly Action? _onHoursChanged;
    private Job? _selectedJob;
    private Guid? _selectedStageId;
    private readonly decimal?[] _hours = new decimal?[7];
    private readonly Guid?[] _logIds = new Guid?[7];

    public WeeklyWorkEntryRow(Func<Guid, JobDetail?> resolveJobDetail, Action? onHoursChanged = null)
    {
        _resolveJobDetail = resolveJobDetail;
        _onHoursChanged = onHoursChanged;
        StageOptions = new ObservableCollection<JobStagePickItem>();
    }

    public ObservableCollection<JobStagePickItem> StageOptions { get; }

    public bool HasStageChoices => StageOptions.Count > 0;

    public Job? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (_selectedJob == value) return;
            _selectedJob = value;
            OnPropertyChanged();
            RefreshStages();
        }
    }

    public Guid? SelectedStageId
    {
        get => _selectedStageId;
        set
        {
            if (_selectedStageId == value) return;
            _selectedStageId = value;
            OnPropertyChanged();
        }
    }

    public decimal RowTotal => _hours.Sum(h => h ?? 0);

    public decimal? HoursMonday { get => _hours[0]; set => SetHours(0, value); }
    public decimal? HoursTuesday { get => _hours[1]; set => SetHours(1, value); }
    public decimal? HoursWednesday { get => _hours[2]; set => SetHours(2, value); }
    public decimal? HoursThursday { get => _hours[3]; set => SetHours(3, value); }
    public decimal? HoursFriday { get => _hours[4]; set => SetHours(4, value); }
    public decimal? HoursSaturday { get => _hours[5]; set => SetHours(5, value); }
    public decimal? HoursSunday { get => _hours[6]; set => SetHours(6, value); }

    public void InitializeJobAndStage(Job? job, Guid? stageId)
    {
        _selectedJob = job;
        OnPropertyChanged(nameof(SelectedJob));
        RefreshStages(preserveStageId: stageId);
    }

    public void SetDayFromLog(int dayIndex, decimal hours, Guid logId)
    {
        if (dayIndex < 0 || dayIndex > 6) return;
        _hours[dayIndex] = hours;
        _logIds[dayIndex] = logId;
        OnPropertyChanged(GetDayPropertyName(dayIndex));
        OnPropertyChanged(nameof(RowTotal));
        _onHoursChanged?.Invoke();
    }

    public decimal? GetHours(int dayIndex) => dayIndex is >= 0 and <= 6 ? _hours[dayIndex] : null;

    public Guid? GetLogId(int dayIndex) => dayIndex is >= 0 and <= 6 ? _logIds[dayIndex] : null;

    public void ClearLogId(int dayIndex)
    {
        if (dayIndex < 0 || dayIndex > 6) return;
        _logIds[dayIndex] = null;
    }

    public bool HasAnyHours() => _hours.Any(h => h is > 0);

    private void SetHours(int index, decimal? value)
    {
        if (_hours[index] == value) return;
        _hours[index] = value;
        OnPropertyChanged(GetDayPropertyName(index));
        OnPropertyChanged(nameof(RowTotal));
        _onHoursChanged?.Invoke();
    }

    private static string GetDayPropertyName(int index) => index switch
    {
        0 => nameof(HoursMonday),
        1 => nameof(HoursTuesday),
        2 => nameof(HoursWednesday),
        3 => nameof(HoursThursday),
        4 => nameof(HoursFriday),
        5 => nameof(HoursSaturday),
        6 => nameof(HoursSunday),
        _ => nameof(HoursMonday)
    };

    private void RefreshStages(Guid? preserveStageId = null)
    {
        StageOptions.Clear();
        if (preserveStageId == null)
            SelectedStageId = null;

        if (_selectedJob == null || WorkLogSpecialJobs.IsOfficeTrip(_selectedJob))
        {
            OnPropertyChanged(nameof(HasStageChoices));
            return;
        }

        var detail = _resolveJobDetail(_selectedJob.Id);
        if (detail?.Stages is { Count: > 0 } stages)
        {
            foreach (var s in stages.OrderBy(x => x.SortOrder))
            {
                var name = string.IsNullOrWhiteSpace(s.Name) ? $"Stage {s.SortOrder + 1}" : s.Name.Trim();
                var desc = (s.Description ?? "").Trim();
                var label = string.IsNullOrEmpty(desc) ? name : $"{name} - {desc}";
                StageOptions.Add(new JobStagePickItem { StageId = s.Id, Label = label });
            }

            if (preserveStageId.HasValue && StageOptions.Any(o => o.StageId == preserveStageId.Value))
                SelectedStageId = preserveStageId;
            else
                SelectedStageId = StageOptions[0].StageId;
        }

        OnPropertyChanged(nameof(HasStageChoices));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
