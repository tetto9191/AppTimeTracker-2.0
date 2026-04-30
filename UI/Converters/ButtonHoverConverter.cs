using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AppTimeTracker.UI.Converters
{
    public class ButtonHoverConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string tag)
            {
                // Возвращаем цвет в зависимости от тега кнопки
                return tag switch
                {
                    "Close" => new SolidColorBrush(Color.FromRgb(211, 47, 47)), // #FFD32F2F
                    "Maximize" => new SolidColorBrush(Color.FromRgb(42, 42, 42)), // #FF2A2A2A
                    "Minimize" => new SolidColorBrush(Color.FromRgb(42, 42, 42)), // #FF2A2A2A
                    _ => new SolidColorBrush(Color.FromRgb(42, 42, 42))
                };
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}