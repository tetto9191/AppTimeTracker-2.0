using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AppTimeTracker.Core.Helpers
{
    public static class ProcessHelper
    {
        public static List<string> GetRunningProcesses()
        {
            var processes = Process.GetProcesses()
                .Select(p =>
                {
                    try
                    {
                        return Path.GetFileNameWithoutExtension(p.ProcessName);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            return processes;
        }

        public static List<string> GetRunningProcessesWithDetails()
        {
            var processes = new List<string>();

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!string.IsNullOrEmpty(process.MainWindowTitle) &&
                        process.MainWindowHandle != IntPtr.Zero)
                    {
                        var name = Path.GetFileNameWithoutExtension(process.ProcessName);
                        if (!string.IsNullOrEmpty(name))
                        {
                            processes.Add(name);
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return processes.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();
        }

        public static bool IsProcessRunning(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;

            return Process.GetProcesses()
                .Any(p =>
                {
                    try
                    {
                        var currentProcessName = Path.GetFileNameWithoutExtension(p.ProcessName);
                        return string.Equals(
                            currentProcessName,
                            processName,
                            StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });
        }

        public static List<ProcessInfo> GetProcessesWithWindows()
        {
            var result = new List<ProcessInfo>();

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!string.IsNullOrEmpty(process.MainWindowTitle) &&
                        process.MainWindowHandle != IntPtr.Zero)
                    {
                        result.Add(new ProcessInfo
                        {
                            Id = process.Id,
                            Name = Path.GetFileNameWithoutExtension(process.ProcessName),
                            WindowTitle = process.MainWindowTitle,
                            HasWindow = true
                        });
                    }
                }
                catch
                {
                    continue;
                }
            }

            return result;
        }
    }

    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public bool HasWindow { get; set; }
    }
}