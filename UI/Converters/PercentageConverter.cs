using System;
using System.Globalization;
using System.Windows.Data;
namespace AppTimeTracker.UI.Converters;

public class PercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is double d ? $"{d:F2}%" : "0%";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}