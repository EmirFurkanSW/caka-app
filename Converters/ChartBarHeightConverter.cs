using System.Globalization;
using System.Windows.Data;

namespace CAKA.PerformanceApp.Converters;

/// <summary>
/// Dikey çubuk grafik yüksekliği için (double değer -> pixel yükseklik, max ~40 için ~120px).
/// </summary>
public class ChartBarHeightConverter : IValueConverter
{
    private const double Scale = 3;
    private const double MinHeight = 8;
    private const double MaxHeight = 120;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && d > 0)
            return Math.Min(MaxHeight, Math.Max(MinHeight, d * Scale));
        return MinHeight;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
