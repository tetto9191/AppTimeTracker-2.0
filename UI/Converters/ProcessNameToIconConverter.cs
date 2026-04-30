using AppTimeTracker.Core.Helpers;
using AppTimeTracker.Data.Services;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppTimeTracker.UI.Converters
{
    public class ProcessNameToIconConverter : IValueConverter
    {
        private static IIconCacheService _iconCacheService;
        private static ConfigManager _configManager;

        public ProcessNameToIconConverter()
        {
        }

        public ProcessNameToIconConverter(IIconCacheService iconCacheService)
        {
            _iconCacheService = iconCacheService;
        }

        public static void SetIconCacheService(IIconCacheService iconCacheService)
        {
            _iconCacheService = iconCacheService;
        }

        public static void SetConfigManager(ConfigManager configManager)
        {
            _configManager = configManager;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string processName && !string.IsNullOrEmpty(processName))
            {
                if (_configManager != null)
                {
                    var customIconBase64 = _configManager.GetCustomIconBase64(processName);
                    if (!string.IsNullOrEmpty(customIconBase64))
                    {
                        try
                        {
                            return ConvertBase64ToImage(customIconBase64);
                        }
                        catch
                        {
                        }
                    }
                }

                return _iconCacheService?.GetIconImage(processName) ?? IconExtractor.GetIconForProcess(processName);
            }
            return IconExtractor.GetIconForProcess(null);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private ImageSource ConvertBase64ToImage(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return null;

            try
            {
                var bytes = System.Convert.FromBase64String(base64String);
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}