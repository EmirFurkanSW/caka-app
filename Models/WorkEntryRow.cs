using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CAKA.PerformanceApp.Models;

/// <summary>İş kaydı gir formunda tek satır: iş + (varsa) aşama + saat.</summary>
public class WorkEntryRow : INotifyPropertyChanged
{
    private readonly Func<Guid, JobDetail?> _resolveJobDetail;
    private Job? _selectedJob;
    private Guid? _selectedStageId;
    private decimal? _hours;

    public WorkEntryRow(Func<Guid, JobDetail?> resolveJobDetail)
    {
        _resolveJobDetail = resolveJobDetail;
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

    public decimal? Hours
    {
        get => _hours;
        set
        {
            if (_hours == value) return;
            _hours = value;
            OnPropertyChanged();
        }
    }

    private void RefreshStages()
    {
        StageOptions.Clear();
        SelectedStageId = null;
        if (_selectedJob == null)
        {
            OnPropertyChanged(nameof(HasStageChoices));
            return;
        }

        var detail = _resolveJobDetail(_selectedJob.Id);
        if (detail?.Stages is { Count: > 0 } stages)
        {
            foreach (var s in stages.OrderBy(x => x.SortOrder))
                StageOptions.Add(new JobStagePickItem { StageId = s.Id, Label = s.Name });
        }
        OnPropertyChanged(nameof(HasStageChoices));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
