using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Views.Admin;

public partial class AdminReportsView : UserControl
{
    public AdminReportsView() => InitializeComponent();

    private void ReportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid dg || dg.DataContext is not WeekWorkLogGroup group)
            return;
        group.SelectedForDelete.Clear();
        foreach (var item in dg.SelectedItems)
            if (item is WorkLog log)
                group.SelectedForDelete.Add(log);
    }
}
