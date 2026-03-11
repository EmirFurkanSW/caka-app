using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class AdminReportsViewModel : ViewModelBase
{
    public AdminReportsViewModel(IWorkLogService workLogService, IUserStore userStore, IReportPdfService reportPdfService, IReportExcelService reportExcelService, BackendApiClient api)
    {
        _workLogService = workLogService;
        _userStore = userStore;
        _reportPdfService = reportPdfService;
        _reportExcelService = reportExcelService;
        _api = api;
        WeekGroups = new ObservableCollection<WeekWorkLogGroup>();
        AllUsers = new ObservableCollection<StoredUser>();
        Jobs = new ObservableCollection<Job>();
        RefreshCommand = new RelayCommand(_ => Refresh());
        ExportJobPerformanceCommand = new RelayCommand(_ => ExportJobPerformance(), _ => SelectedJob != null);
        ExportWeekToPdfCommand = new RelayCommand(param =>
        {
            if (param is WeekWorkLogGroup group)
                ExportWeekToPdf(group);
        });
        ExportAllWeeksToPdfCommand = new RelayCommand(_ => ExportAllWeeksToPdf());
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
        Refresh();
    }

    private readonly IWorkLogService _workLogService;
    private readonly IUserStore _userStore;
    private readonly IReportPdfService _reportPdfService;
    private readonly IReportExcelService _reportExcelService;
    private readonly BackendApiClient _api;
    private Job? _selectedJob;

    public ObservableCollection<WeekWorkLogGroup> WeekGroups { get; }
    public ObservableCollection<StoredUser> AllUsers { get; }
    public ObservableCollection<Job> Jobs { get; }
    public Job? SelectedJob
    {
        get => _selectedJob;
        set => SetProperty(ref _selectedJob, value);
    }
    public ICommand RefreshCommand { get; }
    public ICommand ExportWeekToPdfCommand { get; }
    public ICommand ExportAllWeeksToPdfCommand { get; }
    public ICommand ExportWeekToExcelCommand { get; }
    public ICommand ExportAllWeeksToExcelCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand ExportJobPerformanceCommand { get; }

    private static DateTime GetMonday(DateTime date)
    {
        var d = date.Date;
        var daysToMonday = d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1;
        return d.AddDays(-daysToMonday);
    }

    private void Refresh()
    {
        AllUsers.Clear();
        AllUsers.Add(new StoredUser { UserName = "", DisplayName = "Tüm kullanıcılar" });
        foreach (var u in _userStore.GetAll())
            AllUsers.Add(u);

        Jobs.Clear();
        try
        {
            foreach (var j in _api.GetJobs(activeOnly: false))
                Jobs.Add(j);
        }
        catch { /* API eski olabilir */ }

        WeekGroups.Clear();
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

    private void ExportWeekToPdf(WeekWorkLogGroup group)
    {
        var defaultName = $"Rapor_{group.WeekStart:dd.MM.yyyy}-{group.WeekEnd:dd.MM.yyyy}.pdf";
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF dosyası|*.pdf",
            DefaultExt = ".pdf",
            FileName = defaultName
        };
        if (dlg.ShowDialog() != true) return;
        var userNameToDisplay = _userStore.GetAll().ToDictionary(u => u.UserName, u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName);
        _reportPdfService.GenerateWeekReport(dlg.FileName, group.WeekStart, group.WeekEnd, group.Entries.ToList(), userNameToDisplay);
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
        _reportExcelService.GenerateWeekReport(dlg.FileName, group.WeekStart, group.WeekEnd, group.Entries.ToList(), userNameToDisplay);
    }

    /// <summary>
    /// Kullanıcı bir klasör seçer; tüm haftaların PDF'leri o klasöre, her biri kendi hafta adıyla (Rapor_dd.MM.yyyy-dd.MM.yyyy.pdf) kaydedilir.
    /// </summary>
    private void ExportAllWeeksToPdf()
    {
        if (WeekGroups.Count == 0)
        {
            MessageBox.Show("Rapor oluşturmak için en az bir hafta verisi olmalı.", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var first = WeekGroups[0];
        var defaultName = $"Rapor_{first.WeekStart:dd.MM.yyyy}-{first.WeekEnd:dd.MM.yyyy}.pdf";
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Tüm hafta PDF'lerinin kaydedileceği klasörü seçin (dosya adı yalnızca klasör için kullanılır)",
            Filter = "PDF dosyası|*.pdf",
            DefaultExt = ".pdf",
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
            var filePath = System.IO.Path.Combine(folder, $"Rapor_{group.WeekStart:dd.MM.yyyy}-{group.WeekEnd:dd.MM.yyyy}.pdf");
            _reportPdfService.GenerateWeekReport(filePath, group.WeekStart, group.WeekEnd, group.Entries.ToList(), userNameToDisplay);
            count++;
        }

        MessageBox.Show($"{count} adet haftalık PDF seçilen klasöre kaydedildi.\n\nKlasör: {folder}", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
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
            _reportExcelService.GenerateWeekReport(filePath, group.WeekStart, group.WeekEnd, group.Entries.ToList(), userNameToDisplay);
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
        var allLogs = _workLogService.GetAll();
        var byUser = allLogs
            .Where(l => l.JobId == job.Id)
            .GroupBy(l => l.UserName ?? "")
            .Select(g => (UserName: g.Key, TotalHours: g.Sum(x => x.Hours)))
            .Where(x => x.TotalHours > 0)
            .ToList();
        var userNameToDisplay = _userStore.GetAll().ToDictionary(u => u.UserName, u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName);
        var rows = byUser
            .Select(x => (x.UserName, DisplayName: userNameToDisplay.GetValueOrDefault(x.UserName, x.UserName) ?? x.UserName, x.TotalHours))
            .ToList();

        var suggestedName = SanitizeFileName($"{job.Code} - {job.Description}.xlsx");
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel dosyası|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = suggestedName
        };
        if (dlg.ShowDialog() != true) return;

        _reportExcelService.GenerateJobPerformanceReport(dlg.FileName, job.Code, job.Description, rows);
        MessageBox.Show($"Excel dosyası kaydedildi.\n\n{dlg.FileName}", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
