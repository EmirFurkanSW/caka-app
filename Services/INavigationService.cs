namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Sayfa/ViewModel geçişleri için navigation servisi.
/// İleride frame veya region tabanlı genişletilebilir.
/// </summary>
public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo(object viewModel);
    object? CurrentViewModel { get; }
    event EventHandler? CurrentViewModelChanged;
}
