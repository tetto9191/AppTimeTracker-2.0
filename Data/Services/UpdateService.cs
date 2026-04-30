using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace AppTimeTracker.Data.Services
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("AppTimeTracker");
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync()
        {
            var currentVersion = GetCurrentVersion();
            if (currentVersion == null) return new UpdateCheckResult { HasUpdate = false };

            try
            {
                string url = $"{Core.Common.Constants.Update.GitHubApiBase}/repos/{Core.Common.Constants.Update.GitHubOwner}/{Core.Common.Constants.Update.GitHubRepo}/releases/latest";
                var response = await _httpClient.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                string latestTag = root.GetProperty("tag_name").GetString() ?? "";
                string versionStr = latestTag.StartsWith("v") ? latestTag.Substring(1) : latestTag;
                if (!Version.TryParse(versionStr, out Version latestVersion))
                    return new UpdateCheckResult { HasUpdate = false };

                if (latestVersion <= currentVersion)
                    return new UpdateCheckResult { HasUpdate = false };

                // Ищем нужный asset
                JsonElement assets = root.GetProperty("assets");
                string downloadUrl = null;
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    if (asset.GetProperty("name").GetString() == Core.Common.Constants.Update.AssetName)
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                if (downloadUrl == null)
                    return new UpdateCheckResult { HasUpdate = false };

                return new UpdateCheckResult
                {
                    HasUpdate = true,
                    LatestVersion = latestVersion,
                    CurrentVersion = currentVersion,
                    DownloadUrl = downloadUrl
                };
            }
            catch
            {
                return new UpdateCheckResult { HasUpdate = false };
            }
        }

        public async Task<bool> DownloadUpdateAsync(string downloadUrl, string destinationPath)
        {
            try
            {
                using var response = await _httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ApplyUpdate(string zipPath, string exeName = "AppTimeTracker.exe")
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string tempExtractDir = Path.Combine(Path.GetTempPath(), "AppTimeTracker_Update");
                if (Directory.Exists(tempExtractDir))
                    Directory.Delete(tempExtractDir, true);
                Directory.CreateDirectory(tempExtractDir);

                // Распаковываем zip
                ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

                // Находим exe в распакованном
                string newExe = Directory.GetFiles(tempExtractDir, exeName, SearchOption.AllDirectories).FirstOrDefault();
                if (newExe == null) return false;

                // Создаём bat-файл для замены после завершения текущего процесса
                string batPath = Path.Combine(tempExtractDir, "update.bat");
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(appDirectory, exeName);

                string batContent = $@"
@echo off
timeout /t 2 /nobreak > NUL
move /y ""{newExe}"" ""{currentExe}""
if exist ""{currentExe}"" start """" ""{currentExe}""
rmdir /s /q ""{tempExtractDir}""
del ""%~f0""
";
                File.WriteAllText(batPath, batContent);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C \"{batPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        private Version GetCurrentVersion()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
            catch
            {
                return null;
            }
        }
    }

    public class UpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public Version LatestVersion { get; set; }
        public Version CurrentVersion { get; set; }
        public string DownloadUrl { get; set; }
    }
}