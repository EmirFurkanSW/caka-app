using System.Globalization;
using System.Windows.Data;

namespace CAKA.PerformanceApp.Converters;

/// <summary>
/// TextBox <-> decimal (Hours) için iki yönlü dönüştürücü.
/// </summary>
public class DecimalToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return "";
        if (value is decimal dec)
            return dec == Math.Floor(dec) ? ((int)dec).ToString(culture) : dec.ToString("0.##", culture);
        return "";
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return targetType == typeof(decimal?) ? null : (object)0m;
            if (decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, culture, out var result))
                return result;
        }
        return targetType == typeof(decimal?) ? null : (object)0m;
    }
}
