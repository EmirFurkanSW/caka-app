using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CAKA.PerformanceApp.Models;

/// <summary>
/// Bir haftalık tarih aralığı, o haftadaki tüm iş kayıtları ve seçilen kullanıcıya göre filtrelenmiş liste.
/// </summary>
public class WeekWorkLogGroup : INotifyPropertyChanged
{
    private string? _selectedUserName;

    public WeekWorkLogGroup()
    {
        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EntriesTotalHours));
    }

    public DateTime WeekStart { get; init; }
    public DateTime WeekEnd { get; init; }

    /// <summary>Bu hafta ise kullanıcı kayıtları düzenleyebilir.</summary>
    public bool IsCurrentWeek { get; init; }

    public string DisplayTitle => $"{WeekStart:dd.MM.yyyy} - {WeekEnd:dd.MM.yyyy}";

    public ObservableCollection<WorkLog> Entries { get; } = new();

    /// <summary>Seçilen kullanıcıya göre filtrelenmiş kayıtlar (null = tümü).</summary>
    public ObservableCollection<WorkLog> FilteredEntries { get; } = new();

    /// <summary>Filtrelenmiş kayıtların toplam çalışma saati (seçilen kullanıcı veya tümü). Admin raporlar için.</summary>
    public decimal TotalHours => FilteredEntries.Sum(e => e.Hours);

    /// <summary>Entries toplam saati. Personel geçmiş sayfasında seçilen haftanın toplamı için.</summary>
    public decimal EntriesTotalHours => Entries.Sum(e => e.Hours);

    public string? SelectedUserName
    {
        get => _selectedUserName;
        set
        {
            if (_selectedUserName == value) return;
            _selectedUserName = value;
            OnPropertyChanged();
            RefreshFiltered();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void RefreshFiltered()
    {
        FilteredEntries.Clear();
        var list = string.IsNullOrWhiteSpace(SelectedUserName)
            ? Entries.OrderBy(e => e.Date).ThenBy(e => e.CreatedAt)
            : Entries.Where(e => string.Equals(e.UserName, SelectedUserName!.Trim(), StringComparison.OrdinalIgnoreCase))
                     .OrderBy(e => e.Date).ThenBy(e => e.CreatedAt);
        foreach (var log in list)
            FilteredEntries.Add(log);
        OnPropertyChanged(nameof(TotalHours));
    }
}
