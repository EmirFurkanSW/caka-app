using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class AdminReportsViewModel : ViewModelBase, INavigationRefresh
{
    public AdminReportsViewModel(IWorkLogService workLogService, IUserStore userStore, IReportExcelService reportExcelService, BackendApiClient api)
    {
        _workLogService = workLogService;
        _userStore = userStore;
        _reportExcelService = reportExcelService;
        _api = api;
        WeekGroups = new ObservableCollection<WeekWorkLogGroup>();
        AllUsers = new ObservableCollection<StoredUser>();
        Jobs = new ObservableCollection<Job>();
        FilteredJobs = new ObservableCollection<Job>();
        RefreshCommand = new RelayCommand(_ => Refresh());
        ExportJobPerformanceCommand = new RelayCommand(_ => ExportJobPerformance(), _ => SelectedJob != null);
        ExportWeekToExcelCommand = new RelayCommand(param =>
        {
            if (param is WeekWorkLogGroup group)
                ExportWeekToExcel(group);
        });
        ExportAllWeeksToExcelCommand = new RelayCommand(_ => ExportAllWeeksToExcel());
        DeleteSelectedCommand = new RelayCommand(param =>
        {
            if (param is WeekWorkLogGroup group)
                DeleteSelected(group);
        });
    }

    private readonly IWorkLogService _workLogService;
    private readonly IUserStore _userStore;
    private readonly IReportExcelService _reportExcelService;
    private readonly BackendApiClient _api;
    private Job? _selectedJob;
    private string _jobFilterText = "";

    private static readonly CompareInfo TurkishCompare = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    public ObservableCollection<WeekWorkLogGroup> WeekGroups { get; }
    public ObservableCollection<StoredUser> AllUsers { get; }
    public ObservableCollection<Job> Jobs { get; }
    public ObservableCollection<Job> FilteredJobs { get; }

    /// <summary>İş kodu ve/veya açıklama üzerinden tablo filtresi (canlı).</summary>
    public string JobFilterText
    {
        get => _jobFilterText;
        set
        {
            if (SetProperty(ref _jobFilterText, value ?? ""))
                ApplyJobFilter();
        }
    }
    public Job? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }
    public ICommand RefreshCommand { get; }
    public ICommand ExportWeekToExcelCommand { get; }
    public ICommand ExportAllWeeksToExcelCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand ExportJobPerformanceCommand { get; }

    private bool JobMatchesFilter(Job job)
    {
        var q = (JobFilterText ?? "").Trim();
        if (q.Length == 0)
            return true;
        static int Idx(CompareInfo cmp, string? s, string needle) =>
            cmp.IndexOf(string.IsNullOrEmpty(s) ? "" : s, needle, CompareOptions.IgnoreCase);

        return Idx(TurkishCompare, job.Code, q) >= 0 || Idx(TurkishCompare, job.Description, q) >= 0;
    }

    private void ApplyJobFilter(Guid? preserveSelectionId = null)
    {
        var preserveId = preserveSelectionId ?? SelectedJob?.Id;
        FilteredJobs.Clear();
        foreach (var job in Jobs)
        {
            if (JobMatchesFilter(job))
                FilteredJobs.Add(job);
        }
        SelectedJob = preserveId.HasValue
            ? FilteredJobs.FirstOrDefault(j => j.Id == preserveId.Value)
            : null;
        CommandManager.InvalidateRequerySuggested();
    }

    private static DateTime GetMonday(DateTime date)
    {
        var d = date.Date;
        var daysToMonday = d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1;
        return d.AddDays(-daysToMonday);
    }

    public void Refresh(bool resetJobFilters = false)
    {
        AllUsers.Clear();
        AllUsers.Add(new StoredUser { UserName = "", DisplayName = "Tüm kullanıcılar" });
        try
        {
            foreach (var u in _userStore.GetAll())
                AllUsers.Add(u);
        }
        catch
        {
            /* api/users reddederse veya bağlantı yoksa giriş yine yapılabilsin */
        }

        var preserveJobId = resetJobFilters ? null : SelectedJob?.Id;
        if (resetJobFilters && _jobFilterText.Length > 0)
        {
            _jobFilterText = "";
            OnPropertyChanged(nameof(JobFilterText));
        }
        SelectedJob = null;
        Jobs.Clear();
        try
        {
            foreach (var j in _api.GetJobs(activeOnly: false))
                Jobs.Add(j);
        }
        catch { /* API eski olabilir */ }

        ApplyJobFilter(preserveJobId);

        WeekGroups.Clear();
        try
        {
            var all = _workLogService.GetAll();
            var byWeek = all
                .GroupBy(log => GetMonday(log.Date))
                .OrderByDescending(g => g.Key)
                .ToList();

            foreach (var group in byWeek)
            {
                var weekStart = group.Key;
                var weekEnd = weekStart.AddDays(6);
                var wg = new WeekWorkLogGroup { WeekStart = weekStart, WeekEnd = weekEnd, SelectedUserName = "" };
                foreach (var log in group.OrderBy(l => l.Date).ThenBy(l => l.CreatedAt))
                    wg.Entries.Add(log);
                wg.RefreshFiltered();
                WeekGroups.Add(wg);
            }
        }
        catch
        {
            /* worklogs/all yoksa raporlar sekmesi boş kalır; uygulama çökmez */
        }
    }

    private void ExportWeekToExcel(WeekWorkLogGroup group)
    {
        var defaultName = $"Rapor_{group.WeekStart:dd.MM.yyyy}-{group.WeekEnd:dd.MM.yyyy}.xlsx";
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel dosyası|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = defaultName
        };
        if (dlg.ShowDialog() != true) return;
        var userNameToDisplay = _userStore.GetAll().ToDictionary(u => u.UserName, u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName);
        var lookups = WeekExcelLookupBuilder.Build(group.Entries.ToList(), _api);
        _reportExcelService.GenerateWeekReport(dlg.FileName, group.WeekStart, group.WeekEnd, group.Entries.ToList(),
            userNameToDisplay, lookups);
        MessageBox.Show($"Excel dosyası kaydedildi.\n\n{dlg.FileName}", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportAllWeeksToExcel()
    {
        if (WeekGroups.Count == 0)
        {
            MessageBox.Show("Rapor oluşturmak için en az bir hafta verisi olmalı.", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var first = WeekGroups[0];
        var defaultName = $"Rapor_{first.WeekStart:dd.MM.yyyy}-{first.WeekEnd:dd.MM.yyyy}.xlsx";
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Tüm hafta Excel dosyalarının kaydedileceği klasörü seçin (dosya adı yalnızca klasör için kullanılır)",
            Filter = "Excel dosyası|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = defaultName
        };
        if (dlg.ShowDialog() != true) return;

        var folder = System.IO.Path.GetDirectoryName(dlg.FileName);
        if (string.IsNullOrEmpty(folder))
            return;

        var userNameToDisplay = _userStore.GetAll().ToDictionary(u => u.UserName, u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName);
        var count = 0;
        foreach (var group in WeekGroups)
        {
            var filePath = System.IO.Path.Combine(folder, $"Rapor_{group.WeekStart:dd.MM.yyyy}-{group.WeekEnd:dd.MM.yyyy}.xlsx");
            var lookups = WeekExcelLookupBuilder.Build(group.Entries.ToList(), _api);
            _reportExcelService.GenerateWeekReport(filePath, group.WeekStart, group.WeekEnd, group.Entries.ToList(),
                userNameToDisplay, lookups);
            count++;
        }

        MessageBox.Show($"{count} adet haftalık Excel seçilen klasöre kaydedildi.\n\nKlasör: {folder}", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteSelected(WeekWorkLogGroup group)
    {
        if (group.SelectedForDelete.Count == 0)
        {
            MessageBox.Show("Silmek için önce listeden bir veya daha fazla iş kaydı seçin.", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(
                $"{group.SelectedForDelete.Count} adet iş kaydını silmek istediğinize emin misiniz? Bu işlem geri alınamaz.",
                "İş kayıtlarını sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        var toDelete = group.SelectedForDelete.ToList();
        foreach (var log in toDelete)
            _workLogService.Delete(log.Id);
        Refresh();
        MessageBox.Show($"{toDelete.Count} adet iş kaydı silindi.", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "Rapor" : name.Trim();
    }

    private void ExportJobPerformance()
    {
        if (SelectedJob == null) return;
        var job = SelectedJob;
        var jobLogs = _workLogService.GetAll()
            .Where(l => l.JobId == job.Id)
            .ToList();

        var userNameToDisplay = _userStore.GetAll().ToDictionary(u => u.UserName, u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName);

        var allColumnUsers = _userStore.GetAll()
            .Where(u => !string.IsNullOrWhiteSpace(u.UserName))
            .OrderBy(u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(u => u.UserName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var suggestedName = SanitizeFileName($"{job.Code} - {job.Description}.xlsx");
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel dosyası|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = suggestedName
        };
        if (dlg.ShowDialog() != true) return;

        JobDetail? jobDetail = null;
        try
        {
            jobDetail = _api.GetJobDetail(job.Id);
        }
        catch
        {
            /* Detay alınamazsa rapor yine üretilir */
        }

        _reportExcelService.GenerateJobPerformanceReport(
            dlg.FileName,
            job.Code,
            job.Description,
            jobLogs,
            userNameToDisplay,
            jobDetail,
            allColumnUsers,
            job.Id);
        MessageBox.Show($"Excel dosyası kaydedildi.\n\n{dlg.FileName}", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void RefreshOnNavigate() => Refresh(resetJobFilters: true);
}
