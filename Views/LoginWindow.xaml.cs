using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CAKA.PerformanceApp.ViewModels;

namespace CAKA.PerformanceApp.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            if (!string.IsNullOrEmpty(vm.Password))
                PasswordBox.Password = vm.Password;
        }

        try
        {
            var asmName = Assembly.GetExecutingAssembly().GetName().Name;
            var uri = new Uri($"pack://application:,,,/{asmName};component/Assets/Images/Logo.png", UriKind.Absolute);
            var bitmap = new BitmapImage(uri);
            void ShowLogo()
            {
                LogoImage.Visibility = Visibility.Visible;
                LogoFallbackText.Visibility = Visibility.Collapsed;
            }
            void ShowFallback()
            {
                LogoFallbackText.Visibility = Visibility.Visible;
                LogoImage.Visibility = Visibility.Collapsed;
            }
            bitmap.DownloadCompleted += (_, _) => Dispatcher.BeginInvoke(ShowLogo);
            bitmap.DecodeFailed += (_, _) => Dispatcher.BeginInvoke(ShowFallback);
            LogoImage.Source = bitmap;
            if (!bitmap.IsDownloading)
                ShowLogo();
        }
        catch
        {
            LogoFallbackText.Visibility = Visibility.Visible;
            LogoImage.Visibility = Visibility.Collapsed;
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
            vm.Password = pb.Password;
    }

    private void UserNameCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is not LoginViewModel vm || e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is string selectedUserName)
        {
            var password = vm.GetPasswordForUser(selectedUserName);
            if (password != null)
            {
                vm.Password = password;
                PasswordBox.Password = password;
            }
        }
    }
}
