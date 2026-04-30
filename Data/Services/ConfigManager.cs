using AppTimeTracker.Core.Helpers;
using AppTimeTracker.Data.Models;
using AppTimeTracker.Data.Repositories;
using System;
using System.Collections.Generic;

namespace AppTimeTracker.Data.Services
{
    public class ConfigManager
    {
        private readonly IConfigRepository _repository;
        private readonly IIconCacheService _iconCacheService;
        private AppConfig _config;

        public event Action ConfigChanged;

        public ConfigManager(IConfigRepository repository, IIconCacheService iconCacheService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _iconCacheService = iconCacheService ?? throw new ArgumentNullException(nameof(iconCacheService));
            _config = _repository.Load();
        }

        public AppConfig GetConfig() => _config;

        public void UpdateConfig(Action<AppConfig> updateAction)
        {
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction));

            updateAction(_config);
            _repository.Save(_config);
            NotifyConfigChanged();
        }

        public bool IsBlacklisted(string appName) =>
            _config?.BlacklistedApps?.Contains(appName) ?? false;

        public void ToggleBlacklist(string appName)
        {
            if (string.IsNullOrEmpty(appName)) return;

            if (_config.BlacklistedApps.Contains(appName))
                _config.BlacklistedApps.Remove(appName);
            else
                _config.BlacklistedApps.Add(appName);

            _repository.Save(_config);
            NotifyConfigChanged();
        }

        public string GetDisplayName(string originalProcessName)
        {
            if (string.IsNullOrEmpty(originalProcessName))
                return originalProcessName;

            if (_config.ProcessDisplayNames.TryGetValue(originalProcessName, out var displayName))
                return displayName;

            return originalProcessName;
        }

        public void SetDisplayName(string originalProcessName, string displayName)
        {
            if (string.IsNullOrEmpty(originalProcessName))
                throw new ArgumentException("Имя процесса не может быть пустым", nameof(originalProcessName));

            if (string.IsNullOrEmpty(displayName))
            {
                RemoveDisplayName(originalProcessName);
                return;
            }

            _config.ProcessDisplayNames[originalProcessName] = displayName;
            _repository.Save(_config);
            NotifyConfigChanged();
        }

        public void RemoveDisplayName(string originalProcessName)
        {
            if (_config.ProcessDisplayNames.Remove(originalProcessName))
            {
                _repository.Save(_config);
                NotifyConfigChanged();
            }
        }

        public IReadOnlyDictionary<string, string> GetAllDisplayNames() =>
            _config.ProcessDisplayNames;

        // Иконки
        public string GetCustomIconBase64(string originalProcessName)
        {
            if (string.IsNullOrEmpty(originalProcessName))
                return null;

            if (_config.ProcessIcons.TryGetValue(originalProcessName, out var icon))
                return icon;

            return null;
        }

        public void SetCustomIcon(string originalProcessName, string iconBase64)
        {
            if (string.IsNullOrEmpty(originalProcessName))
                return;

            _config.ProcessIcons[originalProcessName] = iconBase64;
            _repository.Save(_config);

            // Очистить кешированные иконки
            IconExtractor.ClearCacheForProcess(originalProcessName);
            _iconCacheService.ClearCacheForProcess(originalProcessName);
            _iconCacheService.SaveIcon(originalProcessName, iconBase64);

            NotifyConfigChanged();
        }

        public void RemoveCustomIcon(string originalProcessName)
        {
            if (_config.ProcessIcons.Remove(originalProcessName))
            {
                _repository.Save(_config);
                IconExtractor.ClearCacheForProcess(originalProcessName);
                _iconCacheService.ClearCacheForProcess(originalProcessName);
                NotifyConfigChanged();
            }
        }

        private void NotifyConfigChanged()
        {
            // Используем диспетчер, если доступен
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => ConfigChanged?.Invoke());
            }
            else
            {
                ConfigChanged?.Invoke();
            }
        }
    }
}