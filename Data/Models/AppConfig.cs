using System.Collections.Generic;

namespace AppTimeTracker.Data.Models
{
    public class AppConfig
    {
        public List<string> BlacklistedApps { get; set; } = new List<string>();

        public bool RunAtStartup { get; set; } = false;
        public bool UseDarkTheme { get; set; } = true;
        public int TrackingInterval { get; set; } = 1;
        public Dictionary<string, string> ProcessDisplayNames { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ProcessIcons { get; set; } = new Dictionary<string, string>();

        public static List<string> GetDefaultBlacklist()
        {
            return new List<string>
            {
                "System",
                "Registry",
                "smss",
                "csrss",
                "wininit",
                "winlogon",
                "services",
                "lsass",
                "svchost",
                "fontdrvhost",
                "dwm",
                "spoolsv",
                "sihost",
                "taskhostw",
                "ShellHost",
                "CrossDeviceResume",
                "RuntimeBroker",
                "conhost",
                "WUDFHost",
                "WmiPrvSE",
                "dllhost",
                "MicrosoftEdge",
                "MicrosoftEdgeSH",
                "MicrosoftEdgeCP",
                "explorer",
                "SearchIndexer",
                "SearchFilterHost",
                "SearchProtocolHost",
                "TextInputHost",
                "ctfmon",
                "SecurityHealthSystray",
                "NisSrv",
                "MsMpEng",
                "AntimalwareServiceExecutable",
                "ApplicationFrameHost",
                "AppTimeTracker"
            };
        }
    }
}