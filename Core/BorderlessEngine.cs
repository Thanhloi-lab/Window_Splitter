using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WindowsSplitter.Models;
using WindowsSplitter.UI;

namespace WindowsSplitter.Core
{
    public class WindowStateBackup
    {
        public long OriginalStyle { get; set; }
        public long OriginalExStyle { get; set; }
        public NativeMethods.RECT OriginalRect { get; set; }
        public bool IsBorderless { get; set; }
        public FloatingCaptionBar? CaptionBar { get; set; }
    }

    public class BorderlessEngine
    {
        private readonly AppSettings _settings;
        private readonly Dictionary<IntPtr, WindowStateBackup> _managedWindows = new();

        public event Action<IntPtr, bool>? BorderlessStateChanged;

        public BorderlessEngine(AppSettings settings)
        {
            _settings = settings;
        }

        public bool IsBorderless(IntPtr hWnd)
        {
            return _managedWindows.TryGetValue(hWnd, out var state) && state.IsBorderless;
        }

        public BorderlessAppRule? FindRuleForWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            string exePath = NativeMethods.GetProcessExecutablePath(hWnd);
            string exeName = Path.GetFileName(exePath);
            string title = NativeMethods.GetWindowTitle(hWnd);

            foreach (var rule in _settings.BorderlessWhitelist)
            {
                bool exeMatch = !string.IsNullOrEmpty(rule.ProcessName) &&
                               (string.Equals(rule.ProcessName, exeName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(rule.ProcessName, exePath, StringComparison.OrdinalIgnoreCase));

                bool titleMatch = string.IsNullOrEmpty(rule.WindowTitlePattern) ||
                                 title.Contains(rule.WindowTitlePattern, StringComparison.OrdinalIgnoreCase);

                if (exeMatch && titleMatch)
                {
                    return rule;
                }
            }

            return null;
        }

        public bool ApplyBorderless(IntPtr hWnd, BorderlessAppRule? rule = null, Zone? targetZone = null)
        {
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hWnd))
                return false;

            // Restore if maximized
            if (NativeMethods.IsZoomed(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }

            // Save original state if not yet recorded
            if (!_managedWindows.TryGetValue(hWnd, out var state))
            {
                long origStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE).ToInt64();
                long origExStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
                NativeMethods.GetWindowRect(hWnd, out var origRect);

                state = new WindowStateBackup
                {
                    OriginalStyle = origStyle,
                    OriginalExStyle = origExStyle,
                    OriginalRect = origRect,
                    IsBorderless = false
                };
                _managedWindows[hWnd] = state;
            }

            // If rule was not specified, try finding one from whitelist
            if (rule == null)
            {
                rule = FindRuleForWindow(hWnd);
            }

            // Determine target monitor (honoring rule's TargetMonitorDevice if specified)
            MonitorInfoItem targetMonitor;
            if (rule != null && !string.IsNullOrEmpty(rule.TargetMonitorDevice))
            {
                if (rule.TargetMonitorDevice.Equals("primary", StringComparison.OrdinalIgnoreCase))
                {
                    targetMonitor = MonitorManager.Instance.GetPrimaryMonitor() ?? MonitorManager.Instance.GetMonitorForWindow(hWnd);
                }
                else
                {
                    targetMonitor = MonitorManager.Instance.GetMonitorByDevice(rule.TargetMonitorDevice) ?? MonitorManager.Instance.GetMonitorForWindow(hWnd);
                }
            }
            else
            {
                targetMonitor = MonitorManager.Instance.GetMonitorForWindow(hWnd);
            }

            int monLeft = targetMonitor.Bounds.Left;
            int monTop = targetMonitor.Bounds.Top;

            // Strip caption, sizing borders and frame
            long curStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE).ToInt64();
            long newStyle = curStyle;
            newStyle &= ~(NativeMethods.WS_CAPTION |
                          NativeMethods.WS_THICKFRAME |
                          NativeMethods.WS_MINIMIZEBOX |
                          NativeMethods.WS_MAXIMIZEBOX |
                          NativeMethods.WS_SYSMENU |
                          NativeMethods.WS_BORDER |
                          NativeMethods.WS_DLGFRAME);
            newStyle |= NativeMethods.WS_POPUP | NativeMethods.WS_VISIBLE;

            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE, new IntPtr(newStyle));

            // Windows 11 corner preference: no round corners
            if (rule?.RemoveRoundedCorners ?? _settings.DisableWin11RoundedCorners)
            {
                int pref = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
                NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }

            // Determine target position & dimensions
            int targetX, targetY, targetW, targetH;

            // Retrieve preset for target monitor
            var preset = _settings.GetPresetForMonitor(targetMonitor);
            if (targetZone == null && rule != null && rule.TargetZoneId > 0 && preset != null)
            {
                targetZone = preset.Zones.FirstOrDefault(z => z.Id == rule.TargetZoneId);
            }

            if (targetZone != null)
            {
                targetX = monLeft + targetZone.X;
                targetY = monTop + targetZone.Y;
                targetW = targetZone.Width;
                targetH = (rule?.CoverTaskbar ?? true)
                    ? targetZone.Height
                    : Math.Min(targetZone.Height, targetMonitor.WorkArea.Height);
            }
            else
            {
                // Fullscreen borderless
                targetX = monLeft;
                targetY = monTop;
                targetW = targetMonitor.Bounds.Width;
                targetH = (rule?.CoverTaskbar ?? true) ? targetMonitor.Bounds.Height : targetMonitor.WorkArea.Height;
            }

            NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_TOP,
                targetX,
                targetY,
                targetW,
                targetH,
                NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW
            );

            state.IsBorderless = true;

            // Manage Floating Caption Bar
            bool showCaption = rule?.ShowFloatingCaption ?? true;
            if (showCaption)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (state.CaptionBar == null)
                    {
                        state.CaptionBar = new FloatingCaptionBar(hWnd, (targetHwnd) =>
                        {
                            ToggleBorderless(targetHwnd);
                        });
                    }

                    state.CaptionBar.UpdatePosition(targetX + targetW, targetY, targetMonitor.DpiScaleX, targetMonitor.DpiScaleY);
                    state.CaptionBar.Show();
                });
            }
            else if (state.CaptionBar != null)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    state.CaptionBar.Close();
                    state.CaptionBar = null;
                });
            }

            BorderlessStateChanged?.Invoke(hWnd, true);
            return true;
        }

        public bool RestoreWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !_managedWindows.TryGetValue(hWnd, out var state))
                return false;

            // Restore original styles
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE, new IntPtr(state.OriginalStyle));
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, new IntPtr(state.OriginalExStyle));

            // Restore Windows 11 default corners
            int pref = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT;
            NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));

            // Restore original position
            NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_TOP,
                state.OriginalRect.Left,
                state.OriginalRect.Top,
                state.OriginalRect.Width,
                state.OriginalRect.Height,
                NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW
            );

            // Close floating caption bar if active
            if (state.CaptionBar != null)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    state.CaptionBar.Close();
                    state.CaptionBar = null;
                });
            }

            state.IsBorderless = false;
            BorderlessStateChanged?.Invoke(hWnd, false);
            return true;
        }

        public void ToggleBorderless(IntPtr hWnd)
        {
            if (IsBorderless(hWnd))
            {
                RestoreWindow(hWnd);
            }
            else
            {
                ApplyBorderless(hWnd);
            }
        }

        public void CleanUp()
        {
            foreach (var kvp in _managedWindows.ToList())
            {
                if (kvp.Value.IsBorderless)
                {
                    RestoreWindow(kvp.Key);
                }
            }
            _managedWindows.Clear();
        }
    }
}
