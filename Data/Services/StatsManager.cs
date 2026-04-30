using AppTimeTracker.Data.Models;
using AppTimeTracker.Data.Repositories;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AppTimeTracker.Data.Services
{
    public class StatsManager : INotifyPropertyChanged
    {
        private readonly IStatsRepository _repository;
        private readonly ConfigManager _configManager;
        private readonly IIconCacheService _iconCacheService;
        private readonly Dictionary<string, AppStat> _currentStats;

        public event Action StatsUpdated;

        public StatsManager(IStatsRepository repository, ConfigManager configManager, IIconCacheService iconCacheService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _iconCacheService = iconCacheService ?? throw new ArgumentNullException(nameof(iconCacheService));
            _currentStats = new Dictionary<string, AppStat>();

            _configManager.ConfigChanged += OnConfigChanged;
        }

        private void OnConfigChanged()
        {
            _currentStats.Clear();
            OnStatsUpdated();
        }

        public void AddSession(TrackingSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            // ProcessName здесь уже DisplayName, но перестрахуемся
            var displayName = _configManager.GetDisplayName(session.ProcessName);

            var groupedSession = new TrackingSession
            {
                Id = Guid.NewGuid(),
                Date = session.Date,
                ProcessName = displayName,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Duration = session.Duration
            };

            _repository.SaveSession(groupedSession);
            OnStatsUpdated();
        }

        public void AddTimeToApp(string originalProcessName, TimeSpan time)
        {
            if (string.IsNullOrEmpty(originalProcessName)) return;

            var displayName = _configManager.GetDisplayName(originalProcessName);

            if (!_currentStats.ContainsKey(displayName))
            {
                var iconBase64 = _configManager.GetCustomIconBase64(originalProcessName)
                                 ?? _iconCacheService.GetIconBase64(originalProcessName);

                var appStat = new AppStat
                {
                    AppName = displayName,
                    TotalTime = time,
                    LastTracked = DateTime.Now,
                    IconBase64 = iconBase64
                };
                _currentStats[displayName] = appStat;
            }
            else
            {
                _currentStats[displayName].AddTime(time);
            }

            OnStatsUpdated();
        }

        public List<AppStat> GetCurrentStats()
        {
            var from = DateTime.Now.AddDays(-7);
            var to = DateTime.Now;
            var stats = GetStats(from, to);

            var result = new List<AppStat>();
            foreach (var kvp in stats)
            {
                var iconBase64 = _configManager.GetCustomIconBase64(kvp.Key)
                                 ?? _iconCacheService.GetIconBase64(kvp.Key);

                var appStat = new AppStat
                {
                    AppName = kvp.Key,
                    TotalTime = kvp.Value,
                    LastTracked = DateTime.Now,
                    IconBase64 = iconBase64
                };
                result.Add(appStat);
            }

            return result.OrderByDescending(s => s.TotalTime).ToList();
        }

        public Dictionary<string, TimeSpan> GetStats(DateTime from, DateTime to)
        {
            var sessions = _repository.GetSessions(from, to);
            var aggregated = new Dictionary<string, TimeSpan>();

            foreach (var session in sessions)
            {
                var displayName = _configManager.GetDisplayName(session.ProcessName);

                if (aggregated.ContainsKey(displayName))
                {
                    aggregated[displayName] = aggregated[displayName].Add(session.Duration);
                }
                else
                {
                    aggregated[displayName] = session.Duration;
                }
            }

            foreach (var kvp in _currentStats)
            {
                if (kvp.Value.LastTracked >= from && kvp.Value.LastTracked <= to)
                {
                    var displayName = kvp.Key;
                    if (aggregated.ContainsKey(displayName))
                    {
                        aggregated[displayName] = aggregated[displayName].Add(kvp.Value.TotalTime);
                    }
                    else
                    {
                        aggregated[displayName] = kvp.Value.TotalTime;
                    }
                }
            }

            return aggregated;
        }

        public Dictionary<string, TimeSpan> GetAggregatedStats(DateTime from, DateTime to)
        {
            var sessions = _repository.GetSessions(from, to);
            var aggregatedStats = new Dictionary<string, TimeSpan>();

            foreach (var session in sessions)
            {
                var displayName = _configManager.GetDisplayName(session.ProcessName);
                if (aggregatedStats.ContainsKey(displayName))
                {
                    aggregatedStats[displayName] = aggregatedStats[displayName].Add(session.Duration);
                }
                else
                {
                    aggregatedStats[displayName] = session.Duration;
                }
            }

            return aggregatedStats;
        }

        public Dictionary<string, TimeSpan> GetFilteredStats(DateTime from, DateTime to, Func<string, bool> filter)
        {
            var stats = GetStats(from, to);
            return stats
                .Where(kvp => filter(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public Dictionary<string, TimeSpan> GetTopApps(int count, DateTime from, DateTime to)
        {
            var stats = GetStats(from, to);
            return stats
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public Dictionary<string, double> GetTopAppsWithPercentages(int count, DateTime from, DateTime to)
        {
            var stats = GetStats(from, to);
            var totalSeconds = stats.Values.Sum(t => t.TotalSeconds);

            return stats
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => totalSeconds > 0 ? (kvp.Value.TotalSeconds / totalSeconds * 100) : 0
                );
        }

        public List<TrackingSession> GetSessions(DateTime from, DateTime to)
        {
            return _repository.GetSessions(from, to);
        }

        public List<AppStat> GetDailyStats(DateTime from, DateTime to)
        {
            var aggregated = GetStats(from, to);
            var result = new List<AppStat>();

            foreach (var kvp in aggregated)
            {
                var iconBase64 = _configManager.GetCustomIconBase64(kvp.Key)
                                 ?? _iconCacheService.GetIconBase64(kvp.Key);

                var appStat = new AppStat
                {
                    AppName = kvp.Key,
                    TotalTime = kvp.Value,
                    LastTracked = DateTime.Now,
                    IconBase64 = iconBase64
                };
                result.Add(appStat);
            }

            return result.OrderByDescending(s => s.TotalTime).ToList();
        }

        public void ClearCache()
        {
            _currentStats.Clear();
        }

        public void RecalculateStats(DateTime from, DateTime to)
        {
            ClearCache();
            GetStats(from, to);
        }

        public void DeleteAppStats(string appName)
        {
            _repository.DeleteSessionsByProcessName(appName);
            _currentStats.Remove(appName);
            OnStatsUpdated();
        }

        public void ClearAllStats()
        {
            _repository.ClearAllSessions();
            _currentStats.Clear();
            OnStatsUpdated();
        }

        private void OnStatsUpdated()
        {
            StatsUpdated?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Cleanup()
        {
            if (_configManager != null)
            {
                _configManager.ConfigChanged -= OnConfigChanged;
            }
        }
    }
}