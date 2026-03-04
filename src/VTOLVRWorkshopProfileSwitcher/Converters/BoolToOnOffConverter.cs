using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace VTOLVRWorkshopProfileSwitcher.Converters;

public sealed class BoolToOnOffConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "On" : "Off";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text)
        {
            return text.Equals("On", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
