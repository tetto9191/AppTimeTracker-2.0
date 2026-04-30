using AppTimeTracker.Data.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using AppTimeTracker.Core.Common;

namespace AppTimeTracker.Data.Repositories
{
    public class StatsJsonRepository : IStatsRepository
    {
        private readonly string _dataPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public StatsJsonRepository()
        {
            _dataPath = Constants.Paths.StatsDirectory;
            Directory.CreateDirectory(_dataPath);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        private string GetFilePath(DateTime date) =>
            Path.Combine(_dataPath, $"sessions_{date:yyyy-MM-dd}.json");

        public void SaveSession(TrackingSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            try
            {
                if (!session.EndTime.HasValue)
                {
                    session.EndTime = DateTime.Now;
                }

                var filePath = GetFilePath(session.StartTime.Date);
                List<TrackingSession> sessions;

                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    sessions = JsonSerializer.Deserialize<List<TrackingSession>>(json, _jsonOptions)
                        ?? new List<TrackingSession>();
                }
                else
                {
                    sessions = new List<TrackingSession>();
                }

                var existingIndex = sessions.FindIndex(s => s.Id == session.Id);
                if (existingIndex >= 0)
                {
                    sessions[existingIndex] = session;
                }
                else
                {
                    sessions.Add(session);
                }

                var updatedJson = JsonSerializer.Serialize(sessions, _jsonOptions);
                File.WriteAllText(filePath, updatedJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения сессии: {ex.Message}");
            }
        }

        public List<TrackingSession> GetSessions(DateTime from, DateTime to)
        {
            var sessions = new List<TrackingSession>();

            var files = Directory.GetFiles(_dataPath, "sessions_*.json");

            foreach (var filePath in files)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    if (!fileName.StartsWith("sessions_")) continue;

                    var dateString = fileName.Substring(9);
                    if (!DateTime.TryParseExact(dateString, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                    {
                        continue;
                    }

                    if (fileDate >= from.Date && fileDate <= to.Date)
                    {
                        var json = File.ReadAllText(filePath);
                        var daySessions = JsonSerializer.Deserialize<List<TrackingSession>>(json, _jsonOptions);
                        if (daySessions != null)
                        {
                            sessions.AddRange(daySessions);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"Ошибка чтения файла {filePath}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка обработки файла {filePath}: {ex.Message}");
                }
            }

            return sessions;
        }

        public List<TrackingSession> GetSessionsByProcess(string processName)
        {
            var sessions = new List<TrackingSession>();

            var files = Directory.GetFiles(_dataPath, "sessions_*.json");
            foreach (var filePath in files)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var json = File.ReadAllText(filePath);
                        var daySessions = JsonSerializer.Deserialize<List<TrackingSession>>(json, _jsonOptions);
                        if (daySessions != null)
                        {
                            sessions.AddRange(daySessions.Where(s => s.ProcessName == processName));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка чтения файла {filePath}: {ex.Message}");
                }
            }

            return sessions;
        }

        public Dictionary<string, TimeSpan> GetAggregatedStats(DateTime from, DateTime to)
        {
            var sessions = GetSessions(from, to);
            var result = new Dictionary<string, TimeSpan>();

            foreach (var session in sessions)
            {
                var sessionStart = session.StartTime < from ? from : session.StartTime;
                var sessionEnd = session.EndTime.HasValue ?
                    (session.EndTime.Value > to ? to : session.EndTime.Value) : to;

                if (sessionEnd > sessionStart)
                {
                    var duration = sessionEnd - sessionStart;
                    if (duration.TotalSeconds > 0)
                    {
                        if (result.ContainsKey(session.ProcessName))
                        {
                            result[session.ProcessName] = result[session.ProcessName].Add(duration);
                        }
                        else
                        {
                            result[session.ProcessName] = duration;
                        }
                    }
                }
            }

            return result;
        }

        public void DeleteSession(Guid sessionId)
        {
            var files = Directory.GetFiles(_dataPath, "sessions_*.json");
            foreach (var filePath in files)
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var sessions = JsonSerializer.Deserialize<List<TrackingSession>>(json, _jsonOptions);

                    if (sessions != null && sessions.Any(s => s.Id == sessionId))
                    {
                        sessions.RemoveAll(s => s.Id == sessionId);
                        var updatedJson = JsonSerializer.Serialize(sessions, _jsonOptions);
                        File.WriteAllText(filePath, updatedJson);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка удаления сессии: {ex.Message}");
                }
            }
        }

        public void UpdateSession(TrackingSession session)
        {
            if (session == null) return;

            DeleteSession(session.Id);
            SaveSession(session);
        }

        public void DeleteSessionsByProcessName(string processName)
        {
            var files = Directory.GetFiles(_dataPath, "sessions_*.json");
            foreach (var filePath in files)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var json = File.ReadAllText(filePath);
                        var sessions = JsonSerializer.Deserialize<List<TrackingSession>>(json, _jsonOptions);

                        if (sessions != null)
                        {
                            var sessionsToRemove = sessions.Where(s => s.ProcessName == processName).ToList();
                            if (sessionsToRemove.Any())
                            {
                                sessions.RemoveAll(s => sessionsToRemove.Contains(s));
                                var updatedJson = JsonSerializer.Serialize(sessions, _jsonOptions);
                                File.WriteAllText(filePath, updatedJson);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка удаления сессий для {processName} из {filePath}: {ex.Message}");
                }
            }
        }

        public void ClearAllSessions()
        {
            try
            {
                var files = Directory.GetFiles(_dataPath, "sessions_*.json");
                foreach (var filePath in files)
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка удаления файла {filePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка очистки всех сессий: {ex.Message}");
            }
        }
    }
}