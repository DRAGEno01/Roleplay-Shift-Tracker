using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RpShiftTracker
{
    public partial class DepartmentsView : UserControl
    {
        private List<string> departments = new List<string> { "Default" };
        private string currentDepartment = "Default";

        public DepartmentsView()
        {
            InitializeComponent();
            LoadDepartments();
            RefreshDepartmentsList();

            // Ensure scrolling works without needing to click
            this.Loaded += (s, e) =>
            {
                MainScrollViewer.Focusable = true;
                MainScrollViewer.Focus();
            };
        }

        private void LoadDepartments()
        {
            var settings = DataManager.LoadDepartmentsSettings();
            departments = new List<string>(settings.Departments);
            currentDepartment = settings.CurrentDepartment;

            if (departments.Count == 0)
            {
                departments = new List<string> { "Default" };
                currentDepartment = "Default";
            }
        }

        private void SaveDepartments()
        {
            var settings = new DepartmentsSettings
            {
                Departments = new List<string>(departments),
                CurrentDepartment = currentDepartment
            };
            DataManager.SaveDepartmentsSettings(settings);
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

        private void RefreshDepartmentsList()
        {
            // Clear existing list
            DepartmentsListPanel.Children.Clear();

            // Populate list
            foreach (var dept in departments)
            {
                // Create department item frame
                Border deptFrame = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E")),
                    BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252525")),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(4, 2, 4, 2)
                };

                Grid grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Department name label
                TextBlock deptLabel = new TextBlock
                {
                    Text = dept,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F5F5")),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 6, 8, 6)
                };
                Grid.SetColumn(deptLabel, 0);
                grid.Children.Add(deptLabel);

                // Delete button
                bool canDelete = departments.Count > 1 && dept != currentDepartment;

                Button deleteBtn = new Button
                {
                    Content = "Delete",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = 10,
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(4, 4, 4, 4),
                    IsEnabled = canDelete,
                    Cursor = canDelete ? Cursors.Hand : Cursors.Arrow
                };

                // Set button style based on whether it can be deleted
                if (canDelete)
                {
                    deleteBtn.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D32F2F"));
                    deleteBtn.Foreground = System.Windows.Media.Brushes.White;
                    deleteBtn.BorderThickness = new Thickness(0);
                }
                else
                {
                    deleteBtn.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#424242"));
                    deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#999999"));
                    deleteBtn.BorderThickness = new Thickness(0);
                }

                string deptName = dept; // Capture for lambda
                deleteBtn.Click += (s, e) => DeleteDepartment(deptName);

                Grid.SetColumn(deleteBtn, 1);
                grid.Children.Add(deleteBtn);

                deptFrame.Child = grid;
                DepartmentsListPanel.Children.Add(deptFrame);
            }
        }

        private void AddDepartmentButton_Click(object sender, RoutedEventArgs e)
        {
            AddDepartment();
        }

        private void NewDepartmentTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddDepartment();
            }
        }

        private void AddDepartment()
        {
            string name = NewDepartmentTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a department name.", "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (departments.Contains(name))
            {
                MessageBox.Show($"Department '{name}' already exists.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            departments.Add(name);
            SaveDepartments();
            RefreshDepartmentsList();
            NewDepartmentTextBox.Clear();
            MessageBox.Show($"Department '{name}' has been added.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteDepartment(string name)
        {
            if (departments.Count <= 1)
            {
                MessageBox.Show("You must have at least one department.", "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (name == currentDepartment)
            {
                MessageBox.Show("Cannot delete the currently active department. Switch to another department first.", "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"Are you sure you want to delete department '{name}'?\n\nAll shift records for this department will be marked as deleted.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Rename the department in CSV to [DELETED]:original_name
                string deletedName = "[DELETED]:" + name;
                DataManager.RenameDepartmentInCsv(name, deletedName);

                departments.Remove(name);
                SaveDepartments();
                RefreshDepartmentsList();
                MessageBox.Show($"Department '{name}' has been deleted.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}

