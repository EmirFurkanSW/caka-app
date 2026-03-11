using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CAKA.PerformanceApp.Models;

/// <summary>
/// İş Ekle formunda tek satır: seçilen iş (admin tanımlı) + saat.
/// </summary>
public class WorkEntryRow : INotifyPropertyChanged
{
    private Job? _selectedJob;
    private decimal? _hours;

    public Job? SelectedJob
    {
        get => _selectedJob;
        set { _selectedJob = value; OnPropertyChanged(); }
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
