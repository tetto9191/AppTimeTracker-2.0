using System;
using System.IO;

namespace AppTimeTracker.Core.Common
{
    public static class Constants
    {
        public const string AppName = "AppTimeTracker";
        public const string AppRegistryKey = "AppTimeTracker";

        public static class Paths
        {
            public static string AppData => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName);

            public static string DataDirectory => Path.Combine(AppData, "Data");
            public static string StatsDirectory => Path.Combine(DataDirectory, "Stats");
            public static string ConfigFile => Path.Combine(AppData, "config.json");
        }

        public static class Registry
        {
            public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        }

        public static class Tracking
        {
            public const int IntervalSeconds = 5;
            public const int SaveIntervalMinutes = 1;
        }

        public static class UI
        {
            public const int MaxTopApps = 20;
            public const int DefaultTopApps = 5;
            public const string DateFormat = "dd.MM.yyyy";
            public const string TimeFormat = "HH:mm:ss";
        }

        // Новые константы для автообновления
        public static class Update
        {
            public const string GitHubApiBase = "https://api.github.com";
            public static string GitHubOwner => "tetto9191";
            public static string GitHubRepo => "AppTimeTracker";
            public const string AssetName = "AppTimeTracker.zip";
        }
    }
}