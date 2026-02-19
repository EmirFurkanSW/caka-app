using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Personel;

public class PersonelHistoryViewModel : ViewModelBase
{
    public PersonelHistoryViewModel(IAuthService authService, IWorkLogService workLogService, IReportPdfService reportPdfService, IReportExcelService reportExcelService)
    {
        _authService = authService;
        _workLogService = workLogService;
        _reportPdfService = reportPdfService;
        _reportExcelService = reportExcelService;
        WeekGroups = new ObservableCollection<WeekWorkLogGroup>();
        _pendingDeleteIds = new List<Guid>();
        RefreshCommand = new RelayCommand(_ => Refresh());
        SaveWeekEditsCommand = new RelayCommand(param =>
        {
            if (param is WeekWorkLogGroup group)
                SaveWeekEdits(group);
        });
        DeleteEntryCommand = new RelayCommand(param =>
        {
            if (param is WorkLog log)
                DeleteEntry(log);
        });
        ExportWeekToPdfCommand = new RelayCommand(param =>
        {
            if (param is WeekWorkLogGroup group)
                ExportWeekToPdf(group);
        });
        ExportWeekToExcelCommand = new RelayCommand(param =>
        {
            if (param is WeekWorkLogGroup group)
                ExportWeekToExcel(group);
        });
        ExportAllWeeksToPdfCommand = new RelayCommand(_ => ExportAllWeeksToPdf());
        ExportAllWeeksToExcelCommand = new RelayCommand(_ => ExportAllWeeksToExcel());
        Refresh();
    }

    private readonly IAuthService _authService;
    private readonly IWorkLogService _workLogService;
    private readonly IReportPdfService _reportPdfService;
    private readonly IReportExcelService _reportExcelService;
    private readonly List<Guid> _pendingDeleteIds;

    public ObservableCollection<WeekWorkLogGroup> WeekGroups { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SaveWeekEditsCommand { get; }
    public ICommand DeleteEntryCommand { get; }
    public ICommand ExportWeekToPdfCommand { get; }
    public ICommand ExportWeekToExcelCommand { get; }
    public ICommand ExportAllWeeksToPdfCommand { get; }
    public ICommand ExportAllWeeksToExcelCommand { get; }

    private static DateTime GetMonday(DateTime date)
    {
        var d = date.Date;
        var daysToMonday = d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1;
        return d.AddDays(-daysToMonday);
    }

    public void Refresh()
    {
        WeekGroups.Clear();
        var userName = _authService.CurrentUser?.UserName;
        var list = _workLogService.GetByUser(userName)
            .Where(log => !_pendingDeleteIds.Contains(log.Id))
            .ToList();
        var byWeek = list
            .GroupBy(log => GetMonday(log.Date))
            .OrderByDescending(g => g.Key)
            .ToList();

        var today = DateTime.Today;
        var currentWeekStart = GetMonday(today);

        foreach (var group in byWeek)
        {
            var weekStart = group.Key;
            var weekEnd = weekStart.AddDays(6);
            var isCurrentWeek = (weekStart == currentWeekStart);
            var wg = new WeekWorkLogGroup { WeekStart = weekStart, WeekEnd = weekEnd, IsCurrentWeek = isCurrentWeek };
            foreach (var log in group.OrderBy(l => l.Date).ThenBy(l => l.CreatedAt))
                wg.Entries.Add(log);
            WeekGroups.Add(wg);
        }
    }

    private void DeleteEntry(WorkLog log)
    {
        var currentWeekGroup = WeekGroups.FirstOrDefault(g => g.IsCurrentWeek);
        if (currentWeekGroup == null) return;
        var found = currentWeekGroup.Entries.FirstOrDefault(e => e.Id == log.Id);
        if (found == null) return;
        currentWeekGroup.Entries.Remove(found);
        _pendingDeleteIds.Add(log.Id);
    }

    private void SaveWeekEdits(WeekWorkLogGroup group)
    {
        if (!group.IsCurrentWeek) return;
        try
        {
            foreach (var log in group.Entries)
            {
                if (log.Hours < 0 || log.Hours > 24) continue;
                _workLogService.Update(log);
            }
            foreach (var id in _pendingDeleteIds)
                _workLogService.Delete(id);
            _pendingDeleteIds.Clear();
            Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kayıt güncellenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private Dictionary<string, string> GetCurrentUserDisplayMap()
    {
        var u = _authService.CurrentUser;
        var name = u?.UserName ?? "";
        var display = string.IsNullOrWhiteSpace(u?.DisplayName) ? name : u.DisplayName;
        return new Dictionary<string, string> { { name, display } };
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
        var userNameToDisplay = GetCurrentUserDisplayMap();
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
        var userNameToDisplay = GetCurrentUserDisplayMap();
        _reportExcelService.GenerateWeekReport(dlg.FileName, group.WeekStart, group.WeekEnd, group.Entries.ToList(), userNameToDisplay);
    }

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
            Title = "Tüm hafta PDF'lerinin kaydedileceği klasörü seçin",
            Filter = "PDF dosyası|*.pdf",
            DefaultExt = ".pdf",
            FileName = defaultName
        };
        if (dlg.ShowDialog() != true) return;
        var folder = System.IO.Path.GetDirectoryName(dlg.FileName);
        if (string.IsNullOrEmpty(folder)) return;
        var userNameToDisplay = GetCurrentUserDisplayMap();
        var count = 0;
        foreach (var group in WeekGroups)
        {
            var filePath = System.IO.Path.Combine(folder, $"Rapor_{group.WeekStart:dd.MM.yyyy}-{group.WeekEnd:dd.MM.yyyy}.pdf");
            _reportPdfService.GenerateWeekReport(filePath, group.WeekStart, group.WeekEnd, group.Entries.ToList(), userNameToDisplay);
            count++;
        }
        MessageBox.Show($"{count} adet haftalık PDF kaydedildi.\n\nKlasör: {folder}", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
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
            Title = "Tüm hafta Excel dosyalarının kaydedileceği klasörü seçin",
            Filter = "Excel dosyası|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = defaultName
        };
        if (dlg.ShowDialog() != true) return;
        var folder = System.IO.Path.GetDirectoryName(dlg.FileName);
        if (string.IsNullOrEmpty(folder)) return;
        var userNameToDisplay = GetCurrentUserDisplayMap();
        var count = 0;
        foreach (var group in WeekGroups)
        {
            var filePath = System.IO.Path.Combine(folder, $"Rapor_{group.WeekStart:dd.MM.yyyy}-{group.WeekEnd:dd.MM.yyyy}.xlsx");
            _reportExcelService.GenerateWeekReport(filePath, group.WeekStart, group.WeekEnd, group.Entries.ToList(), userNameToDisplay);
            count++;
        }
        MessageBox.Show($"{count} adet haftalık Excel kaydedildi.\n\nKlasör: {folder}", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
