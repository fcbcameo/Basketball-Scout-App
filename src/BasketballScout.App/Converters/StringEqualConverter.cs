using System.Globalization;

namespace BasketballScout.App.Converters;

/// <summary>
/// Returns true if the bound string value equals the ConverterParameter.
/// Used to highlight follow-up type ("assist" vs "rebound").
/// </summary>
public class StringEqualConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
