using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

/// <summary>
/// Admin: Tanımlı işler (iş kodu + açıklama). Çalışanlar bu işlerden seçip saat girer.
/// </summary>
public class AdminJobsViewModel : ViewModelBase
{
    private string _newCode = string.Empty;
    private string _newDescription = string.Empty;
    private string _statusMessage = string.Empty;
    private Job? _selectedJob;

    public AdminJobsViewModel(BackendApiClient api)
    {
        _api = api;
        Jobs = new ObservableCollection<Job>();
        RefreshCommand = new RelayCommand(_ => Refresh());
        AddJobCommand = new RelayCommand(_ => AddJob(), _ => !string.IsNullOrWhiteSpace(NewCode));
        DeleteJobCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedJob != null);
        UpdateJobCommand = new RelayCommand(_ => UpdateSelected(), _ => SelectedJob != null && !string.IsNullOrWhiteSpace(NewCode));
        CloseOrReopenJobCommand = new RelayCommand(_ => CloseOrReopenSelected(), _ => SelectedJob != null);
        try { Refresh(); } catch { /* API eski olabilir; sayfa yine açılsın */ }
    }

    private readonly BackendApiClient _api;

    public ObservableCollection<Job> Jobs { get; }
    public Job? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (!SetProperty(ref _selectedJob, value)) return;
            if (value == null) return;
            NewCode = value.Code;
            NewDescription = value.Description;
        }
    }
    public string NewCode
    {
        get => _newCode;
        set { if (SetProperty(ref _newCode, value ?? "")) ClearStatus(); }
    }
    public string NewDescription
    {
        get => _newDescription;
        set { if (SetProperty(ref _newDescription, value ?? "")) ClearStatus(); }
    }
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand AddJobCommand { get; }
    public ICommand DeleteJobCommand { get; }
    public ICommand UpdateJobCommand { get; }
    public ICommand CloseOrReopenJobCommand { get; }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        (UpdateJobCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Refresh()
    {
        Jobs.Clear();
        try
        {
            foreach (var j in _api.GetJobs(activeOnly: false))
                Jobs.Add(j);
            StatusMessage = string.Empty;
        }
        catch
        {
            StatusMessage = "İş listesi yüklenemedi. API güncel mi kontrol edin.";
        }
    }

    private void AddJob()
    {
        var code = NewCode.Trim();
        if (string.IsNullOrEmpty(code))
        {
            StatusMessage = "İş kodu girin.";
            return;
        }
        var job = new Job { Code = code, Description = NewDescription.Trim() };
        var (success, error) = _api.AddJob(job);
        if (success)
        {
            NewCode = "";
            NewDescription = "";
            Refresh();
            StatusMessage = "İş eklendi.";
        }
        else
            StatusMessage = error ?? "Eklenemedi.";
    }

    private void UpdateSelected()
    {
        if (SelectedJob == null)
        {
            StatusMessage = "Önce listeden bir iş seçin.";
            return;
        }

        var code = NewCode.Trim();
        if (string.IsNullOrEmpty(code))
        {
            StatusMessage = "İş kodu girin.";
            return;
        }

        var dto = new Job
        {
            Id = SelectedJob.Id,
            Code = code,
            Description = NewDescription.Trim(),
            IsActive = SelectedJob.IsActive
        };

        var (success, error) = _api.UpdateJob(dto);
        if (success)
        {
            Refresh();
            SelectedJob = Jobs.FirstOrDefault(x => x.Id == dto.Id);
            StatusMessage = "İş güncellendi.";
        }
        else
            StatusMessage = error ?? "Güncellenemedi.";
    }

    private void DeleteSelected()
    {
        if (SelectedJob == null) return;
        if (MessageBox.Show(
                $"'{SelectedJob.DisplayText}' işini silmek istediğinize emin misiniz? Bu işe ait eski kayıtlar etkilenmez.",
                "İş Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        if (_api.DeleteJob(SelectedJob.Id))
        {
            Refresh();
            StatusMessage = "İş silindi.";
        }
        else
            StatusMessage = "Silinemedi.";
    }

    /// <summary>İşi tamamlandı olarak kapat (çalışanlar artık seçemez) veya tekrar açar.</summary>
    private void CloseOrReopenSelected()
    {
        if (SelectedJob == null) return;
        var job = SelectedJob;
        var kapat = job.IsActive;
        var (success, error) = _api.UpdateJob(new Job { Id = job.Id, Code = job.Code, Description = job.Description, IsActive = !kapat });
        if (success)
        {
            Refresh();
            StatusMessage = kapat ? "İş kapatıldı. Çalışanlar bu işi artık seçemez." : "İş tekrar açıldı.";
        }
        else
            StatusMessage = error ?? "Güncellenemedi.";
    }
}
