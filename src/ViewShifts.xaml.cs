using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RpShiftTracker
{
    public partial class ViewShifts : UserControl
    {
        private DateTime currentWeekStart;
        private DateTime currentWeekEnd;
        private Dictionary<string, bool> departmentSelection = new Dictionary<string, bool>();

        public ViewShifts()
        {
            InitializeComponent();
            InitializeWeek();
            this.Loaded += ViewShifts_Loaded;
        }

        public void RefreshShiftsView()
        {
            // Update week label
            string mondayStr = currentWeekStart.ToString("yyyy-MM-dd");
            string sundayStr = currentWeekEnd.AddDays(-1).ToString("yyyy-MM-dd");
            WeekLabel.Text = $"Week: {mondayStr} (Mon) - {sundayStr} (Sun)";

            // Load departments
            var deptSettings = DataManager.LoadDepartmentsSettings();

            // Initialize selection - all departments selected by default
            departmentSelection.Clear();
            foreach (var dept in deptSettings.Departments)
            {
                if (!departmentSelection.ContainsKey(dept))
                {
                    departmentSelection[dept] = true;
                }
            }

            // Refresh the filter dropdown
            RefreshFilterDropdown();

            // Update display
            UpdateShiftsDisplay();
        }

        private void RefreshFilterDropdown()
        {
            FilterCheckBoxPanel.Children.Clear();

            var deptSettings = DataManager.LoadDepartmentsSettings();

            if (deptSettings.Departments.Count == 0)
            {
                FilterButton.IsEnabled = false;
                return;
            }

            FilterButton.IsEnabled = true;

            foreach (var dept in deptSettings.Departments)
            {
                CheckBox checkBox = new CheckBox
                {
                    Content = dept,
                    IsChecked = departmentSelection.ContainsKey(dept) ? departmentSelection[dept] : true,
                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F5F5")),
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = 12,
                    Margin = new Thickness(4, 2, 4, 2)
                };

                string deptName = dept; // Capture for lambda
                checkBox.Checked += (s, e) => { departmentSelection[deptName] = true; UpdateShiftsDisplay(); };
                checkBox.Unchecked += (s, e) => { departmentSelection[deptName] = false; UpdateShiftsDisplay(); };

                FilterCheckBoxPanel.Children.Add(checkBox);
            }
        }

        private void UpdateShiftsDisplay()
        {
            // Get selected departments
            var selectedDepartments = departmentSelection.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

            // Load all events from selected departments
            var allEvents = new List<ShiftEvent>();
            foreach (var dept in selectedDepartments)
            {
                var events = DataManager.LoadEvents(dept);
                allEvents.AddRange(events);
            }

            // Sort events by timestamp
            allEvents = allEvents.OrderBy(e => e.Timestamp).ToList();

            // Compute shifts for the week
            var shifts = DataManager.ComputeShiftsForWeek(allEvents, currentWeekStart, currentWeekEnd);

            // Calculate total seconds
            int totalSeconds = shifts.Sum(s => s.seconds);

            // Update filter button text
            var deptSettings = DataManager.LoadDepartmentsSettings();
            int selectedCount = selectedDepartments.Count;
            int totalCount = deptSettings.Departments.Count;

            if (totalCount > 0)
            {
                FilterButton.Content = $"Filter by Department ({selectedCount}/{totalCount}) ▼";
            }
            else
            {
                FilterButton.Content = "Filter by Department ▼";
            }

            TotalLabel.Text = $"Total this week: {DataManager.FormatSecondsHms(totalSeconds)}";

            // Clear and populate shifts list
            ShiftsListBox.Items.Clear();

            if (shifts.Count == 0)
            {
                ShiftsListBox.Items.Add("  No shifts recorded for this week.");
            }
            else
            {
                string[] dayAbbrevs = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

                foreach (var shift in shifts)
                {
                    string dayName = dayAbbrevs[(int)shift.start.DayOfWeek];
                    string startStr = shift.start.ToString("yyyy-MM-dd HH:mm:ss");
                    string endStr = shift.end.ToString("yyyy-MM-dd HH:mm:ss");
                    string durationStr = DataManager.FormatSecondsHms(shift.seconds);

                    string shiftLine = $"  {dayName}  {startStr}  {endStr}  {durationStr}";
                    ShiftsListBox.Items.Add(shiftLine);
                }
            }
        }

        private void ViewShifts_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshShiftsView();
        }

        private void InitializeWeek()
        {
            // Get current week (Monday to Sunday)
            DateTime now = DateTime.Now;
            // Calculate days since Monday (Monday = 0, Sunday = 6)
            int daysSinceMonday = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            currentWeekStart = now.Date.AddDays(-daysSinceMonday);
            currentWeekStart = currentWeekStart.Date; // Set to midnight
            currentWeekEnd = currentWeekStart.AddDays(7);
        }

        private void PrevWeekButton_Click(object sender, RoutedEventArgs e)
        {
            currentWeekStart = currentWeekStart.AddDays(-7);
            currentWeekEnd = currentWeekEnd.AddDays(-7);
            RefreshShiftsView();
        }

        private void NextWeekButton_Click(object sender, RoutedEventArgs e)
        {
            currentWeekStart = currentWeekStart.AddDays(7);
            currentWeekEnd = currentWeekEnd.AddDays(7);
            RefreshShiftsView();
        }

        private void ThisWeekButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeWeek();
            RefreshShiftsView();
        }

        private void DateGoButton_Click(object sender, RoutedEventArgs e)
        {
            GoToDateWeek();
        }

        private void DateEntry_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                GoToDateWeek();
            }
        }

        private void GoToDateWeek()
        {
            string dateStr = DateEntry.Text.Trim();
            if (string.IsNullOrEmpty(dateStr))
            {
                return;
            }

            try
            {
                DateTime targetDate = DateTime.Parse(dateStr);

                // Calculate week containing this date
                int daysSinceMonday = ((int)targetDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                currentWeekStart = targetDate.Date.AddDays(-daysSinceMonday);
                currentWeekStart = currentWeekStart.Date;
                currentWeekEnd = currentWeekStart.AddDays(7);

                RefreshShiftsView();
            }
            catch (FormatException)
            {
                MessageBox.Show(
                    "Please enter a valid date in the format YYYY-MM-DD.",
                    "Invalid Date",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            FilterPopup.IsOpen = !FilterPopup.IsOpen;
        }
    }
}

