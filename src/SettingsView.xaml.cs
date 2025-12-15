using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RpShiftTracker
{
    public partial class SettingsView : UserControl
    {
        private const string VERSION = "0.000";

        public SettingsView()
        {
            InitializeComponent();
            VersionLabel.Text = $"Version {VERSION}";
        }

        private void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement update check logic
            MessageBox.Show(
                "Update check functionality will be implemented in a future version.",
                "Check for Updates",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
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

