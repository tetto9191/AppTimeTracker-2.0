using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace AppTimeTracker.Core.Helpers
{
    public static class RegistryHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetStartup(string appName, string appPath)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        key.SetValue(appName, $"\"{appPath}\"");
                    }
                    else
                    {
                        throw new InvalidOperationException("Не удалось открыть ключ реестра");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка установки автозагрузки: {ex.Message}");
                throw;
            }
        }

        public static void RemoveStartup(string appName)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    key?.DeleteValue(appName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка удаления автозагрузки: {ex.Message}");
                throw;
            }
        }

        public static bool IsStartupEnabled(string appName)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key?.GetValue(appName) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}