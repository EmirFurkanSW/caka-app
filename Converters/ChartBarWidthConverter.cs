using System.Globalization;
using System.Windows.Data;

namespace CAKA.PerformanceApp.Converters;

/// <summary>
/// Chart bar genişliği için (double değer -> pixel genişlik, max ~40 için ~320px).
/// </summary>
public class ChartBarWidthConverter : IValueConverter
{
    private const double Scale = 8;
    private const double MinWidth = 24;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && d > 0)
            return Math.Max(MinWidth, d * Scale);
        return MinWidth;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
