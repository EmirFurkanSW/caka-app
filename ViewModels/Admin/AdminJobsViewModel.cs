using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

/// <summary>
/// Admin: İş tanımı (kod, açıklama), aşamalar, işe özel saatlik ücretler ve aşama bazlı planlanan saatler.
/// </summary>
public class AdminJobsViewModel : ViewModelBase, INavigationRefresh
{
    private string _newCode = string.Empty;
    private string _newDescription = string.Empty;
    private string? _selectedProjectManagerUserName;
    private string _statusMessage = string.Empty;
    private Job? _selectedJob;

    public AdminJobsViewModel(BackendApiClient api, IAuthService authService)
    {
        _api = api;
        _authService = authService;
        Jobs = new ObservableCollection<Job>();
        EditStages = new ObservableCollection<JobStageEditRow>();
        EditParticipants = new ObservableCollection<JobParticipantEditRow>();
        EditPlans = new ObservableCollection<JobPlanEditRow>();
        StagePickList = new ObservableCollection<StagePickItem>();
        UserOptions = new ObservableCollection<StoredUser>();
        PlanParticipantPickList = new ObservableCollection<StoredUser>();

        CurrencyOptions.Add(new CurrencyOption { Code = "TRY", Label = "TL (TRY)" });
        CurrencyOptions.Add(new CurrencyOption { Code = "USD", Label = "USD ($)" });

        EditStages.CollectionChanged += OnEditStagesCollectionChanged;
        EditParticipants.CollectionChanged += OnEditParticipantsCollectionChanged;

        RefreshCommand = new RelayCommand(_ => Refresh());
        AddJobCommand = new RelayCommand(_ => AddJob(), _ => !string.IsNullOrWhiteSpace(NewCode) && !string.IsNullOrWhiteSpace(SelectedProjectManagerUserName));
        DeleteJobCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedJob != null && IsFullJobAdmin);
        UpdateJobCommand = new RelayCommand(_ => UpdateSelected(), _ => SelectedJob != null && !string.IsNullOrWhiteSpace(NewCode));
        CloseOrReopenJobCommand = new RelayCommand(_ => CloseOrReopenSelected(), _ => SelectedJob != null && IsFullJobAdmin);

        AddStageCommand = new RelayCommand(_ => AddStageRow());
        RemoveStageCommand = new RelayCommand(p => RemoveStageRow(p as JobStageEditRow));
        AddParticipantCommand = new RelayCommand(_ => EditParticipants.Add(new JobParticipantEditRow()));
        RemoveParticipantCommand = new RelayCommand(p => RemoveParticipantRow(p as JobParticipantEditRow));
        AddPlanCommand = new RelayCommand(_ => AddPlanRow());
        RemovePlanCommand = new RelayCommand(p => RemovePlanRow(p as JobPlanEditRow));

        LoadUserOptions();
        try { Refresh(); } catch { /* API eski olabilir */ }
    }

    private readonly BackendApiClient _api;
    private readonly IAuthService _authService;

    public bool IsFullJobAdmin =>
        _authService.CurrentUser?.Role is UserRole.Admin or UserRole.Yonetici;

    public ObservableCollection<Job> Jobs { get; }
    public ObservableCollection<JobStageEditRow> EditStages { get; }
    public ObservableCollection<JobParticipantEditRow> EditParticipants { get; }
    public ObservableCollection<JobPlanEditRow> EditPlans { get; }
    public ObservableCollection<StagePickItem> StagePickList { get; }
    public ObservableCollection<StoredUser> UserOptions { get; }
    /// <summary>Plan satırları: yalnızca işe eklenmiş çalışanlar (üst liste).</summary>
    public ObservableCollection<StoredUser> PlanParticipantPickList { get; }
    public ObservableCollection<CurrencyOption> CurrencyOptions { get; } = new();

    public Job? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (!SetProperty(ref _selectedJob, value)) return;
            if (value == null)
            {
                ClearEditors();
                return;
            }

            var detail = _api.GetJobDetail(value.Id);
            if (detail == null)
            {
                StatusMessage = "İş detayı yüklenemedi.";
                ClearPlanningEditors();
                NewCode = value.Code;
                NewDescription = value.Description;
                SelectedProjectManagerUserName = value.ProjectManagerUserName;
                return;
            }

            LoadEditorsFromDetail(detail);
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

    public string? SelectedProjectManagerUserName
    {
        get => _selectedProjectManagerUserName;
        set
        {
            if (SetProperty(ref _selectedProjectManagerUserName, value))
            {
                ClearStatus();
                (AddJobCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
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

    public ICommand AddStageCommand { get; }
    public ICommand RemoveStageCommand { get; }
    public ICommand AddParticipantCommand { get; }
    public ICommand RemoveParticipantCommand { get; }
    public ICommand AddPlanCommand { get; }
    public ICommand RemovePlanCommand { get; }

    private void OnEditStagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (JobStageEditRow row in e.NewItems)
                row.PropertyChanged += StageRowOnPropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (JobStageEditRow row in e.OldItems)
                row.PropertyChanged -= StageRowOnPropertyChanged;
        }

        RebuildStagePickList();
    }

    private void StageRowOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JobStageEditRow.StageNumber) || e.PropertyName == nameof(JobStageEditRow.Description))
            RebuildStagePickList();
    }

    private void OnEditParticipantsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (JobParticipantEditRow row in e.NewItems)
                row.PropertyChanged += ParticipantRowOnPropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (JobParticipantEditRow row in e.OldItems)
                row.PropertyChanged -= ParticipantRowOnPropertyChanged;
        }

        RebuildPlanParticipantPickList();
    }

    private void ParticipantRowOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JobParticipantEditRow.UserName))
            RebuildPlanParticipantPickList();
    }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        (UpdateJobCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddJobCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void LoadUserOptions()
    {
        UserOptions.Clear();
        try
        {
            foreach (var u in _api.GetUsers().Where(u => !u.IsSuspended).OrderBy(u => u.DisplayName))
                UserOptions.Add(u);
        }
        catch { /* liste boş kalır */ }
        EnsureAssignedUsersInOptions();
        RebuildPlanParticipantPickList();
    }

    /// <summary>Mevcut iş satırlarındaki kullanıcılar listede yoksa ekle (PM API listesi alamazsa yedek).</summary>
    private void EnsureAssignedUsersInOptions()
    {
        foreach (var p in EditParticipants)
        {
            if (!string.IsNullOrWhiteSpace(p.UserName))
                EnsureUserInOptions(p.UserName.Trim());
        }
        foreach (var pl in EditPlans)
        {
            if (!string.IsNullOrWhiteSpace(pl.UserName))
                EnsureUserInOptions(pl.UserName.Trim());
        }
    }

    private void EnsureUserInOptions(string userName)
    {
        if (UserOptions.Any(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)))
            return;
        UserOptions.Add(new StoredUser
        {
            UserName = userName,
            DisplayName = userName,
            Role = "Personel"
        });
    }

    private void ReleaseParticipantSubscriptions(IEnumerable<JobParticipantEditRow> rows)
    {
        foreach (var r in rows)
            r.PropertyChanged -= ParticipantRowOnPropertyChanged;
    }

    private StoredUser ParticipantPickVm(string userName)
    {
        var un = userName.Trim();
        var src = UserOptions.FirstOrDefault(x => string.Equals(x.UserName, un, StringComparison.OrdinalIgnoreCase));
        return new StoredUser
        {
            UserName = un,
            DisplayName = string.IsNullOrWhiteSpace(src?.DisplayName) ? un : src.DisplayName,
            Password = "",
            Role = src?.Role ?? "Personel"
        };
    }

    private void RebuildPlanParticipantPickList()
    {
        var distinct = EditParticipants
            .Select(p => p.UserName?.Trim() ?? "")
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        PlanParticipantPickList.Clear();

        foreach (var un in distinct.OrderBy(u =>
                 {
                     var src = UserOptions.FirstOrDefault(x => string.Equals(x.UserName, u, StringComparison.OrdinalIgnoreCase));
                     return string.IsNullOrWhiteSpace(src?.DisplayName) ? u : src.DisplayName;
                 }, StringComparer.CurrentCultureIgnoreCase))
            PlanParticipantPickList.Add(ParticipantPickVm(un));

        var allowed = distinct.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in EditPlans.Where(p =>
                     string.IsNullOrWhiteSpace(p.UserName) || !allowed.Contains(p.UserName.Trim())).ToList())
            row.UserName = "";
    }

    private void RebuildStagePickList()
    {
        StagePickList.Clear();
        for (var i = 0; i < EditStages.Count; i++)
        {
            var s = EditStages[i];
            StagePickList.Add(new StagePickItem { Index = i + 1, Label = FormatPlanStagePickLabel(s.Description, s.ResolvedName) });
        }
    }

    /// <summary>Plan aşama combobox: «Stage 1» veya «Stage 1 - açıklama».</summary>
    private static string FormatPlanStagePickLabel(string? stageDescription, string resolvedNameFallback)
    {
        var desc = (stageDescription ?? "").Trim();
        return string.IsNullOrEmpty(desc) ? resolvedNameFallback : $"{resolvedNameFallback} - {desc}";
    }

    private void ClearEditors()
    {
        NewCode = "";
        NewDescription = "";
        SelectedProjectManagerUserName = null;
        ClearPlanningEditors();
    }

    private void ClearPlanningEditors()
    {
        foreach (var r in EditStages)
            r.PropertyChanged -= StageRowOnPropertyChanged;
        ReleaseParticipantSubscriptions(EditParticipants);
        EditStages.Clear();
        EditParticipants.Clear();
        EditPlans.Clear();
        RebuildStagePickList();
        RebuildPlanParticipantPickList();
    }

    private void LoadEditorsFromDetail(JobDetail detail)
    {
        NewCode = detail.Code;
        NewDescription = detail.Description;
        SelectedProjectManagerUserName = detail.ProjectManagerUserName;

        EditStages.Clear();
        foreach (var s in detail.Stages.OrderBy(x => x.SortOrder))
            EditStages.Add(new JobStageEditRow { Description = s.Description ?? "" });
        RenumberStages();

        ReleaseParticipantSubscriptions(EditParticipants);
        EditParticipants.Clear();
        foreach (var p in detail.Participants)
        {
            var cur = string.IsNullOrWhiteSpace(p.HourlyRateCurrency) ? "TRY" : p.HourlyRateCurrency.Trim().ToUpperInvariant();
            if (cur != "TRY" && cur != "USD")
                cur = "TRY";
            EditParticipants.Add(new JobParticipantEditRow { UserName = p.UserName, HourlyRate = p.HourlyRate, Currency = cur });
        }

        EditPlans.Clear();
        var orderedStageIds = detail.Stages.OrderBy(x => x.SortOrder).Select(s => s.Id).ToList();
        foreach (var pl in detail.StagePlans.OrderBy(x => x.StageIndex).ThenBy(x => x.UserName))
        {
            var idx = pl.StageIndex;
            if (pl.StageId.HasValue && pl.StageId.Value != Guid.Empty)
            {
                var byId = orderedStageIds.IndexOf(pl.StageId.Value);
                if (byId >= 0)
                    idx = byId;
            }

            EditPlans.Add(new JobPlanEditRow
            {
                StageIndex = idx,
                UserName = pl.UserName,
                PlannedHours = pl.PlannedHours
            });
        }

        RenormalizePlanStageIndices();

        RebuildStagePickList();
        EnsureAssignedUsersInOptions();
        RebuildPlanParticipantPickList();
        StatusMessage = string.Empty;
    }

    private JobDetail BuildJobDetailForSave(Guid id, bool isActive)
    {
        return new JobDetail
        {
            Id = id,
            Code = NewCode.Trim(),
            Description = NewDescription.Trim(),
            IsActive = isActive,
            ProjectManagerUserName = SelectedProjectManagerUserName?.Trim(),
            Stages = EditStages.Select((s, i) => new JobStageItem
            {
                Id = Guid.Empty,
                Name = s.ResolvedName,
                Description = s.Description.Trim(),
                SortOrder = i
            }).ToList(),
            Participants = EditParticipants
                .Where(p => !string.IsNullOrWhiteSpace(p.UserName))
                .Select(p => new JobParticipantItem
                {
                    UserName = p.UserName.Trim(),
                    HourlyRate = p.HourlyRate,
                    HourlyRateCurrency = p.Currency is "TRY" or "USD" ? p.Currency : "TRY"
                })
                .ToList(),
            StagePlans = BuildSanitizedStagePlans()
        };
    }

    /// <summary>API doğrulaması: aşama yokken plan gönderme; indeksleri mevcut aşama sayısına sıkıştır.</summary>
    private List<JobStagePlanItem> BuildSanitizedStagePlans()
    {
        var n = EditStages.Count;
        if (n == 0)
            return new List<JobStagePlanItem>();
        return EditPlans
            .Where(p => !string.IsNullOrWhiteSpace(p.UserName))
            .Where(p => p.StageIndex >= 0 && p.StageIndex < n)
            .Select(p => new JobStagePlanItem
            {
                StageIndex = p.StageIndex,
                UserName = p.UserName.Trim(),
                PlannedHours = p.PlannedHours
            }).ToList();
    }

    private void AddStageRow()
    {
        EditStages.Add(new JobStageEditRow());
        RenumberStages();
    }

    private void RemoveStageRow(JobStageEditRow? row)
    {
        if (row == null) return;
        var i = EditStages.IndexOf(row);
        if (i < 0) return;
        EditStages.RemoveAt(i);
        AdjustPlansAfterStageRemove(i);
        RenumberStages();
        RenormalizePlanStageIndices();
    }

    /// <summary>Listede kalan satırlar üstten alta Stage 1…N olur.</summary>
    private void RenumberStages()
    {
        for (var i = 0; i < EditStages.Count; i++)
            EditStages[i].StageNumber = i + 1;
    }

    /// <summary>Plan satırındaki aşama indeksi, mevcut aşama sayısını aşıyorsa düzeltilir.</summary>
    private void RenormalizePlanStageIndices()
    {
        var n = EditStages.Count;
        if (n == 0)
        {
            EditPlans.Clear();
            return;
        }

        var toRemove = EditPlans.Where(p => p.StageIndex < 0 || p.StageIndex >= n).ToList();
        foreach (var p in toRemove)
            EditPlans.Remove(p);
        foreach (var p in EditPlans)
            p.StageIndex = Math.Clamp(p.StageIndex, 0, n - 1);
    }

    private void AdjustPlansAfterStageRemove(int removedIndex)
    {
        var toRemove = EditPlans.Where(p => p.StageIndex == removedIndex).ToList();
        foreach (var p in toRemove)
            EditPlans.Remove(p);
        foreach (var p in EditPlans)
        {
            if (p.StageIndex > removedIndex)
                p.StageIndex--;
        }
    }

    private void RemoveParticipantRow(JobParticipantEditRow? row)
    {
        if (row == null || !EditParticipants.Contains(row)) return;
        var un = row.UserName?.Trim();
        EditParticipants.Remove(row);
        if (string.IsNullOrEmpty(un)) return;
        foreach (var p in EditPlans.Where(x => string.Equals(x.UserName, un, StringComparison.OrdinalIgnoreCase)).ToList())
            EditPlans.Remove(p);
    }

    private void AddPlanRow()
    {
        var idx = EditStages.Count > 0 ? 0 : 0;
        EditPlans.Add(new JobPlanEditRow { StageIndex = idx, PlannedHours = 0 });
    }

    private void RemovePlanRow(JobPlanEditRow? row)
    {
        if (row != null && EditPlans.Contains(row))
            EditPlans.Remove(row);
    }

    protected virtual bool ShouldIncludeJobInList(Job job)
    {
        if (IsFullJobAdmin) return true;
        var me = _authService.CurrentUser?.UserName;
        return !string.IsNullOrEmpty(me) &&
               string.Equals(job.ProjectManagerUserName, me, StringComparison.OrdinalIgnoreCase);
    }

    protected Job EnrichJobForList(Job job)
    {
        var pm = job.ProjectManagerUserName?.Trim() ?? "";
        if (string.IsNullOrEmpty(pm))
        {
            job.ProjectManagerDisplay = "";
            return job;
        }

        var src = UserOptions.FirstOrDefault(x => string.Equals(x.UserName, pm, StringComparison.OrdinalIgnoreCase));
        job.ProjectManagerDisplay = string.IsNullOrWhiteSpace(src?.DisplayName) ? pm : src.DisplayName;
        return job;
    }

    public virtual void Refresh(bool preserveSelection = true)
    {
        var prevId = preserveSelection ? SelectedJob?.Id : null;
        Jobs.Clear();
        try
        {
            foreach (var j in _api.GetJobs(activeOnly: false))
            {
                if (!ShouldIncludeJobInList(j)) continue;
                Jobs.Add(EnrichJobForList(j));
            }
            LoadUserOptions();
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = "İş listesi alınamadı. " + ex.Message;
        }

        if (prevId.HasValue)
            SelectedJob = Jobs.FirstOrDefault(j => j.Id == prevId.Value);
    }

    public void RefreshOnNavigate()
    {
        SelectedJob = null;
        Refresh(preserveSelection: false);
    }

    private void AddJob()
    {
        var code = NewCode.Trim();
        if (string.IsNullOrEmpty(code))
        {
            StatusMessage = "İş kodu girin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedProjectManagerUserName))
        {
            StatusMessage = "Proje müdürü seçin.";
            return;
        }

        var detail = BuildJobDetailForSave(Guid.Empty, true);
        var (success, error) = _api.AddJobDetail(detail);
        if (success)
        {
            ClearEditors();
            Refresh();
            if (detail.Id != Guid.Empty)
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == detail.Id);
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

        if (IsFullJobAdmin && string.IsNullOrWhiteSpace(SelectedProjectManagerUserName))
        {
            StatusMessage = "Proje müdürü seçin.";
            return;
        }

        var detail = BuildJobDetailForSave(SelectedJob.Id, SelectedJob.IsActive);
        var (success, error) = _api.UpdateJobDetail(detail);
        if (success)
        {
            Refresh();
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
            SelectedJob = null;
            Refresh();
            StatusMessage = "İş silindi.";
        }
        else
            StatusMessage = "Silinemedi.";
    }

    private void CloseOrReopenSelected()
    {
        if (SelectedJob == null) return;
        var job = SelectedJob;
        var d = _api.GetJobDetail(job.Id);
        if (d == null)
        {
            StatusMessage = "İş bilgisi alınamadı.";
            return;
        }

        d.IsActive = !d.IsActive;
        var (success, error) = _api.UpdateJobDetail(d);
        if (success)
        {
            Refresh();
            StatusMessage = d.IsActive ? "İş tekrar açıldı." : "İş kapatıldı. Çalışanlar bu işi artık seçemez.";
        }
        else
            StatusMessage = error ?? "Güncellenemedi.";
    }
}
