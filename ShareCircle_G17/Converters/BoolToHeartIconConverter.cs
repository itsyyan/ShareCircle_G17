using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ShareCircle_G17.Converters;

public class BoolToHeartIconConverter : IValueConverter
{
    private const string Filled = "https://img.icons8.com/fluency-systems-filled/48/fa314a/like.png";
    private const string Outline = "https://img.icons8.com/fluency-systems-regular/48/8e8e93/like.png";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return Filled;
        return Outline;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return false;
    }
}
