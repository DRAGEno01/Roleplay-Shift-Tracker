using System;
using System.IO;
using System.Net;
using System.Text;

namespace RpShiftTracker
{
    public class UpdateChecker
    {
        private const string VERSION = "0.001";
        private const string UPDATE_URL = "https://raw.githubusercontent.com/DRAGEno01/Roleplay-Shift-Tracker/refs/heads/main/GlobalSettings.json";
        private static readonly string BASE_DIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "RpShiftTracker");
        private static readonly string UPDATE_STATUS_FILE = Path.Combine(BASE_DIR, "update_status.json");

        public class AppInfo
        {
            public string Version { get; set; } = VERSION;
            public bool Supported { get; set; } = true;
            public bool AllowUsage { get; set; } = true;
        }

        private class UpdateStatus
        {
            public bool UpdateAvailable { get; set; } = false;
            public string RemoteVersion { get; set; } = "";
        }

        public static bool IsUpdateAvailable()
        {
            var appInfo = FetchAppInfo();
            if (appInfo == null || string.IsNullOrEmpty(appInfo.Version))
            {
                // If fetch fails, check persisted status
                return LoadUpdateStatus();
            }

            string remoteVersion = appInfo.Version.Trim();
            string localVersion = VERSION.Trim();

            bool updateAvailable = remoteVersion != localVersion;

            // Save status for persistence
            SaveUpdateStatus(updateAvailable, remoteVersion);

            return updateAvailable;
        }

        public static bool HasPersistedUpdate()
        {
            return LoadUpdateStatus();
        }

        private static void SaveUpdateStatus(bool updateAvailable, string remoteVersion)
        {
            try
            {
                if (!Directory.Exists(BASE_DIR))
                {
                    Directory.CreateDirectory(BASE_DIR);
                }

                var status = new UpdateStatus
                {
                    UpdateAvailable = updateAvailable,
                    RemoteVersion = remoteVersion ?? ""
                };

                // Simple JSON serialization
                string json = "{\"updateAvailable\":" + (updateAvailable ? "true" : "false") +
                            ",\"remoteVersion\":\"" + (remoteVersion ?? "").Replace("\"", "\\\"") + "\"}";

                File.WriteAllText(UPDATE_STATUS_FILE, json, Encoding.UTF8);
            }
            catch
            {
                // Silently fail
            }
        }

        private static bool LoadUpdateStatus()
        {
            try
            {
                if (File.Exists(UPDATE_STATUS_FILE))
                {
                    string json = File.ReadAllText(UPDATE_STATUS_FILE, Encoding.UTF8);

                    // Check if update is available in persisted file
                    if (json.Contains("\"updateAvailable\":true"))
                    {
                        // Return true - we'll verify on next actual check
                        // This avoids network calls on every load
                        return true;
                    }
                }
            }
            catch
            {
                // Silently fail
            }
            return false;
        }

        public static AppInfo FetchAppInfo()
        {
            try
            {
                // Add cache-busting parameter to force fresh fetch
                string urlWithCacheBust = UPDATE_URL + "?t=" + DateTime.Now.Ticks;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlWithCacheBust);
                request.Timeout = 5000;
                request.UserAgent = "RpShiftTracker";

                // Disable caching to ensure fresh content
                request.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                request.Headers.Add("Pragma", "no-cache");
                request.Headers.Add("Expires", "0");

                // Force a fresh request
                request.IfModifiedSince = DateTime.Now;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        return ParseAppInfo(json);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static AppInfo ParseAppInfo(string json)
        {
            try
            {
                var info = new AppInfo();

                // Remove whitespace for easier parsing
                json = json.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");

                // Simple JSON parsing for: {"version":"0.000","supported":true,"allowUsage":true}
                int versionStart = json.IndexOf("\"version\"");
                if (versionStart >= 0)
                {
                    int colonPos = json.IndexOf(':', versionStart);
                    int quoteStart = json.IndexOf('"', colonPos);
                    if (quoteStart >= 0)
                    {
                        int quoteEnd = json.IndexOf('"', quoteStart + 1);
                        if (quoteEnd >= 0)
                        {
                            info.Version = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1).Trim();
                        }
                    }
                }

                int supportedStart = json.IndexOf("\"supported\"");
                if (supportedStart >= 0)
                {
                    int colonPos = json.IndexOf(':', supportedStart);
                    int valueEnd = json.IndexOfAny(new[] { ',', '}' }, colonPos + 1);
                    if (valueEnd >= 0)
                    {
                        string value = json.Substring(colonPos + 1, valueEnd - colonPos - 1).Trim();
                        info.Supported = value == "true";
                    }
                }

                int allowUsageStart = json.IndexOf("\"allowUsage\"");
                if (allowUsageStart >= 0)
                {
                    int colonPos = json.IndexOf(':', allowUsageStart);
                    int valueEnd = json.IndexOfAny(new[] { ',', '}' }, colonPos + 1);
                    if (valueEnd >= 0)
                    {
                        string value = json.Substring(colonPos + 1, valueEnd - colonPos - 1).Trim();
                        info.AllowUsage = value == "true";
                    }
                }

                return info;
            }
            catch
            {
                return null;
            }
        }
    }
}

