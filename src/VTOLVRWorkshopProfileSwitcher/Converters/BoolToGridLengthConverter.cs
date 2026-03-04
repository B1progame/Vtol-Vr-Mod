using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace VTOLVRWorkshopProfileSwitcher.Converters;

public sealed class BoolToGridLengthConverter : IValueConverter
{
    public double TruePixels { get; set; } = 420;
    public double FalsePixels { get; set; } = 0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isOpen = value is true;
        var pixels = isOpen ? TruePixels : FalsePixels;
        return new GridLength(pixels, GridUnitType.Pixel);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GridLength length)
        {
            return length.Value > 0;
        }

        return false;
    }
}
