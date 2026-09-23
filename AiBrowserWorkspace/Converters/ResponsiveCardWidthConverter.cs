using System.Globalization;
using Avalonia.Data.Converters;

namespace AiBrowserWorkspace.Converters;

// Computes an even column width for the session WrapPanel: as many columns as fit at
// MinWidth get an equal share of the available space, so 1-2 cards fill the workspace
// (matching the panel width) instead of floating in empty space, while many cards still
// wrap and scroll at a comfortable minimum size instead of shrinking indefinitely.
public sealed class ResponsiveCardWidthConverter : IMultiValueConverter
{
    private const double MinWidth = 340;
    private const double ReservedChrome = 40; // ListBox padding + potential scrollbar width

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not double rawWidth || values[1] is not int itemCount
            || rawWidth <= 0 || itemCount <= 0)
        {
            return MinWidth;
        }

        var containerWidth = Math.Max(MinWidth, rawWidth - ReservedChrome);
        var columns = Math.Max(1, (int)(containerWidth / MinWidth));
        columns = Math.Min(columns, itemCount);

        return containerWidth / columns;
    }
}
