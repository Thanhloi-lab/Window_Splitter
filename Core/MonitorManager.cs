using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace WindowsSplitter.Core
{
    public class MonitorInfoItem
    {
        public IntPtr Handle { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public NativeMethods.RECT Bounds { get; set; }
        public NativeMethods.RECT WorkArea { get; set; }
        public bool IsPrimary { get; set; }
        public double DpiScaleX { get; set; } = 1.0;
        public double DpiScaleY { get; set; } = 1.0;

        public int Width => Bounds.Width;
        public int Height => Bounds.Height;
        public bool IsUltrawide => Height > 0 && ((double)Width / Height) >= 2.0;
        public bool IsVertical => Height > Width;

        public override string ToString() => FriendlyName;
    }

    public class MonitorManager
    {
        private static MonitorManager? _instance;
        public static MonitorManager Instance => _instance ??= new MonitorManager();

        public List<MonitorInfoItem> Monitors { get; private set; } = new();
        public bool IsDuplicateMode { get; private set; } = false;

        public event Action? MonitorsChanged;

        public MonitorManager()
        {
            RefreshMonitors();
        }

        public void RefreshMonitors()
        {
            var list = new List<MonitorInfoItem>();
            int index = 1;

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
            {
                var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
                if (NativeMethods.GetMonitorInfo(hMon, ref mi))
                {
                    bool isPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;

                    // Query DPI scale for this monitor
                    double dpiX = 1.0;
                    double dpiY = 1.0;
                    try
                    {
                        if (NativeMethods.GetDpiForMonitor(hMon, 0, out uint px, out uint py) == 0)
                        {
                            dpiX = px / 96.0;
                            dpiY = py / 96.0;
                        }
                    }
                    catch
                    {
                        dpiX = 1.0;
                        dpiY = 1.0;
                    }

                    int w = mi.rcMonitor.Width;
                    int h = mi.rcMonitor.Height;
                    string tag = "";
                    if (isPrimary) tag += " [Chính]";
                    if (h > 0 && ((double)w / h) >= 2.0) tag += " (Ultrawide 21:9)";
                    else if (h > w) tag += " (Màn dọc)";
                    else tag += " (16:9)";

                    string friendly = $"🖥️ Màn hình {index}: {w} × {h}{tag}";

                    list.Add(new MonitorInfoItem
                    {
                        Handle = hMon,
                        DeviceName = mi.szDevice ?? $"DISPLAY{index}",
                        FriendlyName = friendly,
                        Bounds = mi.rcMonitor,
                        WorkArea = mi.rcWork,
                        IsPrimary = isPrimary,
                        DpiScaleX = dpiX,
                        DpiScaleY = dpiY
                    });

                    index++;
                }
                return true;
            }, IntPtr.Zero);

            // Sort monitors horizontally (left to right)
            list = list.OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top).ToList();

            // Detect duplicate mode: If all monitors share exact same bounds or count > 1 but desktop count is 1
            if (list.Count > 1)
            {
                var first = list[0].Bounds;
                bool allIdentical = list.All(m => m.Bounds.Left == first.Left && m.Bounds.Top == first.Top &&
                                                  m.Bounds.Width == first.Width && m.Bounds.Height == first.Height);
                IsDuplicateMode = allIdentical;
            }
            else
            {
                IsDuplicateMode = false;
            }

            Monitors = list;
            MonitorsChanged?.Invoke();
        }

        public MonitorInfoItem? GetPrimaryMonitor()
        {
            return Monitors.FirstOrDefault(m => m.IsPrimary) ?? Monitors.FirstOrDefault();
        }

        public MonitorInfoItem GetMonitorForWindow(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero)
            {
                IntPtr hMon = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                var match = Monitors.FirstOrDefault(m => m.Handle == hMon);
                if (match != null) return match;
            }
            return GetPrimaryMonitor() ?? new MonitorInfoItem();
        }

        public MonitorInfoItem? GetMonitorByDevice(string? deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return null;
            return Monitors.FirstOrDefault(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        }

        public MonitorInfoItem GetNextMonitor(MonitorInfoItem current, KeyDirection direction)
        {
            if (Monitors.Count <= 1) return current;

            // Ordered left to right
            int currentIndex = Monitors.IndexOf(current);
            if (currentIndex < 0)
            {
                currentIndex = Monitors.FindIndex(m => m.DeviceName == current.DeviceName);
            }
            if (currentIndex < 0) currentIndex = 0;

            if (direction == KeyDirection.Left)
            {
                // Go to monitor on left, or wrap to rightmost
                int nextIndex = currentIndex - 1;
                if (nextIndex < 0) nextIndex = Monitors.Count - 1;
                return Monitors[nextIndex];
            }
            else if (direction == KeyDirection.Right)
            {
                // Go to monitor on right, or wrap to leftmost
                int nextIndex = currentIndex + 1;
                if (nextIndex >= Monitors.Count) nextIndex = 0;
                return Monitors[nextIndex];
            }

            return current;
        }
    }
}
