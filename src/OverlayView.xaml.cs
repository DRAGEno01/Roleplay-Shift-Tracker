using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RpShiftTracker
{
    public partial class OverlayView : UserControl
    {
        private string currentPosition = "top-right";
        private bool useCustomPosition = false;
        private Button[] positionButtons;
        private OverlayWindow overlayWindow;
        private DispatcherTimer updateTimer;
        private int customX = 100;
        private int customY = 100;
        private double transparency = 0.2;
        private bool transparentBackground = false;

        public OverlayView()
        {
            InitializeComponent();
            LoadOverlaySettings();
            InitializePositionButtons();
            UpdatePositionButtonStyles();

            // Initialize UI from loaded settings
            EnableOverlayCheckBox.IsChecked = overlaySettings.Enabled;
            currentPosition = overlaySettings.Position;
            useCustomPosition = overlaySettings.CustomPosition.Enabled;
            customX = overlaySettings.CustomPosition.X;
            customY = overlaySettings.CustomPosition.Y;
            transparency = overlaySettings.Transparency;
            transparentBackground = overlaySettings.TransparentBackground;

            // Initialize custom position text boxes
            CustomXTextBox.Text = customX.ToString();
            CustomYTextBox.Text = customY.ToString();
            UseCustomPositionCheckBox.IsChecked = useCustomPosition;

            // Initialize transparency slider
            TransparencySlider.Value = transparency * 100;
            TransparentBackgroundCheckBox.IsChecked = transparentBackground;

            // Initialize display options
            ShowStatusCheckBox.IsChecked = overlaySettings.DisplayOptions.ShowStatus;
            ShowHoursCheckBox.IsChecked = overlaySettings.DisplayOptions.ShowHours;
            ShowWeekCheckBox.IsChecked = overlaySettings.DisplayOptions.ShowWeek;
            ShowDepartmentCheckBox.IsChecked = overlaySettings.DisplayOptions.ShowDepartment;

            // Create overlay if enabled
            if (overlaySettings.Enabled)
            {
                CreateOverlayWindow();
            }

            // Ensure scrolling works without needing to click
            this.Loaded += (s, e) =>
            {
                MainScrollViewer.Focusable = true;
                MainScrollViewer.Focus();
            };

            // Clean up on unload
            this.Unloaded += OverlayView_Unloaded;
        }

        private OverlaySettings overlaySettings = new OverlaySettings();

        private void LoadOverlaySettings()
        {
            overlaySettings = DataManager.LoadOverlaySettings();
        }

        private void SaveOverlaySettings()
        {
            overlaySettings.Enabled = EnableOverlayCheckBox.IsChecked == true;
            overlaySettings.Position = currentPosition;
            overlaySettings.CustomPosition.Enabled = useCustomPosition;
            overlaySettings.CustomPosition.X = customX;
            overlaySettings.CustomPosition.Y = customY;
            overlaySettings.Transparency = transparency;
            overlaySettings.TransparentBackground = transparentBackground;
            overlaySettings.DisplayOptions.ShowStatus = ShowStatusCheckBox.IsChecked == true;
            overlaySettings.DisplayOptions.ShowHours = ShowHoursCheckBox.IsChecked == true;
            overlaySettings.DisplayOptions.ShowWeek = ShowWeekCheckBox.IsChecked == true;
            overlaySettings.DisplayOptions.ShowDepartment = ShowDepartmentCheckBox.IsChecked == true;

            DataManager.SaveOverlaySettings(overlaySettings);
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // Normalize delta (WPF uses 120 per notch, but we want smooth scrolling)
                double delta = e.Delta > 0 ? 30 : -30;
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - delta);
                e.Handled = true;
            }
        }

        private void OverlayView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Don't destroy overlay on unload - let user control it via checkbox
            // DestroyOverlayWindow();
        }

        private void InitializePositionButtons()
        {
            positionButtons = new Button[]
            {
                TopLeftButton,
                TopCenterButton,
                TopRightButton,
                MiddleLeftButton,
                MiddleRightButton,
                BottomLeftButton,
                BottomCenterButton,
                BottomRightButton
            };
        }

        private void UpdatePositionButtonStyles()
        {
            foreach (var button in positionButtons)
            {
                string position = button.Tag?.ToString();
                if (position == currentPosition && !useCustomPosition)
                {
                    button.Style = (Style)FindResource("SelectedPositionButtonStyle");
                }
                else
                {
                    button.Style = (Style)FindResource("PositionButtonStyle");
                }
                button.IsEnabled = !useCustomPosition;
            }
        }

        private void PositionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string position)
            {
                currentPosition = position;
                useCustomPosition = false;
                UseCustomPositionCheckBox.IsChecked = false;
                UpdatePositionButtonStyles();
                UpdateOverlayPosition();
                SaveOverlaySettings();
            }
        }

        private void UseCustomPositionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            useCustomPosition = true;
            UpdatePositionButtonStyles();
            UpdateOverlayPosition();
            SaveOverlaySettings();
        }

        private void UseCustomPositionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            useCustomPosition = false;
            UpdatePositionButtonStyles();
            UpdateOverlayPosition();
            SaveOverlaySettings();
        }

        private void CustomPosition_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (useCustomPosition)
            {
                if (int.TryParse(CustomXTextBox.Text, out int x) &&
                    int.TryParse(CustomYTextBox.Text, out int y))
                {
                    customX = x;
                    customY = y;
                    UpdateOverlayPosition();
                    SaveOverlaySettings();
                }
            }
        }

        private void EnableOverlayCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CreateOverlayWindow();
            // Position will be set after window loads
            SaveOverlaySettings();
        }

        private void EnableOverlayCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            DestroyOverlayWindow();
            SaveOverlaySettings();
        }

        private void DisplayOption_Changed(object sender, RoutedEventArgs e)
        {
            UpdateOverlayContent(); // This will also update position automatically
            SaveOverlaySettings();
        }

        private void TransparentBackgroundCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            transparentBackground = TransparentBackgroundCheckBox.IsChecked == true;
            UpdateOverlayContent(); // This will also update position automatically
            SaveOverlaySettings();
        }

        private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TransparencyValueLabel != null)
            {
                int value = (int)e.NewValue;
                TransparencyValueLabel.Text = $"{value}%";
                transparency = value / 100.0;
                if (overlayWindow != null)
                {
                    // Update transparency using the public method which stores and maintains it
                    double opacityValue = 1.0 - transparency;
                    overlayWindow.SetOpacity(opacityValue);
                }
                SaveOverlaySettings();
            }
        }

        private void CreateOverlayWindow()
        {
            if (overlayWindow != null)
                return;

            overlayWindow = new OverlayWindow();

            // Update content first
            UpdateOverlayContent();

            // Show window
            overlayWindow.Show();

            // Position after window is loaded and has size
            overlayWindow.Loaded += (s, e) =>
            {
                // Use multiple dispatcher calls to ensure size is calculated
                overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    overlayWindow.UpdateLayout();
                    overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateOverlayPosition();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }), System.Windows.Threading.DispatcherPriority.Render);
            };

            // Also try to position immediately after show
            overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                overlayWindow.UpdateLayout();
                UpdateOverlayPosition();
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            // Start update timer
            updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            updateTimer.Tick += (s, e) =>
            {
                UpdateOverlayContent();
                // Maintain opacity in case it gets reset
                if (overlayWindow != null)
                {
                    double expectedOpacity = 1.0 - transparency;
                    if (Math.Abs(overlayWindow.Opacity - expectedOpacity) > 0.01)
                    {
                        overlayWindow.SetOpacity(expectedOpacity);
                    }
                }
            };
            updateTimer.Start();
        }

        public void CloseOverlay()
        {
            DestroyOverlayWindow();
        }

        private void DestroyOverlayWindow()
        {
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer = null;
            }

            if (overlayWindow != null)
            {
                overlayWindow.Close();
                overlayWindow = null;
            }
        }

        private void UpdateOverlayContent()
        {
            if (overlayWindow == null)
                return;

            // Get actual data from DataManager
            var deptSettings = DataManager.LoadDepartmentsSettings();
            string currentDept = deptSettings.CurrentDepartment;
            var events = DataManager.LoadEvents(currentDept);
            bool clockedIn = DataManager.IsCurrentlyClockedIn(currentDept);

            // Calculate weekly hours
            var now = DateTime.Now;
            var weekStart = GetStartOfWeek(now);
            var weekEnd = weekStart.AddDays(7);
            int totalSeconds = DataManager.CalculateWeeklyHours(events, weekStart, weekEnd);
            string hoursText = DataManager.FormatSecondsHms(totalSeconds);

            // Format week text
            string weekText = $"{weekStart:MM/dd} - {weekEnd.AddDays(-1):MM/dd}";

            string departmentText = currentDept;

            overlayWindow.UpdateContent(
                ShowStatusCheckBox.IsChecked == true,
                ShowHoursCheckBox.IsChecked == true,
                ShowWeekCheckBox.IsChecked == true,
                ShowDepartmentCheckBox.IsChecked == true,
                clockedIn,
                hoursText,
                weekText,
                departmentText,
                transparency,
                transparentBackground
            );

            // Auto-update position after content changes to maintain alignment
            // The position will be updated automatically when window size changes
            UpdateOverlayPosition();
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        private void UpdateOverlayPosition()
        {
            if (overlayWindow == null)
                return;

            if (useCustomPosition)
            {
                overlayWindow.SetPosition(null, customX, customY);
            }
            else
            {
                overlayWindow.SetPosition(currentPosition);
            }
        }
    }
}

