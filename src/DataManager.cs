using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;

namespace RpShiftTracker
{
    public class ShiftEvent
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } // "IN" or "OUT"
        public string Department { get; set; }
    }

    public class DepartmentsSettings
    {
        public List<string> Departments { get; set; } = new List<string> { "Default" };
        public string CurrentDepartment { get; set; } = "Default";
    }

    public class OverlaySettings
    {
        public bool Enabled { get; set; } = false;
        public string Position { get; set; } = "top-right";
        public CustomPosition CustomPosition { get; set; } = new CustomPosition();
        public double Transparency { get; set; } = 0.8;
        public bool TransparentBackground { get; set; } = false;
        public DisplayOptions DisplayOptions { get; set; } = new DisplayOptions();
    }

    public class CustomPosition
    {
        public int X { get; set; } = 100;
        public int Y { get; set; } = 100;
        public bool Enabled { get; set; } = false;
    }

    public class DisplayOptions
    {
        public bool ShowStatus { get; set; } = true;
        public bool ShowHours { get; set; } = true;
        public bool ShowWeek { get; set; } = false;
        public bool ShowDepartment { get; set; } = false;
    }

    public class DataManager
    {
        private static readonly string BASE_DIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "RpShiftTracker");
        private static readonly string CSV_FILE = Path.Combine(BASE_DIR, "time_log.csv");
        private static readonly string DEPARTMENTS_FILE = Path.Combine(BASE_DIR, "departments_settings.json");
        private static readonly string OVERLAY_FILE = Path.Combine(BASE_DIR, "overlay_settings.json");
        private static readonly string DATE_FORMAT = "yyyy-MM-ddTHH:mm:ss";

        static DataManager()
        {
            // Ensure base directory exists
            if (!Directory.Exists(BASE_DIR))
            {
                Directory.CreateDirectory(BASE_DIR);
            }
            EnsureCsvExists();
        }

        #region CSV Operations

        private static void EnsureCsvExists()
        {
            if (!File.Exists(CSV_FILE))
            {
                using (var writer = new StreamWriter(CSV_FILE, false, Encoding.UTF8))
                {
                    writer.WriteLine("timestamp,action,department");
                }
            }
            else
            {
                // Check if migration is needed (old format without department column)
                try
                {
                    var lines = File.ReadAllLines(CSV_FILE);
                    if (lines.Length > 0)
                    {
                        var header = lines[0].Split(',');
                        if (header.Length < 3 || !header.Contains("department"))
                        {
                            MigrateCsvFile();
                        }
                    }
                }
                catch
                {
                    // Ignore migration errors
                }
            }
        }

        private static void MigrateCsvFile()
        {
            try
            {
                var lines = File.ReadAllLines(CSV_FILE);
                var migratedLines = new List<string> { "timestamp,action,department" };

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length == 2)
                    {
                        migratedLines.Add($"{parts[0]},{parts[1]},Default");
                    }
                    else if (parts.Length >= 3)
                    {
                        var dept = parts[2].Trim();
                        if (string.IsNullOrEmpty(dept))
                            dept = "Default";
                        migratedLines.Add($"{parts[0]},{parts[1]},{dept}");
                    }
                }

                File.WriteAllLines(CSV_FILE, migratedLines, Encoding.UTF8);
            }
            catch
            {
                // Ignore migration errors
            }
        }

        public static List<ShiftEvent> LoadEvents(string department = null)
        {
            EnsureCsvExists();
            var events = new List<ShiftEvent>();

            try
            {
                var targetDept = string.IsNullOrEmpty(department) ? "Default" : department;

                using (var reader = new StreamReader(CSV_FILE, Encoding.UTF8))
                {
                    string line;
                    bool isFirstLine = true;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (isFirstLine)
                        {
                            isFirstLine = false;
                            continue; // Skip header
                        }

                        var parts = line.Split(',');
                        if (parts.Length >= 2)
                        {
                            var eventDept = parts.Length >= 3 ? parts[2].Trim() : "Default";
                            if (string.IsNullOrEmpty(eventDept))
                                eventDept = "Default";

                            // Filter by department if specified
                            if (targetDept != null && eventDept != targetDept)
                                continue;

                            if (DateTime.TryParseExact(parts[0], DATE_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
                            {
                                events.Add(new ShiftEvent
                                {
                                    Timestamp = timestamp,
                                    Action = parts[1].Trim(),
                                    Department = eventDept
                                });
                            }
                        }
                    }
                }

                events = events.OrderBy(e => e.Timestamp).ToList();
            }
            catch
            {
                // Return empty list on error
            }

            return events;
        }

        public static void AppendEvent(string action, string department = null)
        {
            EnsureCsvExists();
            var deptName = string.IsNullOrEmpty(department) ? "Default" : department;
            var timestamp = DateTime.Now.ToString(DATE_FORMAT);

            try
            {
                using (var writer = new StreamWriter(CSV_FILE, true, Encoding.UTF8))
                {
                    writer.WriteLine($"{timestamp},{action},{deptName}");
                }
            }
            catch
            {
                // Silently fail if we can't write
            }
        }

        public static void RenameDepartmentInCsv(string oldDepartmentName, string newDepartmentName)
        {
            EnsureCsvExists();

            try
            {
                // Read all lines
                var lines = new List<string>();
                using (var reader = new StreamReader(CSV_FILE, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }

                // Update lines with matching department
                for (int i = 0; i < lines.Count; i++)
                {
                    if (i == 0)
                        continue; // Skip header

                    var parts = lines[i].Split(',');
                    if (parts.Length >= 3)
                    {
                        var dept = parts[2].Trim();
                        if (dept == oldDepartmentName)
                        {
                            // Replace the department name
                            parts[2] = newDepartmentName;
                            lines[i] = string.Join(",", parts);
                        }
                    }
                }

                // Write all lines back
                using (var writer = new StreamWriter(CSV_FILE, false, Encoding.UTF8))
                {
                    foreach (var line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }
            }
            catch
            {
                // Silently fail if we can't update
            }
        }

        public static bool IsCurrentlyClockedIn(string department = null)
        {
            var events = LoadEvents(department);
            if (events.Count == 0)
                return false;

            var lastEvent = events.Last();
            return lastEvent.Action == "IN";
        }

        public static string GetClockedInDepartment()
        {
            var allEvents = LoadEvents(); // Load all events without filtering
            var deptGroups = allEvents.GroupBy(e => e.Department);

            foreach (var group in deptGroups)
            {
                var deptEvents = group.OrderBy(e => e.Timestamp).ToList();
                if (deptEvents.Count > 0 && deptEvents.Last().Action == "IN")
                {
                    return group.Key;
                }
            }

            return null;
        }

        public static int CalculateWeeklyHours(List<ShiftEvent> events, DateTime weekStart, DateTime weekEnd)
        {
            int totalSeconds = 0;
            DateTime? lastIn = null;
            DateTime now = DateTime.Now;

            foreach (var evt in events)
            {
                if (evt.Action == "IN")
                {
                    lastIn = evt.Timestamp;
                }
                else if (evt.Action == "OUT" && lastIn.HasValue)
                {
                    DateTime intervalStart = lastIn.Value > weekStart ? lastIn.Value : weekStart;
                    DateTime intervalEnd = evt.Timestamp < weekEnd ? evt.Timestamp : weekEnd;

                    if (intervalEnd > intervalStart)
                    {
                        totalSeconds += (int)(intervalEnd - intervalStart).TotalSeconds;
                    }
                    lastIn = null;
                }
            }

            if (lastIn.HasValue)
            {
                DateTime intervalStart = lastIn.Value > weekStart ? lastIn.Value : weekStart;
                DateTime intervalEnd = now < weekEnd ? now : weekEnd;

                if (intervalEnd > intervalStart)
                {
                    totalSeconds += (int)(intervalEnd - intervalStart).TotalSeconds;
                }
            }

            return totalSeconds;
        }

        public static List<(DateTime start, DateTime end, int seconds)> ComputeShiftsForWeek(List<ShiftEvent> events, DateTime weekStart, DateTime weekEnd)
        {
            var shifts = new List<(DateTime start, DateTime end, int seconds)>();
            if (events.Count == 0)
                return shifts;

            DateTime? lastIn = null;
            DateTime now = DateTime.Now;

            foreach (var evt in events)
            {
                if (evt.Action == "IN")
                {
                    lastIn = evt.Timestamp;
                }
                else if (evt.Action == "OUT" && lastIn.HasValue)
                {
                    DateTime intervalStart = lastIn.Value > weekStart ? lastIn.Value : weekStart;
                    DateTime intervalEnd = evt.Timestamp < weekEnd ? evt.Timestamp : weekEnd;

                    if (intervalEnd > intervalStart)
                    {
                        shifts.Add((intervalStart, intervalEnd, (int)(intervalEnd - intervalStart).TotalSeconds));
                    }
                    lastIn = null;
                }
            }

            if (lastIn.HasValue)
            {
                DateTime intervalStart = lastIn.Value > weekStart ? lastIn.Value : weekStart;
                DateTime intervalEnd = now < weekEnd ? now : weekEnd;

                if (intervalEnd > intervalStart)
                {
                    shifts.Add((intervalStart, intervalEnd, (int)(intervalEnd - intervalStart).TotalSeconds));
                }
            }

            return shifts;
        }

        public static string FormatSecondsHms(int totalSeconds)
        {
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        #endregion

        #region Departments Operations

        public static DepartmentsSettings LoadDepartmentsSettings()
        {
            try
            {
                if (File.Exists(DEPARTMENTS_FILE))
                {
                    var json = File.ReadAllText(DEPARTMENTS_FILE, Encoding.UTF8);
                    var settings = DeserializeDepartmentsSettings(json);
                    if (settings != null && settings.Departments != null && settings.Departments.Count > 0)
                    {
                        // Ensure current department exists
                        if (!settings.Departments.Contains(settings.CurrentDepartment))
                        {
                            settings.CurrentDepartment = settings.Departments[0];
                        }
                        return settings;
                    }
                }
            }
            catch
            {
                // Return default on error
            }

            return new DepartmentsSettings();
        }

        public static void SaveDepartmentsSettings(DepartmentsSettings settings)
        {
            try
            {
                var json = SerializeDepartmentsSettings(settings);
                File.WriteAllText(DEPARTMENTS_FILE, json, Encoding.UTF8);
            }
            catch
            {
                // Silently fail if we can't save
            }
        }

        private static string SerializeDepartmentsSettings(DepartmentsSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"departments\": [");
            for (int i = 0; i < settings.Departments.Count; i++)
            {
                sb.Append("    \"").Append(EscapeJson(settings.Departments[i])).Append("\"");
                if (i < settings.Departments.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");
            sb.Append("  \"current_department\": \"").Append(EscapeJson(settings.CurrentDepartment)).AppendLine("\"");
            sb.Append("}");
            return sb.ToString();
        }

        private static DepartmentsSettings DeserializeDepartmentsSettings(string json)
        {
            var settings = new DepartmentsSettings();
            try
            {
                // Simple JSON parsing - extract departments array and current_department
                int deptStart = json.IndexOf("\"departments\"");
                if (deptStart >= 0)
                {
                    int arrayStart = json.IndexOf('[', deptStart);
                    int arrayEnd = json.IndexOf(']', arrayStart);
                    if (arrayStart >= 0 && arrayEnd >= 0)
                    {
                        string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                        var deptList = new List<string>();
                        foreach (var item in arrayContent.Split(','))
                        {
                            string trimmed = item.Trim();
                            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                            {
                                deptList.Add(UnescapeJson(trimmed.Substring(1, trimmed.Length - 2)));
                            }
                        }
                        settings.Departments = deptList;
                    }
                }

                int currentDeptStart = json.IndexOf("\"current_department\"");
                if (currentDeptStart >= 0)
                {
                    int colonPos = json.IndexOf(':', currentDeptStart);
                    int quoteStart = json.IndexOf('"', colonPos);
                    int quoteEnd = json.IndexOf('"', quoteStart + 1);
                    if (quoteStart >= 0 && quoteEnd >= 0)
                    {
                        settings.CurrentDepartment = UnescapeJson(json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1));
                    }
                }
            }
            catch
            {
                return new DepartmentsSettings();
            }
            return settings;
        }

        #endregion

        #region Overlay Operations

        public static OverlaySettings LoadOverlaySettings()
        {
            try
            {
                if (File.Exists(OVERLAY_FILE))
                {
                    var json = File.ReadAllText(OVERLAY_FILE, Encoding.UTF8);
                    var settings = DeserializeOverlaySettings(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch
            {
                // Return default on error
            }

            return new OverlaySettings();
        }

        public static void SaveOverlaySettings(OverlaySettings settings)
        {
            try
            {
                var json = SerializeOverlaySettings(settings);
                File.WriteAllText(OVERLAY_FILE, json, Encoding.UTF8);
            }
            catch
            {
                // Silently fail if we can't save
            }
        }

        private static string SerializeOverlaySettings(OverlaySettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.Append("  \"enabled\": ").Append(settings.Enabled.ToString().ToLower()).AppendLine(",");
            sb.Append("  \"position\": \"").Append(EscapeJson(settings.Position)).AppendLine("\",");
            sb.AppendLine("  \"custom_position\": {");
            sb.Append("    \"x\": ").Append(settings.CustomPosition.X).AppendLine(",");
            sb.Append("    \"y\": ").Append(settings.CustomPosition.Y).AppendLine(",");
            sb.Append("    \"enabled\": ").Append(settings.CustomPosition.Enabled.ToString().ToLower()).AppendLine();
            sb.AppendLine("  },");
            sb.Append("  \"transparency\": ").Append(settings.Transparency.ToString("F1", CultureInfo.InvariantCulture)).AppendLine(",");
            sb.Append("  \"transparent_background\": ").Append(settings.TransparentBackground.ToString().ToLower()).AppendLine(",");
            sb.AppendLine("  \"display_options\": {");
            sb.Append("    \"show_status\": ").Append(settings.DisplayOptions.ShowStatus.ToString().ToLower()).AppendLine(",");
            sb.Append("    \"show_hours\": ").Append(settings.DisplayOptions.ShowHours.ToString().ToLower()).AppendLine(",");
            sb.Append("    \"show_week\": ").Append(settings.DisplayOptions.ShowWeek.ToString().ToLower()).AppendLine(",");
            sb.Append("    \"show_department\": ").Append(settings.DisplayOptions.ShowDepartment.ToString().ToLower()).AppendLine();
            sb.AppendLine("  }");
            sb.Append("}");
            return sb.ToString();
        }

        private static OverlaySettings DeserializeOverlaySettings(string json)
        {
            var settings = new OverlaySettings();
            try
            {
                // Parse enabled
                int enabledPos = json.IndexOf("\"enabled\"");
                if (enabledPos >= 0)
                {
                    int colonPos = json.IndexOf(':', enabledPos);
                    int valueEnd = json.IndexOfAny(new[] { ',', '\n', '}' }, colonPos + 1);
                    if (valueEnd >= 0)
                    {
                        string value = json.Substring(colonPos + 1, valueEnd - colonPos - 1).Trim();
                        settings.Enabled = value == "true";
                    }
                }

                // Parse position
                int positionPos = json.IndexOf("\"position\"");
                if (positionPos >= 0)
                {
                    int colonPos = json.IndexOf(':', positionPos);
                    int quoteStart = json.IndexOf('"', colonPos);
                    int quoteEnd = json.IndexOf('"', quoteStart + 1);
                    if (quoteStart >= 0 && quoteEnd >= 0)
                    {
                        settings.Position = UnescapeJson(json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1));
                    }
                }

                // Parse custom_position
                int customPosStart = json.IndexOf("\"custom_position\"");
                if (customPosStart >= 0)
                {
                    int objStart = json.IndexOf('{', customPosStart);
                    int objEnd = FindMatchingBrace(json, objStart);
                    if (objStart >= 0 && objEnd >= 0)
                    {
                        string customJson = json.Substring(objStart, objEnd - objStart + 1);
                        int xPos = customJson.IndexOf("\"x\"");
                        if (xPos >= 0)
                        {
                            int colonPos = customJson.IndexOf(':', xPos);
                            int valueEnd = customJson.IndexOfAny(new[] { ',', '\n', '}' }, colonPos + 1);
                            if (valueEnd >= 0 && int.TryParse(customJson.Substring(colonPos + 1, valueEnd - colonPos - 1).Trim(), out int x))
                                settings.CustomPosition.X = x;
                        }
                        int yPos = customJson.IndexOf("\"y\"");
                        if (yPos >= 0)
                        {
                            int colonPos = customJson.IndexOf(':', yPos);
                            int valueEnd = customJson.IndexOfAny(new[] { ',', '\n', '}' }, colonPos + 1);
                            if (valueEnd >= 0 && int.TryParse(customJson.Substring(colonPos + 1, valueEnd - colonPos - 1).Trim(), out int y))
                                settings.CustomPosition.Y = y;
                        }
                        int enabledPos2 = customJson.IndexOf("\"enabled\"");
                        if (enabledPos2 >= 0)
                        {
                            int colonPos = customJson.IndexOf(':', enabledPos2);
                            int valueEnd = customJson.IndexOfAny(new[] { ',', '\n', '}' }, colonPos + 1);
                            if (valueEnd >= 0)
                            {
                                string value = customJson.Substring(colonPos + 1, valueEnd - colonPos - 1).Trim();
                                settings.CustomPosition.Enabled = value == "true";
                            }
                        }
                    }
                }

                // Parse transparency
                int transparencyPos = json.IndexOf("\"transparency\"");
                if (transparencyPos >= 0)
                {
                    int colonPos = json.IndexOf(':', transparencyPos);
                    int valueEnd = json.IndexOfAny(new[] { ',', '\n', '}' }, colonPos + 1);
                    if (valueEnd >= 0 && double.TryParse(json.Substring(colonPos + 1, valueEnd - colonPos - 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double trans))
                        settings.Transparency = trans;
                }

                // Parse transparent_background
                int transBgPos = json.IndexOf("\"transparent_background\"");
                if (transBgPos >= 0)
                {
                    int colonPos = json.IndexOf(':', transBgPos);
                    int valueEnd = json.IndexOfAny(new[] { ',', '\n', '}' }, colonPos + 1);
                    if (valueEnd >= 0)
                    {
                        string value = json.Substring(colonPos + 1, valueEnd - colonPos - 1).Trim();
                        settings.TransparentBackground = value == "true";
                    }
                }

                // Parse display_options
                int displayOptsStart = json.IndexOf("\"display_options\"");
                if (displayOptsStart >= 0)
                {
                    int objStart = json.IndexOf('{', displayOptsStart);
                    int objEnd = FindMatchingBrace(json, objStart);
                    if (objStart >= 0 && objEnd >= 0)
                    {
                        string displayJson = json.Substring(objStart, objEnd - objStart + 1);
                        settings.DisplayOptions.ShowStatus = ParseBoolValue(displayJson, "\"show_status\"");
                        settings.DisplayOptions.ShowHours = ParseBoolValue(displayJson, "\"show_hours\"");
                        settings.DisplayOptions.ShowWeek = ParseBoolValue(displayJson, "\"show_week\"");
                        settings.DisplayOptions.ShowDepartment = ParseBoolValue(displayJson, "\"show_department\"");
                    }
                }
            }
            catch
            {
                return new OverlaySettings();
            }
            return settings;
        }

        private static bool ParseBoolValue(string json, string key)
        {
            int keyPos = json.IndexOf(key);
            if (keyPos >= 0)
            {
                int colonPos = json.IndexOf(':', keyPos);
                int valueEnd = json.IndexOfAny(new[] { ',', '\n', '}' }, colonPos + 1);
                if (valueEnd >= 0)
                {
                    string value = json.Substring(colonPos + 1, valueEnd - colonPos - 1).Trim();
                    return value == "true";
                }
            }
            return false;
        }

        private static int FindMatchingBrace(string json, int startPos)
        {
            int depth = 0;
            for (int i = startPos; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') depth--;
                if (depth == 0) return i;
            }
            return -1;
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static string UnescapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
        }

        #endregion
    }
}

