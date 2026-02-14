using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CAKA.PerformanceApp.ViewModels.Admin;

namespace CAKA.PerformanceApp.Views.Admin;

public partial class AdminEmployeesView : UserControl
{
    public AdminEmployeesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SubscribeToNewPassword();
    }

    private void SubscribeToNewPassword()
    {
        if (DataContext is INotifyPropertyChanged vm)
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AdminEmployeesViewModel.NewPassword) &&
                    DataContext is AdminEmployeesViewModel m && string.IsNullOrEmpty(m.NewPassword))
                    NewPasswordBox.Password = "";
            };
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminEmployeesViewModel vm && sender is PasswordBox pb)
            vm.NewPassword = pb.Password;
    }
}
