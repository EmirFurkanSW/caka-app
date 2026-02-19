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

    public PersonelAddWorkViewModel(IAuthService authService, IWorkLogService workLogService)
    {
        _authService = authService;
        _workLogService = workLogService;
        Entries = new ObservableCollection<WorkEntryRow>();
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
        AddRow(); // Başlangıçta bir satır
    }

    /// <summary>
    /// Haftalık periyot: Pazartesi 00:00 - Pazar 23:59 (içinde bulunulan hafta).
    /// </summary>
    private static (DateTime WeekStart, DateTime WeekEnd) GetCurrentWeekRange()
    {
        var today = DateTime.Today;
        // .NET: Pazar=0, Pazartesi=1, ..., Cumartesi=6
        var daysToMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var weekStart = today.AddDays(-daysToMonday);
        var weekEnd = weekStart.AddDays(6); // Pazar
        return (weekStart, weekEnd);
    }

    private readonly IAuthService _authService;
    private readonly IWorkLogService _workLogService;

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value);
    }

    /// <summary>Bu haftanın pazartesi günü (tarih).</summary>
    public DateTime WeekStart { get; }

    /// <summary>Bu haftanın pazar günü (tarih).</summary>
    public DateTime WeekEnd { get; }

    /// <summary>Örn. "10.02.2026 - 16.02.2026"</summary>
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

    private void AddRow()
    {
        Entries.Add(new WorkEntryRow { Hours = null });
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
            .Where(e => !string.IsNullOrWhiteSpace(e.Description) && e.Hours.HasValue && e.Hours.Value >= 0 && e.Hours.Value <= 24)
            .ToList();

        if (validRows.Count == 0)
        {
            StatusMessage = "En az bir satırda açıklama girin ve saat 0–24 arasında olsun.";
            return;
        }

        try
        {
            foreach (var row in validRows)
            {
                _workLogService.Add(new WorkLog
                {
                    Date = date,
                    Description = row.Description.Trim(),
                    Hours = row.Hours!.Value,
                    UserName = _authService.CurrentUser?.UserName
                });
            }

            // Doldurulmuş satırları temizle (kaydedilenleri kaldır)
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
