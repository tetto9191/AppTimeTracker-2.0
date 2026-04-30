using AppTimeTracker.Core.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AppTimeTracker.Core.Common;
using System.Windows;

namespace AppTimeTracker.Data.Services
{
    public interface IIconCacheService
    {
        string GetIconBase64(string processName);
        ImageSource GetIconImage(string processName);
        void SaveIcon(string processName, string base64Icon);
        void LoadCache();
        void SaveCache();
        void ClearCache();
        void ClearCacheForProcess(string processName);
    }

    public class IconCacheService : IIconCacheService
    {
        private readonly ConcurrentDictionary<string, string> _iconCache = new();
        private readonly string _cachePath;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, string lpIconPath, out ushort lpiIcon);

        public IconCacheService()
        {
            _cachePath = Path.Combine(Constants.Paths.DataDirectory, "icon_cache.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath));
            LoadCache();
        }

        public string GetIconBase64(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return GetDefaultIconBase64();

            if (_iconCache.TryGetValue(processName, out var base64Icon))
                return base64Icon;

            var iconBase64 = ExtractAndCacheIcon(processName);
            return iconBase64 ?? GetDefaultIconBase64();
        }

        public ImageSource GetIconImage(string processName)
        {
            var base64Icon = GetIconBase64(processName);
            return ConvertBase64ToImage(base64Icon);
        }

        public void SaveIcon(string processName, string base64Icon)
        {
            if (string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(base64Icon))
                return;

            _iconCache[processName] = base64Icon;
            SaveCache();
        }

        public void LoadCache()
        {
            if (!File.Exists(_cachePath))
                return;

            try
            {
                var json = File.ReadAllText(_cachePath);
                var cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (cache != null)
                {
                    foreach (var kvp in cache)
                    {
                        _iconCache[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch
            {
            }
        }

        public void SaveCache()
        {
            try
            {
                var dict = _iconCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_cachePath, json);
            }
            catch
            {
            }
        }

        public void ClearCache()
        {
            _iconCache.Clear();
            if (File.Exists(_cachePath))
                File.Delete(_cachePath);
        }

        public void ClearCacheForProcess(string processName)
        {
            _iconCache.TryRemove(processName, out _);
            SaveCache();
        }

        private string ExtractAndCacheIcon(string processName)
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                    return null;

                var process = processes[0];
                var processPath = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(processPath) || !File.Exists(processPath))
                    return null;

                using var icon = Icon.ExtractAssociatedIcon(processPath);
                if (icon == null)
                    return null;

                var base64Icon = IconToBase64Png(icon);
                if (!string.IsNullOrEmpty(base64Icon))
                {
                    _iconCache[processName] = base64Icon;
                    SaveCache();
                }

                return base64Icon;
            }
            catch
            {
                return null;
            }
        }

        private string IconToBase64Png(Icon icon)
        {
            try
            {
                using var bitmap = icon.ToBitmap();
                using var ms = new MemoryStream();

                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
            catch
            {
                return null;
            }
        }

        private string GetDefaultIconBase64()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Resources/Icons/app.ico", UriKind.Absolute);
                var info = Application.GetResourceStream(uri);
                if (info != null)
                {
                    using (var stream = info.Stream)
                    {
                        using var icon = new Icon(stream);
                        return IconToBase64Png(icon);
                    }
                }
            }
            catch
            {
            }

            try
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                using var exeIcon = Icon.ExtractAssociatedIcon(exePath);
                return IconToBase64Png(exeIcon);
            }
            catch
            {
                return null;
            }
        }

        private ImageSource ConvertBase64ToImage(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return null;

            try
            {
                var bytes = Convert.FromBase64String(base64String);
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