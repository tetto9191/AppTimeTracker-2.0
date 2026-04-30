using AppTimeTracker.Data.Models;
using System;
using System.Collections.Generic;

namespace AppTimeTracker.Data.Repositories
{
    public interface IStatsRepository
    {
        void SaveSession(TrackingSession session);
        List<TrackingSession> GetSessions(DateTime from, DateTime to);
        List<TrackingSession> GetSessionsByProcess(string processName);
        Dictionary<string, TimeSpan> GetAggregatedStats(DateTime from, DateTime to);
        void DeleteSession(Guid sessionId);
        void UpdateSession(TrackingSession session);
        void DeleteSessionsByProcessName(string processName);
        void ClearAllSessions();
    }
}