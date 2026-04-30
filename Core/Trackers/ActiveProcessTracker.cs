using AppTimeTracker.Core.Helpers;
using AppTimeTracker.Data.Models;
using AppTimeTracker.Data.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace AppTimeTracker.Core.Trackers
{
    public class ActiveProcessTracker : IDisposable
    {
        private readonly Stopwatch _updateStopwatch;
        private Thread _trackingThread;
        private readonly StatsManager _statsManager;
        private readonly ConfigManager _configManager;
        private readonly IIconCacheService _iconCacheService;
        private readonly ConcurrentDictionary<string, ProcessInfo> _trackedProcesses;
        private readonly HashSet<string> _excludedProcesses = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _systemProcesses;
        private readonly object _updateLock = new();
        private readonly ManualResetEventSlim _stopEvent;
        private bool _isTracking;
        private bool _disposed;

        public event Action<string, string, TimeSpan> ProcessTimeUpdated;

        public ActiveProcessTracker(StatsManager statsManager, ConfigManager configManager, IIconCacheService iconCacheService)
        {
            _statsManager = statsManager ?? throw new ArgumentNullException(nameof(statsManager));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _iconCacheService = iconCacheService ?? throw new ArgumentNullException(nameof(iconCacheService));

            _trackedProcesses = new ConcurrentDictionary<string, ProcessInfo>();
            _stopEvent = new ManualResetEventSlim(false);
            _updateStopwatch = new Stopwatch();

            _systemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Idle", "System", "Registry", "Memory Compression",
                "smss", "csrss", "wininit", "services", "lsass",
                "svchost", "explorer", "taskhostw", "dwm", "ctfmon",
                "RuntimeBroker", "SecurityHealthService", "WmiPrvSE",
                "sihost", "ApplicationFrameHost", "ShellExperienceHost",
                "SearchUI", "StartMenuExperienceHost", "SearchIndexer"
            };
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ActiveProcessTracker));

            _isTracking = true;
            _stopEvent.Reset();
            _updateStopwatch.Restart();

            _trackingThread = new Thread(TrackingLoop)
            {
                Name = "ProcessTrackerThread",
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            _trackingThread.Start();
        }

        private void TrackingLoop()
        {
            const int targetIntervalMs = 1000;
            var lastUpdateTime = DateTime.Now;

            while (_isTracking && !_disposed)
            {
                try
                {
                    var now = DateTime.Now;
                    var timeSinceLastUpdate = (now - lastUpdateTime).TotalMilliseconds;

                    if (timeSinceLastUpdate >= targetIntervalMs)
                    {
                        UpdateRunningProcesses();
                        lastUpdateTime = now;
                    }

                    var sleepTime = targetIntervalMs - (DateTime.Now - lastUpdateTime).TotalMilliseconds;
                    if (sleepTime > 0)
                    {
                        _stopEvent.Wait(TimeSpan.FromMilliseconds(sleepTime));
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка в трекинге: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        private void UpdateRunningProcesses()
        {
            if (!_isTracking || _disposed) return;

            lock (_updateLock)
            {
                try
                {
                    var now = DateTime.Now;
                    var currentProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var processesByPath = new Dictionary<string, string>();

                    foreach (var process in Process.GetProcesses())
                    {
                        try
                        {
                            if (process.Id <= 0 || process.SessionId == 0)
                                continue;

                            var normalizedName = GetNormalizedProcessName(process);
                            if (string.IsNullOrEmpty(normalizedName))
                                continue;

                            currentProcesses.Add(normalizedName);
                            processesByPath[normalizedName] = process.ProcessName;
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    var processesToRemove = new List<string>();
                    foreach (var processName in _trackedProcesses.Keys)
                    {
                        if (!currentProcesses.Contains(processName) ||
                            _configManager.IsBlacklisted(processName) ||
                            _excludedProcesses.Contains(processName))
                        {
                            processesToRemove.Add(processName);
                        }
                    }

                    foreach (var processName in processesToRemove)
                    {
                        if (_trackedProcesses.TryRemove(processName, out var processInfo))
                        {
                            SaveProcessSession(processInfo);
                        }
                    }

                    foreach (var processName in currentProcesses)
                    {
                        if (ShouldSkipProcess(processName))
                            continue;

                        if (_configManager.IsBlacklisted(processName) || _excludedProcesses.Contains(processName))
                            continue;

                        // Получаем отображаемое имя через ConfigManager
                        var displayName = _configManager.GetDisplayName(processName);

                        if (_trackedProcesses.TryGetValue(processName, out var existingProcess))
                        {
                            if (existingProcess.DisplayName != displayName)
                            {
                                existingProcess.DisplayName = displayName;
                            }

                            var elapsed = now - existingProcess.LastUpdateTime;
                            if (elapsed > TimeSpan.Zero)
                            {
                                var timeToAdd = TimeSpan.FromSeconds(Math.Min(elapsed.TotalSeconds, 5));
                                existingProcess.TotalTime = existingProcess.TotalTime.Add(timeToAdd);
                                existingProcess.LastUpdateTime = now;

                                _statsManager.AddTimeToApp(processName, timeToAdd);
                                ProcessTimeUpdated?.Invoke(processName, displayName, timeToAdd);
                            }
                        }
                        else
                        {
                            var iconBase64 = _iconCacheService.GetIconBase64(processName);
                            var newProcess = new ProcessInfo
                            {
                                ProcessName = processName,
                                DisplayName = displayName,
                                StartTime = now,
                                LastUpdateTime = now,
                                TotalTime = TimeSpan.Zero,
                                IconBase64 = iconBase64
                            };

                            if (_trackedProcesses.TryAdd(processName, newProcess))
                            {
                                _statsManager.AddTimeToApp(processName, TimeSpan.Zero);
                                ProcessTimeUpdated?.Invoke(processName, displayName, TimeSpan.Zero);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка обновления процессов: {ex.Message}");
                }
            }
        }

        private string GetNormalizedProcessName(Process process)
        {
            try
            {
                var processName = Path.GetFileNameWithoutExtension(process.ProcessName);
                if (string.IsNullOrEmpty(processName))
                    return null;

                return processName;
            }
            catch
            {
                return null;
            }
        }

        private bool ShouldSkipProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return true;

            if (_systemProcesses.Contains(processName))
                return true;

            if (processName.Equals("AppTimeTracker", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void SaveProcessSession(ProcessInfo processInfo)
        {
            if (processInfo == null) return;

            var session = new TrackingSession
            {
                Date = processInfo.StartTime.Date,
                ProcessName = processInfo.DisplayName,
                StartTime = processInfo.StartTime,
                EndTime = DateTime.Now,
                Duration = processInfo.TotalTime
            };

            _statsManager.AddSession(session);
        }

        public void Stop()
        {
            _isTracking = false;
            _stopEvent.Set();

            if (_trackingThread != null && _trackingThread.IsAlive)
            {
                if (!_trackingThread.Join(2000))
                {
                    _trackingThread.Interrupt();
                }
            }

            SaveAllCurrentSessions();
            _updateStopwatch.Stop();
        }

        private void SaveAllCurrentSessions()
        {
            lock (_updateLock)
            {
                foreach (var kvp in _trackedProcesses)
                {
                    SaveProcessSession(kvp.Value);
                }
                _trackedProcesses.Clear();
            }
        }

        public List<AppStat> GetCurrentProcessStats()
        {
            var stats = new List<AppStat>();

            lock (_updateLock)
            {
                foreach (var kvp in _trackedProcesses)
                {
                    var processInfo = kvp.Value;
                    var appStat = new AppStat
                    {
                        AppName = processInfo.DisplayName,
                        TotalTime = processInfo.TotalTime,
                        LastTracked = processInfo.LastUpdateTime,
                        IconBase64 = processInfo.IconBase64
                    };
                    stats.Add(appStat);
                }
            }

            return stats.OrderByDescending(s => s.TotalTime).ToList();
        }

        public void ExcludeProcess(string processName)
        {
            lock (_updateLock)
            {
                if (!string.IsNullOrEmpty(processName) && !_excludedProcesses.Contains(processName))
                {
                    _excludedProcesses.Add(processName);

                    if (_trackedProcesses.TryRemove(processName, out var processInfo))
                    {
                        SaveProcessSession(processInfo);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            Stop();
            _stopEvent?.Dispose();

            GC.SuppressFinalize(this);
        }

        private class ProcessInfo
        {
            public string ProcessName { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public DateTime LastUpdateTime { get; set; }
            public TimeSpan TotalTime { get; set; }
            public string IconBase64 { get; set; } = string.Empty;
        }
    }
}