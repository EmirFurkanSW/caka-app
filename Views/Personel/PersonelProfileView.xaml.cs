using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CAKA.PerformanceApp.ViewModels.Personel;

namespace CAKA.PerformanceApp.Views.Personel;

public partial class PersonelProfileView : UserControl
{
    public PersonelProfileView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SubscribeToPasswordClear();
    }

    private void SubscribeToPasswordClear()
    {
        if (DataContext is INotifyPropertyChanged vm)
            vm.PropertyChanged += (_, e) =>
            {
                if (DataContext is not PersonelProfileViewModel m) return;
                if (e.PropertyName == nameof(PersonelProfileViewModel.CurrentPassword) && string.IsNullOrEmpty(m.CurrentPassword))
                    CurrentPasswordBox.Password = "";
                if (e.PropertyName == nameof(PersonelProfileViewModel.NewPassword) && string.IsNullOrEmpty(m.NewPassword))
                    NewPasswordBox.Password = "";
                if (e.PropertyName == nameof(PersonelProfileViewModel.ConfirmPassword) && string.IsNullOrEmpty(m.ConfirmPassword))
                    ConfirmPasswordBox.Password = "";
            };
    }

    private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is PersonelProfileViewModel vm && sender is PasswordBox pb)
            vm.CurrentPassword = pb.Password;
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is PersonelProfileViewModel vm && sender is PasswordBox pb)
            vm.NewPassword = pb.Password;
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is PersonelProfileViewModel vm && sender is PasswordBox pb)
            vm.ConfirmPassword = pb.Password;
    }
}
