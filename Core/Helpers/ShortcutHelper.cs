using System;
using System.Diagnostics;
using System.IO;

namespace AppTimeTracker.Core.Helpers
{
    public static class ShortcutHelper
    {
        public static string GetExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        public static void CreateDesktopShortcut(string appName, string targetPath, string iconPath = null)
        {
            try
            {
                if (!File.Exists(targetPath))
                {
                    throw new FileNotFoundException($"Исполняемый файл не найден: {targetPath}");
                }

                var desktopPath = GetDesktopPath();
                var shortcutPath = Path.Combine(desktopPath, $"{appName}.lnk");

                Debug.WriteLine($"Создание ярлыка по пути: {shortcutPath}");
                Debug.WriteLine($"Путь к рабочему столу: {desktopPath}");
                Debug.WriteLine($"Существует ли папка: {Directory.Exists(desktopPath)}");

                if (File.Exists(shortcutPath))
                {
                    try
                    {
                        File.Delete(shortcutPath);
                    }
                    catch { }
                }

                var shell = new IWshRuntimeLibrary.WshShell();
                var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);

                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = $"Ярлык для {appName}";

                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    shortcut.IconLocation = iconPath + ",0";
                }
                else
                {
                    shortcut.IconLocation = targetPath + ",0";
                }

                shortcut.Save();

                if (!File.Exists(shortcutPath))
                {
                    throw new IOException($"Не удалось создать файл ярлыка по пути: {shortcutPath}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка создания ярлыка: {ex.Message}", ex);
            }
        }

        private static string GetDesktopPath()
        {
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                if (!string.IsNullOrEmpty(desktopPath) && Directory.Exists(desktopPath))
                {
                    return desktopPath;
                }

                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
            catch
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
        }

        public static void DeleteDesktopShortcut(string appName)
        {
            try
            {
                var desktopPath = GetDesktopPath();
                var shortcutPath = Path.Combine(desktopPath, $"{appName}.lnk");

                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка удаления ярлыка: {ex.Message}", ex);
            }
        }

        public static bool DesktopShortcutExists(string appName)
        {
            try
            {
                var desktopPath = GetDesktopPath();
                var shortcutPath = Path.Combine(desktopPath, $"{appName}.lnk");
                return File.Exists(shortcutPath);
            }
            catch
            {
                return false;
            }
        }

        public static void CreateStartMenuShortcut(string appName, string targetPath)
        {
            try
            {
                if (!File.Exists(targetPath))
                {
                    throw new FileNotFoundException($"Исполняемый файл не найден: {targetPath}");
                }

                var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                var programsPath = Path.Combine(startMenuPath, "Programs");
                Directory.CreateDirectory(programsPath);

                var shortcutPath = Path.Combine(programsPath, $"{appName}.lnk");

                var shell = new IWshRuntimeLibrary.WshShell();
                var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);

                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = $"Ярлык для {appName}";
                shortcut.IconLocation = targetPath + ",0";

                shortcut.Save();
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка создания ярлыка в меню Пуск: {ex.Message}", ex);
            }
        }
    }
}