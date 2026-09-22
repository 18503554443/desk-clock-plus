using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace DeskClock
{
    internal static class Config
    {
        public static int PayDay = 10;
        public static int PayHour = 0;
        public static int PayMinute = 0;
        public static double PayAmount = 0;
        public static double PayMonths = 12;
        public static int RentDay = 0;
        public static int WorkStartHour = 9;
        public static int WorkStartMinute = 0;
        public static int WorkEndHour = 18;
        public static int WorkEndMinute = 0;
        public static string HolidaysRaw = "";
        public static List<DateTime> Holidays = new List<DateTime>();
        public static List<CountdownItem> Countdowns = new List<CountdownItem>();

        public static string FilePath()
        {
            return Path.Combine(DataDirectory(), "config.ini");
        }

        public static string DataDirectory()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskClockPlus");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string LegacyFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
        }

        public static void Load()
        {
            try
            {
                string path = FilePath();
                if (!File.Exists(path))
                {
                    string legacy = LegacyFilePath();
                    if (File.Exists(legacy)) File.Copy(legacy, path, false);
                }
                if (!File.Exists(path)) { Save(); return; }
                Holidays.Clear();
                Countdowns.Clear();
                string[] lines = File.ReadAllLines(path);
                for (int li = 0; li < lines.Length; li++)
                {
                    int i = lines[li].IndexOf('=');
                    if (i <= 0) continue;
                    string k = lines[li].Substring(0, i).Trim();
                    string v = lines[li].Substring(i + 1).Trim();
                    int n;
                    if (k == "payDay" && int.TryParse(v, out n) && n >= 1 && n <= 28) PayDay = n;
                    else if (k == "payHour" && int.TryParse(v, out n) && n >= 0 && n <= 23) PayHour = n;
                    else if (k == "payMinute" && int.TryParse(v, out n) && n >= 0 && n <= 59) PayMinute = n;
                    else if (k == "payAmount")
                    {
                        double dv;
                        if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv) && dv >= 0) PayAmount = dv;
                    }
                    else if (k == "payMonths")
                    {
                        double dv;
                        if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv) && dv >= 1 && dv <= 36) PayMonths = dv;
                    }
                    else if (k == "rentDay" && int.TryParse(v, out n) && n >= 1 && n <= 31) RentDay = n;
                    else if (k == "workStart")
                    {
                        int wh, wm;
                        if (TryParseTime(v, out wh, out wm)) { WorkStartHour = wh; WorkStartMinute = wm; }
                    }
                    else if (k == "workEnd")
                    {
                        int wh, wm;
                        if (TryParseTime(v, out wh, out wm)) { WorkEndHour = wh; WorkEndMinute = wm; }
                    }
                    else if (k == "holidays")
                    {
                        HolidaysRaw = v;
                        Holidays.Clear();
                        string[] parts = v.Split(',');
                        for (int pi = 0; pi < parts.Length; pi++)
                        {
                            DateTime dt;
                            if (DateTime.TryParseExact(parts[pi].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) Holidays.Add(dt);
                        }
                    }
                    else if (k == "countdown")
                    {
                        string[] parts = v.Split('|');
                        int cm, cd;
                        if (parts.Length >= 4 && parts[0].Trim().Length > 0 && int.TryParse(parts[2], out cm) && int.TryParse(parts[3], out cd) && cm >= 1 && cm <= 12 && cd >= 1 && cd <= 31)
                        {
                            bool lunar = parts[1].Trim().Equals("lunar", StringComparison.OrdinalIgnoreCase);
                            Countdowns.Add(new CountdownItem(parts[0].Trim(), lunar, cm, cd));
                        }
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath(),
                    "payDay=" + PayDay + Environment.NewLine +
                    "payHour=" + PayHour + Environment.NewLine +
                    "payMinute=" + PayMinute + Environment.NewLine +
                    "payAmount=" + PayAmount.ToString("0.00", CultureInfo.InvariantCulture) + Environment.NewLine +
                    "payMonths=" + PayMonths.ToString("0.##", CultureInfo.InvariantCulture) + Environment.NewLine +
                    "rentDay=" + RentDay + Environment.NewLine +
                    "workStart=" + WorkStartHour.ToString("00") + ":" + WorkStartMinute.ToString("00") + Environment.NewLine +
                    "workEnd=" + WorkEndHour.ToString("00") + ":" + WorkEndMinute.ToString("00") + Environment.NewLine);
                if (HolidaysRaw.Length > 0) File.AppendAllText(FilePath(), "holidays=" + HolidaysRaw + Environment.NewLine);
                for (int i = 0; i < Countdowns.Count; i++)
                {
                    CountdownItem item = Countdowns[i];
                    File.AppendAllText(FilePath(), "countdown=" + item.Name + "|" + (item.Lunar ? "lunar" : "solar") + "|" + item.Month + "|" + item.Day + Environment.NewLine);
                }
            }
            catch { }
        }

        public static bool TryParseTime(string text, out int hour, out int minute)
        {
            hour = 0;
            minute = 0;
            string[] parts = (text ?? "").Trim().Split(':');
            if (parts.Length < 1 || parts.Length > 2) return false;
            if (!int.TryParse(parts[0], out hour) || hour < 0 || hour > 23) return false;
            if (parts.Length == 2 && (!int.TryParse(parts[1], out minute) || minute < 0 || minute > 59)) return false;
            return true;
        }
    }

    internal sealed class HolidayPeriod
    {
        public readonly string Name;
        public readonly DateTime Start;
        public readonly DateTime End;

        public HolidayPeriod(string name, DateTime start, DateTime end)
        {
            Name = name;
            Start = start.Date;
            End = end.Date;
        }

        public bool Contains(DateTime day)
        {
            day = day.Date;
            return day >= Start && day <= End;
        }
    }

    internal sealed class CountdownItem
    {
        public string Name;
        public bool Lunar;
        public int Month;
        public int Day;

        public CountdownItem(string name, bool lunar, int month, int day)
        {
            Name = name;
            Lunar = lunar;
            Month = month;
            Day = day;
        }

        public override string ToString()
        {
            return Name + "  " + (Lunar ? "农历" : "公历") + " " + Month + "月" + Day + "日";
        }
    }

    internal sealed class CountdownOccurrence
    {
        public readonly CountdownItem Item;
        public readonly DateTime Date;
        public readonly int Days;

        public CountdownOccurrence(CountdownItem item, DateTime date, int days)
        {
            Item = item;
            Date = date;
            Days = days;
        }
    }

    internal static class StartupManager
    {
        private const string TaskName = "DeskClockPlusAutoStart";

        public static string ExecutablePath()
        {
            try { return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName; }
            catch { return System.Reflection.Assembly.GetExecutingAssembly().Location; }
        }

        public static bool IsEnabled()
        {
            return RunSchtasks("/Query /TN \"" + TaskName + "\"") == 0;
        }

        public static void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DeskClockPlus", "DeskClockPlus.exe");
                string dir = Path.GetDirectoryName(target);
                Directory.CreateDirectory(dir);
                if (!string.Equals(ExecutablePath(), target, StringComparison.OrdinalIgnoreCase)) File.Copy(ExecutablePath(), target, true);
                string arguments = "/Create /TN \"" + TaskName + "\" /TR \"\\\"" + target + "\\\"\" /SC ONLOGON /RL HIGHEST /F";
                if (RunSchtasks(arguments) != 0) throw new InvalidOperationException("failed to create startup task");
            }
            else
            {
                RunSchtasks("/Delete /TN \"" + TaskName + "\" /F");
            }
        }

        private static int RunSchtasks(string arguments)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), arguments);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info))
                {
                    process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    process.WaitForExit(10000);
                    return process.HasExited ? process.ExitCode : -1;
                }
            }
            catch { return -1; }
        }
    }

    internal static class CountdownCalendar
    {
        public static CountdownOccurrence FindNearest(DateTime now)
        {
            CountdownOccurrence nearest = null;
            DateTime today = now.Date;
            for (int i = 0; i < Config.Countdowns.Count; i++)
            {
                DateTime date;
                if (!TryGetNext(Config.Countdowns[i], today, out date)) continue;
                int days = (date - today).Days;
                if (nearest == null || date < nearest.Date) nearest = new CountdownOccurrence(Config.Countdowns[i], date, days);
            }
            return nearest;
        }

        private static bool TryGetNext(CountdownItem item, DateTime today, out DateTime date)
        {
            if (!item.Lunar)
            {
                for (int y = today.Year; y <= today.Year + 4; y++)
                {
                    try
                    {
                        DateTime candidate = new DateTime(y, item.Month, item.Day);
                        if (candidate >= today) { date = candidate; return true; }
                    }
                    catch { }
                }
            }
            else
            {
                ChineseLunisolarCalendar calendar = new ChineseLunisolarCalendar();
                int lunarYear = calendar.GetYear(today);
                for (int y = lunarYear; y <= lunarYear + 4; y++)
                {
                    try
                    {
                        int leapMonth = calendar.GetLeapMonth(y);
                        int actualMonth = item.Month;
                        if (leapMonth > 0 && item.Month >= leapMonth) actualMonth++;
                        DateTime candidate = calendar.ToDateTime(y, actualMonth, item.Day, 0, 0, 0, 0);
                        if (candidate >= today) { date = candidate.Date; return true; }
                    }
                    catch { }
                }
            }
            date = DateTime.MinValue;
            return false;
        }
    }

    internal static class HolidayCalendar
    {
        // 国办发明电〔2025〕7号：国务院办公厅关于2026年部分节假日安排的通知
        private static readonly HolidayPeriod[] Official = new HolidayPeriod[]
        {
            new HolidayPeriod("元旦", new DateTime(2026, 1, 1), new DateTime(2026, 1, 3)),
            new HolidayPeriod("春节", new DateTime(2026, 2, 15), new DateTime(2026, 2, 23)),
            new HolidayPeriod("清明节", new DateTime(2026, 4, 4), new DateTime(2026, 4, 6)),
            new HolidayPeriod("劳动节", new DateTime(2026, 5, 1), new DateTime(2026, 5, 5)),
            new HolidayPeriod("端午节", new DateTime(2026, 6, 19), new DateTime(2026, 6, 21)),
            new HolidayPeriod("中秋节", new DateTime(2026, 9, 25), new DateTime(2026, 9, 27)),
            new HolidayPeriod("国庆节", new DateTime(2026, 10, 1), new DateTime(2026, 10, 7))
        };

        private static readonly DateTime[] AdjustedWorkdays = new DateTime[]
        {
            new DateTime(2026, 1, 4),
            new DateTime(2026, 2, 14),
            new DateTime(2026, 2, 28),
            new DateTime(2026, 5, 9),
            new DateTime(2026, 9, 20),
            new DateTime(2026, 10, 10)
        };

        private static readonly object RemoteLock = new object();
        private static readonly List<HolidayPeriod> RemotePeriods = new List<HolidayPeriod>();
        private static readonly HashSet<DateTime> RemoteHolidayDays = new HashSet<DateTime>();
        private static readonly HashSet<DateTime> RemoteAdjustedWorkdays = new HashSet<DateTime>();
        private static readonly HashSet<int> RemoteYears = new HashSet<int>();
        private const string HolidayDataUrl = "https://cdn.jsdelivr.net/gh/NateScarlet/holiday-cn@master/";
        private const int HolidayCacheDays = 14;

        public static string Countdown(DateTime today)
        {
            DateTime day = today.Date;
            if (!IsWorkday(day)) return "假期中";

            for (int i = 1; i <= 370; i++)
            {
                if (!IsWorkday(day.AddDays(i))) return "距放假 " + i + "天";
            }
            return "放假安排待公布";
        }

        public static bool IsHoliday(DateTime day)
        {
            day = day.Date;
            lock (RemoteLock)
            {
                if (RemoteYears.Contains(day.Year))
                {
                    if (RemoteHolidayDays.Contains(day)) return true;
                }
                else
                {
                    for (int i = 0; i < Official.Length; i++)
                    {
                        if (Official[i].Contains(day)) return true;
                    }
                }
            }
            return Config.Holidays.Contains(day);
        }

        public static bool IsWorkday(DateTime day)
        {
            day = day.Date;
            bool remoteYear;
            lock (RemoteLock)
            {
                remoteYear = RemoteYears.Contains(day.Year);
                if (remoteYear && RemoteAdjustedWorkdays.Contains(day)) return true;
            }
            if (!remoteYear)
            {
                for (int i = 0; i < AdjustedWorkdays.Length; i++)
                {
                    if (AdjustedWorkdays[i] == day) return true;
                }
            }
            if (IsHoliday(day)) return false;
            return day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday;
        }

        public static int WorkdayCountInMonth(int year, int month)
        {
            int count = 0;
            int days = DateTime.DaysInMonth(year, month);
            for (int d = 1; d <= days; d++)
            {
                if (IsWorkday(new DateTime(year, month, d))) count++;
            }
            return count;
        }

        public static void Initialize()
        {
            int year = DateTime.Now.Year;
            for (int y = year; y <= year + 1; y++)
            {
                try
                {
                    string path = CachePath(y);
                    if (File.Exists(path)) ApplyYearJson(File.ReadAllText(path, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    SysInfo.Log("holiday cache load failed " + y + ": " + ex.Message);
                }
            }
        }

        public static void RefreshAsync(Dispatcher dispatcher)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int year = DateTime.Now.Year;
                for (int y = year; y <= year + 1; y++)
                {
                    string path = CachePath(y);
                    try
                    {
                        if (File.Exists(path) && (DateTime.Now - File.GetLastWriteTime(path)).TotalDays < HolidayCacheDays) continue;

                        string json = DownloadYear(y);
                        File.WriteAllText(path, json, Encoding.UTF8);
                        string payload = json;
                        dispatcher.BeginInvoke((Action)delegate { ApplyYearJson(payload); });
                    }
                    catch (Exception ex)
                    {
                        SysInfo.Log("holiday update failed " + y + ": " + ex.Message);
                        try
                        {
                            if (!File.Exists(path)) continue;
                            string cached = File.ReadAllText(path, Encoding.UTF8);
                            dispatcher.BeginInvoke((Action)delegate { ApplyYearJson(cached); });
                        }
                        catch { }
                    }
                }
            });
        }

        private static string CachePath(int year)
        {
            return Path.Combine(Config.DataDirectory(), "holiday-" + year + ".json");
        }

        private static string DownloadYear(int year)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.UserAgent] = "DeskClockPlus/1.0";
                return client.DownloadString(HolidayDataUrl + year + ".json");
            }
        }

        private static void ApplyYearJson(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("days")) return;

            object[] days = root["days"] as object[];
            if (days == null || days.Length == 0) return;

            int year = Convert.ToInt32(root["year"]);
            List<DateTime> offDays = new List<DateTime>();
            Dictionary<DateTime, string> offNames = new Dictionary<DateTime, string>();
            HashSet<DateTime> workdays = new HashSet<DateTime>();

            for (int i = 0; i < days.Length; i++)
            {
                Dictionary<string, object> item = days[i] as Dictionary<string, object>;
                if (item == null) continue;
                DateTime date;
                if (!DateTime.TryParse(Convert.ToString(item["date"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) continue;
                date = date.Date;
                bool isOffDay = Convert.ToBoolean(item["isOffDay"]);
                if (isOffDay)
                {
                    offDays.Add(date);
                    offNames[date] = Convert.ToString(item["name"]);
                }
                else
                {
                    workdays.Add(date);
                }
            }

            offDays.Sort();
            List<HolidayPeriod> periods = new List<HolidayPeriod>();
            for (int i = 0; i < offDays.Count; )
            {
                DateTime start = offDays[i];
                DateTime end = start;
                string name = offNames[start];
                i++;
                while (i < offDays.Count && offDays[i] == end.AddDays(1) && offNames[offDays[i]] == name)
                {
                    end = offDays[i];
                    i++;
                }
                periods.Add(new HolidayPeriod(name, start, end));
            }

            lock (RemoteLock)
            {
                RemotePeriods.RemoveAll(delegate(HolidayPeriod p) { return p.Start.Year == year || p.End.Year == year; });
                RemoteHolidayDays.RemoveWhere(delegate(DateTime d) { return d.Year == year; });
                RemoteAdjustedWorkdays.RemoveWhere(delegate(DateTime d) { return d.Year == year; });
                RemoteYears.Remove(year);
                for (int i = 0; i < offDays.Count; i++) RemoteHolidayDays.Add(offDays[i]);
                foreach (DateTime d in workdays) RemoteAdjustedWorkdays.Add(d);
                RemotePeriods.AddRange(periods);
                RemoteYears.Add(year);
            }
            SysInfo.Log("holiday data loaded for " + year);
        }
    }

    internal static class LibreCpuMonitor
    {
        private static readonly object SyncRoot = new object();
        private static LibreHardwareMonitor.Hardware.Computer computer;
        private static bool failed;

        public static int CpuTemperature()
        {
            if (failed) return int.MinValue;
            lock (SyncRoot)
            {
                try
                {
                    if (computer == null)
                    {
                        computer = new LibreHardwareMonitor.Hardware.Computer();
                        computer.IsCpuEnabled = true;
                        computer.Open();
                    }

                    int preferred = int.MinValue;
                    int maximum = int.MinValue;
                    foreach (LibreHardwareMonitor.Hardware.IHardware hardware in computer.Hardware)
                    {
                        if (hardware.HardwareType != LibreHardwareMonitor.Hardware.HardwareType.Cpu) continue;
                        hardware.Update();
                        foreach (LibreHardwareMonitor.Hardware.ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType != LibreHardwareMonitor.Hardware.SensorType.Temperature || !sensor.Value.HasValue) continue;
                            int value = (int)Math.Round(sensor.Value.Value);
                            if (value < -50 || value > 150) continue;
                            if (value > maximum) maximum = value;
                            if (sensor.Name != null && sensor.Name.IndexOf("Tctl", StringComparison.OrdinalIgnoreCase) >= 0) preferred = value;
                        }
                    }
                    if (preferred != int.MinValue) return preferred;
                    return maximum;
                }
                catch
                {
                    failed = true;
                    return int.MinValue;
                }
            }
        }
    }

    internal static class SysInfo
    {
        internal static void Log(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(Config.DataDirectory(), "clock.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg + Environment.NewLine);
            }
            catch { }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME { public uint Lo; public uint Hi; }

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        private static ulong ToUlong(FILETIME f) { return ((ulong)f.Hi << 32) | f.Lo; }
        private static ulong prevIdle, prevKernel, prevUser;
        private static bool hasPrev;

        public static int CpuPercent()
        {
            FILETIME i, k, u;
            if (!GetSystemTimes(out i, out k, out u)) return 0;
            ulong ci = ToUlong(i), ck = ToUlong(k), cu = ToUlong(u);
            int pct = 0;
            if (hasPrev)
            {
                ulong dk = ck - prevKernel;
                ulong du = cu - prevUser;
                ulong di = ci - prevIdle;
                ulong total = dk + du;
                if (total > 0)
                {
                    double p = 100.0 * (double)(total - di) / (double)total;
                    pct = (int)Math.Round(p);
                }
            }
            prevIdle = ci; prevKernel = ck; prevUser = cu; hasPrev = true;
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;
            return pct;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

        public static int MemPercent()
        {
            MEMORYSTATUSEX m = new MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref m)) return (int)m.dwMemoryLoad;
            return 0;
        }

        public static int CpuTemperature()
        {
            int libre = LibreCpuMonitor.CpuTemperature();
            if (libre != int.MinValue) return libre;
            int lenovo = LenovoCpuTemperature();
            if (lenovo != int.MinValue) return lenovo;
            try
            {
                double max = double.MinValue;
                bool found = false;
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "root\\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (ManagementBaseObject item in searcher.Get())
                    {
                        try
                        {
                            double celsius = Convert.ToDouble(item["CurrentTemperature"]) / 10.0 - 273.15;
                            if (celsius > max) max = celsius;
                            found = true;
                        }
                        finally
                        {
                            item.Dispose();
                        }
                    }
                }
                if (found && max > -50 && max < 150) return (int)Math.Round(max);
            }
            catch { }
            return int.MinValue;
        }

        private static int LenovoCpuTemperature()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "root\\WMI", "SELECT * FROM LENOVO_GAMEZONE_DATA"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        try
                        {
                            using (ManagementBaseObject result = item.InvokeMethod("GetCPUTemp", null, null))
                            {
                                if (result == null) continue;
                                int value = Convert.ToInt32(result["Data"]);
                                if (value > 0 && value < 150) { LogCpuTempSuccess(value, "instance"); return value; }
                            }
                        }
                        finally
                        {
                            item.Dispose();
                        }
                    }
                }
            }
            catch { }
            try
            {
                using (ManagementClass cls = new ManagementClass("root\\WMI", "LENOVO_GAMEZONE_DATA", null))
                using (ManagementBaseObject input = cls.GetMethodParameters("GetCPUTemp"))
                using (ManagementBaseObject result = cls.InvokeMethod("GetCPUTemp", input, null))
                {
                    if (result != null)
                    {
                        int value = Convert.ToInt32(result["Data"]);
                        if (value > 0 && value < 150) { LogCpuTempSuccess(value, "class"); return value; }
                    }
                }
            }
            catch { }
            return int.MinValue;
        }

        private static bool cpuTempLogged;

        private static void LogCpuTempSuccess(int value, string source)
        {
            if (cpuTempLogged) return;
            cpuTempLogged = true;
            Log("CPU temperature source=" + source + " value=" + value);
        }

        public static void GpuStats(out int usage, out int temperature)
        {
            usage = int.MinValue;
            temperature = int.MinValue;
            try
            {
                string exe = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
                if (!File.Exists(exe)) return;

                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                info.FileName = exe;
                info.Arguments = "--query-gpu=utilization.gpu,temperature.gpu --format=csv,noheader,nounits";
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;

                using (System.Diagnostics.Process process = new System.Diagnostics.Process())
                {
                    process.StartInfo = info;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(2000))
                    {
                        try { process.Kill(); } catch { }
                        return;
                    }
                    string first = output.Trim().Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    string[] parts = first.Split(',');
                    int value;
                    if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out value)) usage = value;
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out value)) temperature = value;
                }
            }
            catch { }
        }

        public static TimeSpan Uptime()
        {
            return TimeSpan.FromMilliseconds((double)GetTickCount64());
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MIB_IFROW
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string wszName;
            public uint dwIndex;
            public uint dwType;
            public uint dwMtu;
            public uint dwSpeed;
            public uint dwPhysAddrLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] bPhysAddr;
            public uint dwAdminStatus;
            public uint dwOperStatus;
            public uint dwLastChange;
            public uint dwInOctets;
            public uint dwInUcastPkts;
            public uint dwInNUcastPkts;
            public uint dwInDiscards;
            public uint dwInErrors;
            public uint dwInUnknownProtos;
            public uint dwOutOctets;
            public uint dwOutUcastPkts;
            public uint dwOutNUcastPkts;
            public uint dwOutDiscards;
            public uint dwOutErrors;
            public uint dwOutQLen;
            public uint dwDescrLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] bDescr;
        }

        [DllImport("iphlpapi.dll")]
        private static extern int GetIfTable(IntPtr pTable, ref uint pdwSize, bool bOrder);

        private static long prevSent = -1, prevRecv = -1;
        private static DateTime prevAt;

        private static void NetTotals(out long sent, out long recv)
        {
            sent = 0; recv = 0;
            uint size = 0;
            GetIfTable(IntPtr.Zero, ref size, false);
            if (size == 0) return;
            IntPtr buf = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetIfTable(buf, ref size, false) != 0) return;
                int n = Marshal.ReadInt32(buf);
                int rowSize = Marshal.SizeOf(typeof(MIB_IFROW));
                for (int i = 0; i < n; i++)
                {
                    MIB_IFROW row = (MIB_IFROW)Marshal.PtrToStructure(IntPtr.Add(buf, 4 + i * rowSize), typeof(MIB_IFROW));
                    if (row.dwOperStatus != 1) continue;   // only interfaces that are up
                    if (row.dwType == 24) continue;        // skip software loopback
                    sent += row.dwOutOctets;
                    recv += row.dwInOctets;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        private static long Delta(long cur, long prev)
        {
            long d = cur - prev;
            if (d < 0) d += 4294967296L;   // 32-bit counter wrap
            return d;
        }

        public static void NetRates(out double up, out double down)
        {
            up = 0; down = 0;
            try
            {
                long sent, recv;
                NetTotals(out sent, out recv);
                DateTime now = DateTime.UtcNow;
                if (prevSent >= 0)
                {
                    double dt = (now - prevAt).TotalSeconds;
                    if (dt > 0.2)
                    {
                        up = (double)Delta(sent, prevSent) / dt;
                        down = (double)Delta(recv, prevRecv) / dt;
                    }
                }
                prevSent = sent; prevRecv = recv; prevAt = now;
            }
            catch (Exception ex)
            {
                Log("net error: " + ex.Message);
            }
            if (up < 0) up = 0;
            if (down < 0) down = 0;
        }

        public static string FmtRate(double b)
        {
            if (b >= 1048576.0) return (b / 1048576.0).ToString("0.0") + "M";
            if (b >= 1024.0) return (b / 1024.0).ToString("0.0") + "K";
            return ((long)b).ToString() + "B";
        }
    }

    internal sealed class PayDialog : Window
    {
        private TextBox dayBox;
        private TextBox timeBox;
        private TextBox amtBox;
        private TextBox monthsBox;
        private TextBox rentDayBox;
        private TextBox workStartBox;
        private TextBox workEndBox;

        public PayDialog()
        {
            Title = "发薪日设置";
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            Width = 320;
            Height = 370;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            Background = new SolidColorBrush(Color.FromRgb(0x24, 0x27, 0x2b));

            Grid g = new Grid();
            g.Margin = new Thickness(14);
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            FontFamily yahei = new FontFamily("Microsoft YaHei UI");
            TextBlock l1 = new TextBlock { Text = "发薪日", Foreground = Brushes.WhiteSmoke, FontFamily = yahei, VerticalAlignment = VerticalAlignment.Center };
            TextBlock l2 = new TextBlock { Text = "到账时间", Foreground = Brushes.WhiteSmoke, FontFamily = yahei, VerticalAlignment = VerticalAlignment.Center };
            TextBlock l3 = new TextBlock { Text = "月薪(元)", Foreground = Brushes.WhiteSmoke, FontFamily = yahei, VerticalAlignment = VerticalAlignment.Center };
            TextBlock l4 = new TextBlock { Text = "年薪月数", Foreground = Brushes.WhiteSmoke, FontFamily = yahei, VerticalAlignment = VerticalAlignment.Center };
            TextBlock l5 = new TextBlock { Text = "交房租日", Foreground = Brushes.WhiteSmoke, FontFamily = yahei, VerticalAlignment = VerticalAlignment.Center };
            TextBlock l6 = new TextBlock { Text = "上班时间", Foreground = Brushes.WhiteSmoke, FontFamily = yahei, VerticalAlignment = VerticalAlignment.Center };
            TextBlock l7 = new TextBlock { Text = "下班时间", Foreground = Brushes.WhiteSmoke, FontFamily = yahei, VerticalAlignment = VerticalAlignment.Center };
            dayBox = new TextBox { Text = Config.PayDay.ToString(), VerticalContentAlignment = VerticalAlignment.Center, Height = 26 };
            timeBox = new TextBox { Text = Config.PayHour.ToString("00") + ":" + Config.PayMinute.ToString("00"), VerticalContentAlignment = VerticalAlignment.Center, Height = 26 };
            amtBox = new TextBox { Text = Config.PayAmount > 0 ? Config.PayAmount.ToString("0.00", CultureInfo.InvariantCulture) : "", VerticalContentAlignment = VerticalAlignment.Center, Height = 26 };
            monthsBox = new TextBox { Text = Config.PayMonths.ToString("0.##", CultureInfo.InvariantCulture), VerticalContentAlignment = VerticalAlignment.Center, Height = 26 };
            rentDayBox = new TextBox { Text = Config.RentDay > 0 ? Config.RentDay.ToString() : "", VerticalContentAlignment = VerticalAlignment.Center, Height = 26 };
            workStartBox = new TextBox { Text = Config.WorkStartHour.ToString("00") + ":" + Config.WorkStartMinute.ToString("00"), VerticalContentAlignment = VerticalAlignment.Center, Height = 26 };
            workEndBox = new TextBox { Text = Config.WorkEndHour.ToString("00") + ":" + Config.WorkEndMinute.ToString("00"), VerticalContentAlignment = VerticalAlignment.Center, Height = 26 };
            Grid.SetRow(l1, 0); Grid.SetColumn(l1, 0);
            Grid.SetRow(dayBox, 0); Grid.SetColumn(dayBox, 1);
            Grid.SetRow(l2, 1); Grid.SetColumn(l2, 0);
            Grid.SetRow(timeBox, 1); Grid.SetColumn(timeBox, 1);
            Grid.SetRow(l3, 2); Grid.SetColumn(l3, 0);
            Grid.SetRow(amtBox, 2); Grid.SetColumn(amtBox, 1);
            Grid.SetRow(l4, 3); Grid.SetColumn(l4, 0);
            Grid.SetRow(monthsBox, 3); Grid.SetColumn(monthsBox, 1);
            Grid.SetRow(l5, 4); Grid.SetColumn(l5, 0);
            Grid.SetRow(rentDayBox, 4); Grid.SetColumn(rentDayBox, 1);
            Grid.SetRow(l6, 5); Grid.SetColumn(l6, 0);
            Grid.SetRow(workStartBox, 5); Grid.SetColumn(workStartBox, 1);
            Grid.SetRow(l7, 6); Grid.SetColumn(l7, 0);
            Grid.SetRow(workEndBox, 6); Grid.SetColumn(workEndBox, 1);

            Button ok = new Button { Content = "保存", Width = 80, Height = 28, HorizontalAlignment = HorizontalAlignment.Right };
            ok.Click += delegate { TrySave(); };
            Grid.SetRow(ok, 7); Grid.SetColumn(ok, 1);
            ok.VerticalAlignment = VerticalAlignment.Bottom;

            g.Children.Add(l1); g.Children.Add(l2); g.Children.Add(dayBox); g.Children.Add(timeBox); g.Children.Add(ok);
            g.Children.Add(l3); g.Children.Add(amtBox); g.Children.Add(l4); g.Children.Add(monthsBox);
            g.Children.Add(l5); g.Children.Add(rentDayBox); g.Children.Add(l6); g.Children.Add(workStartBox);
            g.Children.Add(l7); g.Children.Add(workEndBox);
            Content = g;
        }

        private void TrySave()
        {
            int d;
            if (!int.TryParse(dayBox.Text.Trim(), out d) || d < 1 || d > 28)
            {
                MessageBox.Show("发薪日请填 1-28 的数字", "提示");
                return;
            }
            int h = 0, mi = 0;
            int wsH = 0, wsM = 0, weH = 0, weM = 0;
            double amt = 0;
            double months = 12;
            int rentDay = 0;
            string t = timeBox.Text.Trim();
            if (t.Length > 0 && !Config.TryParseTime(t, out h, out mi))
            {
                MessageBox.Show("到账时间请填 HH:mm，例如 09:00", "提示");
                return;
            }
            if (!Config.TryParseTime(workStartBox.Text, out wsH, out wsM))
            {
                MessageBox.Show("上班时间请填 HH:mm，例如 09:00", "提示");
                return;
            }
            if (!Config.TryParseTime(workEndBox.Text, out weH, out weM))
            {
                MessageBox.Show("下班时间请填 HH:mm，例如 18:00", "提示");
                return;
            }
            if (weH * 60 + weM <= wsH * 60 + wsM)
            {
                MessageBox.Show("下班时间需要晚于上班时间", "提示");
                return;
            }
            string a = amtBox.Text.Trim();
            if (a.Length > 0)
            {
                if (!double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out amt) || amt < 0)
                {
                    MessageBox.Show("月薪请填写数字，例如 12000", "提示");
                    return;
                }
            }
            if (!double.TryParse(monthsBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out months) || months < 1 || months > 36)
            {
                MessageBox.Show("年薪月数请填 1-36，例如 13", "提示");
                return;
            }
            string rentText = rentDayBox.Text.Trim();
            if (rentText.Length > 0 && (!int.TryParse(rentText, out rentDay) || rentDay < 1 || rentDay > 31))
            {
                MessageBox.Show("交房租日请填 1-31，留空表示不设置", "提示");
                return;
            }
            Config.PayDay = d; Config.PayHour = h; Config.PayMinute = mi; Config.PayAmount = amt;
            Config.PayMonths = months;
            Config.RentDay = rentDay;
            Config.WorkStartHour = wsH; Config.WorkStartMinute = wsM;
            Config.WorkEndHour = weH; Config.WorkEndMinute = weM;
            Config.Save();
            DialogResult = true;
            Close();
        }
    }

    internal sealed class CountdownDialog : Window
    {
        private readonly List<CountdownItem> items = new List<CountdownItem>();
        private ListBox list;
        private TextBox nameBox;
        private ComboBox typeBox;
        private TextBox monthBox;
        private TextBox dayBox;

        public CountdownDialog()
        {
            Title = "倒计时设置";
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            Width = 430;
            Height = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            Background = new SolidColorBrush(Color.FromRgb(0x24, 0x27, 0x2b));

            for (int i = 0; i < Config.Countdowns.Count; i++)
            {
                CountdownItem item = Config.Countdowns[i];
                items.Add(new CountdownItem(item.Name, item.Lunar, item.Month, item.Day));
            }

            Grid root = new Grid();
            root.Margin = new Thickness(14);
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            list = new ListBox { MinHeight = 150, Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetRow(list, 0);
            root.Children.Add(list);

            Grid form = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock nameLabel = Label("名称");
            TextBlock typeLabel = Label("类型");
            TextBlock monthLabel = Label("月份");
            TextBlock dayLabel = Label("日期");
            Grid.SetRow(nameLabel, 0); Grid.SetColumn(nameLabel, 0); Grid.SetColumnSpan(nameLabel, 2);
            Grid.SetRow(typeLabel, 0); Grid.SetColumn(typeLabel, 2);
            Grid.SetRow(monthLabel, 0); Grid.SetColumn(monthLabel, 3);
            Grid.SetRow(dayLabel, 0); Grid.SetColumn(dayLabel, 4);
            form.Children.Add(nameLabel); form.Children.Add(typeLabel); form.Children.Add(monthLabel); form.Children.Add(dayLabel);

            nameBox = new TextBox { Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            typeBox = new ComboBox { Height = 26, Margin = new Thickness(4, 0, 4, 0) };
            typeBox.Items.Add("公历");
            typeBox.Items.Add("农历");
            typeBox.SelectedIndex = 0;
            monthBox = new TextBox { Height = 26, Margin = new Thickness(4, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center };
            dayBox = new TextBox { Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            Grid.SetRow(nameBox, 1); Grid.SetColumn(nameBox, 0); Grid.SetColumnSpan(nameBox, 2);
            Grid.SetRow(typeBox, 1); Grid.SetColumn(typeBox, 2);
            Grid.SetRow(monthBox, 1); Grid.SetColumn(monthBox, 3);
            Grid.SetRow(dayBox, 1); Grid.SetColumn(dayBox, 4);
            form.Children.Add(nameBox); form.Children.Add(typeBox); form.Children.Add(monthBox); form.Children.Add(dayBox);
            Grid.SetRow(form, 1);
            root.Children.Add(form);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button add = new Button { Content = "添加", Width = 64, Height = 28, Margin = new Thickness(4, 0, 0, 0) };
            Button update = new Button { Content = "更新", Width = 64, Height = 28, Margin = new Thickness(4, 0, 0, 0) };
            Button delete = new Button { Content = "删除", Width = 64, Height = 28, Margin = new Thickness(4, 0, 0, 0) };
            Button save = new Button { Content = "保存", Width = 64, Height = 28, Margin = new Thickness(12, 0, 0, 0) };
            add.Click += delegate { AddItem(); };
            update.Click += delegate { UpdateItem(); };
            delete.Click += delegate { DeleteItem(); };
            save.Click += delegate { SaveItems(); };
            buttons.Children.Add(add); buttons.Children.Add(update); buttons.Children.Add(delete); buttons.Children.Add(save);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            list.SelectionChanged += delegate { LoadSelected(); };
            Content = root;
            RefreshList(-1);
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Foreground = Brushes.WhiteSmoke, FontFamily = new FontFamily("Microsoft YaHei UI"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
        }

        private void RefreshList(int selected)
        {
            list.Items.Clear();
            for (int i = 0; i < items.Count; i++) list.Items.Add(items[i]);
            if (selected >= 0 && selected < items.Count) list.SelectedIndex = selected;
        }

        private bool TryBuildItem(out CountdownItem item)
        {
            item = null;
            string name = nameBox.Text.Trim();
            int month, day;
            if (name.Length == 0 || name.IndexOf('|') >= 0)
            {
                MessageBox.Show("请输入名称，名称中不能包含 | 字符", "提示");
                return false;
            }
            if (!int.TryParse(monthBox.Text.Trim(), out month) || month < 1 || month > 12)
            {
                MessageBox.Show("月份请填 1-12", "提示");
                return false;
            }
            if (!int.TryParse(dayBox.Text.Trim(), out day) || day < 1 || day > 31)
            {
                MessageBox.Show("日期请填 1-31", "提示");
                return false;
            }
            bool lunar = typeBox.SelectedIndex == 1;
            if (day > DateTime.DaysInMonth(2024, month))
            {
                MessageBox.Show(lunar ? "农历日期请填 1-30" : "该月份没有这个日期", "提示");
                return false;
            }
            item = new CountdownItem(name, lunar, month, day);
            return true;
        }

        private void AddItem()
        {
            CountdownItem item;
            if (!TryBuildItem(out item)) return;
            items.Add(item);
            RefreshList(items.Count - 1);
        }

        private void UpdateItem()
        {
            if (list.SelectedIndex < 0) { MessageBox.Show("请先选择要更新的倒计时", "提示"); return; }
            CountdownItem item;
            if (!TryBuildItem(out item)) return;
            items[list.SelectedIndex] = item;
            RefreshList(list.SelectedIndex);
        }

        private void DeleteItem()
        {
            if (list.SelectedIndex < 0) { MessageBox.Show("请先选择要删除的倒计时", "提示"); return; }
            items.RemoveAt(list.SelectedIndex);
            nameBox.Text = ""; monthBox.Text = ""; dayBox.Text = ""; typeBox.SelectedIndex = 0;
            RefreshList(-1);
        }

        private void LoadSelected()
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= items.Count) return;
            CountdownItem item = items[list.SelectedIndex];
            nameBox.Text = item.Name;
            typeBox.SelectedIndex = item.Lunar ? 1 : 0;
            monthBox.Text = item.Month.ToString();
            dayBox.Text = item.Day.ToString();
        }

        private void SaveItems()
        {
            Config.Countdowns.Clear();
            for (int i = 0; i < items.Count; i++) Config.Countdowns.Add(items[i]);
            Config.Save();
            DialogResult = true;
            Close();
        }
    }

    public sealed class ClockWindow : Window
    {
        private TextBlock hh, mm, ss, payLabel, payValue, payDaily, payToday, customCountdown, rentLine, dateLine, upLine, cpuLine, gpuLine, memLine, netLine;
        private double dailyEarn;
        private DispatcherTimer fastTimer, slowTimer;
        private IntPtr myHwnd;
        private VirtualDesktopFollower desktopFollower;
        private WinForms.NotifyIcon trayIcon;
        private TextBlock restLine;
        private double upRate, downRate;
        private static readonly FontFamily Digits = new FontFamily("Segoe UI Light");
        private static readonly FontFamily Yahei = new FontFamily("Microsoft YaHei UI");

        public ClockWindow()
        {
            Config.Load();
            HolidayCalendar.Initialize();
            Title = "桌面时钟+";
            WindowStyle = WindowStyle.None;
            Background = Hex("#FF262226");
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Width = 1560;
            Height = 250;

            Border panel = new Border();
            panel.CornerRadius = new CornerRadius(16);
            panel.Background = Hex("#FF262226");
            panel.Padding = new Thickness(10);

            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(TimeTile(out hh));
            row.Children.Add(TimeTile(out mm));
            row.Children.Add(TimeTile(out ss));
            row.Children.Add(PayTile());
            row.Children.Add(InfoTile());
            panel.Child = row;
            Content = panel;

            ContextMenu menu = new ContextMenu();
            MenuItem setPay = new MenuItem { Header = "设置发薪日..." };
            setPay.Click += delegate { OpenPayDialog(); };
            MenuItem setCountdown = new MenuItem { Header = "设置倒计时..." };
            setCountdown.Click += delegate { OpenCountdownDialog(); };
            MenuItem startup = new MenuItem { Header = "开机自启", IsCheckable = true, IsChecked = StartupManager.IsEnabled() };
            startup.Click += delegate
            {
                StartupManager.SetEnabled(startup.IsChecked);
                startup.IsChecked = StartupManager.IsEnabled();
            };
            MenuItem hide = new MenuItem { Header = "隐藏到托盘" };
            hide.Click += delegate { Hide(); };
            MenuItem exit = new MenuItem { Header = "退出" };
            exit.Click += delegate { ExitFromTray(); };
            menu.Items.Add(setPay);
            menu.Items.Add(setCountdown);
            menu.Items.Add(startup);
            menu.Items.Add(hide);
            menu.Items.Add(exit);
            ContextMenu = menu;

            SetupTrayIcon();

            fastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            fastTimer.Tick += delegate { RefreshTime(); };
            slowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            slowTimer.Tick += delegate { RefreshSys(); };

            Loaded += delegate
            {
                myHwnd = new WindowInteropHelper(this).Handle;
                
                // 先不挂到桌面层，改成普通窗口
                // IntPtr host = DesktopLayer.FindHost();
                // if (host != IntPtr.Zero)
                // {
                //     DesktopLayer.SetParent(myHwnd, host);
                //     SysInfo.Log("parented to desktop layer " + host.ToString());
                // }
                // else
                // {
                    SysInfo.Log("normal window, not parented");
                // }
                
                PlaceTopCenterPrimary();

                HwndSource source = HwndSource.FromHwnd(myHwnd);
                if (source != null) source.AddHook(WndProc);
                desktopFollower = VirtualDesktopFollower.Start(myHwnd);
                HolidayCalendar.RefreshAsync(Dispatcher);
                
                RefreshTime();
                RefreshSys();
                fastTimer.Start();
                slowTimer.Start();
            };
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }

        private void SetupTrayIcon()
        {
            trayIcon = new WinForms.NotifyIcon();
            trayIcon.Icon = LoadApplicationIcon();
            trayIcon.Text = "时钟发薪倒计时";
            trayIcon.Visible = true;

            WinForms.ContextMenuStrip trayMenu = new WinForms.ContextMenuStrip();
            WinForms.ToolStripMenuItem showHide = new WinForms.ToolStripMenuItem("显示/隐藏");
            showHide.Click += delegate { ToggleClockVisibility(); };
            WinForms.ToolStripMenuItem settings = new WinForms.ToolStripMenuItem("设置发薪日...");
            settings.Click += delegate { OpenPayDialog(); };
            WinForms.ToolStripMenuItem countdowns = new WinForms.ToolStripMenuItem("设置倒计时...");
            countdowns.Click += delegate { OpenCountdownDialog(); };
            WinForms.ToolStripMenuItem startup = new WinForms.ToolStripMenuItem("开机自启");
            startup.CheckOnClick = true;
            startup.Checked = StartupManager.IsEnabled();
            startup.CheckedChanged += delegate { StartupManager.SetEnabled(startup.Checked); };
            WinForms.ToolStripMenuItem exit = new WinForms.ToolStripMenuItem("退出");
            exit.Click += delegate { ExitFromTray(); };
            trayMenu.Items.Add(showHide);
            trayMenu.Items.Add(settings);
            trayMenu.Items.Add(countdowns);
            trayMenu.Items.Add(startup);
            trayMenu.Items.Add(new WinForms.ToolStripSeparator());
            trayMenu.Items.Add(exit);
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.MouseClick += delegate(object sender, WinForms.MouseEventArgs e)
            {
                if (e.Button == WinForms.MouseButtons.Left) ToggleClockVisibility();
            };
        }

        private static System.Drawing.Icon LoadApplicationIcon()
        {
            try
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string[] names = assembly.GetManifestResourceNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (!names[i].EndsWith("AppIcon.ico", StringComparison.OrdinalIgnoreCase)) continue;
                    using (Stream stream = assembly.GetManifestResourceStream(names[i]))
                    {
                        if (stream != null) return new System.Drawing.Icon(stream);
                    }
                }
            }
            catch { }
            return System.Drawing.SystemIcons.Application;
        }

        private void OpenPayDialog()
        {
            PayDialog dlg = new PayDialog();
            if (dlg.ShowDialog() == true) RefreshPay();
        }

        private void OpenCountdownDialog()
        {
            CountdownDialog dlg = new CountdownDialog();
            if (dlg.ShowDialog() == true) RefreshCountdown();
        }

        private void ToggleClockVisibility()
        {
            if (IsVisible)
            {
                Hide();
                return;
            }
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitFromTray()
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            base.OnClosed(e);
        }

        // 虚拟桌面切换监听
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        private static int _vdmSwitchMsg;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 第一次调用时注册虚拟桌面切换消息
            if (_vdmSwitchMsg == 0)
            {
                _vdmSwitchMsg = RegisterWindowMessage("VirtualDesktopManager");
            }

            // 收到虚拟桌面切换消息时立即尝试跟随当前桌面。
            if (msg == _vdmSwitchMsg && _vdmSwitchMsg != 0)
            {
                if (desktopFollower != null) desktopFollower.Refresh();
                SysInfo.Log("virtual desktop switch message received");
            }
            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT2 { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT2 rcMonitor;
            public RECT2 rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        // Put the window at the horizontal center / near-top of the primary monitor's
        // work area, converting physical pixels to WPF device-independent units.
        private void PlaceTopCenterPrimary()
        {
            try
            {
                POINT pt = new POINT();
                pt.x = 0; pt.y = 0;
                IntPtr hMon = MonitorFromPoint(pt, 2);
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (!GetMonitorInfo(hMon, ref mi)) return;
                PresentationSource src = PresentationSource.FromVisual(this);
                if (src == null || src.CompositionTarget == null)
                {
                    Left = mi.rcWork.Left + (mi.rcWork.Right - mi.rcWork.Left - Width) / 2.0;
                    Top = mi.rcWork.Top + 40;
                    return;
                }
                Matrix toDevice = src.CompositionTarget.TransformToDevice;
                Matrix fromDevice = src.CompositionTarget.TransformFromDevice;
                double physW = Width * toDevice.M11;
                double physLeft = mi.rcWork.Left + (mi.rcWork.Right - mi.rcWork.Left - physW) / 2.0;
                double physTop = mi.rcWork.Top + 40 * toDevice.M22;
                physTop = mi.rcWork.Top;
                Point dip = fromDevice.Transform(new Point(physLeft, physTop));
                Left = dip.X;
                Top = dip.Y;
                SysInfo.Log("placed work=(" + mi.rcWork.Left + "," + mi.rcWork.Top + "," + mi.rcWork.Right + "," + mi.rcWork.Bottom + ") dpiX=" + toDevice.M11 + " left=" + Left + " top=" + Top);
            }
            catch (Exception ex)
            {
                SysInfo.Log("place error: " + ex.Message);
            }
        }

        private static Brush Hex(string s)
        {
            return (Brush)new BrushConverter().ConvertFromString(s);
        }

        private Border TimeTile(out TextBlock target)
        {
            TextBlock tb = new TextBlock();
            tb.FontFamily = Digits;
            tb.FontSize = 150;
            tb.Foreground = Hex("#C9CDD1");
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;
            tb.Text = "--";
            target = tb;
            Border b = TileShell();
            b.Child = tb;
            return b;
        }

        private Border TileShell()
        {
            Border b = new Border();
            b.Width = 300;
            b.Height = 230;
            b.Margin = new Thickness(0, 0, 12, 0);
            b.CornerRadius = new CornerRadius(14);
            b.Background = Hex("#FF262B2E");
            b.BorderBrush = Hex("#26FFFFFF");
            b.BorderThickness = new Thickness(1);
            return b;
        }

        private Border PayTile()
        {
            StackPanel sp = new StackPanel();
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.VerticalAlignment = VerticalAlignment.Center;
            payLabel = new TextBlock { Text = "发薪倒计时", FontFamily = Yahei, FontSize = 20, Foreground = Hex("#9AA3AB"), TextAlignment = TextAlignment.Center };
            payValue = new TextBlock { Text = "--", FontFamily = Yahei, FontSize = 38, Foreground = Hex("#F0C674"), TextAlignment = TextAlignment.Center };
            payValue.Margin = new Thickness(0, 10, 0, 0);
            payDaily = new TextBlock { Text = "", FontFamily = Yahei, FontSize = 21, Foreground = Hex("#C9CDD1"), TextAlignment = TextAlignment.Center };
            payDaily.Margin = new Thickness(0, 12, 0, 0);
            payToday = new TextBlock { Text = "", FontFamily = Yahei, FontSize = 21, Foreground = Hex("#8FBF9F"), TextAlignment = TextAlignment.Center };
            payToday.Margin = new Thickness(0, 4, 0, 0);
            customCountdown = new TextBlock { Text = "", FontFamily = Yahei, FontSize = 19, Foreground = Hex("#F0C674"), TextAlignment = TextAlignment.Center };
            customCountdown.Margin = new Thickness(0, 6, 0, 0);
            rentLine = new TextBlock { Text = "", FontFamily = Yahei, FontSize = 19, Foreground = Hex("#F0A85A"), TextAlignment = TextAlignment.Center };
            rentLine.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(payLabel);
            sp.Children.Add(payValue);
            sp.Children.Add(payDaily);
            sp.Children.Add(payToday);
            sp.Children.Add(customCountdown);
            sp.Children.Add(rentLine);
            Border b = TileShell();
            b.Child = sp;
            return b;
        }

        private Border InfoTile()
        {
            StackPanel sp = new StackPanel();
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.VerticalAlignment = VerticalAlignment.Center;
            dateLine = Line();
            restLine = Line();
            upLine = Line();
            cpuLine = Line();
            gpuLine = Line();
            memLine = Line();
            netLine = Line();
            sp.Children.Add(dateLine);
            sp.Children.Add(restLine);
            sp.Children.Add(upLine);
            sp.Children.Add(cpuLine);
            sp.Children.Add(gpuLine);
            sp.Children.Add(memLine);
            sp.Children.Add(netLine);
            Border b = TileShell();
            b.Width = 288;
            b.Margin = new Thickness(0);
            b.Child = sp;
            return b;
        }

        private TextBlock Line()
        {
            TextBlock tb = new TextBlock();
            tb.FontFamily = Yahei;
            tb.FontSize = 19;
            tb.Foreground = Hex("#D9D9D9");
            tb.TextAlignment = TextAlignment.Center;
            tb.Margin = new Thickness(0, 3, 0, 3);
            return tb;
        }

        private void RefreshTime()
        {
            DateTime now = DateTime.Now;
            hh.Text = now.Hour.ToString("00");
            mm.Text = now.Minute.ToString("00");
            ss.Text = now.Second.ToString("00");
            RefreshPay();
        }

        private void RefreshPay()
        {
            DateTime now = DateTime.Now;
            int day = Math.Min(Config.PayDay, DateTime.DaysInMonth(now.Year, now.Month));
            DateTime target = new DateTime(now.Year, now.Month, day, Config.PayHour, Config.PayMinute, 0);
            if (target <= now)
            {
                DateTime nm = now.AddMonths(1);
                day = Math.Min(Config.PayDay, DateTime.DaysInMonth(nm.Year, nm.Month));
                target = new DateTime(nm.Year, nm.Month, day, Config.PayHour, Config.PayMinute, 0);
            }
            TimeSpan left = target - now;
            string clock = left.Hours.ToString("00") + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
            if (left.Days > 0)
            {
                payLabel.Text = "发薪倒计时";
                payValue.Text = left.Days + "天 " + clock;
            }
            else
            {
                payLabel.Text = "今天发薪";
                payValue.Text = clock;
            }
            if (Config.PayAmount > 0)
            {
                int workdays = HolidayCalendar.WorkdayCountInMonth(now.Year, now.Month);
                double averageMonthly = Config.PayAmount * Config.PayMonths / 12.0;
                dailyEarn = workdays > 0 ? averageMonthly / workdays : 0;
                payDaily.Text = "日薪 ¥" + dailyEarn.ToString("0.00");
                if (!HolidayCalendar.IsWorkday(now))
                {
                    payToday.Text = "今日不计薪";
                }
                else
                {
                    double progress = WorkDayProgress(now);
                    payToday.Text = "今日已赚 ¥" + (dailyEarn * progress).ToString("0.00");
                }
            }
            else
            {
                dailyEarn = 0;
                payDaily.Text = "未设置薪资";
                payToday.Text = "";
            }
            RefreshRent();
        }

        private void RefreshRent()
        {
            if (rentLine == null) return;
            if (Config.RentDay <= 0)
            {
                rentLine.Text = "";
                return;
            }
            DateTime today = DateTime.Today;
            int day = Math.Min(Config.RentDay, DateTime.DaysInMonth(today.Year, today.Month));
            DateTime target = new DateTime(today.Year, today.Month, day);
            if (target < today)
            {
                DateTime next = today.AddMonths(1);
                day = Math.Min(Config.RentDay, DateTime.DaysInMonth(next.Year, next.Month));
                target = new DateTime(next.Year, next.Month, day);
            }
            int days = (target - today).Days;
            rentLine.Text = days == 0 ? "今天交房租" : "距交房租 " + days + "天";
        }

        private static double WorkDayProgress(DateTime now)
        {
            int start = Config.WorkStartHour * 60 + Config.WorkStartMinute;
            int end = Config.WorkEndHour * 60 + Config.WorkEndMinute;
            if (end <= start) return 0;
            double elapsed = now.TimeOfDay.TotalMinutes - start;
            if (elapsed <= 0) return 0;
            if (elapsed >= end - start) return 1;
            return elapsed / (end - start);
        }

        private static string FormatMetric(int value)
        {
            return value == int.MinValue ? "--" : value + "%";
        }

        private static string FormatTemperature(int value)
        {
            return value == int.MinValue ? " --" : " " + value + "°C";
        }

        private void RefreshSys()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    double u, d;
                    SysInfo.NetRates(out u, out d);
                    upRate = u; downRate = d;
                    int cpu = SysInfo.CpuPercent();
                    int cpuTemp = SysInfo.CpuTemperature();
                    int gpuUsage, gpuTemp;
                    SysInfo.GpuStats(out gpuUsage, out gpuTemp);
                    int mem = SysInfo.MemPercent();
                    TimeSpan up = SysInfo.Uptime();
                    Dispatcher.BeginInvoke((Action)delegate
                    {
                    DateTime now = DateTime.Now;
                    dateLine.Text = now.ToString("yyyy年M月d日 ") + WeekName(now);
                    restLine.Text = RestCountdown(now);
                    RefreshCountdown();
                    upLine.Text = "开机:" + (int)up.TotalHours + "时" + up.Minutes + "分" + up.Seconds + "秒";
                        cpuLine.Text = "CPU:" + cpu + "%" + FormatTemperature(cpuTemp);
                        gpuLine.Text = "GPU:" + FormatMetric(gpuUsage) + FormatTemperature(gpuTemp);
                        memLine.Text = "内存:" + mem + " %";
                        netLine.Text = "↑:" + SysInfo.FmtRate(upRate) + " ↓:" + SysInfo.FmtRate(downRate);
                    });
                }
                catch (Exception ex)
                {
                    SysInfo.Log("sys error: " + ex.ToString());
                }
            });
        }

        private void RefreshCountdown()
        {
            if (customCountdown == null) return;
            CountdownOccurrence occurrence = CountdownCalendar.FindNearest(DateTime.Now);
            if (occurrence == null)
            {
                customCountdown.Text = "";
                return;
            }
            if (occurrence.Days == 0) customCountdown.Text = "今天是" + occurrence.Item.Name;
            else customCountdown.Text = "距" + occurrence.Item.Name + " " + occurrence.Days + "天";
        }

        private static string WeekName(DateTime d)
        {
            string[] names = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            return names[((int)d.DayOfWeek + 6) % 7];
        }

        private static string RestCountdown(DateTime today)
        {
            return HolidayCalendar.Countdown(today);
        }
    }

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid service, ref Guid riid, out IntPtr ppvObject);
    }

    [ComImport, Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IApplicationViewCollection
    {
        [PreserveSig]
        int GetViews(out IntPtr views);

        [PreserveSig]
        int GetViewsByZOrder(out IntPtr views);

        [PreserveSig]
        int GetViewsByAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, out IntPtr views);

        [PreserveSig]
        int GetViewForHwnd(IntPtr hwnd, out IntPtr view);
    }

    [ComImport, Guid("4CE81583-1E4C-4632-A621-07A53543148F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopPinnedApps
    {
        [PreserveSig]
        int IsViewPinned(IntPtr view, out int pinned);

        [PreserveSig]
        int PinView(IntPtr view);

        [PreserveSig]
        int UnpinView(IntPtr view);
    }

    [ComImport, Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopInternal
    {
        [PreserveSig]
        int IsViewVisible(IntPtr view, out int visible);

        [PreserveSig]
        int GetID(out Guid desktopId);
    }

    [ComImport, Guid("53F5CA0B-158F-4124-900C-057158060B27"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopManagerInternal
    {
        [PreserveSig]
        int GetCount(out int count);

        [PreserveSig]
        int MoveViewToDesktop(IntPtr view, IntPtr desktop);

        [PreserveSig]
        int CanViewMoveDesktops(IntPtr view, out int canMove);

        [PreserveSig]
        int GetCurrentDesktop(out IntPtr desktop);

        [PreserveSig]
        int GetDesktops(out IntPtr desktops);

        [PreserveSig]
        int GetAdjacentDesktop(IntPtr desktopReference, int direction, out IntPtr adjacentDesktop);

        [PreserveSig]
        int SwitchDesktop(IntPtr desktop);

        [PreserveSig]
        int SwitchDesktopAndMoveForegroundView(IntPtr desktop);

        [PreserveSig]
        int CreateDesktop(out IntPtr desktop);

        [PreserveSig]
        int MoveDesktop(IntPtr desktop, int nIndex);

        [PreserveSig]
        int RemoveDesktop(IntPtr desktop, IntPtr fallback);

        [PreserveSig]
        int FindDesktop(ref Guid desktopId, out IntPtr desktop);
    }

    internal static class VirtualDesktopMover
    {
        private static readonly Guid ImmersiveShellClsid = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        private static readonly Guid ManagerInternalClsid = new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
        private static readonly Guid ViewCollectionIid = new Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5");
        private static readonly Guid ManagerInternalIid = new Guid("53F5CA0B-158F-4124-900C-057158060B27");

        public static bool TryMoveToCurrent(IntPtr hwnd, out bool moved, out string message)
        {
            moved = false;
            string step = "create shell";
            object shell = null;
            object collectionObject = null;
            object managerObject = null;
            IntPtr view = IntPtr.Zero;
            IntPtr currentDesktop = IntPtr.Zero;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromCLSID(ImmersiveShellClsid));
                IServiceProvider serviceProvider = (IServiceProvider)shell;
                step = "query view collection";
                collectionObject = QueryService(serviceProvider, ViewCollectionIid, ViewCollectionIid);
                step = "query desktop manager";
                managerObject = QueryService(serviceProvider, ManagerInternalClsid, ManagerInternalIid);

                step = "cast view collection";
                IApplicationViewCollection collection = (IApplicationViewCollection)collectionObject;
                step = "get view for hwnd";
                int hr = collection.GetViewForHwnd(hwnd, out view);
                if (hr < 0 || view == IntPtr.Zero)
                {
                    message = "GetViewForHwnd hr=0x" + hr.ToString("X8");
                    return false;
                }

                step = "cast desktop manager";
                IVirtualDesktopManagerInternal manager = (IVirtualDesktopManagerInternal)managerObject;
                step = "get current desktop";
                int hrCurrent = manager.GetCurrentDesktop(out currentDesktop);
                if (hrCurrent < 0 || currentDesktop == IntPtr.Zero)
                {
                    message = "GetCurrentDesktop hr=0x" + hrCurrent.ToString("X8");
                    return false;
                }

                step = "move view to desktop";
                int hrMove = manager.MoveViewToDesktop(view, currentDesktop);
                if (hrMove < 0)
                {
                    message = "MoveViewToDesktop hr=0x" + hrMove.ToString("X8");
                    return false;
                }

                moved = true;
                message = "moved to current desktop";
                return true;
            }
            catch (Exception ex)
            {
                message = step + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (view != IntPtr.Zero) Marshal.Release(view);
                if (currentDesktop != IntPtr.Zero) Marshal.Release(currentDesktop);
                if (managerObject != null) Marshal.ReleaseComObject(managerObject);
                if (collectionObject != null) Marshal.ReleaseComObject(collectionObject);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        private static object QueryService(IServiceProvider serviceProvider, Guid service, Guid iid)
        {
            IntPtr ptr = IntPtr.Zero;
            int hr = serviceProvider.QueryService(ref service, ref iid, out ptr);
            if (hr < 0 || ptr == IntPtr.Zero) throw Marshal.GetExceptionForHR(hr);
            try
            {
                return Marshal.GetObjectForIUnknown(ptr);
            }
            finally
            {
                Marshal.Release(ptr);
            }
        }
    }

    internal static class VirtualDesktopPin
    {
        private static readonly Guid ImmersiveShellClsid = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        private static readonly Guid PinnedAppsClsid = new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
        private static readonly Guid PinnedAppsIid = new Guid("4CE81583-1E4C-4632-A621-07A53543148F");
        private static readonly Guid ViewCollectionIid = new Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5");

        public static bool TryPin(IntPtr hwnd, out string message)
        {
            object shell = null;
            object collectionObject = null;
            object pinnedObject = null;
            IntPtr view = IntPtr.Zero;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromCLSID(ImmersiveShellClsid));
                IServiceProvider serviceProvider = (IServiceProvider)shell;
                collectionObject = QueryService(serviceProvider, ViewCollectionIid, ViewCollectionIid);
                pinnedObject = QueryService(serviceProvider, PinnedAppsClsid, PinnedAppsIid);

                IApplicationViewCollection collection = (IApplicationViewCollection)collectionObject;
                int hr = collection.GetViewForHwnd(hwnd, out view);
                if (hr < 0 || view == IntPtr.Zero)
                {
                    message = "GetViewForHwnd hr=0x" + hr.ToString("X8");
                    return false;
                }

                IVirtualDesktopPinnedApps pinnedApps = (IVirtualDesktopPinnedApps)pinnedObject;
                int pinned;
                hr = pinnedApps.IsViewPinned(view, out pinned);
                if (hr < 0)
                {
                    message = "IsViewPinned hr=0x" + hr.ToString("X8");
                    return false;
                }
                if (pinned == 0)
                {
                    hr = pinnedApps.PinView(view);
                    if (hr < 0)
                    {
                        message = "PinView hr=0x" + hr.ToString("X8");
                        return false;
                    }
                }

                message = pinned == 0 ? "pinned now" : "already pinned";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
            finally
            {
                if (view != IntPtr.Zero) Marshal.Release(view);
                if (pinnedObject != null) Marshal.ReleaseComObject(pinnedObject);
                if (collectionObject != null) Marshal.ReleaseComObject(collectionObject);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        private static object QueryService(IServiceProvider serviceProvider, Guid service, Guid iid)
        {
            IntPtr ptr = IntPtr.Zero;
            int hr = serviceProvider.QueryService(ref service, ref iid, out ptr);
            if (hr < 0 || ptr == IntPtr.Zero) throw Marshal.GetExceptionForHR(hr);
            try
            {
                return Marshal.GetObjectForIUnknown(ptr);
            }
            finally
            {
                Marshal.Release(ptr);
            }
        }
    }

    [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, [In] ref Guid desktopId);
    }

    [ComImport, Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")]
    internal class VirtualDesktopManagerClass
    {
    }

    internal sealed class VirtualDesktopFollower
    {
        private readonly IntPtr _hwnd;
        private readonly IVirtualDesktopManager _manager;
        private DispatcherTimer _timer;
        private bool _failedLogged;
        private bool _movedLogged;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        private VirtualDesktopFollower(IntPtr hwnd)
        {
            _hwnd = hwnd;
            _manager = (IVirtualDesktopManager)new VirtualDesktopManagerClass();
        }

        public static VirtualDesktopFollower Start(IntPtr hwnd)
        {
            try
            {
                VirtualDesktopFollower follower = new VirtualDesktopFollower(hwnd);
                follower._timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                follower._timer.Tick += delegate { follower.Refresh(); };
                follower._timer.Start();
                follower.Refresh();
                SysInfo.Log("virtual desktop follower started");
                return follower;
            }
            catch (Exception ex)
            {
                SysInfo.Log("virtual desktop follower unavailable: " + ex.Message);
                return null;
            }
        }

        public void Refresh()
        {
            try
            {
                bool moved;
                string message;
                if (!VirtualDesktopMover.TryMoveToCurrent(_hwnd, out moved, out message))
                {
                    LogFailure(message);
                    return;
                }
                if (!moved) return;

                if (!_movedLogged)
                {
                    _movedLogged = true;
                    SysInfo.Log("virtual desktop moved: " + message);
                }
            }
            catch (Exception ex)
            {
                LogFailure(ex.Message);
            }
        }

        private void LogFailure(string message)
        {
            if (_failedLogged) return;
            _failedLogged = true;
            SysInfo.Log("virtual desktop follower error: " + message);
        }
    }

    internal static class DesktopLayer
    {
        private delegate bool EnumProc(IntPtr h, IntPtr l);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc cb, IntPtr l);

        [DllImport("user32.dll")]
        private static extern bool GetClassName(IntPtr h, System.Text.StringBuilder s, int n);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr child, string cls, string title);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr h);

        [DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);

        public static void ApplyRoundRegion(IntPtr h, int width, int height, int radius)
        {
            IntPtr rgn = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius, radius);
            SetWindowRgn(h, rgn, true);
        }

        private static string Cls(IntPtr h)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
            GetClassName(h, sb, 64);
            return sb.ToString();
        }

        // The wallpaper/icons layer (Progman or the WorkerW behind it) is shown on
        // every virtual desktop, so parenting the clock into it makes it a desktop widget.
        public static IntPtr FindHost()
        {
            IntPtr progman = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (Cls(h) == "Progman") { progman = h; return false; }
                return true;
            }, IntPtr.Zero);
            if (progman == IntPtr.Zero) return IntPtr.Zero;
            IntPtr view = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (view != IntPtr.Zero) return GetParent(view);
            IntPtr worker = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (Cls(h) == "WorkerW" && FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                {
                    worker = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return worker;
        }
    }

    public static class Program
    {
        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name + ".dll";
            if (name != "LibreHardwareMonitorLib.dll" && name != "HidSharp.dll") return null;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return Assembly.Load(data);
            }
        }

        [STAThread]
        public static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
            bool created;
            System.Threading.Mutex mutex = new System.Threading.Mutex(true, "DeskClockPlus.SingleInstance", out created);
            if (!created) return;
            Application app = new Application();
            app.Run(new ClockWindow());
            GC.KeepAlive(mutex);
        }
    }
}
