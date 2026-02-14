using System.Windows;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// ViewModel tabanlı navigation. Window'ları açar veya içerik değiştirir.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private object? _currentViewModel;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (_currentViewModel == value) return;
            _currentViewModel = value;
            CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var vm = _serviceProvider.GetService(typeof(TViewModel)) as TViewModel;
        if (vm != null)
            NavigateTo(vm);
    }

    public void NavigateTo(object viewModel)
    {
        CurrentViewModel = viewModel;
    }
}
