using AppTimeTracker.Data.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AppTimeTracker.Core.Common;

namespace AppTimeTracker.Data.Repositories
{
    public class ConfigJsonRepository : IConfigRepository
    {
        private readonly string _configPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigJsonRepository()
        {
            _configPath = Constants.Paths.ConfigFile;
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreNullValues = false
            };
        }

        public AppConfig Load()
        {
            AppConfig config = null;

            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);

                    if (config != null)
                    {
                        config.BlacklistedApps ??= new List<string>();

                        var appTimeTracker = config.BlacklistedApps.FirstOrDefault(b =>
                            b.Equals("AppTimeTracker", StringComparison.OrdinalIgnoreCase));

                        if (appTimeTracker == null)
                        {
                            config.BlacklistedApps.Add("AppTimeTracker");
                        }

                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки конфигурации: {ex.Message}");
            }

            config = CreateDefaultConfig();
            Save(config);
            return config;
        }

        public void Save(AppConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                if (!config.BlacklistedApps.Any(b => b.Equals("AppTimeTracker", StringComparison.OrdinalIgnoreCase)))
                {
                    config.BlacklistedApps.Add("AppTimeTracker");
                }

                var json = JsonSerializer.Serialize(config, _jsonOptions);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения конфигурации: {ex.Message}");
                throw;
            }
        }

        private AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                BlacklistedApps = new List<string>(AppConfig.GetDefaultBlacklist()),
                RunAtStartup = false,
                UseDarkTheme = true,
                TrackingInterval = 1
            };
        }
    }
}