using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RpShiftTracker
{
    public partial class SettingsView : UserControl
    {
        private const string VERSION = "0.001";

        private bool updateAvailable = false;
        private string availableVersion = "";

        public SettingsView()
        {
            InitializeComponent();
            VersionLabel.Text = $"Version {VERSION}";

            // Automatically check for updates when view loads (silently, change button if update available)
            this.Loaded += SettingsView_Loaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Load persisted update status first
            bool persistedUpdate = UpdateChecker.HasPersistedUpdate();
            if (persistedUpdate)
            {
                UpdateNotificationIcon.Visibility = Visibility.Visible;
            }

            // Check for updates automatically (silently, change button if update is available)
            CheckForUpdates(showMessage: false);
        }

        private void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            if (updateAvailable)
            {
                // Update button clicked - show "in the works" message
                MessageBox.Show(
                    "Automatic update functionality is currently in development.\n\n" +
                    "Please download the latest version manually from:\n" +
                    "https://github.com/DRAGEno01/Roleplay-Shift-Tracker\n\n" +
                    $"Your version: {VERSION}\n" +
                    $"Available version: {availableVersion}",
                    "Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                // Check for updates button clicked
                CheckForUpdates(showMessage: true);
            }
        }

        private void CheckForUpdates(bool showMessage)
        {
            if (CheckUpdatesButton != null)
            {
                CheckUpdatesButton.IsEnabled = false;
                CheckUpdatesButton.Content = "Checking...";
            }

            try
            {
                // Fetch update info on a background thread
                System.Threading.ThreadPool.QueueUserWorkItem((state) =>
                {
                    try
                    {
                        var appInfo = UpdateChecker.FetchAppInfo();

                        // Update UI on the main thread
                        Dispatcher.Invoke(() =>
                        {
                            if (CheckUpdatesButton != null)
                            {
                                CheckUpdatesButton.IsEnabled = true;
                                CheckUpdatesButton.Content = "Check for Updates";
                            }

                            if (appInfo == null)
                            {
                                if (showMessage)
                                {
                                    MessageBox.Show(
                                        "Could not check for updates.\n\nPlease check your internet connection and try again.",
                                        "Update Check Failed",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error
                                    );
                                }
                                return;
                            }

                            // Check allowUsage first
                            if (!appInfo.AllowUsage)
                            {
                                MessageBox.Show(
                                    "This version of the Roleplay Shift Tracker is no longer supported.\n\n" +
                                    "Please download the latest version from:\n" +
                                    "https://github.com/DRAGEno01/Roleplay-Shift-Tracker\n\n" +
                                    $"Your version: {VERSION}\n" +
                                    $"Latest version: {appInfo.Version}",
                                    "Update Required",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error
                                );
                                return;
                            }

                            // Check if supported
                            if (!appInfo.Supported)
                            {
                                MessageBox.Show(
                                    "This version is no longer supported, but you may continue using it.\n\n" +
                                    "It is recommended to update to the latest version:\n" +
                                    "https://github.com/DRAGEno01/Roleplay-Shift-Tracker\n\n" +
                                    $"Your version: {VERSION}\n" +
                                    $"Latest version: {appInfo.Version}",
                                    "Update Recommended",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning
                                );
                                return;
                            }

                            // Validate that we got a version
                            if (string.IsNullOrEmpty(appInfo.Version))
                            {
                                if (showMessage)
                                {
                                    MessageBox.Show(
                                        "Could not determine the latest version from the update server.",
                                        "Update Check Failed",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Warning
                                    );
                                }
                                return;
                            }

                            // Trim and normalize versions for comparison
                            string remoteVersion = appInfo.Version.Trim();
                            string localVersion = VERSION.Trim();

                            // Debug output
                            System.Diagnostics.Debug.WriteLine($"Version check: Remote='{remoteVersion}', Local='{localVersion}'");

                            // If versions are different, change button to Update button
                            if (remoteVersion != localVersion)
                            {
                                // Versions are different - change button to green Update button
                                updateAvailable = true;
                                availableVersion = remoteVersion;

                                if (CheckUpdatesButton != null)
                                {
                                    CheckUpdatesButton.Content = "Update";
                                    CheckUpdatesButton.Style = (Style)FindResource("UpdateButtonStyle");
                                }

                                // Show notification icon
                                if (UpdateNotificationIcon != null)
                                {
                                    UpdateNotificationIcon.Visibility = Visibility.Visible;
                                }
                            }
                            else
                            {
                                // Versions are equal - keep as Check for Updates button
                                updateAvailable = false;
                                availableVersion = "";

                                if (CheckUpdatesButton != null)
                                {
                                    CheckUpdatesButton.Content = "Check for Updates";
                                    CheckUpdatesButton.Style = (Style)FindResource("CheckUpdatesButtonStyle");
                                }

                                // Hide notification icon
                                if (UpdateNotificationIcon != null)
                                {
                                    UpdateNotificationIcon.Visibility = Visibility.Collapsed;
                                }

                                // Only show message if user manually clicked
                                if (showMessage)
                                {
                                    MessageBox.Show(
                                        $"You are using the latest version.\n\n" +
                                        $"Your version: {localVersion}\n" +
                                        $"Latest version: {remoteVersion}",
                                        "Up to Date",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information
                                    );
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (CheckUpdatesButton != null)
                            {
                                CheckUpdatesButton.IsEnabled = true;
                                CheckUpdatesButton.Content = "Check for Updates";
                            }
                            if (showMessage)
                            {
                                MessageBox.Show(
                                    $"Could not check for updates.\n\nError: {ex.Message}",
                                    "Update Check Failed",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error
                                );
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                if (CheckUpdatesButton != null)
                {
                    CheckUpdatesButton.IsEnabled = true;
                    CheckUpdatesButton.Content = "Check for Updates";
                }
                if (showMessage)
                {
                    MessageBox.Show(
                        $"Could not check for updates.\n\nError: {ex.Message}",
                        "Update Check Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        private int CompareVersions(string version1, string version2)
        {
            // Compare version strings like "0.000", "0.001", etc.
            // Returns: >0 if version1 > version2, 0 if equal, <0 if version1 < version2
            try
            {
                // Normalize versions - remove any whitespace
                version1 = version1.Trim();
                version2 = version2.Trim();

                // Handle version strings with decimal points (e.g., "0.000" -> treat as "0.000")
                // Split by '.' and compare each part numerically
                var parts1 = version1.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                var parts2 = version2.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

                int maxLength = Math.Max(parts1.Length, parts2.Length);
                for (int i = 0; i < maxLength; i++)
                {
                    // Parse each part, treating empty as 0
                    string part1Str = i < parts1.Length ? parts1[i].Trim() : "0";
                    string part2Str = i < parts2.Length ? parts2[i].Trim() : "0";

                    // Handle leading zeros by parsing as int
                    int part1 = string.IsNullOrEmpty(part1Str) ? 0 : int.Parse(part1Str);
                    int part2 = string.IsNullOrEmpty(part2Str) ? 0 : int.Parse(part2Str);

                    if (part1 != part2)
                    {
                        int result = part1.CompareTo(part2);
                        System.Diagnostics.Debug.WriteLine($"Version part {i}: {part1} vs {part2} = {result}");
                        return result;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Versions are equal: {version1} == {version2}");
                return 0;
            }
            catch (Exception ex)
            {
                // If parsing fails, do string comparison as fallback
                // But also log for debugging
                System.Diagnostics.Debug.WriteLine($"Version comparison failed: {ex.Message}. v1='{version1}', v2='{version2}'");
                int result = string.Compare(version1, version2, StringComparison.Ordinal);
                System.Diagnostics.Debug.WriteLine($"String comparison result: {result}");
                return result;
            }
        }


        private void WebsiteLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://drageno01.web.app/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to open the website.\n\nError: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}

