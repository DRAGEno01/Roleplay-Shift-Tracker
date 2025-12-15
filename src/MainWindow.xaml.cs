using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RpShiftTracker
{
    public partial class MainWindow : Window
    {
        private bool menuOpen = false;
        private const double MenuWidth = 260;
        private HomeView homeViewPage;
        private ViewShifts viewShiftsPage;
        private OverlayView overlayViewPage;
        private DepartmentsView departmentsViewPage;
        private SettingsView settingsViewPage;

        public MainWindow()
        {
            InitializeComponent();
            InitializeMenuAnimation();
            homeViewPage = new HomeView();
            viewShiftsPage = new ViewShifts();
            overlayViewPage = new OverlayView();
            departmentsViewPage = new DepartmentsView();
            settingsViewPage = new SettingsView();

            // Set initial view to Home
            ContentArea.Content = homeViewPage;

            // Handle window-level mouse events to close menu when clicking outside
            this.PreviewMouseDown += MainWindow_PreviewMouseDown;

            // Close overlay when main window closes
            this.Closing += MainWindow_Closing;

            // Check for updates on startup
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load persisted update status first (for immediate display)
            bool persistedUpdate = UpdateChecker.HasPersistedUpdate();
            if (persistedUpdate)
            {
                UpdateAlertIcon.Visibility = Visibility.Visible;
                SettingsAlertIcon.Visibility = Visibility.Visible;
            }

            // Then check for updates in the background
            CheckForUpdates();
        }

        private void CheckForUpdates()
        {
            // Check for updates on a background thread
            System.Threading.ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    bool updateAvailable = UpdateChecker.IsUpdateAvailable();

                    // Update UI on the main thread
                    Dispatcher.Invoke(() =>
                    {
                        if (updateAvailable)
                        {
                            // Show alert icons
                            UpdateAlertIcon.Visibility = Visibility.Visible;
                            SettingsAlertIcon.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            // Hide alert icons only if no update is needed
                            UpdateAlertIcon.Visibility = Visibility.Collapsed;
                            SettingsAlertIcon.Visibility = Visibility.Collapsed;
                        }
                    });
                }
                catch
                {
                    // Silently fail - don't show alerts if check fails
                }
            });
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Close overlay window when main window closes
            if (overlayViewPage != null)
            {
                overlayViewPage.CloseOverlay();
            }
        }

        private void MainWindow_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (menuOpen)
            {
                // Check if the click source is part of the menu
                System.Windows.DependencyObject source = e.OriginalSource as System.Windows.DependencyObject;
                bool isMenuClick = false;

                while (source != null)
                {
                    if (source == SideMenu)
                    {
                        isMenuClick = true;
                        break;
                    }
                    source = System.Windows.Media.VisualTreeHelper.GetParent(source);
                }

                // If click is outside menu, close it
                if (!isMenuClick)
                {
                    CloseMenu();
                    menuOpen = false;
                }
            }
        }

        private void InitializeMenuAnimation()
        {
            // Set initial menu position
            SideMenu.Margin = new Thickness(-MenuWidth, 0, 0, 0);
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenu();
        }

        private void ToggleMenu()
        {
            menuOpen = !menuOpen;

            if (menuOpen)
            {
                OpenMenu();
            }
            else
            {
                CloseMenu();
            }
        }

        private void OpenMenu()
        {
            MenuOverlay.Visibility = Visibility.Visible;

            ThicknessAnimation marginAnimation = new ThicknessAnimation
            {
                From = new Thickness(-MenuWidth, 0, 0, 0),
                To = new Thickness(0, 0, 0, 0),
                Duration = TimeSpan.FromMilliseconds(200)
            };

            SideMenu.BeginAnimation(FrameworkElement.MarginProperty, marginAnimation);
        }

        private void CloseMenu()
        {
            MenuOverlay.Visibility = Visibility.Collapsed;

            ThicknessAnimation marginAnimation = new ThicknessAnimation
            {
                From = new Thickness(0, 0, 0, 0),
                To = new Thickness(-MenuWidth, 0, 0, 0),
                Duration = TimeSpan.FromMilliseconds(200)
            };

            SideMenu.BeginAnimation(FrameworkElement.MarginProperty, marginAnimation);
        }


        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (menuOpen)
            {
                CloseMenu();
                menuOpen = false;
            }
            if (ContentArea != null)
            {
                ContentArea.Content = homeViewPage;
                // Refresh departments when navigating to home
                if (homeViewPage != null)
                {
                    homeViewPage.RefreshDepartments();
                }
            }
            CheckForUpdates();
        }

        private void ViewShiftsButton_Click(object sender, RoutedEventArgs e)
        {
            if (menuOpen)
            {
                CloseMenu();
                menuOpen = false;
            }
            if (ContentArea != null)
            {
                ContentArea.Content = viewShiftsPage;
                // Refresh shifts when navigating to view shifts
                if (viewShiftsPage != null)
                {
                    viewShiftsPage.RefreshShiftsView();
                }
            }
            CheckForUpdates();
        }

        private void OverlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (menuOpen)
            {
                CloseMenu();
                menuOpen = false;
            }
            if (ContentArea != null)
            {
                ContentArea.Content = overlayViewPage;
            }
            CheckForUpdates();
        }

        private void DepartmentsButton_Click(object sender, RoutedEventArgs e)
        {
            if (menuOpen)
            {
                CloseMenu();
                menuOpen = false;
            }
            if (ContentArea != null)
            {
                ContentArea.Content = departmentsViewPage;
            }
            CheckForUpdates();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (menuOpen)
            {
                CloseMenu();
                menuOpen = false;
            }
            if (ContentArea != null)
            {
                ContentArea.Content = settingsViewPage;
            }
            CheckForUpdates();
        }
    }
}

