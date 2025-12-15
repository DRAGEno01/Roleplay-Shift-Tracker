using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace RpShiftTracker
{
    public partial class OverlayWindow : Window
    {
        private const string TransparentBgColor = "#010101";
        private string currentPosition;
        private int? customX;
        private int? customY;
        private bool needsReposition = false;
        private double currentOpacity = 1.0;

        // Windows API to keep window truly on top
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public OverlayWindow()
        {
            InitializeComponent();

            // Ensure window is always on top, even over taskbar
            this.Topmost = true;

            // Use Windows API to keep window truly on top
            this.Loaded += OverlayWindow_Loaded;

            // Reposition when size changes
            this.SizeChanged += OverlayWindow_SizeChanged;

            // Keep window on top and maintain opacity when app loses/regains focus
            this.Activated += OverlayWindow_Activated;
            this.Deactivated += OverlayWindow_Deactivated;

            // Also maintain opacity on GotFocus/LostFocus
            this.GotFocus += (s, e) => { MaintainOpacity(); };
            this.LostFocus += (s, e) => { MaintainOpacity(); };
        }

        private void OverlayWindow_Activated(object sender, EventArgs e)
        {
            SetTopMost();
            MaintainOpacity();
        }

        private void OverlayWindow_Deactivated(object sender, EventArgs e)
        {
            SetTopMost();
            MaintainOpacity();
        }

        private void MaintainOpacity()
        {
            // Re-apply the stored opacity value to ensure it persists
            this.Opacity = currentOpacity;
        }

        public void SetOpacity(double opacity)
        {
            currentOpacity = opacity;
            this.Opacity = opacity;
            // Force re-application
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.Opacity = currentOpacity;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetTopMost();
        }

        private void SetTopMost()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
            }
            catch
            {
                // Fallback to Topmost property if API call fails
                this.Topmost = true;
            }
        }

        private void OverlayWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (needsReposition)
            {
                needsReposition = false;
                if (currentPosition != null)
                {
                    SetPositionInternal(currentPosition, customX, customY);
                }
            }
        }

        public void UpdateContent(bool showStatus, bool showHours, bool showWeek, bool showDepartment,
                                  bool clockedIn, string hoursText, string weekText, string departmentText,
                                  double transparency, bool transparentBackground)
        {
            ContentPanel.Children.Clear();

            // Set background and transparency
            if (transparentBackground)
            {
                // Make the border completely transparent
                ContentBorder.Background = Brushes.Transparent;
                ContentBorder.BorderThickness = new Thickness(0);
                this.Background = Brushes.Transparent;
            }
            else
            {
                ContentBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"));
                ContentBorder.BorderThickness = new Thickness(1);
                this.Background = Brushes.Transparent;
            }

            // Apply transparency - opacity affects the entire window
            double opacityValue = 1.0 - (transparency / 100.0);
            currentOpacity = opacityValue;
            this.Opacity = opacityValue;

            bool hasContent = false;

            if (showStatus)
            {
                var statusText = clockedIn ? "🟢 CLOCKED IN" : "🔴 CLOCKED OUT";
                var statusColor = clockedIn ?
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")) :
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

                var statusLabel = new TextBlock
                {
                    Text = statusText,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = statusColor,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                ContentPanel.Children.Add(statusLabel);
                hasContent = true;
            }

            if (showHours)
            {
                var hoursLabel = new TextBlock
                {
                    Text = $"Hours: {hoursText}",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64B5F6")),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                ContentPanel.Children.Add(hoursLabel);
                hasContent = true;
            }

            if (showWeek)
            {
                var weekLabel = new TextBlock
                {
                    Text = $"Week: {weekText}",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                ContentPanel.Children.Add(weekLabel);
                hasContent = true;
            }

            if (showDepartment)
            {
                var deptLabel = new TextBlock
                {
                    Text = $"Dept: {departmentText}",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                ContentPanel.Children.Add(deptLabel);
                hasContent = true;
            }

            if (!hasContent)
            {
                var placeholder = new TextBlock
                {
                    Text = "No items selected",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")),
                    Margin = new Thickness(0, 8, 0, 0)
                };
                ContentPanel.Children.Add(placeholder);
            }

            // Update window size
            this.UpdateLayout();
            this.SizeToContent = SizeToContent.WidthAndHeight;

            // Force layout update to ensure size is calculated
            this.InvalidateVisual();

            // Use dispatcher to wait for layout to complete before positioning
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Position will be updated by caller after this completes
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public void SetPosition(string position, int? customX = null, int? customY = null)
        {
            this.currentPosition = position;
            this.customX = customX;
            this.customY = customY;

            SetPositionInternal(position, customX, customY);
        }

        private void SetPositionInternal(string position, int? customX, int? customY)
        {
            // Get window dimensions
            double windowWidth = this.ActualWidth;
            double windowHeight = this.ActualHeight;

            // If size not available yet, mark for reposition on size change
            if (windowWidth <= 0 || windowHeight <= 0)
            {
                needsReposition = true;
                return;
            }

            if (customX.HasValue && customY.HasValue)
            {
                this.Left = customX.Value;
                this.Top = customY.Value;
                return;
            }

            // Use working area to exclude taskbar
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;
            var screenLeft = SystemParameters.WorkArea.Left;
            var screenTop = SystemParameters.WorkArea.Top;
            double margin = 20;

            double x, y;

            switch (position)
            {
                case "top-left":
                    x = screenLeft + margin;
                    y = screenTop + margin;
                    break;
                case "top-center":
                    x = screenLeft + (screenWidth - windowWidth) / 2;
                    y = screenTop + margin;
                    break;
                case "top-right":
                    x = screenLeft + screenWidth - windowWidth - margin;
                    y = screenTop + margin;
                    break;
                case "middle-left":
                    x = screenLeft + margin;
                    y = screenTop + (screenHeight - windowHeight) / 2;
                    break;
                case "middle-right":
                    x = screenLeft + screenWidth - windowWidth - margin;
                    y = screenTop + (screenHeight - windowHeight) / 2;
                    break;
                case "bottom-left":
                    x = screenLeft + margin;
                    y = screenTop + screenHeight - windowHeight - margin;
                    break;
                case "bottom-center":
                    x = screenLeft + (screenWidth - windowWidth) / 2;
                    y = screenTop + screenHeight - windowHeight - margin;
                    break;
                case "bottom-right":
                    x = screenLeft + screenWidth - windowWidth - margin;
                    y = screenTop + screenHeight - windowHeight - margin;
                    break;
                default:
                    x = screenLeft + margin;
                    y = screenTop + margin;
                    break;
            }

            this.Left = x;
            this.Top = y;
        }
    }
}

