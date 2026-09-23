using System.Globalization;
using Avalonia.Data.Converters;

namespace AiBrowserWorkspace.Converters;

// Stretches session cards to fill the left panel's available height (minus its padding)
// so a single row of cards matches the panel instead of sitting at a fixed 420px.
public sealed class ResponsiveCardHeightConverter : IValueConverter
{
    private const double MinHeight = 420;
    private const double ReservedChrome = 24; // ListBox top + bottom padding

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double rawHeight || rawHeight <= 0)
        {
            return MinHeight;
        }

        return Math.Max(MinHeight, rawHeight - ReservedChrome);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
