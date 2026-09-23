using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace AiBrowserWorkspace.Converters;

public sealed class EnumToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isChecked || !isChecked || parameter is null)
        {
            return BindingOperations.DoNothing;
        }

        return Enum.Parse(targetType, parameter.ToString()!);
    }
}
