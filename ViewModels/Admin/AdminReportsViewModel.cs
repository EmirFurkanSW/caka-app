using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class AdminReportsViewModel : ViewModelBase
{
    public AdminReportsViewModel(IWorkLogService workLogService, IUserStore userStore, IReportPdfService reportPdfService)
    {
        _workLogService = workLogService;
        _userStore = userStore;
        _reportPdfService = reportPdfService;
        WeekGroups = new ObservableCollection<WeekWorkLogGroup>();
        AllUsers = new ObservableCollection<StoredUser>();
        RefreshCommand = new RelayCommand(_ => Refresh());
        ExportWeekToPdfCommand = new RelayCommand(param =>
        {
            if (param is WeekWorkLogGroup group)
                ExportWeekToPdf(group);
        });
        ExportAllWeeksToPdfCommand = new RelayCommand(_ => ExportAllWeeksToPdf());
        Refresh();
    }

    private readonly IWorkLogService _workLogService;
    private readonly IUserStore _userStore;
    private readonly IReportPdfService _reportPdfService;

    public ObservableCollection<WeekWorkLogGroup> WeekGroups { get; }
    public ObservableCollection<StoredUser> AllUsers { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ExportWeekToPdfCommand { get; }
    public ICommand ExportAllWeeksToPdfCommand { get; }

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
}
