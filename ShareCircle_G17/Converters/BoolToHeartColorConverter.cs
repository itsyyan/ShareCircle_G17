using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ShareCircle_G17.Converters;

public class BoolToHeartColorConverter : IValueConverter
{
    private static readonly Color SavedColor = Color.FromArgb("#FA314A");
    private static readonly Color DefaultColor = Color.FromArgb("#111827");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isSaved && isSaved ? SavedColor : DefaultColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return false;
    }
}
