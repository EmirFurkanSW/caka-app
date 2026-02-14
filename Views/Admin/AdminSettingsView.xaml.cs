using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CAKA.PerformanceApp.ViewModels.Admin;

namespace CAKA.PerformanceApp.Views.Admin;

public partial class AdminSettingsView : UserControl
{
    public AdminSettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SubscribeToPasswordClear();
    }

    private void SubscribeToPasswordClear()
    {
        if (DataContext is INotifyPropertyChanged vm)
            vm.PropertyChanged += (_, e) =>
            {
                if (DataContext is not AdminSettingsViewModel m) return;
                if (e.PropertyName == nameof(AdminSettingsViewModel.CurrentPassword) && string.IsNullOrEmpty(m.CurrentPassword))
                    CurrentPasswordBox.Password = "";
                if (e.PropertyName == nameof(AdminSettingsViewModel.NewPassword) && string.IsNullOrEmpty(m.NewPassword))
                    NewPasswordBox.Password = "";
                if (e.PropertyName == nameof(AdminSettingsViewModel.ConfirmPassword) && string.IsNullOrEmpty(m.ConfirmPassword))
                    ConfirmPasswordBox.Password = "";
            };
    }

    private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminSettingsViewModel vm && sender is PasswordBox pb)
            vm.CurrentPassword = pb.Password;
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminSettingsViewModel vm && sender is PasswordBox pb)
            vm.NewPassword = pb.Password;
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminSettingsViewModel vm && sender is PasswordBox pb)
            vm.ConfirmPassword = pb.Password;
    }
}
