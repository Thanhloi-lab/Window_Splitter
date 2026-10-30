using System.Runtime.InteropServices;
using WindowsSplitter.Models;

namespace WindowsSplitter.Core
{
    public class ZoneEngine
    {
        private readonly AppSettings _settings;
        private readonly Dictionary<IntPtr, int> _windowCurrentZoneMap = new();
        private readonly Dictionary<IntPtr, NativeMethods.RECT> _windowOriginalBoundsMap = new();

        public event Action<IntPtr, Zone, NativeMethods.RECT>? WindowSnapped;

        public ZoneEngine(AppSettings settings)
        {
            _settings = settings;
        }

        public Zone? GetWindowCurrentZone(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            var currentMonitor = MonitorManager.Instance.GetMonitorForWindow(hWnd);
            var preset = _settings.GetPresetForMonitor(currentMonitor);
            if (preset == null || preset.Zones.Count == 0) return null;

            if (_windowCurrentZoneMap.TryGetValue(hWnd, out int zoneId))
            {
                var found = preset.Zones.FirstOrDefault(z => z.Id == zoneId);
                if (found != null) return found;
            }

            // Detect by screen coordinates
            if (NativeMethods.GetWindowRect(hWnd, out var winRect))
            {
                int relX = winRect.Left - currentMonitor.Bounds.Left;
                int relW = winRect.Width;

                Zone? bestZone = null;
                int maxOverlap = 0;

                foreach (var z in preset.Zones)
                {
                    int overlapLeft = Math.Max(relX, z.X);
                    int overlapRight = Math.Min(relX + relW, z.X + z.Width);
                    int overlap = Math.Max(0, overlapRight - overlapLeft);

                    if (overlap > maxOverlap)
                    {
                        maxOverlap = overlap;
                        bestZone = z;
                    }
                }

                if (bestZone != null && maxOverlap > 100)
                {
                    _windowCurrentZoneMap[hWnd] = bestZone.Id;
                    return bestZone;
                }
            }

            return null;
        }

        public void SetWindowCurrentZone(IntPtr hWnd, int zoneId)
        {
            if (hWnd != IntPtr.Zero)
            {
                _windowCurrentZoneMap[hWnd] = zoneId;
            }
        }

        public bool SnapWindow(IntPtr hWnd, KeyDirection direction)
        {
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hWnd))
                return false;

            // Don't snap special system windows (desktop, shell, taskbar)
            if (hWnd == NativeMethods.GetDesktopWindow() || hWnd == NativeMethods.GetShellWindow())
                return false;

            string className = NativeMethods.GetWindowClassName(hWnd);
            if (className.Contains("Shell_TrayWnd") || className.Contains("Progman") || className.Contains("WorkerW"))
                return false;

            // Handle KeyDirection.Down: Minimize window or restore original position
            if (direction == KeyDirection.Down)
            {
                if (NativeMethods.IsZoomed(hWnd))
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                    return true;
                }

                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
                return true;
            }

            // Handle KeyDirection.Up: Maximize window to fill monitor, or restore
            if (direction == KeyDirection.Up)
            {
                if (NativeMethods.IsZoomed(hWnd))
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                }
                else
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MAXIMIZE);
                }
                return true;
            }

            // Restore if maximized before moving to a zone
            if (NativeMethods.IsZoomed(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }

            // Save original bounds if not already saved
            if (!_windowOriginalBoundsMap.ContainsKey(hWnd))
            {
                if (NativeMethods.GetWindowRect(hWnd, out var origRect))
                {
                    _windowOriginalBoundsMap[hWnd] = origRect;
                }
            }

            // Identify current monitor and preset tailored for this monitor
            var currentMonitor = MonitorManager.Instance.GetMonitorForWindow(hWnd);
            var preset = _settings.GetPresetForMonitor(currentMonitor);
            if (preset == null || preset.Zones.Count == 0)
                return false;

            int targetZoneIndex = 0;
            if (_windowCurrentZoneMap.TryGetValue(hWnd, out int currentZoneId))
            {
                int curIdx = preset.Zones.FindIndex(z => z.Id == currentZoneId);
                if (curIdx >= 0)
                {
                    if (direction == KeyDirection.Left)
                    {
                        if (curIdx == 0 && _settings.EnableCrossMonitorWrap && MonitorManager.Instance.Monitors.Count > 1)
                        {
                            // Wrap to the monitor to the left
                            var nextMon = MonitorManager.Instance.GetNextMonitor(currentMonitor, KeyDirection.Left);
                            if (nextMon.DeviceName != currentMonitor.DeviceName)
                            {
                                currentMonitor = nextMon;
                                preset = _settings.GetPresetForMonitor(currentMonitor);
                                targetZoneIndex = Math.Max(0, preset.Zones.Count - 1);
                            }
                            else
                            {
                                targetZoneIndex = 0;
                            }
                        }
                        else
                        {
                            targetZoneIndex = Math.Max(0, curIdx - 1);
                        }
                    }
                    else if (direction == KeyDirection.Right)
                    {
                        if (curIdx == preset.Zones.Count - 1 && _settings.EnableCrossMonitorWrap && MonitorManager.Instance.Monitors.Count > 1)
                        {
                            // Wrap to the monitor to the right
                            var nextMon = MonitorManager.Instance.GetNextMonitor(currentMonitor, KeyDirection.Right);
                            if (nextMon.DeviceName != currentMonitor.DeviceName)
                            {
                                currentMonitor = nextMon;
                                preset = _settings.GetPresetForMonitor(currentMonitor);
                                targetZoneIndex = 0;
                            }
                            else
                            {
                                targetZoneIndex = preset.Zones.Count - 1;
                            }
                        }
                        else
                        {
                            targetZoneIndex = Math.Min(preset.Zones.Count - 1, curIdx + 1);
                        }
                    }
                }
            }
            else
            {
                // First time on this monitor
                if (direction == KeyDirection.Left)
                {
                    targetZoneIndex = 0;
                }
                else if (direction == KeyDirection.Right)
                {
                    targetZoneIndex = Math.Min(1, preset.Zones.Count - 1);
                }
            }

            var targetZone = preset.Zones[targetZoneIndex];
            _windowCurrentZoneMap[hWnd] = targetZone.Id;

            ApplyWindowToZone(hWnd, targetZone, currentMonitor);
            return true;
        }

        public bool MoveWindowToMonitor(IntPtr hWnd, KeyDirection direction)
        {
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hWnd))
                return false;

            if (MonitorManager.Instance.Monitors.Count <= 1)
                return false;

            if (direction != KeyDirection.Left && direction != KeyDirection.Right)
                return false;

            var currentMonitor = MonitorManager.Instance.GetMonitorForWindow(hWnd);
            var targetMonitor = MonitorManager.Instance.GetNextMonitor(currentMonitor, direction);

            if (targetMonitor.DeviceName == currentMonitor.DeviceName)
                return false;

            var targetPreset = _settings.GetPresetForMonitor(targetMonitor);
            if (targetPreset == null || targetPreset.Zones.Count == 0)
                return false;

            // Preserve current zone index or land on corresponding zone
            int targetZoneIndex = 0;
            if (_windowCurrentZoneMap.TryGetValue(hWnd, out int currentZoneId))
            {
                var curPreset = _settings.GetPresetForMonitor(currentMonitor);
                int curIdx = curPreset.Zones.FindIndex(z => z.Id == currentZoneId);
                if (curIdx >= 0)
                {
                    targetZoneIndex = Math.Min(curIdx, targetPreset.Zones.Count - 1);
                }
            }

            var targetZone = targetPreset.Zones[targetZoneIndex];
            _windowCurrentZoneMap[hWnd] = targetZone.Id;

            ApplyWindowToZone(hWnd, targetZone, targetMonitor);
            return true;
        }

        public void ApplyWindowToZone(IntPtr hWnd, Zone targetZone, MonitorInfoItem monitor)
        {
            int monLeft = monitor.WorkArea.Left;
            int monTop = monitor.WorkArea.Top;

            // Target visual rectangle
            int targetX = monLeft + targetZone.X;
            int targetY = monTop + targetZone.Y;
            int targetWidth = targetZone.Width;
            int targetHeight = Math.Min(targetZone.Height, monitor.WorkArea.Height);

            // Clamp if zone definition exceeds this monitor's work area
            if (targetWidth > monitor.WorkArea.Width) targetWidth = monitor.WorkArea.Width;
            if (targetHeight > monitor.WorkArea.Height) targetHeight = monitor.WorkArea.Height;

            // Invisible Border Compensation
            int actualX = targetX;
            int actualY = targetY;
            int actualW = targetWidth;
            int actualH = targetHeight;

            if (_settings.DwmInvisibleBorderCompensation)
            {
                int leftOffset = 0;
                int topOffset = 0;
                int rightOffset = 0;
                int bottomOffset = 0;

                int hr = NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out NativeMethods.RECT frameBounds, Marshal.SizeOf<NativeMethods.RECT>());
                if (hr == 0 && NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT windowRect))
                {
                    leftOffset = frameBounds.Left - windowRect.Left;
                    rightOffset = windowRect.Right - frameBounds.Right;
                    topOffset = frameBounds.Top - windowRect.Top;
                    bottomOffset = windowRect.Bottom - frameBounds.Bottom;
                }

                // If DWM values were 0 or negative, fall back to standard compensation
                if (leftOffset <= 0 && rightOffset <= 0)
                {
                    leftOffset = _settings.BorderCompensationPixels;
                    rightOffset = _settings.BorderCompensationPixels;
                    bottomOffset = _settings.BorderCompensationPixels;
                    topOffset = 0;
                }

                actualX = targetX - leftOffset;
                actualY = targetY - topOffset;
                actualW = targetWidth + leftOffset + rightOffset;
                actualH = targetHeight + topOffset + bottomOffset;
            }

            // Disable Windows 11 rounded corners if configured
            if (_settings.DisableWin11RoundedCorners)
            {
                int pref = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
                NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }

            // Reposition window
            NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_TOP,
                actualX,
                actualY,
                actualW,
                actualH,
                NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE
            );

            var visualRect = new NativeMethods.RECT
            {
                Left = targetX,
                Top = targetY,
                Right = targetX + targetWidth,
                Bottom = targetY + targetHeight
            };

            WindowSnapped?.Invoke(hWnd, targetZone, visualRect);
        }

        public void ResetWindowCorner(IntPtr hWnd)
        {
            int pref = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT;
            NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }

        public void RestoreWindowBounds(IntPtr hWnd)
        {
            if (_windowOriginalBoundsMap.TryGetValue(hWnd, out var origRect))
            {
                NativeMethods.SetWindowPos(
                    hWnd,
                    NativeMethods.HWND_TOP,
                    origRect.Left,
                    origRect.Top,
                    origRect.Width,
                    origRect.Height,
                    NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW
                );
                ResetWindowCorner(hWnd);
                _windowCurrentZoneMap.Remove(hWnd);
            }
        }
    }
}
