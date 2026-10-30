using System.IO;
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
        public bool CoveredTaskbar { get; set; }
        public FloatingCaptionBar? CaptionBar { get; set; }
    }

    public class BorderlessEngine
    {
        private readonly AppSettings _settings;
        private ZoneEngine? _zoneEngine;
        private readonly Dictionary<IntPtr, WindowStateBackup> _managedWindows = new();

        public ZoneEngine? ZoneEngine
        {
            get => _zoneEngine;
            set => _zoneEngine = value;
        }

        public event Action<IntPtr, bool>? BorderlessStateChanged;

        public BorderlessEngine(AppSettings settings, ZoneEngine? zoneEngine = null)
        {
            _settings = settings;
            _zoneEngine = zoneEngine;
        }

        public bool IsBorderless(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            if (_managedWindows.TryGetValue(hWnd, out var state))
                return state.IsBorderless;

            // If not tracked in memory (e.g. app reopened), inspect OS window style directly
            if (NativeMethods.IsWindowVisible(hWnd))
            {
                long style = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE).ToInt64();
                if ((style & NativeMethods.WS_POPUP) != 0 && (style & NativeMethods.WS_CAPTION) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsCoveringTaskbar(IntPtr hWnd)
        {
            return _managedWindows.TryGetValue(hWnd, out var state) && state.IsBorderless && state.CoveredTaskbar;
        }

        public void BringAboveTaskbar(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero && NativeMethods.IsWindowVisible(hWnd))
            {
                NativeMethods.SetWindowPos(
                    hWnd,
                    NativeMethods.HWND_TOPMOST,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE
                );
            }
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

                // If window was captured while already borderless, reconstruct standard overlapped window style!
                if ((origStyle & NativeMethods.WS_CAPTION) == 0)
                {
                    origStyle &= ~NativeMethods.WS_POPUP;
                    origStyle |= NativeMethods.WS_OVERLAPPEDWINDOW | NativeMethods.WS_VISIBLE;
                }

                // If origRect is full screen, pick a reasonable centered window size
                var mon = MonitorManager.Instance.GetMonitorForWindow(hWnd);
                if (origRect.Width <= 0 || origRect.Height <= 0 || (origRect.Width >= mon.Bounds.Width && origRect.Height >= mon.Bounds.Height))
                {
                    int defaultW = (int)(mon.WorkArea.Width * 0.75);
                    int defaultH = (int)(mon.WorkArea.Height * 0.75);
                    int defaultX = mon.WorkArea.Left + (mon.WorkArea.Width - defaultW) / 2;
                    int defaultY = mon.WorkArea.Top + (mon.WorkArea.Height - defaultH) / 2;
                    origRect = new NativeMethods.RECT
                    {
                        Left = defaultX,
                        Top = defaultY,
                        Right = defaultX + defaultW,
                        Bottom = defaultY + defaultH
                    };
                }

                state = new WindowStateBackup
                {
                    OriginalStyle = origStyle,
                    OriginalExStyle = origExStyle & ~NativeMethods.WS_EX_TOPMOST,
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

            // 1. If targetZone not explicitly given, try rule's TargetZoneId
            if (targetZone == null && rule != null && rule.TargetZoneId > 0 && preset != null)
            {
                targetZone = preset.Zones.FirstOrDefault(z => z.Id == rule.TargetZoneId);
            }

            // 2. If targetZone is still null, and user DID NOT explicitly choose Fullscreen (TargetZoneId == 0):
            // Check if window is already snapped in a zone!
            if (targetZone == null && (rule == null || rule.TargetZoneId != 0))
            {
                targetZone = _zoneEngine?.GetWindowCurrentZone(hWnd);
            }

            bool coverTaskbar = rule != null ? rule.CoverTaskbar : _settings.DefaultCoverTaskbar;

            if (targetZone != null)
            {
                targetX = monLeft + targetZone.X;
                targetY = monTop + targetZone.Y;
                targetW = targetZone.Width;
                targetH = coverTaskbar
                    ? targetZone.Height
                    : Math.Min(targetZone.Height, targetMonitor.WorkArea.Height);
            }
            else if (rule != null && rule.TargetZoneId == 0)
            {
                // Fullscreen borderless explicitly requested by rule
                targetX = monLeft;
                targetY = monTop;
                targetW = targetMonitor.Bounds.Width;
                targetH = coverTaskbar ? targetMonitor.Bounds.Height : targetMonitor.WorkArea.Height;
            }
            else
            {
                // Free-floating window without a zone: keep current position and size instead of blowing up to fullscreen!
                if (state.OriginalRect.Width > 0 && state.OriginalRect.Height > 0)
                {
                    targetX = state.OriginalRect.Left;
                    targetY = state.OriginalRect.Top;
                    targetW = state.OriginalRect.Width;
                    targetH = coverTaskbar ? targetMonitor.Bounds.Height : state.OriginalRect.Height;
                }
                else
                {
                    targetX = targetMonitor.WorkArea.Left;
                    targetY = targetMonitor.WorkArea.Top;
                    targetW = targetMonitor.WorkArea.Width;
                    targetH = targetMonitor.WorkArea.Height;
                }
            }

            IntPtr zOrder = coverTaskbar ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_TOP;

            NativeMethods.SetWindowPos(
                hWnd,
                zOrder,
                targetX,
                targetY,
                targetW,
                targetH,
                NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW
            );

            state.IsBorderless = true;
            state.CoveredTaskbar = coverTaskbar;

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
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hWnd))
                return false;

            if (!_managedWindows.TryGetValue(hWnd, out var state))
            {
                // Window was not tracked in memory (e.g. app reopened after being closed)
                long curStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE).ToInt64();
                long curExStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
                NativeMethods.GetWindowRect(hWnd, out var curRect);

                var mon = MonitorManager.Instance.GetMonitorForWindow(hWnd);
                int defaultW = (int)(mon.WorkArea.Width * 0.75);
                int defaultH = (int)(mon.WorkArea.Height * 0.75);
                int defaultX = mon.WorkArea.Left + (mon.WorkArea.Width - defaultW) / 2;
                int defaultY = mon.WorkArea.Top + (mon.WorkArea.Height - defaultH) / 2;

                state = new WindowStateBackup
                {
                    OriginalStyle = (curStyle & ~NativeMethods.WS_POPUP) | NativeMethods.WS_OVERLAPPEDWINDOW | NativeMethods.WS_VISIBLE,
                    OriginalExStyle = curExStyle & ~NativeMethods.WS_EX_TOPMOST,
                    OriginalRect = new NativeMethods.RECT
                    {
                        Left = defaultX,
                        Top = defaultY,
                        Right = defaultX + defaultW,
                        Bottom = defaultY + defaultH
                    },
                    IsBorderless = true,
                    CoveredTaskbar = false
                };
                _managedWindows[hWnd] = state;
            }

            // Guarantee that restoreStyle has WS_CAPTION and strips WS_POPUP
            long restoreStyle = state.OriginalStyle;
            restoreStyle &= ~NativeMethods.WS_POPUP;
            if ((restoreStyle & NativeMethods.WS_CAPTION) == 0)
            {
                restoreStyle |= (NativeMethods.WS_OVERLAPPEDWINDOW | NativeMethods.WS_VISIBLE);
            }

            long restoreExStyle = state.OriginalExStyle & ~NativeMethods.WS_EX_TOPMOST;

            // 1. Restore styles
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE, new IntPtr(restoreStyle));
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, new IntPtr(restoreExStyle));

            // 2. Restore Windows 11 default corners
            int pref = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT;
            NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));

            // 3. Restore original position & reset topmost
            int rX = state.OriginalRect.Left;
            int rY = state.OriginalRect.Top;
            int rW = state.OriginalRect.Width;
            int rH = state.OriginalRect.Height;

            var targetMon = MonitorManager.Instance.GetMonitorForWindow(hWnd);
            if (rW <= 0 || rH <= 0 || (rW >= targetMon.Bounds.Width && rH >= targetMon.Bounds.Height))
            {
                rW = (int)(targetMon.WorkArea.Width * 0.75);
                rH = (int)(targetMon.WorkArea.Height * 0.75);
                rX = targetMon.WorkArea.Left + (targetMon.WorkArea.Width - rW) / 2;
                rY = targetMon.WorkArea.Top + (targetMon.WorkArea.Height - rH) / 2;
            }

            NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_NOTOPMOST,
                rX,
                rY,
                rW,
                rH,
                NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW
            );

            // 4. Force non-client frame redraw (Title bar, buttons, borders)
            NativeMethods.RedrawWindow(
                hWnd,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.RDW_FRAME | NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_ALLCHILDREN
            );

            // 5. Close floating caption bar if active
            if (state.CaptionBar != null)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    state.CaptionBar.Close();
                    state.CaptionBar = null;
                });
            }

            state.IsBorderless = false;
            state.CoveredTaskbar = false;
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
