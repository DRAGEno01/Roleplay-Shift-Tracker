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

            try
            {
                LoadOverlaySettings();
            }
            catch (Exception ex)
            {
                // If loading settings fails, use defaults
                overlaySettings = new OverlaySettings();
                System.Diagnostics.Debug.WriteLine($"Failed to load overlay settings: {ex.Message}");
            }

            try
            {
                // Initialize local variables from loaded settings FIRST
                currentPosition = overlaySettings.Position;
                useCustomPosition = overlaySettings.CustomPosition.Enabled;
                customX = overlaySettings.CustomPosition.X;
                customY = overlaySettings.CustomPosition.Y;
                transparency = overlaySettings.Transparency;
                transparentBackground = overlaySettings.TransparentBackground;

                // Initialize UI from loaded settings
                EnableOverlayCheckBox.IsChecked = overlaySettings.Enabled;

                // Initialize custom position text boxes
                CustomXTextBox.Text = customX.ToString();
                CustomYTextBox.Text = customY.ToString();
                UseCustomPositionCheckBox.IsChecked = useCustomPosition;

                // Initialize transparency slider (transparency is stored as 0-1, slider uses 0-100)
                TransparencySlider.Value = transparency * 100;
                TransparentBackgroundCheckBox.IsChecked = transparentBackground;

                // Initialize display options
                ShowStatusCheckBox.IsChecked = overlaySettings.DisplayOptions.ShowStatus;
                ShowHoursCheckBox.IsChecked = overlaySettings.DisplayOptions.ShowHours;
                ShowWeekCheckBox.IsChecked = overlaySettings.DisplayOptions.ShowWeek;
                ShowDepartmentCheckBox.IsChecked = overlaySettings.DisplayOptions.ShowDepartment;

                // Initialize position buttons AFTER local variables are set
                InitializePositionButtons();
                UpdatePositionButtonStyles();
            }
            catch (Exception ex)
            {
                // If UI initialization fails, log but don't crash
                System.Diagnostics.Debug.WriteLine($"Failed to initialize overlay UI: {ex.Message}");
            }
            finally
            {
                // Mark initialization as complete - now saves are allowed
                isInitializing = false;
            }

            // Ensure scrolling works without needing to click
            this.Loaded += OverlayView_Loaded;

            // Clean up on unload
            this.Unloaded += OverlayView_Unloaded;
        }

        private bool hasInitializedOverlay = false;
        private bool isInitializing = true; // Flag to prevent saving during initialization

        private void OverlayView_Loaded(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.Focusable = true;
            MainScrollViewer.Focus();

            // Only create overlay on first load if it was previously enabled
            // This happens when user navigates to this page
            if (!hasInitializedOverlay && overlaySettings.Enabled)
            {
                hasInitializedOverlay = true;
                // Delay to ensure everything is ready
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (overlaySettings.Enabled && overlayWindow == null)
                        {
                            CreateOverlayWindow();
                        }
                    }
                    catch (Exception ex)
                    {
                        // If it fails, uncheck the box so user knows
                        EnableOverlayCheckBox.IsChecked = false;
                        overlaySettings.Enabled = false;
                        SaveOverlaySettings();
                        System.Diagnostics.Debug.WriteLine($"Failed to restore overlay on load: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private OverlaySettings overlaySettings = new OverlaySettings();

        private void LoadOverlaySettings()
        {
            overlaySettings = DataManager.LoadOverlaySettings();
        }

        private void SaveOverlaySettings()
        {
            // Don't save during initialization to prevent overwriting loaded settings
            if (isInitializing)
                return;

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
            try
            {
                CreateOverlayWindow();
                // Position will be set after window loads
                SaveOverlaySettings();
            }
            catch (Exception ex)
            {
                // If overlay creation fails, uncheck the box and show error
                EnableOverlayCheckBox.IsChecked = false;
                System.Diagnostics.Debug.WriteLine($"Failed to create overlay: {ex.Message}");
                MessageBox.Show(
                    "Failed to create overlay window.\n\nPlease try again or check your display settings.",
                    "Overlay Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void EnableOverlayCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                DestroyOverlayWindow();
                SaveOverlaySettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to destroy overlay: {ex.Message}");
            }
        }

        private void DisplayOption_Changed(object sender, RoutedEventArgs e)
        {
            // Update overlaySettings immediately when checkbox changes
            if (sender is CheckBox checkbox)
            {
                if (checkbox == ShowStatusCheckBox)
                    overlaySettings.DisplayOptions.ShowStatus = checkbox.IsChecked == true;
                else if (checkbox == ShowHoursCheckBox)
                    overlaySettings.DisplayOptions.ShowHours = checkbox.IsChecked == true;
                else if (checkbox == ShowWeekCheckBox)
                    overlaySettings.DisplayOptions.ShowWeek = checkbox.IsChecked == true;
                else if (checkbox == ShowDepartmentCheckBox)
                    overlaySettings.DisplayOptions.ShowDepartment = checkbox.IsChecked == true;
            }

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

            // Calculate opacity value before try block so it's accessible in lambdas
            double opacityValue = 1.0 - transparency;

            try
            {
                overlayWindow = new OverlayWindow();

                // Update content first (this will also set transparency)
                UpdateOverlayContent();

                // Apply saved opacity immediately
                overlayWindow.SetOpacity(opacityValue);

                // Show window
                overlayWindow.Show();
            }
            catch (Exception ex)
            {
                // Clean up if creation fails
                if (overlayWindow != null)
                {
                    try
                    {
                        overlayWindow.Close();
                    }
                    catch { }
                    overlayWindow = null;
                }
                throw; // Re-throw to let caller handle it
            }

            // Position after window is loaded and has size
            overlayWindow.Loaded += (s, e) =>
            {
                // Use multiple dispatcher calls to ensure size is calculated
                overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    overlayWindow.UpdateLayout();
                    // Re-apply opacity after layout
                    overlayWindow.SetOpacity(opacityValue);
                    overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateOverlayPosition();
                        // Ensure opacity is maintained
                        overlayWindow.SetOpacity(opacityValue);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }), System.Windows.Threading.DispatcherPriority.Render);
            };

            // Also try to position immediately after show
            overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                overlayWindow.UpdateLayout();
                UpdateOverlayPosition();
                // Ensure opacity is maintained
                overlayWindow.SetOpacity(opacityValue);
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

            try
            {
                // Get actual data from DataManager
                var deptSettings = DataManager.LoadDepartmentsSettings();
                string currentDept = deptSettings?.CurrentDepartment ?? "Default";
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

                // Convert transparency from 0-1 to 0-100 for UpdateContent
                double transparencyPercent = transparency * 100.0;

                // ALWAYS use saved settings from overlaySettings as the primary source
                // This ensures saved settings are used even if checkboxes aren't initialized yet
                // The checkboxes are synced FROM the settings, not the other way around
                bool showStatus = overlaySettings.DisplayOptions.ShowStatus;
                bool showHours = overlaySettings.DisplayOptions.ShowHours;
                bool showWeek = overlaySettings.DisplayOptions.ShowWeek;
                bool showDepartment = overlaySettings.DisplayOptions.ShowDepartment;

                overlayWindow.UpdateContent(
                    showStatus,
                    showHours,
                    showWeek,
                    showDepartment,
                    clockedIn,
                    hoursText,
                    weekText,
                    departmentText,
                    transparencyPercent,
                    transparentBackground
                );

                // Auto-update position after content changes to maintain alignment
                // The position will be updated automatically when window size changes
                UpdateOverlayPosition();
            }
            catch (Exception ex)
            {
                // Silently fail - don't crash if data loading fails
                System.Diagnostics.Debug.WriteLine($"Failed to update overlay content: {ex.Message}");
            }
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

