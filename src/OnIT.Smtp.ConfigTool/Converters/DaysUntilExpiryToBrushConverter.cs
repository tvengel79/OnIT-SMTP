using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OnIT.Smtp.ConfigTool.Converters;

/// <summary>Color-codes a nullable days-until-expiry value: green (healthy) through red (expired), gray when unknown.</summary>
public sealed class DaysUntilExpiryToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int days) return Brushes.Gray;

        return days switch
        {
            <= 0 => Brushes.Firebrick,
            <= 7 => Brushes.OrangeRed,
            <= 30 => Brushes.DarkOrange,
            _ => Brushes.SeaGreen
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
