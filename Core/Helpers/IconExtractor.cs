using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppTimeTracker.Core.Helpers
{
    public static class IconExtractor
    {
        private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new();

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, string lpIconPath, out ushort lpiIcon);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static ImageSource GetIconForProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return GetDefaultIcon();

            if (_iconCache.TryGetValue(processName, out var cachedIcon))
                return cachedIcon;

            ImageSource icon = null;

            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    try
                    {
                        var process = processes[0];
                        var processPath = process.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                        {
                            icon = ExtractIconFromFile(processPath);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            icon ??= GetDefaultIcon();
            _iconCache.TryAdd(processName, icon);
            return icon;
        }

        public static ImageSource ExtractIconFromFile(string filePath)
        {
            try
            {
                ExtractAssociatedIcon(IntPtr.Zero, filePath, out ushort iconIndex);

                using var icon = Icon.ExtractAssociatedIcon(filePath);
                if (icon != null)
                {
                    using var bitmap = icon.ToBitmap();
                    var bitmapData = bitmap.LockBits(
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    try
                    {
                        var bitmapSource = BitmapSource.Create(
                            bitmapData.Width,
                            bitmapData.Height,
                            96,
                            96,
                            PixelFormats.Bgra32,
                            null,
                            bitmapData.Scan0,
                            bitmapData.Stride * bitmapData.Height,
                            bitmapData.Stride);

                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static string ExtractIconAsBase64(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".exe")
                {
                    // Извлечение иконки из .exe файла
                    using var icon = Icon.ExtractAssociatedIcon(filePath);
                    if (icon != null)
                    {
                        using var bitmap = icon.ToBitmap();
                        using var stream = new MemoryStream();

                        // Сохраняем в PNG формате для сохранения прозрачности
                        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        return Convert.ToBase64String(stream.ToArray());
                    }
                }
                else
                {
                    // Чтение изображения напрямую для PNG, JPG, ICO
                    var bytes = File.ReadAllBytes(filePath);
                    return Convert.ToBase64String(bytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка извлечения иконки из {filePath}: {ex.Message}");
            }

            return null;
        }

        public static ImageSource LoadImageFromBase64(string base64String)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64String);
                using var stream = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return GetDefaultIcon();
            }
        }

        private static ImageSource GetDefaultIcon()
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri("pack://application:,,,/Resources/Icons/app.ico");
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(
                        System.Windows.Media.Brushes.Gray,
                        null,
                        new Rect(0, 0, 16, 16));
                }

                var bitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(drawingVisual);
                bitmap.Freeze();
                return bitmap;
            }
        }

        public static void ClearCache()
        {
            _iconCache.Clear();
        }

        public static void ClearCacheForProcess(string processName)
        {
            _iconCache.TryRemove(processName, out _);
        }
    }
}