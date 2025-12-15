using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RpShiftTracker
{
    public partial class HomeView : UserControl
    {
        private string currentDepartment = "Default";
        private System.Windows.Threading.DispatcherTimer updateTimer;

        public HomeView()
        {
            InitializeComponent();
            LoadDepartments();
            LoadCurrentState();
            UpdateWeekLabel();
            UpdateStatusAndHours();

            // Set up timer to update hours every second
            updateTimer = new System.Windows.Threading.DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromSeconds(1);
            updateTimer.Tick += (s, e) => UpdateStatusAndHours();
            updateTimer.Start();
        }

        public void RefreshDepartments()
        {
            LoadDepartments();
            UpdateStatusAndHours();
        }

        private void LoadDepartments()
        {
            var settings = DataManager.LoadDepartmentsSettings();
            string previousSelection = DepartmentComboBox.SelectedItem?.ToString();
            DepartmentComboBox.Items.Clear();

            foreach (var dept in settings.Departments)
            {
                DepartmentComboBox.Items.Add(dept);
            }

            currentDepartment = settings.CurrentDepartment;
            if (DepartmentComboBox.Items.Count > 0)
            {
                if (DepartmentComboBox.Items.Contains(currentDepartment))
                {
                    DepartmentComboBox.SelectedItem = currentDepartment;
                }
                else
                {
                    DepartmentComboBox.SelectedIndex = 0;
                    currentDepartment = DepartmentComboBox.SelectedItem.ToString();
                }
            }
        }

        private void LoadCurrentState()
        {
            // Check if user is clocked in (any department)
            var clockedInDept = DataManager.GetClockedInDepartment();
            if (clockedInDept != null && DepartmentComboBox.Items.Contains(clockedInDept))
            {
                currentDepartment = clockedInDept;
                DepartmentComboBox.SelectedItem = clockedInDept;
            }
        }

        private void UpdateWeekLabel()
        {
            var now = DateTime.Now;
            var weekStart = GetStartOfWeek(now);
            var weekEnd = weekStart.AddDays(7).AddSeconds(-1);

            string[] dayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            WeekLabel.Text = $"Current week: {weekStart:yyyy-MM-dd} ({dayNames[(int)weekStart.DayOfWeek]}) - {weekEnd:yyyy-MM-dd} ({dayNames[(int)weekEnd.DayOfWeek]})";
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        private void UpdateStatusAndHours()
        {
            var events = DataManager.LoadEvents(currentDepartment);
            bool isClockedIn = DataManager.IsCurrentlyClockedIn(currentDepartment);

            // Update status
            if (isClockedIn)
            {
                StatusLabel.Text = "Status: CLOCKED IN";
                StatusLabel.Foreground = (SolidColorBrush)FindResource("Positive");
                ClockButton.Content = "Clock Out";
                ClockButton.Style = (Style)FindResource("ClockOutButtonStyle");
            }
            else
            {
                StatusLabel.Text = "Status: CLOCKED OUT";
                StatusLabel.Foreground = (SolidColorBrush)FindResource("MutedText");
                ClockButton.Content = "Clock In";
                ClockButton.Style = (Style)FindResource("ClockButtonStyle");
            }

            // Calculate and update weekly hours
            var weekStart = GetStartOfWeek(DateTime.Now);
            var weekEnd = weekStart.AddDays(7);
            int totalSeconds = DataManager.CalculateWeeklyHours(events, weekStart, weekEnd);
            HoursLabel.Text = DataManager.FormatSecondsHms(totalSeconds);
        }

        private void ClockButton_Click(object sender, RoutedEventArgs e)
        {
            var events = DataManager.LoadEvents(currentDepartment);
            bool isClockedIn = DataManager.IsCurrentlyClockedIn(currentDepartment);

            if (isClockedIn)
            {
                // Clock out
                DataManager.AppendEvent("OUT", currentDepartment);
            }
            else
            {
                // Clock in
                DataManager.AppendEvent("IN", currentDepartment);
            }

            UpdateStatusAndHours();
        }

        private void DepartmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DepartmentComboBox.SelectedItem != null)
            {
                string newDept = DepartmentComboBox.SelectedItem.ToString();
                if (newDept != currentDepartment)
                {
                    currentDepartment = newDept;

                    // Save to settings
                    var settings = DataManager.LoadDepartmentsSettings();
                    settings.CurrentDepartment = currentDepartment;
                    DataManager.SaveDepartmentsSettings(settings);

                    UpdateStatusAndHours();
                }
            }
        }

        private void ComboBoxArea_Click(object sender, RoutedEventArgs e)
        {
            // Open the dropdown when clicking on the text area
            DepartmentComboBox.IsDropDownOpen = true;
        }

    }
}

