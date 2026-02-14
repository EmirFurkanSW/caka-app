using System.Windows;
using System.Windows.Threading;

namespace CAKA.PerformanceApp;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Beklenmeyen hata:\n\n" + e.Exception.Message + "\n\n" + e.Exception.StackTrace,
            "CAKA - Hata",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        try
        {
            ServiceProvider = AppBootstrapper.ConfigureServices();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Servisler yüklenirken hata:\n\n" + ex.Message + "\n\n" + (ex.InnerException?.Message ?? "") + "\n\n" + ex.StackTrace,
                "CAKA - Başlatma Hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        try
        {
            var loginWindow = ServiceProvider.GetService(typeof(Views.LoginWindow)) as Window;
            if (loginWindow == null)
            {
                MessageBox.Show("Giriş penceresi oluşturulamadı.", "CAKA - Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }
            loginWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Uygulama başlatılırken hata:\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                "CAKA - Başlatma Hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
