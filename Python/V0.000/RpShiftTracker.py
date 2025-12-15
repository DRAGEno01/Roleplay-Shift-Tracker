import csv
import os
import json
from datetime import datetime, timedelta
import tkinter as tk
from tkinter import messagebox
import webbrowser
import urllib.request
import urllib.error


CSV_FILENAME = "time_log.csv"
OVERLAY_SETTINGS_FILENAME = "overlay_settings.json"
DEPARTMENTS_SETTINGS_FILENAME = "departments_settings.json"
DATE_FORMAT = "%Y-%m-%dT%H:%M:%S"
VERSION = "0.000"
INFO_JSON_URL = "https://raw.githubusercontent.com/DRAGEno01/Roleplay-Shift-Tracker/refs/heads/main/Python/info.json"


def get_csv_path() -> str:
    """Return path to CSV file in the same directory as this script."""
    base_dir = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base_dir, CSV_FILENAME)


def ensure_csv_exists():
    """Create CSV file with header if it does not exist. Also migrates old CSV files to include department column."""
    path = get_csv_path()
    if not os.path.exists(path):
        with open(path, mode="w", newline="", encoding="utf-8") as f:
            writer = csv.writer(f)
            writer.writerow(["timestamp", "action", "department"])
    else:
        try:
            needs_migration = False
            with open(path, mode="r", newline="", encoding="utf-8") as f:
                reader = csv.reader(f)
                header = next(reader, None)
                if header:
                    if len(header) < 3:
                        needs_migration = True
                    else:
                        first_row = next(reader, None)
                        if first_row and len(first_row) > len(header):
                            needs_migration = True
                
            if needs_migration:
                rows = []
                with open(path, mode="r", newline="", encoding="utf-8") as f:
                    reader = csv.reader(f)
                    next(reader, None)  # Skip header
                    for row in reader:
                        if len(row) == 2:
                            rows.append([row[0], row[1], "Default"])
                        elif len(row) >= 3:
                            rows.append([row[0], row[1], row[2] if len(row) > 2 and row[2].strip() else "Default"])
                        else:
                            continue
                
                with open(path, mode="w", newline="", encoding="utf-8") as f_write:
                    writer = csv.writer(f_write)
                    writer.writerow(["timestamp", "action", "department"])
                    for row in rows:
                        writer.writerow(row)
        except (IOError, csv.Error, StopIteration):
            pass


def load_events(department: str = None):
    """Load all events from CSV as list of (datetime, action). Filters by department if provided."""
    ensure_csv_exists()
    events = []
    path = get_csv_path()
    
    # Default to "Default" if no department specified
    target_dept = department if department else "Default"
    
    with open(path, mode="r", newline="", encoding="utf-8") as f:
        # Use csv.reader instead of DictReader to handle malformed headers
        reader = csv.reader(f)
        header = next(reader, None)
        
        # Determine column indices
        if header:
            try:
                ts_idx = header.index("timestamp")
                action_idx = header.index("action")
                dept_idx = header.index("department") if "department" in header else -1
            except ValueError:
                # Fallback: assume standard order
                ts_idx = 0
                action_idx = 1
                dept_idx = 2 if len(header) > 2 else -1
        else:
            ts_idx = 0
            action_idx = 1
            dept_idx = 2
        
        for row in reader:
            if len(row) <= ts_idx or len(row) <= action_idx:
                continue
            
            ts_str = row[ts_idx].strip() if ts_idx < len(row) else ""
            action = row[action_idx].strip() if action_idx < len(row) else ""
            
            # Get department from row
            if dept_idx >= 0 and dept_idx < len(row):
                row_dept = row[dept_idx].strip()
            else:
                row_dept = ""
            
            # For backward compatibility: if no department column exists or is empty, 
            # treat as "Default" department
            if not row_dept:
                row_dept = "Default"
            
            # Filter by department if specified
            if row_dept != target_dept:
                continue
            
            if not ts_str or not action:
                continue
            try:
                ts = datetime.strptime(ts_str, DATE_FORMAT)
                events.append((ts, action))
            except ValueError:
                continue
    events.sort(key=lambda x: x[0])
    return events


def append_event(action: str, department: str = None):
    """Append a clock IN or OUT event to CSV."""
    ensure_csv_exists()
    path = get_csv_path()
    now = datetime.now()
    # Default to "Default" if no department specified
    dept_name = department if department else "Default"
    with open(path, mode="a", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow([now.strftime(DATE_FORMAT), action, dept_name])


def start_of_current_week():
    """
    Return datetime for Monday 00:00:00 of the current week (local time).
    Week is Monday 00:00 to next Monday 00:00 (Sunday 23:59:59 inclusive).
    """
    now = datetime.now()
    days_since_monday = now.isoweekday() - 1
    monday = datetime(year=now.year, month=now.month, day=now.day)
    monday -= timedelta(days=days_since_monday)
    monday = monday.replace(hour=0, minute=0, second=0, microsecond=0)
    return monday


def current_week_range():
    """Return (week_start, week_end) datetimes for current week."""
    week_start = start_of_current_week()
    week_end = week_start + timedelta(days=7)
    return week_start, week_end


def week_range_for_date(d: datetime):
    """Return (week_start, week_end) for the week containing the given date (Mon–Sun)."""
    days_since_monday = d.isoweekday() - 1
    monday = datetime(year=d.year, month=d.month, day=d.day)
    monday -= timedelta(days=days_since_monday)
    monday = monday.replace(hour=0, minute=0, second=0, microsecond=0)
    week_start = monday
    week_end = week_start + timedelta(days=7)
    return week_start, week_end


def compute_weekly_seconds(events):
    """
    Compute total seconds worked in the current week, given a list of
    (datetime, action) events. Handles ongoing session (no OUT yet) and
    clamps intervals to the current week [Monday 00:00, next Monday 00:00).
    """
    if not events:
        return 0

    week_start, week_end = current_week_range()
    total_seconds = 0

    last_in = None

    for ts, action in events:
        if action == "IN":
            last_in = ts
        elif action == "OUT":
            if last_in is None:
                continue
            interval_start = max(last_in, week_start)
            interval_end = min(ts, week_end)
            if interval_end > interval_start:
                total_seconds += (interval_end - interval_start).total_seconds()
            last_in = None

    if last_in is not None:
        now = datetime.now()
        interval_start = max(last_in, week_start)
        interval_end = min(now, week_end)
        if interval_end > interval_start:
            total_seconds += (interval_end - interval_start).total_seconds()

    return int(total_seconds)


def compute_shifts_for_week(events, week_start: datetime, week_end: datetime):
    """
    Given events and a specific week [week_start, week_end),
    return a list of (shift_start, shift_end, duration_seconds) for that week.
    Shifts are clamped to the given week range.
    """
    shifts = []
    if not events:
        return shifts

    last_in = None
    now = datetime.now()

    for ts, action in events:
        if action == "IN":
            last_in = ts
        elif action == "OUT":
            if last_in is None:
                continue
            interval_start = max(last_in, week_start)
            interval_end = min(ts, week_end)
            if interval_end > interval_start:
                shifts.append(
                    (interval_start, interval_end, int((interval_end - interval_start).total_seconds()))
                )
            last_in = None

    if last_in is not None:
        interval_start = max(last_in, week_start)
        interval_end = min(now, week_end)
        if interval_end > interval_start:
            shifts.append(
                (interval_start, interval_end, int((interval_end - interval_start).total_seconds()))
            )

    return shifts


def format_seconds_hms(total_seconds: int) -> str:
    """Format seconds as HH:MM:SS."""
    hours = total_seconds // 3600
    minutes = (total_seconds % 3600) // 60
    seconds = total_seconds % 60
    return f"{hours:02d}:{minutes:02d}:{seconds:02d}"


def is_currently_clocked_in(events):
    """Return True if last event is IN without a following OUT."""
    if not events:
        return False
    last_action = events[-1][1]
    return last_action == "IN"


def get_clocked_in_department():
    """Return the department name if user is currently clocked in, None otherwise."""
    ensure_csv_exists()
    path = get_csv_path()
    
    # Read all events and group by department
    dept_events = {}
    with open(path, mode="r", newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            row_dept = row.get("department", "").strip()
            if not row_dept:
                row_dept = "Default"
            
            if row_dept not in dept_events:
                dept_events[row_dept] = []
            
            ts_str = row.get("timestamp")
            action = row.get("action")
            if ts_str and action:
                try:
                    ts = datetime.strptime(ts_str, DATE_FORMAT)
                    dept_events[row_dept].append((ts, action))
                except ValueError:
                    continue
    
    # Check each department to see if clocked in
    for dept, events in dept_events.items():
        events.sort(key=lambda x: x[0])
        if is_currently_clocked_in(events):
            return dept
    
    return None


def fetch_app_info():
    """Fetch app info from GitHub. Returns dict with version, supported, allowUsage, or None on error."""
    try:
        with urllib.request.urlopen(INFO_JSON_URL, timeout=5) as response:
            data = json.loads(response.read().decode())
            return {
                "version": data.get("version", VERSION),
                "supported": data.get("supported", True),
                "allowUsage": data.get("allowUsage", True),
            }
    except (urllib.error.URLError, urllib.error.HTTPError, urllib.error.ContentTooShortError, 
            json.JSONDecodeError, ValueError, OSError, Exception) as e:
        # Silently fail - no internet or other network issues
        return None


def get_overlay_settings_path() -> str:
    """Return path to overlay settings JSON file in the same directory as this script."""
    base_dir = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base_dir, OVERLAY_SETTINGS_FILENAME)


def load_overlay_settings():
    """Load overlay settings from JSON file. Returns default settings if file doesn't exist."""
    path = get_overlay_settings_path()
    default_settings = {
        "enabled": False,
        "position": "top-right",
        "custom_position": {"x": 100, "y": 100, "enabled": False},
        "transparency": 0.8,
        "transparent_background": False,
        "display_options": {
            "show_status": True,
            "show_hours": True,
            "show_week": False,
            "show_department": False,
        }
    }
    
    if not os.path.exists(path):
        return default_settings
    
    try:
        with open(path, mode="r", encoding="utf-8") as f:
            settings = json.load(f)
            # Merge with defaults to ensure all keys exist
            for key, value in default_settings.items():
                if key not in settings:
                    settings[key] = value
            return settings
    except (json.JSONDecodeError, IOError):
        return default_settings


def save_overlay_settings(settings):
    """Save overlay settings to JSON file."""
    path = get_overlay_settings_path()
    try:
        with open(path, mode="w", encoding="utf-8") as f:
            json.dump(settings, f, indent=2)
    except IOError:
        pass  # Silently fail if we can't save


def get_departments_settings_path() -> str:
    """Return path to departments settings JSON file in the same directory as this script."""
    base_dir = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base_dir, DEPARTMENTS_SETTINGS_FILENAME)


def load_departments_settings():
    """Load departments settings from JSON file. Returns default settings if file doesn't exist."""
    path = get_departments_settings_path()
    default_settings = {
        "departments": ["Default"],
        "current_department": "Default"
    }
    
    if not os.path.exists(path):
        return default_settings
    
    try:
        with open(path, mode="r", encoding="utf-8") as f:
            settings = json.load(f)
            # Merge with defaults to ensure all keys exist
            for key, value in default_settings.items():
                if key not in settings:
                    settings[key] = value
            # Ensure current_department exists in departments list
            if settings["current_department"] not in settings["departments"]:
                settings["current_department"] = settings["departments"][0] if settings["departments"] else "Default"
            return settings
    except (json.JSONDecodeError, IOError):
        return default_settings


def save_departments_settings(settings):
    """Save departments settings to JSON file."""
    path = get_departments_settings_path()
    try:
        with open(path, mode="w", encoding="utf-8") as f:
            json.dump(settings, f, indent=2)
    except IOError:
        pass  # Silently fail if we can't save


class TimeTrackerApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("Roleplay Shift Tracker")
        self.root.geometry("840x480")
        self.root.resizable(False, False)

        self.bg_color = "#121212"
        self.primary_text = "#f5f5f5"
        self.muted_text = "#aaaaaa"
        self.accent = "#64b5f6"
        self.positive = "#4caf50"
        self.negative = "#ef5350"

        self.root.configure(bg=self.bg_color)

        self.menu_width = 260
        self.menu_x = -self.menu_width
        self.menu_open = False

        # Overlay state (load from settings file)
        overlay_settings = load_overlay_settings()
        self.overlay_enabled = overlay_settings["enabled"]
        self.overlay_position = overlay_settings["position"]
        self.overlay_custom_position = overlay_settings["custom_position"]
        self.overlay_transparency = overlay_settings["transparency"]
        self.overlay_transparent_background = overlay_settings["transparent_background"]
        self.overlay_display_options = overlay_settings["display_options"]
        self.overlay_window = None
        # Color used for transparent background (slightly off-black to avoid conflicts)
        self.transparent_bg_color = "#010101"

        # Departments state (load from settings file)
        departments_settings = load_departments_settings()
        self.departments = departments_settings["departments"]
        self.current_department = departments_settings["current_department"]
        # Ensure current_department exists
        if not self.departments:
            self.departments = ["Default"]
        if self.current_department not in self.departments:
            self.current_department = self.departments[0]
        
        # Auto-detect department: if user is clocked in, switch to that department
        # Otherwise, use the last department from settings
        clocked_in_dept = get_clocked_in_department()
        if clocked_in_dept and clocked_in_dept in self.departments:
            self.current_department = clocked_in_dept
        elif self.current_department not in self.departments:
            self.current_department = self.departments[0]
        
        # Save the current department as the last used one
        self.save_departments_settings()

        self.events = load_events(self.current_department)
        
        # Update state
        self.update_available = False
        self.app_info = None

        self.main_frame = tk.Frame(self.root, bg=self.bg_color)
        self.main_frame.pack(fill=tk.BOTH, expand=True)

        self.content_frame = tk.Frame(self.main_frame, bg=self.bg_color)
        self.content_frame.pack(fill=tk.BOTH, expand=True)

        # Menu button container for badge
        menu_button_frame = tk.Frame(self.content_frame, bg=self.bg_color)
        menu_button_frame.pack(anchor="nw", padx=10, pady=8)
        
        self.menu_button = tk.Button(
            menu_button_frame,
            text="☰",
            font=("Segoe UI", 12, "bold"),
            width=3,
            command=self.toggle_menu,
            bg="#1f1f1f",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=0,
            relief=tk.FLAT,
            cursor="hand2",
        )
        self.menu_button.pack(side=tk.LEFT)
        
        # Badge for update notification on menu button
        self.menu_badge = tk.Label(
            menu_button_frame,
            text="1",
            font=("Segoe UI", 8, "bold"),
            bg="#ef5350",
            fg="white",
            width=2,
            height=1,
        )
        self.menu_badge.pack(side=tk.LEFT, padx=(2, 0))
        self.menu_badge.pack_forget()  # Initially hidden

        self.home_frame = tk.Frame(self.content_frame, bg=self.bg_color)
        self.home_frame.pack(expand=True)
    
        # Inner centered frame for main Home content
        self.center_frame = tk.Frame(self.home_frame, bg=self.bg_color)
        self.center_frame.pack(expand=True)

        # Department selector
        dept_selector_frame = tk.Frame(self.center_frame, bg=self.bg_color)
        dept_selector_frame.pack(pady=(0, 12))
        
        dept_label = tk.Label(
            dept_selector_frame,
            text="Department:",
            font=("Segoe UI", 10),
            bg=self.bg_color,
            fg=self.muted_text,
        )
        dept_label.pack(side=tk.LEFT, padx=(0, 8))
        
        self.home_department_var = tk.StringVar(value=self.current_department)
        self.home_department_dropdown = tk.OptionMenu(
            dept_selector_frame,
            self.home_department_var,
            *self.departments,
            command=self.change_department
        )
        self.home_department_dropdown.config(
            font=("Segoe UI", 10),
            bg="#1e1e1e",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=1,
            relief=tk.SOLID,
            highlightthickness=0,
        )
        self.home_department_dropdown.pack(side=tk.LEFT)

        # UI elements (Home screen)
        self.status_label = tk.Label(
            self.center_frame, text="", font=("Segoe UI", 18, "bold"),
            bg=self.bg_color, fg=self.primary_text
        )
        self.status_label.pack(pady=(0, 8))

        self.week_label = tk.Label(
            self.center_frame, text="", font=("Segoe UI", 11),
            bg=self.bg_color, fg=self.muted_text
        )
        self.week_label.pack(pady=2)

        self.hours_label_title = tk.Label(
            self.center_frame, text="Hours this week:", font=("Segoe UI", 13),
            bg=self.bg_color, fg=self.primary_text
        )
        self.hours_label_title.pack(pady=(16, 0))

        self.hours_label = tk.Label(
            self.center_frame, text="00:00:00", font=("Consolas", 26, "bold"),
            fg=self.accent, bg=self.bg_color
        )
        self.hours_label.pack(pady=(2, 14))

        self.button = tk.Button(
            self.center_frame,
            text="Clock In",
            font=("Segoe UI", 13, "bold"),
            width=18,
            command=self.toggle_clock,
            bg="#1e88e5",
            fg="white",
            activebackground="#1565c0",
            activeforeground="white",
        )
        self.button.pack(pady=8)

        self.footer_label = tk.Label(
            self.home_frame,
            text="Week is Monday 00:00 to Sunday 23:59:59\nAll times are local.",
            font=("Segoe UI", 8),
            fg=self.muted_text,
            bg=self.bg_color,
        )
        self.footer_label.pack(side=tk.BOTTOM, pady=4)

        # --- Shifts view frame (initially hidden) ---
        self.shifts_frame = tk.Frame(self.content_frame, bg=self.bg_color)
        # State for which week is being viewed
        self.current_shifts_week_start, self.current_shifts_week_end = current_week_range()

        top_controls = tk.Frame(self.shifts_frame, bg=self.bg_color)
        top_controls.pack(fill=tk.X, padx=16, pady=(16, 8))

        self.shifts_title = tk.Label(
            top_controls,
            text="Weekly Shifts",
            font=("Segoe UI", 14, "bold"),
            bg=self.bg_color,
            fg=self.primary_text,
        )
        self.shifts_title.pack(side=tk.LEFT)

        nav_frame = tk.Frame(top_controls, bg=self.bg_color)
        nav_frame.pack(side=tk.RIGHT)

        self.prev_week_btn = tk.Button(
            nav_frame,
            text="◀ Prev",
            font=("Segoe UI", 9),
            command=self.go_prev_week,
            bg="#1e1e1e",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=0,
            relief=tk.FLAT,
            cursor="hand2",
        )
        self.prev_week_btn.pack(side=tk.LEFT, padx=2)

        self.this_week_btn = tk.Button(
            nav_frame,
            text="This Week",
            font=("Segoe UI", 9),
            command=self.go_this_week,
            bg="#1e1e1e",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=0,
            relief=tk.FLAT,
            cursor="hand2",
        )
        self.this_week_btn.pack(side=tk.LEFT, padx=2)

        self.next_week_btn = tk.Button(
            nav_frame,
            text="Next ▶",
            font=("Segoe UI", 9),
            command=self.go_next_week,
            bg="#1e1e1e",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=0,
            relief=tk.FLAT,
            cursor="hand2",
        )
        self.next_week_btn.pack(side=tk.LEFT, padx=2)

        # Date selector row
        date_row = tk.Frame(self.shifts_frame, bg=self.bg_color)
        date_row.pack(fill=tk.X, padx=16, pady=(4, 8))

        date_label = tk.Label(
            date_row,
            text="Jump to week containing date (YYYY-MM-DD):",
            font=("Segoe UI", 9),
            bg=self.bg_color,
            fg=self.muted_text,
        )
        date_label.pack(side=tk.LEFT)

        self.date_entry = tk.Entry(
            date_row,
            width=12,
            font=("Consolas", 10),
            bg="#1e1e1e",
            fg=self.primary_text,
            insertbackground=self.primary_text,
            bd=1,
            relief=tk.SOLID,
        )
        self.date_entry.pack(side=tk.LEFT, padx=(6, 4))

        self.date_go_btn = tk.Button(
            date_row,
            text="Go",
            font=("Segoe UI", 9),
            command=self.go_to_date_week,
            bg="#1e88e5",
            fg="white",
            activebackground="#1565c0",
            activeforeground="white",
            bd=0,
            relief=tk.FLAT,
            cursor="hand2",
        )
        self.date_go_btn.pack(side=tk.LEFT)

        # Week summary "card"
        summary_card = tk.Frame(self.shifts_frame, bg="#181818", bd=1, relief=tk.SOLID)
        summary_card.pack(fill=tk.X, padx=16, pady=(4, 8))

        self.shifts_week_label = tk.Label(
            summary_card,
            text="",
            font=("Segoe UI", 10),
            bg="#181818",
            fg=self.muted_text,
        )
        self.shifts_week_label.pack(anchor="w", padx=10, pady=(8, 0))

        self.shifts_total_label = tk.Label(
            summary_card,
            text="Total this week: 00:00:00",
            font=("Segoe UI", 11, "bold"),
            bg="#181818",
            fg=self.accent,
        )
        self.shifts_total_label.pack(anchor="w", padx=10, pady=(2, 8))

        # Shifts list "table"
        table_card = tk.Frame(self.shifts_frame, bg="#181818", bd=1, relief=tk.SOLID)
        table_card.pack(fill=tk.BOTH, expand=True, padx=16, pady=(4, 16))

        header_frame = tk.Frame(table_card, bg="#181818")
        header_frame.pack(fill=tk.X, padx=8, pady=(6, 4))

        hdr_day = tk.Label(
            header_frame,
            text="DAY",
            font=("Segoe UI", 9, "bold"),
            bg="#181818",
            fg=self.muted_text,
            width=6,
            anchor="w",
        )
        hdr_day.pack(side=tk.LEFT)

        hdr_start = tk.Label(
            header_frame,
            text="START",
            font=("Segoe UI", 9, "bold"),
            bg="#181818",
            fg=self.muted_text,
            width=20,
            anchor="w",
        )
        hdr_start.pack(side=tk.LEFT)

        hdr_end = tk.Label(
            header_frame,
            text="END",
            font=("Segoe UI", 9, "bold"),
            bg="#181818",
            fg=self.muted_text,
            width=20,
            anchor="w",
        )
        hdr_end.pack(side=tk.LEFT)

        hdr_dur = tk.Label(
            header_frame,
            text="DURATION",
            font=("Segoe UI", 9, "bold"),
            bg="#181818",
            fg=self.muted_text,
            anchor="w",
        )
        hdr_dur.pack(side=tk.LEFT, padx=(4, 0))

        header_divider = tk.Frame(table_card, bg="#252525", height=1)
        header_divider.pack(fill=tk.X, padx=8, pady=(0, 2))

        list_frame = tk.Frame(table_card, bg="#181818")
        list_frame.pack(fill=tk.BOTH, expand=True, padx=4, pady=(0, 6))

        self.shifts_listbox = tk.Listbox(
            list_frame,
            font=("Consolas", 10),
            bg="#101010",
            fg=self.primary_text,
            selectbackground="#333333",
            selectforeground=self.accent,
            activestyle="none",
            borderwidth=0,
            highlightthickness=0,
        )
        self.shifts_listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        scrollbar = tk.Scrollbar(list_frame, orient=tk.VERTICAL, command=self.shifts_listbox.yview)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.shifts_listbox.config(yscrollcommand=scrollbar.set)

        # --- Overlay view frame (initially hidden) ---
        self.overlay_frame = tk.Frame(self.content_frame, bg=self.bg_color)
        
        # Create scrollable canvas
        overlay_canvas = tk.Canvas(self.overlay_frame, bg=self.bg_color, highlightthickness=0)
        overlay_scrollbar = tk.Scrollbar(self.overlay_frame, orient=tk.VERTICAL, command=overlay_canvas.yview)
        overlay_scrollable_frame = tk.Frame(overlay_canvas, bg=self.bg_color)
        
        overlay_scrollable_frame.bind(
            "<Configure>",
            lambda e: overlay_canvas.configure(scrollregion=overlay_canvas.bbox("all"))
        )
        
        canvas_window = overlay_canvas.create_window((0, 0), window=overlay_scrollable_frame, anchor="nw")
        overlay_canvas.configure(yscrollcommand=overlay_scrollbar.set)
        
        overlay_canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        overlay_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        # Update canvas window width when canvas is resized
        def _configure_canvas(event):
            canvas_width = event.width
            overlay_canvas.itemconfig(canvas_window, width=canvas_width)
        overlay_canvas.bind('<Configure>', _configure_canvas)
        
        # Update scroll region when scrollable frame content changes
        def _update_scroll_region(event=None):
            overlay_canvas.configure(scrollregion=overlay_canvas.bbox("all"))
        overlay_scrollable_frame.bind('<Configure>', _update_scroll_region)
        
        # Bind mousewheel to canvas and scrollable frame
        def _on_overlay_mousewheel(event):
            # Only scroll if overlay frame is currently visible
            try:
                if self.overlay_frame.winfo_viewable():
                    overlay_canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
            except:
                pass
        
        # Bind to canvas and scrollable frame
        overlay_canvas.bind("<MouseWheel>", _on_overlay_mousewheel)
        overlay_scrollable_frame.bind("<MouseWheel>", _on_overlay_mousewheel)
        # Also bind to all child widgets recursively
        def bind_mousewheel_to_children(parent):
            for child in parent.winfo_children():
                try:
                    child.bind("<MouseWheel>", _on_overlay_mousewheel)
                    bind_mousewheel_to_children(child)
                except:
                    pass
        bind_mousewheel_to_children(overlay_scrollable_frame)
        
        overlay_inner = tk.Frame(overlay_scrollable_frame, bg=self.bg_color)
        overlay_inner.pack(expand=True, fill=tk.BOTH, padx=32, pady=32)
        
        # Header
        overlay_title = tk.Label(
            overlay_inner,
            text="Overlay",
            font=("Segoe UI", 18, "bold"),
            bg=self.bg_color,
            fg=self.primary_text,
        )
        overlay_title.pack(anchor="w", pady=(0, 4))
        
        overlay_sub = tk.Label(
            overlay_inner,
            text="Configure the on-screen overlay display for tracking your shifts.",
            font=("Segoe UI", 9),
            bg=self.bg_color,
            fg=self.muted_text,
        )
        overlay_sub.pack(anchor="w", pady=(0, 16))
        
        # Enable/Disable overlay card
        enable_card = tk.Frame(overlay_inner, bg="#181818", bd=1, relief=tk.SOLID)
        enable_card.pack(fill=tk.X, pady=(0, 16))
        
        enable_label = tk.Label(
            enable_card,
            text="🖥  Enable Overlay",
            font=("Segoe UI", 11, "bold"),
            bg="#181818",
            fg=self.primary_text,
        )
        enable_label.pack(anchor="w", padx=12, pady=(10, 2))
        
        enable_desc = tk.Label(
            enable_card,
            text="Show an always-on-top overlay window displaying your shift information.",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
            wraplength=520,
            justify="left",
        )
        enable_desc.pack(anchor="w", padx=12, pady=(0, 10))
        
        self.overlay_enable_var = tk.BooleanVar(value=self.overlay_enabled)
        overlay_enable_check = tk.Checkbutton(
            enable_card,
            text="Enable overlay",
            font=("Segoe UI", 10),
            variable=self.overlay_enable_var,
            command=self.toggle_overlay,
            bg="#181818",
            fg=self.primary_text,
            activebackground="#181818",
            activeforeground=self.primary_text,
            selectcolor="#1e1e1e",
        )
        overlay_enable_check.pack(anchor="w", padx=12, pady=(0, 12))
        
        # Display options card
        display_card = tk.Frame(overlay_inner, bg="#181818", bd=1, relief=tk.SOLID)
        display_card.pack(fill=tk.X, pady=(0, 16))
        
        display_label = tk.Label(
            display_card,
            text="📊  Display Options",
            font=("Segoe UI", 11, "bold"),
            bg="#181818",
            fg=self.primary_text,
        )
        display_label.pack(anchor="w", padx=12, pady=(10, 2))
        
        display_desc = tk.Label(
            display_card,
            text="Select what information to display in the overlay.",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
            wraplength=520,
            justify="left",
        )
        display_desc.pack(anchor="w", padx=12, pady=(0, 8))
        
        self.overlay_show_status_var = tk.BooleanVar(value=self.overlay_display_options["show_status"])
        self.overlay_show_hours_var = tk.BooleanVar(value=self.overlay_display_options["show_hours"])
        self.overlay_show_week_var = tk.BooleanVar(value=self.overlay_display_options["show_week"])
        self.overlay_show_department_var = tk.BooleanVar(value=self.overlay_display_options.get("show_department", False))
        
        check_status = tk.Checkbutton(
            display_card,
            text="Show clock status (CLOCKED IN/OUT)",
            font=("Segoe UI", 9),
            variable=self.overlay_show_status_var,
            command=self.update_overlay_display_options,
            bg="#181818",
            fg=self.primary_text,
            activebackground="#181818",
            activeforeground=self.primary_text,
            selectcolor="#1e1e1e",
        )
        check_status.pack(anchor="w", padx=12, pady=(2, 0))
        
        check_hours = tk.Checkbutton(
            display_card,
            text="Show weekly hours",
            font=("Segoe UI", 9),
            variable=self.overlay_show_hours_var,
            command=self.update_overlay_display_options,
            bg="#181818",
            fg=self.primary_text,
            activebackground="#181818",
            activeforeground=self.primary_text,
            selectcolor="#1e1e1e",
        )
        check_hours.pack(anchor="w", padx=12, pady=(2, 0))
        
        check_week = tk.Checkbutton(
            display_card,
            text="Show current week range",
            font=("Segoe UI", 9),
            variable=self.overlay_show_week_var,
            command=self.update_overlay_display_options,
            bg="#181818",
            fg=self.primary_text,
            activebackground="#181818",
            activeforeground=self.primary_text,
            selectcolor="#1e1e1e",
        )
        check_week.pack(anchor="w", padx=12, pady=(2, 0))
        
        check_department = tk.Checkbutton(
            display_card,
            text="Show current department",
            font=("Segoe UI", 9),
            variable=self.overlay_show_department_var,
            command=self.update_overlay_display_options,
            bg="#181818",
            fg=self.primary_text,
            activebackground="#181818",
            activeforeground=self.primary_text,
            selectcolor="#1e1e1e",
        )
        check_department.pack(anchor="w", padx=12, pady=(2, 12))
        
        # Position selector card
        position_card = tk.Frame(overlay_inner, bg="#181818", bd=1, relief=tk.SOLID)
        position_card.pack(fill=tk.X, pady=(0, 16))
        
        position_label = tk.Label(
            position_card,
            text="📍  Position",
            font=("Segoe UI", 11, "bold"),
            bg="#181818",
            fg=self.primary_text,
        )
        position_label.pack(anchor="w", padx=12, pady=(10, 2))
        
        position_desc = tk.Label(
            position_card,
            text="Select where the overlay should appear on your screen.",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
            wraplength=520,
            justify="left",
        )
        position_desc.pack(anchor="w", padx=12, pady=(0, 8))
        
        # 3x3 grid (8 buttons, middle empty)
        grid_frame = tk.Frame(position_card, bg="#181818")
        grid_frame.pack(padx=12, pady=(0, 12))
        
        positions = [
            ("top-left", "↖"), ("top-center", "↑"), ("top-right", "↗"),
            ("middle-left", "←"), None, ("middle-right", "→"),
            ("bottom-left", "↙"), ("bottom-center", "↓"), ("bottom-right", "↘"),
        ]
        
        self.position_buttons = {}
        for i, pos_data in enumerate(positions):
            row = i // 3
            col = i % 3
            if pos_data is None:
                # Empty middle cell
                empty = tk.Frame(grid_frame, bg="#181818", width=60, height=40)
                empty.grid(row=row, column=col, padx=4, pady=4)
            else:
                pos_name, pos_icon = pos_data
                btn = tk.Button(
                    grid_frame,
                    text=pos_icon,
                    font=("Segoe UI", 12),
                    width=4,
                    height=2,
                    command=lambda p=pos_name: self.set_overlay_position(p),
                    bg="#1e1e1e" if self.overlay_position != pos_name else self.accent,
                    fg=self.primary_text if self.overlay_position != pos_name else "white",
                    activebackground="#333333",
                    activeforeground=self.accent,
                    bd=1,
                    relief=tk.SOLID,
                    cursor="hand2",
                    state=tk.DISABLED if self.overlay_custom_position["enabled"] else tk.NORMAL,
                )
                btn.grid(row=row, column=col, padx=4, pady=4)
                self.position_buttons[pos_name] = btn
        
        # Advanced positioning section
        advanced_frame = tk.Frame(position_card, bg="#181818")
        advanced_frame.pack(fill=tk.X, padx=12, pady=(0, 12))
        
        advanced_divider = tk.Frame(advanced_frame, bg="#252525", height=1)
        advanced_divider.pack(fill=tk.X, pady=(4, 8))
        
        self.use_custom_position_var = tk.BooleanVar(value=self.overlay_custom_position["enabled"])
        custom_pos_check = tk.Checkbutton(
            advanced_frame,
            text="Use custom position",
            font=("Segoe UI", 9, "bold"),
            variable=self.use_custom_position_var,
            command=self.toggle_custom_position,
            bg="#181818",
            fg=self.primary_text,
            activebackground="#181818",
            activeforeground=self.primary_text,
            selectcolor="#1e1e1e",
        )
        custom_pos_check.pack(anchor="w", pady=(0, 6))
        
        custom_pos_frame = tk.Frame(advanced_frame, bg="#181818")
        custom_pos_frame.pack(fill=tk.X, pady=(0, 4))
        
        x_label = tk.Label(
            custom_pos_frame,
            text="X:",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
        )
        x_label.pack(side=tk.LEFT, padx=(0, 4))
        
        self.custom_x_var = tk.StringVar(value=str(self.overlay_custom_position["x"]))
        custom_x_entry = tk.Entry(
            custom_pos_frame,
            textvariable=self.custom_x_var,
            width=8,
            font=("Consolas", 9),
            bg="#1e1e1e",
            fg=self.primary_text,
            insertbackground=self.primary_text,
            bd=1,
            relief=tk.SOLID,
        )
        custom_x_entry.pack(side=tk.LEFT, padx=(0, 12))
        custom_x_entry.bind("<KeyRelease>", self.update_custom_position)
        
        y_label = tk.Label(
            custom_pos_frame,
            text="Y:",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
        )
        y_label.pack(side=tk.LEFT, padx=(0, 4))
        
        self.custom_y_var = tk.StringVar(value=str(self.overlay_custom_position["y"]))
        custom_y_entry = tk.Entry(
            custom_pos_frame,
            textvariable=self.custom_y_var,
            width=8,
            font=("Consolas", 9),
            bg="#1e1e1e",
            fg=self.primary_text,
            insertbackground=self.primary_text,
            bd=1,
            relief=tk.SOLID,
        )
        custom_y_entry.pack(side=tk.LEFT)
        custom_y_entry.bind("<KeyRelease>", self.update_custom_position)
        
        # Transparency slider card
        transparency_card = tk.Frame(overlay_inner, bg="#181818", bd=1, relief=tk.SOLID)
        transparency_card.pack(fill=tk.X)
        
        transparency_label = tk.Label(
            transparency_card,
            text="🔍  Transparency",
            font=("Segoe UI", 11, "bold"),
            bg="#181818",
            fg=self.primary_text,
        )
        transparency_label.pack(anchor="w", padx=12, pady=(10, 2))
        
        transparency_desc = tk.Label(
            transparency_card,
            text="Adjust the transparency of overlay content. You can also make the background transparent.",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
            wraplength=520,
            justify="left",
        )
        transparency_desc.pack(anchor="w", padx=12, pady=(0, 8))
        
        # Transparent background checkbox
        self.transparent_bg_var = tk.BooleanVar(value=self.overlay_transparent_background)
        transparent_bg_check = tk.Checkbutton(
            transparency_card,
            text="Make background transparent (content transparency still applies)",
            font=("Segoe UI", 9),
            variable=self.transparent_bg_var,
            command=self.update_transparent_background,
            bg="#181818",
            fg=self.primary_text,
            activebackground="#181818",
            activeforeground=self.primary_text,
            selectcolor="#1e1e1e",
        )
        transparent_bg_check.pack(anchor="w", padx=12, pady=(0, 8))
        
        transparency_control_frame = tk.Frame(transparency_card, bg="#181818")
        transparency_control_frame.pack(fill=tk.X, padx=12, pady=(0, 12))
        
        slider_label = tk.Label(
            transparency_control_frame,
            text="Content transparency:",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
        )
        slider_label.pack(side=tk.LEFT, padx=(0, 8))
        
        self.transparency_var = tk.DoubleVar(value=int(self.overlay_transparency * 100))
        transparency_slider = tk.Scale(
            transparency_control_frame,
            from_=0,
            to=100,
            orient=tk.HORIZONTAL,
            variable=self.transparency_var,
            command=self.update_overlay_transparency,
            bg="#181818",
            fg=self.primary_text,
            troughcolor="#1e1e1e",
            activebackground=self.accent,
            highlightthickness=0,
            length=300,
        )
        transparency_slider.pack(side=tk.LEFT, padx=(0, 8))
        
        self.transparency_value_label = tk.Label(
            transparency_control_frame,
            text=f"{int(self.overlay_transparency * 100)}%",
            font=("Segoe UI", 10, "bold"),
            bg="#181818",
            fg=self.accent,
            width=5,
        )
        self.transparency_value_label.pack(side=tk.LEFT)

        # --- Departments view frame (initially hidden) ---
        self.departments_frame = tk.Frame(self.content_frame, bg=self.bg_color)
        
        # Create scrollable canvas
        departments_canvas = tk.Canvas(self.departments_frame, bg=self.bg_color, highlightthickness=0)
        departments_scrollbar = tk.Scrollbar(self.departments_frame, orient=tk.VERTICAL, command=departments_canvas.yview)
        departments_scrollable_frame = tk.Frame(departments_canvas, bg=self.bg_color)
        
        departments_scrollable_frame.bind(
            "<Configure>",
            lambda e: departments_canvas.configure(scrollregion=departments_canvas.bbox("all"))
        )
        
        canvas_window = departments_canvas.create_window((0, 0), window=departments_scrollable_frame, anchor="nw")
        departments_canvas.configure(yscrollcommand=departments_scrollbar.set)
        
        departments_canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        departments_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        # Update canvas window width when canvas is resized
        def _configure_departments_canvas(event):
            canvas_width = event.width
            departments_canvas.itemconfig(canvas_window, width=canvas_width)
        departments_canvas.bind('<Configure>', _configure_departments_canvas)
        
        # Update scroll region when scrollable frame content changes
        def _update_departments_scroll_region(event=None):
            departments_canvas.configure(scrollregion=departments_canvas.bbox("all"))
        departments_scrollable_frame.bind('<Configure>', _update_departments_scroll_region)
        
        # Bind mousewheel to canvas and scrollable frame
        def _on_departments_mousewheel(event):
            # Only scroll if departments frame is currently visible
            try:
                if self.departments_frame.winfo_viewable():
                    departments_canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
            except:
                pass
        
        # Bind to canvas and scrollable frame
        departments_canvas.bind("<MouseWheel>", _on_departments_mousewheel)
        departments_scrollable_frame.bind("<MouseWheel>", _on_departments_mousewheel)
        # Also bind to all child widgets recursively
        def bind_mousewheel_to_children_dept(parent):
            for child in parent.winfo_children():
                try:
                    child.bind("<MouseWheel>", _on_departments_mousewheel)
                    bind_mousewheel_to_children_dept(child)
                except:
                    pass
        bind_mousewheel_to_children_dept(departments_scrollable_frame)
        
        departments_inner = tk.Frame(departments_scrollable_frame, bg=self.bg_color)
        departments_inner.pack(expand=True, fill=tk.BOTH, padx=32, pady=32)
        
        # Header
        departments_title = tk.Label(
            departments_inner,
            text="Departments",
            font=("Segoe UI", 18, "bold"),
            bg=self.bg_color,
            fg=self.primary_text,
        )
        departments_title.pack(anchor="w", pady=(0, 4))
        
        departments_sub = tk.Label(
            departments_inner,
            text="Manage departments and servers. Each department has its own separate shift tracking.",
            font=("Segoe UI", 9),
            bg=self.bg_color,
            fg=self.muted_text,
        )
        departments_sub.pack(anchor="w", pady=(0, 16))
        
        # Explanation card
        explanation_card = tk.Frame(departments_inner, bg="#181818", bd=1, relief=tk.SOLID)
        explanation_card.pack(fill=tk.X, pady=(0, 16))
        
        explanation_label = tk.Label(
            explanation_card,
            text="ℹ  What are Departments?",
            font=("Segoe UI", 11, "bold"),
            bg="#181818",
            fg=self.primary_text,
        )
        explanation_label.pack(anchor="w", padx=12, pady=(10, 2))
        
        explanation_text = tk.Label(
            explanation_card,
            text=(
                "Departments allow you to track shifts separately for different servers, "
                "departments, or roleplay communities. Each department maintains its own "
                "time log, so you can clock in/out for different departments independently.\n\n"
                "For example, you might create departments like 'Police Department', "
                "'Fire Department', 'EMS', or different server names. This lets you "
                "track your hours for each one separately."
            ),
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
            wraplength=520,
            justify="left",
        )
        explanation_text.pack(anchor="w", padx=12, pady=(0, 12))
        
        # Manage departments card
        manage_card = tk.Frame(departments_inner, bg="#181818", bd=1, relief=tk.SOLID)
        manage_card.pack(fill=tk.X)
        
        manage_label = tk.Label(
            manage_card,
            text="⚙  Manage Departments",
            font=("Segoe UI", 11, "bold"),
            bg="#181818",
            fg=self.primary_text,
        )
        manage_label.pack(anchor="w", padx=12, pady=(10, 2))
        
        manage_desc = tk.Label(
            manage_card,
            text="Create, rename, or delete departments.",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
            wraplength=520,
            justify="left",
        )
        manage_desc.pack(anchor="w", padx=12, pady=(0, 8))
        
        # Departments list
        list_frame = tk.Frame(manage_card, bg="#181818")
        list_frame.pack(fill=tk.BOTH, expand=True, padx=12, pady=(0, 8))
        
        # Scrollable list
        list_canvas = tk.Canvas(list_frame, bg="#181818", highlightthickness=0, height=200)
        list_scrollbar = tk.Scrollbar(list_frame, orient=tk.VERTICAL, command=list_canvas.yview)
        list_scrollable = tk.Frame(list_canvas, bg="#181818")
        
        list_scrollable.bind(
            "<Configure>",
            lambda e: list_canvas.configure(scrollregion=list_canvas.bbox("all"))
        )
        
        list_canvas.create_window((0, 0), window=list_scrollable, anchor="nw")
        list_canvas.configure(yscrollcommand=list_scrollbar.set)
        
        list_canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        list_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        self.departments_list_frame = list_scrollable
        
        # Add/New department section
        add_frame = tk.Frame(manage_card, bg="#181818")
        add_frame.pack(fill=tk.X, padx=12, pady=(0, 12))
        
        add_label = tk.Label(
            add_frame,
            text="New Department:",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
        )
        add_label.pack(side=tk.LEFT, padx=(0, 8))
        
        self.new_dept_entry = tk.Entry(
            add_frame,
            width=20,
            font=("Segoe UI", 9),
            bg="#1e1e1e",
            fg=self.primary_text,
            insertbackground=self.primary_text,
            bd=1,
            relief=tk.SOLID,
        )
        self.new_dept_entry.pack(side=tk.LEFT, padx=(0, 8))
        self.new_dept_entry.bind("<Return>", lambda e: self.add_department())
        
        add_btn = tk.Button(
            add_frame,
            text="Add",
            font=("Segoe UI", 9, "bold"),
            command=self.add_department,
            bg="#1e88e5",
            fg="white",
            activebackground="#1565c0",
            activeforeground="white",
            bd=0,
            relief=tk.FLAT,
            cursor="hand2",
            padx=12,
            pady=2,
        )
        add_btn.pack(side=tk.LEFT)
        
        # --- Settings view frame (initially hidden) ---
        self.settings_frame = tk.Frame(self.content_frame, bg=self.bg_color)

        settings_inner = tk.Frame(self.settings_frame, bg=self.bg_color)
        settings_inner.pack(expand=True, fill=tk.BOTH, padx=32, pady=32)

        # Header
        settings_title = tk.Label(
            settings_inner,
            text="Settings",
            font=("Segoe UI", 18, "bold"),
            bg=self.bg_color,
            fg=self.primary_text,
        )
        settings_title.pack(anchor="w", pady=(0, 4))

        settings_sub = tk.Label(
            settings_inner,
            text="Manage updates and view information about the Roleplay Shift Tracker.",
            font=("Segoe UI", 9),
            bg=self.bg_color,
            fg=self.muted_text,
        )
        settings_sub.pack(anchor="w", pady=(0, 16))

        # Check for updates section (full-width card)
        updates_card = tk.Frame(settings_inner, bg="#181818", bd=1, relief=tk.SOLID)
        updates_card.pack(fill=tk.X, pady=(0, 16))

        updates_label = tk.Label(
            updates_card,
            text="⬆  Updates",
            font=("Segoe UI", 11, "bold"),
            bg="#181818",
            fg=self.primary_text,
        )
        updates_label.pack(anchor="w", padx=12, pady=(10, 2))

        updates_desc = tk.Label(
            updates_card,
            text="Check if a newer version of the Roleplay Shift Tracker is available.\n"
                 "This will contact the update server in a future version.",
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
            wraplength=520,
            justify="left",
        )
        updates_desc.pack(anchor="w", padx=12, pady=(0, 10))

        self.check_updates_btn = tk.Button(
            updates_card,
            text="Check for updates",
            font=("Segoe UI", 10, "bold"),
            command=self.check_for_updates,
            bg="#1e88e5",
            fg="white",
            activebackground="#1565c0",
            activeforeground="white",
            bd=0,
            relief=tk.FLAT,
            cursor="hand2",
            padx=16,
            pady=4,
        )
        self.check_updates_btn.pack(anchor="w", padx=12, pady=(0, 12))

        # About / credits section (full-width card)
        about_card = tk.Frame(settings_inner, bg="#181818", bd=1, relief=tk.SOLID)
        about_card.pack(fill=tk.X)

        about_title = tk.Label(
            about_card,
            text="ℹ  About",
            font=("Segoe UI", 11, "bold"),
            bg="#181818",
            fg=self.primary_text,
        )
        about_title.pack(anchor="w", padx=12, pady=(10, 2))

        version_label = tk.Label(
            about_card,
            text=f"Version {VERSION}",
            font=("Segoe UI", 9, "bold"),
            bg="#181818",
            fg=self.accent,
            justify="left",
        )
        version_label.pack(anchor="w", padx=12, pady=(0, 6))
        
        about_text = tk.Label(
            about_card,
            text=(
                "Roleplay Shift Tracker is created by DRAGEno01.\n"
                "For more tools and projects, visit the creator's website:"
            ),
            font=("Segoe UI", 9),
            bg="#181818",
            fg=self.muted_text,
            justify="left",
            wraplength=520,
        )
        about_text.pack(anchor="w", padx=12, pady=(0, 6))

        self.link_label = tk.Label(
            about_card,
            text="https://drageno01.web.app/",
            font=("Segoe UI", 9, "underline"),
            bg="#181818",
            fg=self.accent,
            cursor="hand2",
        )
        self.link_label.pack(anchor="w", padx=12, pady=(0, 12))
        self.link_label.bind("<Button-1>", self.open_creator_website)

        # --- Side menu (slides in from the left) ---
        self.menu_frame = tk.Frame(self.root, bg="#101010")
        # Start hidden just off the left edge
        self.menu_frame.place(x=self.menu_x, y=0, width=self.menu_width, relheight=1.0)

        menu_header = tk.Label(
            self.menu_frame,
            text="☰  Menu",
            font=("Segoe UI", 12, "bold"),
            bg="#101010",
            fg=self.primary_text,
            anchor="w",
        )
        menu_header.pack(fill=tk.X, padx=16, pady=(12, 4))

        # subtle divider under header
        divider = tk.Frame(self.menu_frame, bg="#252525", height=1)
        divider.pack(fill=tk.X, padx=12, pady=(0, 8))

        # Home tab (represents current screen)
        self.home_btn = tk.Button(
            self.menu_frame,
            text="🏠  Home",
            font=("Segoe UI", 11),
            bg="#202020",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=1,
            command=self.on_menu_home,
            anchor="w",
            padx=14,
            relief=tk.FLAT,
            highlightthickness=0,
        )
        self.home_btn.pack(fill=tk.X, padx=10, pady=(2, 4))

        # Placeholder tabs requested by user
        self.view_shifts_btn = tk.Button(
            self.menu_frame,
            text="📋  View Shifts",
            font=("Segoe UI", 11),
            bg="#202020",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=1,
            command=self.on_menu_view_shifts,
            anchor="w",
            padx=14,
            relief=tk.FLAT,
            highlightthickness=0,
        )
        self.view_shifts_btn.pack(fill=tk.X, padx=10, pady=(2, 4))

        self.overlay_btn = tk.Button(
            self.menu_frame,
            text="🖥  Overlay",
            font=("Segoe UI", 11),
            bg="#202020",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=1,
            command=self.on_menu_overlay,
            anchor="w",
            padx=14,
            relief=tk.FLAT,
            highlightthickness=0,
        )
        self.overlay_btn.pack(fill=tk.X, padx=10, pady=(2, 4))

        self.departments_btn = tk.Button(
            self.menu_frame,
            text="🏢  Departments",
            font=("Segoe UI", 11),
            bg="#202020",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=1,
            command=self.on_menu_departments,
            anchor="w",
            padx=14,
            relief=tk.FLAT,
            highlightthickness=0,
        )
        self.departments_btn.pack(fill=tk.X, padx=10, pady=(2, 4))

        # Settings button container for badge
        settings_btn_frame = tk.Frame(self.menu_frame, bg="#101010")
        settings_btn_frame.pack(fill=tk.X, padx=10, pady=(2, 8))
        
        self.settings_btn = tk.Button(
            settings_btn_frame,
            text="⚙  Settings",
            font=("Segoe UI", 11),
            bg="#202020",
            fg=self.primary_text,
            activebackground="#333333",
            activeforeground=self.accent,
            bd=1,
            command=self.on_menu_settings,
            anchor="w",
            padx=14,
            relief=tk.FLAT,
            highlightthickness=0,
        )
        self.settings_btn.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        
        # Badge for update notification on settings button
        self.settings_badge = tk.Label(
            settings_btn_frame,
            text="1",
            font=("Segoe UI", 8, "bold"),
            bg="#ef5350",
            fg="white",
            width=2,
            height=1,
        )
        self.settings_badge.pack(side=tk.RIGHT, padx=(4, 8))
        self.settings_badge.pack_forget()  # Initially hidden

        # Global click binding to detect clicks outside the side menu
        self.root.bind("<Button-1>", self.on_root_click)

        # Ensure graceful close when OS requests window close (Alt+F4, etc.)
        self.root.protocol("WM_DELETE_WINDOW", self.on_close_request)

        self.update_ui_initial()
        # Start periodic update of hours while window is open
        self.schedule_live_update()

    def update_ui_initial(self):
        """Initial UI state based on loaded events."""
        week_start, week_end = current_week_range()
        monday_str = week_start.strftime("%Y-%m-%d")
        sunday_str = (week_end - timedelta(seconds=1)).strftime("%Y-%m-%d")
        self.week_label.config(
            text=f"Current week: {monday_str} (Mon) - {sunday_str} (Sun)"
        )

        self.refresh_status_and_hours()
        # Ensure Home view is visible by default
        self.show_home_view()
        # Restore overlay if it was enabled
        if self.overlay_enabled:
            self.root.after(500, self.create_overlay_window)
        
        # Check for updates on startup (non-blocking, no message)
        self.root.after(1000, lambda: self.check_for_updates(show_message=False))

    def refresh_status_and_hours(self):
        """Refresh status label, button text, and weekly hours display."""
        self.events = load_events(self.current_department)
        
        # Update home department dropdown if it exists
        if hasattr(self, 'home_department_var'):
            self.home_department_var.set(self.current_department)

        clocked_in = is_currently_clocked_in(self.events)
        if clocked_in:
            self.status_label.config(text="Status: CLOCKED IN", fg=self.positive)
            self.button.config(text="Clock Out", bg=self.negative, activebackground="#c62828")
        else:
            self.status_label.config(text="Status: CLOCKED OUT", fg=self.muted_text)
            self.button.config(text="Clock In", bg="#1e88e5", activebackground="#1565c0")

        seconds = compute_weekly_seconds(self.events)
        self.hours_label.config(text=format_seconds_hms(seconds))

    def toggle_clock(self):
        """Handle Clock In / Clock Out button click."""
        try:
            self.events = load_events(self.current_department)
            clocked_in = is_currently_clocked_in(self.events)

            if not clocked_in:
                append_event("IN", self.current_department)
            else:
                append_event("OUT", self.current_department)

            self.refresh_status_and_hours()
        except Exception as e:
            messagebox.showerror("Error", f"An error occurred:\n{e}")

    def schedule_live_update(self):
        """Update weekly hours every second so it grows while clocked in."""
        self.refresh_status_and_hours()
        # Schedule next update after 1000 ms
        self.root.after(1000, self.schedule_live_update)

    def on_close_request(self):
        """
        Ask the user to confirm closing.
        If currently clocked in, warn that time will continue to count while closed.
        """
        events = load_events(self.current_department)
        clocked_in = is_currently_clocked_in(events)

        if clocked_in:
            message = (
                "You are currently ON SHIFT.\n\n"
                "If you close this application, your time ON DUTY will continue to be counted "
                "in the background until you clock out next time you open the tracker.\n\n"
                "Are you sure you want to close?"
            )
        else:
            message = "Are you sure you want to close the Roleplay Shift Tracker?"

        if messagebox.askyesno("Confirm Exit", message):
            # Close overlay window if it exists
            if self.overlay_window:
                self.destroy_overlay_window()
            self.root.destroy()

    # --- Side menu interaction & animation logic ---
    def on_root_click(self, event):
        """
        Close the side menu when clicking outside of it.
        Keep it open if the click is inside the menu (including on empty space).
        """
        if not self.menu_open:
            return

        try:
            x_root, y_root = event.x_root, event.y_root
            menu_x = self.menu_frame.winfo_rootx()
            menu_y = self.menu_frame.winfo_rooty()
            menu_w = self.menu_frame.winfo_width()
            menu_h = self.menu_frame.winfo_height()
        except tk.TclError:
            return

        inside_x = menu_x <= x_root <= menu_x + menu_w
        inside_y = menu_y <= y_root <= menu_y + menu_h

        if inside_x and inside_y:
            # Click was inside the menu area: do nothing
            return

        # Click outside menu: close it
        self._animate_menu(opening=False)

    # --- Side menu animation logic ---
    def toggle_menu(self):
        """Toggle the side menu open/closed with a simple slide animation."""
        if self.menu_open:
            self._animate_menu(opening=False)
        else:
            self._animate_menu(opening=True)

    def _animate_menu(self, opening: bool):
        target_x = 0 if opening else -self.menu_width
        step = 20 if opening else -20

        def _step():
            self.menu_x += step

            if (opening and self.menu_x >= target_x) or (not opening and self.menu_x <= target_x):
                self.menu_x = target_x
                self.menu_frame.place(x=self.menu_x, y=0, width=self.menu_width, relheight=1.0)
                self.menu_open = opening
                return

            self.menu_frame.place(x=self.menu_x, y=0, width=self.menu_width, relheight=1.0)
            self.root.after(10, _step)

        _step()

    # Menu item callbacks
    def on_menu_home(self):
        if self.menu_open:
            self._animate_menu(opening=False)
        self.show_home_view()

    def on_menu_view_shifts(self):
        if self.menu_open:
            self._animate_menu(opening=False)
        self.show_shifts_view()

    def on_menu_overlay(self):
        if self.menu_open:
            self._animate_menu(opening=False)
        self.show_overlay_view()

    def on_menu_departments(self):
        if self.menu_open:
            self._animate_menu(opening=False)
        self.show_departments_view()

    def on_menu_settings(self):
        if self.menu_open:
            self._animate_menu(opening=False)
        self.show_settings_view()

    # --- View switching ---
    def show_home_view(self):
        """Show the main Home view."""
        self.shifts_frame.pack_forget()
        self.overlay_frame.pack_forget()
        self.departments_frame.pack_forget()
        self.settings_frame.pack_forget()
        self.home_frame.pack(expand=True, fill=tk.BOTH)

    def show_shifts_view(self):
        """Show the weekly shifts view."""
        self.home_frame.pack_forget()
        self.overlay_frame.pack_forget()
        self.departments_frame.pack_forget()
        self.settings_frame.pack_forget()
        self.shifts_frame.pack(expand=True, fill=tk.BOTH)
        self.refresh_shifts_view()

    def show_overlay_view(self):
        """Show the overlay settings view."""
        self.home_frame.pack_forget()
        self.shifts_frame.pack_forget()
        self.departments_frame.pack_forget()
        self.settings_frame.pack_forget()
        self.overlay_frame.pack(expand=True, fill=tk.BOTH)

    def show_departments_view(self):
        """Show the departments management view."""
        self.home_frame.pack_forget()
        self.shifts_frame.pack_forget()
        self.overlay_frame.pack_forget()
        self.settings_frame.pack_forget()
        self.departments_frame.pack(expand=True, fill=tk.BOTH)
        self.refresh_departments_view()

    def show_settings_view(self):
        """Show the settings view."""
        self.home_frame.pack_forget()
        self.shifts_frame.pack_forget()
        self.overlay_frame.pack_forget()
        self.departments_frame.pack_forget()
        self.settings_frame.pack(expand=True, fill=tk.BOTH)

    def refresh_shifts_view(self):
        """Populate the shifts view for the currently selected week."""
        events = load_events(self.current_department)
        week_start = self.current_shifts_week_start
        week_end = self.current_shifts_week_end

        shifts = compute_shifts_for_week(events, week_start, week_end)

        monday_str = week_start.strftime("%Y-%m-%d")
        sunday_str = (week_end - timedelta(seconds=1)).strftime("%Y-%m-%d")
        self.shifts_week_label.config(
            text=f"Week: {monday_str} (Mon) - {sunday_str} (Sun)"
        )

        total_seconds = sum(dur for _, _, dur in shifts)
        self.shifts_total_label.config(
            text=f"Total this week: {format_seconds_hms(total_seconds)}"
        )

        self.shifts_listbox.delete(0, tk.END)
        if not shifts:
            self.shifts_listbox.insert(tk.END, "  No shifts recorded for this week.")
            return

        for start, end, dur in shifts:
            day = start.strftime("%a")
            start_str = start.strftime("%Y-%m-%d %H:%M:%S")
            end_str = end.strftime("%Y-%m-%d %H:%M:%S")
            dur_str = format_seconds_hms(dur)
            # Align into rough columns: DAY | START | END | DURATION
            line = f"  {day:<3}  {start_str:<20}  {end_str:<20}  {dur_str:<8}"
            self.shifts_listbox.insert(tk.END, line)

    # --- Week navigation for shifts view ---
    def go_prev_week(self):
        """Go to the previous week in the shifts view."""
        self.current_shifts_week_start -= timedelta(days=7)
        self.current_shifts_week_end -= timedelta(days=7)
        self.refresh_shifts_view()

    def go_next_week(self):
        """Go to the next week in the shifts view."""
        self.current_shifts_week_start += timedelta(days=7)
        self.current_shifts_week_end += timedelta(days=7)
        self.refresh_shifts_view()

    def go_this_week(self):
        """Jump back to the current week in the shifts view."""
        self.current_shifts_week_start, self.current_shifts_week_end = current_week_range()
        self.refresh_shifts_view()

    def go_to_date_week(self):
        """Jump to the week containing the entered date (YYYY-MM-DD)."""
        date_str = self.date_entry.get().strip()
        if not date_str:
            return
        try:
            d = datetime.strptime(date_str, "%Y-%m-%d")
        except ValueError:
            messagebox.showerror("Invalid Date", "Please enter a valid date in the format YYYY-MM-DD.")
            return

        self.current_shifts_week_start, self.current_shifts_week_end = week_range_for_date(d)
        self.refresh_shifts_view()

    # --- Settings helpers ---
    def open_creator_website(self, event=None):
        """Open the creator's website in the default web browser."""
        try:
            webbrowser.open("https://drageno01.web.app/")
        except Exception:
            messagebox.showerror(
                "Error",
                "Unable to open the website. Please check your default browser settings.",
            )

    def check_for_updates(self, show_message=True):
        """Check for updates and handle accordingly."""
        try:
            app_info = fetch_app_info()
        except Exception:
            # Silently handle any unexpected errors - only show message if user manually checked
            if show_message:
                messagebox.showerror(
                    "Update Check Failed",
                    "Could not check for updates.\n\nPlease check your internet connection and try again."
                )
            return
        
        if app_info is None:
            # No internet or connection failed - only show message if user manually checked
            if show_message:
                messagebox.showerror(
                    "Update Check Failed",
                    "Could not check for updates.\n\nPlease check your internet connection and try again."
                )
            return
        
        self.app_info = app_info
        
        # Check allowUsage first - this must be checked before anything else
        if not app_info["allowUsage"]:
            messagebox.showerror(
                "Update Required",
                "This version of the Roleplay Shift Tracker is no longer supported.\n\n"
                "Please download the latest version from:\n"
                "https://github.com/DRAGEno01/Roleplay-Shift-Tracker\n\n"
                "The application will now close."
            )
            self.root.quit()
            return
        
        # Check supported flag
        if not app_info["supported"]:
            messagebox.showwarning(
                "Version Deprecated",
                "The Python version of this app is no longer supported.\n\n"
                "Please switch to a newer version before you can no longer use the app.\n\n"
                "Download the latest version from:\n"
                "https://github.com/DRAGEno01/Roleplay-Shift-Tracker"
            )
        
        # Check version
        if app_info["version"] != VERSION:
            self.update_available = True
            self.show_update_indicators()
            if show_message:
                messagebox.showinfo(
                    "Update Available",
                    f"A new version ({app_info['version']}) is available!\n\n"
                    f"Current version: {VERSION}\n"
                    f"Latest version: {app_info['version']}\n\n"
                    "Click the 'Update' button to download the latest version."
                )
        else:
            self.update_available = False
            self.hide_update_indicators()
            if show_message:
                messagebox.showinfo(
                    "Up to Date",
                    f"You are running the latest version ({VERSION})."
                )
    
    def show_update_indicators(self):
        """Show update notification badges and change button text."""
        self.menu_badge.pack(side=tk.LEFT, padx=(2, 0))
        self.settings_badge.pack(side=tk.RIGHT, padx=(4, 8))
        if hasattr(self, 'check_updates_btn'):
            self.check_updates_btn.config(text="Update", bg="#4caf50", activebackground="#388e3c", command=self.download_update)
    
    def hide_update_indicators(self):
        """Hide update notification badges and restore button text."""
        self.menu_badge.pack_forget()
        self.settings_badge.pack_forget()
        if hasattr(self, 'check_updates_btn'):
            self.check_updates_btn.config(text="Check for updates", bg="#1e88e5", activebackground="#1565c0", command=self.check_for_updates)
    
    def download_update(self):
        """Download the new version and prepare for update."""
        if not self.app_info:
            # Re-check for updates if we don't have the info
            self.check_for_updates(show_message=False)
            if not self.app_info:
                messagebox.showerror(
                    "Update Failed",
                    "Could not fetch update information.\n\nPlease check your internet connection and try again."
                )
                return
        
        new_version = self.app_info["version"]
        update_url = f"https://raw.githubusercontent.com/DRAGEno01/Roleplay-Shift-Tracker/refs/heads/main/Python/V{new_version}/RpShiftTracker.py"
        
        # Get the directory where this script is located
        base_dir = os.path.dirname(os.path.abspath(__file__))
        new_file_path = os.path.join(base_dir, "tracker_new.py")
        
        try:
            # Download the new version
            with urllib.request.urlopen(update_url, timeout=10) as response:
                new_code = response.read().decode()
            
            # Save the new version
            with open(new_file_path, 'w', encoding='utf-8') as f:
                f.write(new_code)
            
            # Create a batch file to swap the files on next startup
            batch_file = os.path.join(base_dir, "update_tracker.bat")
            current_file = os.path.abspath(__file__)
            old_file = os.path.join(base_dir, "tracker_old.py")
            
            batch_content = f"""@echo off
REM Auto-generated update script for Roleplay Shift Tracker
echo Updating Roleplay Shift Tracker...
if exist "{old_file}" del "{old_file}"
ren "{current_file}" tracker_old.py
ren "{new_file_path}" tracker.py
echo Update complete! Starting new version...
start tracker.py
del "{batch_file}"
"""
            
            with open(batch_file, 'w', encoding='utf-8') as f:
                f.write(batch_content)
            
            messagebox.showinfo(
                "Update Downloaded",
                f"Version {new_version} has been downloaded successfully!\n\n"
                f"The update will be applied when you restart the application.\n\n"
                f"Click 'OK' to restart now, or close the app manually to apply the update later."
            )
            
            # Ask if user wants to restart now
            if messagebox.askyesno("Restart Now?", "Would you like to restart the application now to apply the update?"):
                # Close the app and run the batch file
                import subprocess
                import sys
                subprocess.Popen([batch_file], shell=True, cwd=base_dir)
                self.root.quit()
                sys.exit(0)
            
        except urllib.error.HTTPError as e:
            messagebox.showerror(
                "Download Failed",
                f"Could not download the update.\n\n"
                f"HTTP Error: {e.code}\n"
                f"URL: {update_url}\n\n"
                f"Please try again later or download manually from GitHub."
            )
        except urllib.error.URLError:
            messagebox.showerror(
                "Download Failed",
                "Could not connect to the update server.\n\n"
                "Please check your internet connection and try again."
            )
        except Exception as e:
            messagebox.showerror(
                "Update Failed",
                f"An error occurred while downloading the update:\n\n{str(e)}\n\n"
                f"Please try again later or download manually from GitHub."
            )

    # --- Departments functionality ---
    def save_departments_settings(self):
        """Save current departments settings to file."""
        settings = {
            "departments": self.departments,
            "current_department": self.current_department,
        }
        save_departments_settings(settings)
    
    def refresh_departments_view(self):
        """Refresh the departments list display."""
        # Clear existing list
        for widget in self.departments_list_frame.winfo_children():
            widget.destroy()
        
        # Populate list
        for dept in self.departments:
            dept_frame = tk.Frame(self.departments_list_frame, bg="#1e1e1e", bd=1, relief=tk.SOLID)
            dept_frame.pack(fill=tk.X, padx=4, pady=2)
            
            dept_label = tk.Label(
                dept_frame,
                text=dept,
                font=("Segoe UI", 10),
                bg="#1e1e1e",
                fg=self.primary_text,
                anchor="w",
            )
            dept_label.pack(side=tk.LEFT, padx=8, pady=6)
            
            # Delete button (disable if it's the only department or if it's the current one)
            can_delete = len(self.departments) > 1 and dept != self.current_department
            
            delete_btn = tk.Button(
                dept_frame,
                text="Delete",
                font=("Segoe UI", 8),
                command=lambda d=dept: self.delete_department(d),
                bg="#d32f2f" if can_delete else "#424242",
                fg="white",
                activebackground="#b71c1c" if can_delete else "#616161",
                activeforeground="white",
                bd=0,
                relief=tk.FLAT,
                cursor="hand2" if can_delete else "arrow",
                state=tk.NORMAL if can_delete else tk.DISABLED,
                padx=8,
                pady=2,
            )
            delete_btn.pack(side=tk.RIGHT, padx=4, pady=4)
        
        # Update home dropdown
        if hasattr(self, 'home_department_dropdown'):
            menu = self.home_department_dropdown["menu"]
            menu.delete(0, "end")
            for dept in self.departments:
                menu.add_command(
                    label=dept,
                    command=lambda d=dept: self.home_department_var.set(d) or self.change_department()
                )
            self.home_department_var.set(self.current_department)
    
    def add_department(self):
        """Add a new department."""
        name = self.new_dept_entry.get().strip()
        if not name:
            messagebox.showwarning("Invalid Name", "Please enter a department name.")
            return
        
        if name in self.departments:
            messagebox.showwarning("Duplicate", f"Department '{name}' already exists.")
            return
        
        self.departments.append(name)
        self.save_departments_settings()
        self.refresh_departments_view()
        self.new_dept_entry.delete(0, tk.END)
        messagebox.showinfo("Success", f"Department '{name}' has been added.")
    
    def delete_department(self, name):
        """Delete a department."""
        if len(self.departments) <= 1:
            messagebox.showwarning("Cannot Delete", "You must have at least one department.")
            return
        
        if name == self.current_department:
            messagebox.showwarning("Cannot Delete", "Cannot delete the currently active department. Switch to another department first.")
            return
        
        if messagebox.askyesno("Confirm Delete", f"Are you sure you want to delete department '{name}'?\n\nThis will permanently delete all shift records for this department."):
            self.departments.remove(name)
            self.save_departments_settings()
            self.refresh_departments_view()
            messagebox.showinfo("Deleted", f"Department '{name}' has been deleted.")
    
    def change_department(self, event=None):
        """Change the current active department."""
        # Get new department from home dropdown
        if hasattr(self, 'home_department_var') and self.home_department_var.get():
            new_dept = self.home_department_var.get()
        else:
            return
        
        if new_dept == self.current_department:
            return
        
        # Check if currently clocked in
        events = load_events(self.current_department)
        clocked_in = is_currently_clocked_in(events)
        
        if clocked_in:
            if not messagebox.askyesno(
                "Switch Department",
                f"You are currently clocked in for '{self.current_department}'.\n\n"
                f"Switching to '{new_dept}' will clock you out from the current department.\n\n"
                f"Continue?"
            ):
                if hasattr(self, 'home_department_var'):
                    self.home_department_var.set(self.current_department)
                return
            # Clock out from current department
            append_event("OUT", self.current_department)
        
        self.current_department = new_dept
        self.save_departments_settings()
        
        # Update home dropdown
        if hasattr(self, 'home_department_var'):
            self.home_department_var.set(self.current_department)
        
        # Reload events for new department
        self.events = load_events(self.current_department)
        self.refresh_status_and_hours()
        
        # Refresh shifts view if it's currently visible
        if hasattr(self, 'shifts_frame') and self.shifts_frame.winfo_viewable():
            self.refresh_shifts_view()
        
        # Update overlay if it's enabled
        if self.overlay_window:
            self.update_overlay_content()

    # --- Overlay functionality ---
    def save_overlay_settings(self):
        """Save current overlay settings to file."""
        settings = {
            "enabled": self.overlay_enabled,
            "position": self.overlay_position,
            "custom_position": self.overlay_custom_position,
            "transparency": self.overlay_transparency,
            "transparent_background": self.overlay_transparent_background,
            "display_options": self.overlay_display_options,
        }
        save_overlay_settings(settings)
    
    def toggle_overlay(self):
        """Enable or disable the overlay window."""
        self.overlay_enabled = self.overlay_enable_var.get()
        if self.overlay_enabled:
            self.create_overlay_window()
        else:
            self.destroy_overlay_window()
        self.save_overlay_settings()

    def update_overlay_display_options(self):
        """Update overlay display options from checkboxes."""
        self.overlay_display_options["show_status"] = self.overlay_show_status_var.get()
        self.overlay_display_options["show_hours"] = self.overlay_show_hours_var.get()
        self.overlay_display_options["show_week"] = self.overlay_show_week_var.get()
        self.overlay_display_options["show_department"] = self.overlay_show_department_var.get()
        if self.overlay_window:
            self.update_overlay_content()
        self.save_overlay_settings()

    def set_overlay_position(self, position):
        """Set the overlay position and update button highlights."""
        self.overlay_position = position
        # Disable custom position when using preset
        self.overlay_custom_position["enabled"] = False
        if hasattr(self, 'use_custom_position_var'):
            self.use_custom_position_var.set(False)
        # Update button highlights
        for pos_name, btn in self.position_buttons.items():
            if pos_name == position:
                btn.config(bg=self.accent, fg="white")
            else:
                btn.config(bg="#1e1e1e", fg=self.primary_text)
        if self.overlay_window:
            self.position_overlay_window()
        self.save_overlay_settings()

    def update_overlay_transparency(self, value):
        """Update overlay transparency from slider."""
        self.overlay_transparency = float(value) / 100.0
        self.transparency_value_label.config(text=f"{int(value)}%")
        if self.overlay_window:
            try:
                # Always apply alpha for content transparency
                # When background is transparent, alpha affects the content
                # When background is not transparent, alpha affects the whole window
                self.overlay_window.attributes('-alpha', 1.0 - self.overlay_transparency)
            except:
                pass  # Some systems may not support transparency
        self.save_overlay_settings()
    
    def update_transparent_background(self):
        """Update transparent background setting."""
        self.overlay_transparent_background = self.transparent_bg_var.get()
        if self.overlay_window:
            try:
                if self.overlay_transparent_background:
                    # Enable transparent background
                    self.overlay_window.configure(bg=self.transparent_bg_color)
                    self.overlay_window.attributes('-transparentcolor', self.transparent_bg_color)
                    # Reapply alpha for content transparency
                    self.overlay_window.attributes('-alpha', 1.0 - self.overlay_transparency)
                else:
                    # Disable transparent background - set transparentcolor to a color that won't match
                    self.overlay_window.configure(bg="#1a1a1a")
                    # Set transparentcolor to a color that doesn't exist in the window (white)
                    self.overlay_window.attributes('-transparentcolor', '#FFFFFF')
                    # Reapply alpha for window transparency
                    self.overlay_window.attributes('-alpha', 1.0 - self.overlay_transparency)
            except Exception as e:
                # If setting transparentcolor fails, try without it
                try:
                    self.overlay_window.attributes('-alpha', 1.0 - self.overlay_transparency)
                except:
                    pass
            self.update_overlay_content()
        self.save_overlay_settings()
    
    def toggle_custom_position(self):
        """Toggle custom position mode."""
        self.overlay_custom_position["enabled"] = self.use_custom_position_var.get()
        # Disable preset buttons when custom is enabled
        for btn in self.position_buttons.values():
            btn.config(state=tk.DISABLED if self.overlay_custom_position["enabled"] else tk.NORMAL)
        if self.overlay_window:
            self.position_overlay_window()
        self.save_overlay_settings()
    
    def update_custom_position(self, event=None):
        """Update custom position from entry fields."""
        try:
            x = int(self.custom_x_var.get())
            y = int(self.custom_y_var.get())
            self.overlay_custom_position["x"] = x
            self.overlay_custom_position["y"] = y
            if self.overlay_window and self.overlay_custom_position["enabled"]:
                self.position_overlay_window()
            self.save_overlay_settings()
        except ValueError:
            pass  # Invalid input, ignore

    def create_overlay_window(self):
        """Create and show the overlay window."""
        if self.overlay_window:
            return  # Already exists
        
        self.overlay_window = tk.Toplevel(self.root)
        self.overlay_window.title("Shift Tracker Overlay")
        self.overlay_window.overrideredirect(True)  # Remove window decorations
        self.overlay_window.attributes('-topmost', True)  # Always on top
        
        # Set background and transparency
        if self.overlay_transparent_background:
            self.overlay_window.configure(bg=self.transparent_bg_color)
            try:
                # Make the background color transparent
                self.overlay_window.attributes('-transparentcolor', self.transparent_bg_color)
                # Apply content transparency via alpha
                self.overlay_window.attributes('-alpha', 1.0 - self.overlay_transparency)
            except:
                pass
        else:
            self.overlay_window.configure(bg="#1a1a1a")
            try:
                # Apply window transparency
                self.overlay_window.attributes('-alpha', 1.0 - self.overlay_transparency)
            except:
                pass
        
        self.update_overlay_content()  # This will set the size
        self.position_overlay_window()
        
        # Update overlay content periodically
        self.schedule_overlay_update()

    def destroy_overlay_window(self):
        """Destroy the overlay window."""
        if self.overlay_window:
            self.overlay_window.destroy()
            self.overlay_window = None

    def position_overlay_window(self):
        """Position the overlay window based on selected position."""
        if not self.overlay_window:
            return
        
        self.overlay_window.update_idletasks()
        
        # Get actual window size (will be set dynamically)
        overlay_width = self.overlay_window.winfo_reqwidth()
        overlay_height = self.overlay_window.winfo_reqheight()
        
        if self.overlay_custom_position["enabled"]:
            # Use custom position
            x = self.overlay_custom_position["x"]
            y = self.overlay_custom_position["y"]
        else:
            # Use preset position
            screen_width = self.root.winfo_screenwidth()
            screen_height = self.root.winfo_screenheight()
            margin = 20
            
            position_map = {
                "top-left": (margin, margin),
                "top-center": ((screen_width - overlay_width) // 2, margin),
                "top-right": (screen_width - overlay_width - margin, margin),
                "middle-left": (margin, (screen_height - overlay_height) // 2),
                "middle-right": (screen_width - overlay_width - margin, (screen_height - overlay_height) // 2),
                "bottom-left": (margin, screen_height - overlay_height - margin),
                "bottom-center": ((screen_width - overlay_width) // 2, screen_height - overlay_height - margin),
                "bottom-right": (screen_width - overlay_width - margin, screen_height - overlay_height - margin),
            }
            x, y = position_map.get(self.overlay_position, (margin, margin))
        
        self.overlay_window.geometry(f"+{x}+{y}")

    def update_overlay_content(self):
        """Update the content displayed in the overlay window."""
        if not self.overlay_window:
            return
        
        # Clear existing widgets
        for widget in self.overlay_window.winfo_children():
            widget.destroy()
        
        # Determine background color based on transparent background setting
        if self.overlay_transparent_background:
            bg_color = self.transparent_bg_color  # Use the transparent color
        else:
            bg_color = "#1a1a1a"  # Regular dark background
        
        # Create content frame
        if self.overlay_transparent_background:
            # No border when background is transparent, use transparent color
            content_frame = tk.Frame(self.overlay_window, bg=bg_color)
            content_frame.pack(fill=tk.BOTH, expand=True)
        else:
            content_frame = tk.Frame(self.overlay_window, bg=bg_color, bd=1, relief=tk.SOLID)
            content_frame.pack(fill=tk.BOTH, expand=True, padx=2, pady=2)
        
        events = load_events(self.current_department)
        clocked_in = is_currently_clocked_in(events)
        
        # Track if we have any content
        has_content = False
        
        if self.overlay_display_options["show_status"]:
            status_text = "🟢 CLOCKED IN" if clocked_in else "🔴 CLOCKED OUT"
            status_color = self.positive if clocked_in else self.muted_text
            status_label = tk.Label(
                content_frame,
                text=status_text,
                font=("Segoe UI", 10, "bold"),
                bg=bg_color,
                fg=status_color,
            )
            status_label.pack(pady=(8, 4))
            has_content = True
        
        if self.overlay_display_options["show_hours"]:
            seconds = compute_weekly_seconds(events)
            hours_text = format_seconds_hms(seconds)
            hours_label = tk.Label(
                content_frame,
                text=f"Hours: {hours_text}",
                font=("Consolas", 11, "bold"),
                bg=bg_color,
                fg=self.accent,
            )
            hours_label.pack(pady=2)
            has_content = True
        
        if self.overlay_display_options["show_week"]:
            week_start, week_end = current_week_range()
            monday_str = week_start.strftime("%m/%d")
            sunday_str = (week_end - timedelta(seconds=1)).strftime("%m/%d")
            week_label = tk.Label(
                content_frame,
                text=f"Week: {monday_str} - {sunday_str}",
                font=("Segoe UI", 8),
                bg=bg_color,
                fg=self.muted_text,
            )
            week_label.pack(pady=2)
            has_content = True
        
        if self.overlay_display_options.get("show_department", False):
            dept_label = tk.Label(
                content_frame,
                text=f"Dept: {self.current_department}",
                font=("Segoe UI", 8),
                bg=bg_color,
                fg=self.muted_text,
            )
            dept_label.pack(pady=2)
            has_content = True
        
        # If no content, show a minimal placeholder
        if not has_content:
            placeholder = tk.Label(
                content_frame,
                text="No items selected",
                font=("Segoe UI", 9),
                bg=bg_color,
                fg=self.muted_text,
            )
            placeholder.pack(pady=8)
        
        # Update window size to fit content
        self.overlay_window.update_idletasks()
        req_width = content_frame.winfo_reqwidth() + (0 if self.overlay_transparent_background else 4)
        req_height = content_frame.winfo_reqheight() + (0 if self.overlay_transparent_background else 4)
        self.overlay_window.geometry(f"{req_width}x{req_height}")
        
        # Reposition after size change
        self.position_overlay_window()

    def schedule_overlay_update(self):
        """Schedule periodic updates to the overlay content."""
        if self.overlay_window and self.overlay_enabled:
            self.update_overlay_content()
            self.root.after(1000, self.schedule_overlay_update)


def apply_pending_update():
    """Check for and apply any pending update file swap."""
    base_dir = os.path.dirname(os.path.abspath(__file__))
    new_file = os.path.join(base_dir, "tracker_new.py")
    current_file = os.path.abspath(__file__)
    old_file = os.path.join(base_dir, "tracker_old.py")
    
    if os.path.exists(new_file):
        try:
            # Backup current file
            if os.path.exists(current_file):
                if os.path.exists(old_file):
                    os.remove(old_file)
                os.rename(current_file, old_file)
            
            # Replace with new file
            os.rename(new_file, current_file)
            
            # If there's an update batch file, remove it
            batch_file = os.path.join(base_dir, "update_tracker.bat")
            if os.path.exists(batch_file):
                try:
                    os.remove(batch_file)
                except:
                    pass
            
            return True
        except Exception:
            # If swap fails, just continue with current version
            return False
    return False


def main():
    # Apply any pending updates before starting
    if apply_pending_update():
        # Restart with new version
        import subprocess
        import sys
        subprocess.Popen([sys.executable, __file__], cwd=os.path.dirname(os.path.abspath(__file__)))
        sys.exit(0)
    
    ensure_csv_exists()
    root = tk.Tk()
    app = TimeTrackerApp(root)

    root.mainloop()


if __name__ == "__main__":
    main()


