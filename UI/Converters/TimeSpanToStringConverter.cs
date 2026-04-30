using System;
using System.Globalization;
using System.Windows.Data;

namespace AppTimeTracker.UI.Converters
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                if (timeSpan.TotalHours >= 1)
                    return string.Format("{0:00}:{1:00}:{2:00}",
                        (int)timeSpan.TotalHours,
                        timeSpan.Minutes,
                        timeSpan.Seconds);
                else
                    return string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);
            }
            return "00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}