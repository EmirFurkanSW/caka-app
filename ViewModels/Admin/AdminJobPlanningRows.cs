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
        set => SetProperty(ref _stageIndex, value);
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
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
}
