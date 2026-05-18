using CAKA.PerformanceApp.Core;

namespace CAKA.PerformanceApp.ViewModels.Admin;

public class CurrencyOption
{
    public string Code { get; set; } = "TRY";
    public string Label { get; set; } = "";
}

/// <summary>Aşama: sıra (yukarıdan aşağı) = Stage 1, 2, …; silince numaralar otomatik kayar.</summary>
public class JobStageEditRow : ViewModelBase
{
    private int _stageNumber = 1;
    private string _description = string.Empty;

    public JobStageEditRow()
    {
        _stageNumber = 1;
    }

    public int StageNumber
    {
        get => _stageNumber;
        set
        {
            var v = Math.Clamp(value, 1, 99);
            SetProperty(ref _stageNumber, v);
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value ?? string.Empty);
    }

    /// <summary>API ve liste için sabit isim.</summary>
    public string ResolvedName => $"Stage {StageNumber}";
}

public class JobParticipantEditRow : ViewModelBase
{
    private string _userName = string.Empty;
    private decimal _hourlyRate;
    private string _currency = "TRY";

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value ?? string.Empty);
    }

    public decimal HourlyRate
    {
        get => _hourlyRate;
        set => SetProperty(ref _hourlyRate, value);
    }

    /// <summary>TRY veya USD.</summary>
    public string Currency
    {
        get => _currency;
        set => SetProperty(ref _currency, string.IsNullOrWhiteSpace(value) ? "TRY" : value.Trim().ToUpperInvariant());
    }
}

public class JobPlanEditRow : ViewModelBase
{
    private int _stageIndex;
    private string _userName = string.Empty;
    private decimal _plannedHours;

    public int StageIndex
    {
        get => _stageIndex;
        set
        {
            if (SetProperty(ref _stageIndex, value))
                OnPropertyChanged(nameof(StagePickerValue));
        }
    }

    /// <summary>WPF ComboBox SelectedValue=0 hatasını önlemek için 1 tabanlı bağlama.</summary>
    public int StagePickerValue
    {
        get => StageIndex + 1;
        set => StageIndex = Math.Max(0, value - 1);
    }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value ?? string.Empty);
    }

    public decimal PlannedHours
    {
        get => _plannedHours;
        set => SetProperty(ref _plannedHours, value);
    }
}

public class StagePickItem
{
    /// <summary>ComboBox için 1 tabanlı aşama numarası (0 WPF SelectedValue hatasını önler).</summary>
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
}
