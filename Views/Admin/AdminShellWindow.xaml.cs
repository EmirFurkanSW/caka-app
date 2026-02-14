using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CAKA.PerformanceApp.ViewModels.Admin;

namespace CAKA.PerformanceApp.Views.Admin;

public partial class AdminShellWindow : Window
{
    public AdminShellWindow(AdminShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => AddHandler(MouseWheelEvent, (MouseWheelEventHandler)OnMouseWheel, handledEventsToo: true);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (!IsUnder(source, ContentScrollViewer)) return;

        var offset = Math.Max(0, Math.Min(ContentScrollViewer.VerticalOffset - e.Delta, ContentScrollViewer.ScrollableHeight));
        ContentScrollViewer.ScrollToVerticalOffset(offset);
    }

    private static bool IsUnder(DependencyObject element, DependencyObject ancestor)
    {
        while (element != null)
        {
            if (element == ancestor) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }
}
