using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RpShiftTracker
{
    public class UpdateService
    {
        private const string GITHUB_BASE_URL = "https://raw.githubusercontent.com/DRAGEno01/Roleplay-Shift-Tracker/main/src";
        private static readonly string BASE_DIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "RpShiftTracker");
        private static readonly string UPDATE_CODE_DIR = Path.Combine(BASE_DIR, "updateCode");

        private static readonly List<string> FilesToUpdate = new List<string>
        {
            "DepartmentsView.xaml",
            "DepartmentsView.xaml.cs",
            "HomeView.xaml",
            "HomeView.xaml.cs",
            "MainWindow.xaml",
            "MainWindow.xaml.cs",
            "OverlayView.xaml",
            "OverlayView.xaml.cs",
            "OverlayWindow.xaml",
            "OverlayWindow.xaml.cs",
            "SettingsView.xaml",
            "SettingsView.xaml.cs",
            "ViewShifts.xaml",
            "ViewShifts.xaml.cs",
            "DataManager.cs",
            "UpdateChecker.cs",
            "UpdateService.cs"
        };

        public static async Task<bool> DownloadUpdateFiles(IProgress<string> progress = null)
        {
            try
            {
                // Ensure update directory exists
                if (!Directory.Exists(UPDATE_CODE_DIR))
                {
                    Directory.CreateDirectory(UPDATE_CODE_DIR);
                }

                progress?.Report("Starting download...");

                int totalFiles = FilesToUpdate.Count;
                int downloaded = 0;

                foreach (string fileName in FilesToUpdate)
                {
                    try
                    {
                        string url = $"{GITHUB_BASE_URL}/{fileName}";
                        string localPath = Path.Combine(UPDATE_CODE_DIR, fileName);

                        progress?.Report($"Downloading {fileName}... ({downloaded + 1}/{totalFiles})");

                        // Download file
                        using (WebClient client = new WebClient())
                        {
                            client.Encoding = Encoding.UTF8;
                            await client.DownloadFileTaskAsync(new Uri(url), localPath);
                        }

                        downloaded++;
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"Failed to download {fileName}: {ex.Message}");
                        // Continue with other files
                    }
                }

                progress?.Report($"Downloaded {downloaded}/{totalFiles} files successfully.");
                return downloaded == totalFiles;
            }
            catch (Exception ex)
            {
                progress?.Report($"Update download failed: {ex.Message}");
                return false;
            }
        }

        public static bool ApplyUpdate(IProgress<string> progress = null)
        {
            try
            {
                progress?.Report("Preparing to apply update...");

                // Get the application directory (where the executable is)
                string appDir = AppDomain.CurrentDomain.BaseDirectory;

                // For Visual Studio projects, we need to find the actual source files
                // This is tricky - we'll need to determine the project directory
                // For now, we'll try to find it relative to common locations
                string projectDir = FindProjectDirectory();

                if (string.IsNullOrEmpty(projectDir))
                {
                    progress?.Report("Could not locate project directory. Update cannot be applied automatically.");
                    return false;
                }

                progress?.Report($"Project directory: {projectDir}");

                int copied = 0;
                foreach (string fileName in FilesToUpdate)
                {
                    try
                    {
                        string sourcePath = Path.Combine(UPDATE_CODE_DIR, fileName);
                        string destPath = Path.Combine(projectDir, fileName);

                        // Handle files in Services folder
                        if (fileName == "DataManager.cs" || fileName == "UpdateChecker.cs")
                        {
                            string servicesDir = Path.Combine(projectDir, "Services");
                            if (!Directory.Exists(servicesDir))
                            {
                                Directory.CreateDirectory(servicesDir);
                            }
                            destPath = Path.Combine(servicesDir, fileName);
                        }

                        if (File.Exists(sourcePath))
                        {
                            // Backup original file
                            if (File.Exists(destPath))
                            {
                                string backupPath = destPath + ".backup";
                                File.Copy(destPath, backupPath, true);
                            }

                            // Copy new file
                            File.Copy(sourcePath, destPath, true);
                            copied++;
                            progress?.Report($"Updated {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"Failed to update {fileName}: {ex.Message}");
                    }
                }

                progress?.Report($"Update applied: {copied}/{FilesToUpdate.Count} files updated.");
                return copied > 0;
            }
            catch (Exception ex)
            {
                progress?.Report($"Failed to apply update: {ex.Message}");
                return false;
            }
        }

        private static string FindProjectDirectory()
        {
            // Try to find the project directory
            // Common locations:
            // 1. Current directory
            // 2. Parent of executable
            // 3. Look for Visual Studio 2022 folder

            string currentDir = Directory.GetCurrentDirectory();

            // Check if we're in a Visual Studio project structure
            if (Directory.Exists(Path.Combine(currentDir, "Visual Studio 2022")))
            {
                return Path.Combine(currentDir, "Visual Studio 2022");
            }

            // Check parent directory
            string parentDir = Directory.GetParent(currentDir)?.FullName;
            if (parentDir != null && Directory.Exists(Path.Combine(parentDir, "Visual Studio 2022")))
            {
                return Path.Combine(parentDir, "Visual Studio 2022");
            }

            // Try to find by looking for one of our files
            string searchDir = currentDir;
            for (int i = 0; i < 5; i++) // Search up to 5 levels up
            {
                if (File.Exists(Path.Combine(searchDir, "MainWindow.xaml")) ||
                    File.Exists(Path.Combine(searchDir, "MainWindow.xaml.cs")))
                {
                    return searchDir;
                }

                string parent = Directory.GetParent(searchDir)?.FullName;
                if (string.IsNullOrEmpty(parent))
                    break;
                searchDir = parent;
            }

            return null;
        }

        public static string GetUpdateCodeDirectory()
        {
            return UPDATE_CODE_DIR;
        }
    }
}

