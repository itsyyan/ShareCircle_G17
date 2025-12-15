using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ShareCircle_G17.Converters;

public class BoolToHeartTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Standard Unicode hearts to avoid garbled glyphs
        if (value is bool b && b)
            return "♥"; // Filled heart
        return "♡"; // Outline heart
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return false;
    }
}
