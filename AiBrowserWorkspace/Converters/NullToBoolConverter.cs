using System.Globalization;
using Avalonia.Data.Converters;

namespace AiBrowserWorkspace.Converters;

// Converts a DataContext (or any object) to a bool for IsVisible bindings.
// Pass ConverterParameter="Invert" to get true when the value IS null instead.
public sealed class NullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNotNull = value is not null;
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        return invert ? !isNotNull : isNotNull;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
