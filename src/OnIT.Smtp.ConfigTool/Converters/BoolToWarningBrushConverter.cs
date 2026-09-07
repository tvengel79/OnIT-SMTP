using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OnIT.Smtp.ConfigTool.Converters;

/// <summary>True (needs attention) -> a warning color, false (fine) -> a healthy color.</summary>
public sealed class BoolToWarningBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Brushes.DarkOrange : Brushes.SeaGreen;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
