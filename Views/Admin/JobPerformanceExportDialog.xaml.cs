using System.Globalization;
using System.Windows;

namespace CAKA.PerformanceApp.Views.Admin;

public partial class JobPerformanceExportDialog : Window
{
    public decimal? TargetHoursPerEmployee { get; private set; }

    public JobPerformanceExportDialog()
    {
        InitializeComponent();
        TargetHoursBox.Text = "40";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var text = (TargetHoursBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(text))
        {
            MessageBox.Show("Maksimum hedef süre (saat) girin.", "CAKA", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var v) &&
            !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out v))
        {
            MessageBox.Show("Geçerli bir sayı girin (örn: 40 veya 37,5).", "CAKA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (v <= 0)
        {
            MessageBox.Show("Hedef süre 0'dan büyük olmalıdır.", "CAKA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TargetHoursPerEmployee = v;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
