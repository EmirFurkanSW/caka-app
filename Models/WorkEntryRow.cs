using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CAKA.PerformanceApp.Models;

/// <summary>
/// İş Ekle formunda tek satır: açıklama + saat.
/// </summary>
public class WorkEntryRow : INotifyPropertyChanged
{
    private string _description = string.Empty;
    private decimal? _hours;

    public string Description
    {
        get => _description;
        set { _description = value ?? ""; OnPropertyChanged(); }
    }

    public decimal? Hours
    {
        get => _hours;
        set { _hours = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
