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

        public List<Zone> ResolveZonesForMonitor(ZonePreset preset, MonitorInfoItem monitor)
        {
            int workW = monitor.WorkArea.Width > 0 ? monitor.WorkArea.Width : monitor.Width;
            int workH = monitor.WorkArea.Height > 0 ? monitor.WorkArea.Height : monitor.Height;

            if (preset.SplitRoot != null)
            {
                return preset.SplitRoot.ComputeZones(workW, workH, preset.ZoneGap);
            }

            double scaleX = preset.ScreenWidth > 0 ? workW / (double)preset.ScreenWidth : 1.0;
            double scaleY = preset.ScreenHeight > 0 ? workH / (double)preset.ScreenHeight : 1.0;

            return preset.Zones.Select(z => new Zone(
                z.Id,
                z.Name,
                (int)Math.Round(z.X * scaleX),
                (int)Math.Round(z.Y * scaleY),
                (int)Math.Round(z.Width * scaleX),
                (int)Math.Round(z.Height * scaleY))
            {
                PercentX = z.PercentX,
                PercentY = z.PercentY,
                PercentWidth = z.PercentWidth,
                PercentHeight = z.PercentHeight
            }).ToList();
        }

        public bool IsWindowSnappedAtTopEdge(IntPtr hWnd, MonitorInfoItem monitor)
        {
            if (hWnd == IntPtr.Zero || !_windowCurrentZoneMap.TryGetValue(hWnd, out int zoneId))
                return false;

            var preset = _settings.GetPresetForMonitor(monitor);
            if (preset == null) return false;

            var zones = ResolveZonesForMonitor(preset, monitor);
            var zone = zones.FirstOrDefault(z => z.Id == zoneId);
            return zone != null && zone.Y <= 4;
        }

        public Zone? GetWindowCurrentZone(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            var currentMonitor = MonitorManager.Instance.GetMonitorForWindow(hWnd);
            var preset = _settings.GetPresetForMonitor(currentMonitor);
            if (preset == null) return null;

            var zones = ResolveZonesForMonitor(preset, currentMonitor);
            if (zones.Count == 0) return null;

            if (_windowCurrentZoneMap.TryGetValue(hWnd, out int zoneId))
            {
                var found = zones.FirstOrDefault(z => z.Id == zoneId);
                if (found != null) return found;
            }

            // Detect by 2D screen intersection area against WorkArea
            NativeMethods.RECT winRect;
            int hr = NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out winRect, Marshal.SizeOf<NativeMethods.RECT>());
            if (hr != 0 && !NativeMethods.GetWindowRect(hWnd, out winRect))
                return null;

            int relX = winRect.Left - currentMonitor.WorkArea.Left;
            int relY = winRect.Top - currentMonitor.WorkArea.Top;
            int relW = winRect.Width;
            int relH = winRect.Height;

            Zone? bestZone = null;
            long maxArea = 0;

            foreach (var z in zones)
            {
                int overlapX = Math.Max(0, Math.Min(relX + relW, z.Right) - Math.Max(relX, z.X));
                int overlapY = Math.Max(0, Math.Min(relY + relH, z.Bottom) - Math.Max(relY, z.Y));
                long area = (long)overlapX * overlapY;

                if (area > maxArea)
                {
                    maxArea = area;
                    bestZone = z;
                }
            }

            if (bestZone != null && maxArea > 10000)
            {
                _windowCurrentZoneMap[hWnd] = bestZone.Id;
                return bestZone;
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

            if (hWnd == NativeMethods.GetDesktopWindow() || hWnd == NativeMethods.GetShellWindow())
                return false;

            string className = NativeMethods.GetWindowClassName(hWnd);
            if (className.Contains("Shell_TrayWnd") || className.Contains("Progman") || className.Contains("WorkerW"))
                return false;

            var currentMonitor = MonitorManager.Instance.GetMonitorForWindow(hWnd);
            var preset = _settings.GetPresetForMonitor(currentMonitor);
            if (preset == null) return false;

            var zones = ResolveZonesForMonitor(preset, currentMonitor);
            if (zones.Count == 0) return false;

            // If currently maximized
            if (NativeMethods.IsZoomed(hWnd))
            {
                if (direction == KeyDirection.Down || direction == KeyDirection.Up)
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                    if (direction == KeyDirection.Down && _windowCurrentZoneMap.TryGetValue(hWnd, out int prevZoneId))
                    {
                        var prevZone = zones.FirstOrDefault(z => z.Id == prevZoneId);
                        if (prevZone != null)
                        {
                            ApplyWindowToZone(hWnd, prevZone, currentMonitor);
                        }
                    }
                    return true;
                }

                // Left / Right from maximized: restore and snap into leftmost/rightmost or neighbor zone
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

            // Check if window already has a tracked zone
            Zone? currentZone = null;
            if (_windowCurrentZoneMap.TryGetValue(hWnd, out int mappedZoneId))
            {
                currentZone = zones.FirstOrDefault(z => z.Id == mappedZoneId);
            }

            // Unsnapped / floating window
            if (currentZone == null)
            {
                if (direction == KeyDirection.Up)
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MAXIMIZE);
                    return true;
                }

                if (direction == KeyDirection.Down)
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
                    return true;
                }

                Zone firstZone = direction == KeyDirection.Left
                    ? zones.OrderBy(z => z.X).ThenBy(z => z.Y).First()
                    : zones.OrderByDescending(z => z.Right).ThenBy(z => z.Y).First();

                _windowCurrentZoneMap[hWnd] = firstZone.Id;
                ApplyWindowToZone(hWnd, firstZone, currentMonitor);
                return true;
            }

            // Window is already in a zone: 2D Spatial Navigation
            Zone? neighbor = FindNeighborZone(zones, currentZone, direction);
            if (neighbor != null)
            {
                _windowCurrentZoneMap[hWnd] = neighbor.Id;
                ApplyWindowToZone(hWnd, neighbor, currentMonitor);
                return true;
            }

            // Reached layout edge along pressed direction
            if (direction == KeyDirection.Up)
            {
                // Keep _windowCurrentZoneMap[hWnd] so Win+Down restores back into this top zone
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MAXIMIZE);
                return true;
            }

            if (direction == KeyDirection.Down)
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
                return true;
            }

            // Left / Right edge: try wrapping across adjacent monitor if enabled
            if (_settings.EnableCrossMonitorWrap && MonitorManager.Instance.Monitors.Count > 1)
            {
                var nextMon = MonitorManager.Instance.GetNextMonitor(currentMonitor, direction);
                if (nextMon.DeviceName != currentMonitor.DeviceName)
                {
                    var nextPreset = _settings.GetPresetForMonitor(nextMon);
                    var nextZones = ResolveZonesForMonitor(nextPreset, nextMon);
                    if (nextZones.Count > 0)
                    {
                        double relCenterY = currentMonitor.WorkArea.Height > 0
                            ? currentZone.CenterY / currentMonitor.WorkArea.Height
                            : 0.5;

                        Zone targetZone = SelectEdgeZoneOnMonitor(nextZones, nextMon, direction, relCenterY);
                        _windowCurrentZoneMap[hWnd] = targetZone.Id;
                        ApplyWindowToZone(hWnd, targetZone, nextMon);
                        return true;
                    }
                }
            }

            // Even if at boundary with no wrap, ensure window stays snapped cleanly to currentZone
            ApplyWindowToZone(hWnd, currentZone, currentMonitor);
            return true;
        }

        private static Zone SelectEdgeZoneOnMonitor(List<Zone> zones, MonitorInfoItem monitor, KeyDirection moveDirection, double targetRelCenterY)
        {
            double targetY = targetRelCenterY * Math.Max(1, monitor.WorkArea.Height);
            if (moveDirection == KeyDirection.Right)
            {
                int minX = zones.Min(z => z.X);
                return zones
                    .Where(z => z.X <= minX + 12)
                    .OrderBy(z => Math.Abs(z.CenterY - targetY))
                    .First();
            }
            else
            {
                int maxRight = zones.Max(z => z.Right);
                return zones
                    .Where(z => z.Right >= maxRight - 12)
                    .OrderBy(z => Math.Abs(z.CenterY - targetY))
                    .First();
            }
        }

        public static Zone? FindNeighborZone(List<Zone> zones, Zone current, KeyDirection direction)
        {
            const int touchTolerance = 12;
            var candidates = new List<(Zone Zone, int EdgeGap, double OrthoDist, int OrthoOverlap)>();

            foreach (var z in zones)
            {
                if (z.Id == current.Id) continue;

                switch (direction)
                {
                    case KeyDirection.Right:
                        if (z.X >= current.Right - touchTolerance && z.CenterX > current.CenterX + 1)
                        {
                            int overlapY = Math.Min(current.Bottom, z.Bottom) - Math.Max(current.Y, z.Y);
                            if (overlapY > 4)
                            {
                                int gap = Math.Max(0, z.X - current.Right);
                                double orthoDist = Math.Abs(z.CenterY - current.CenterY);
                                candidates.Add((z, gap, orthoDist, overlapY));
                            }
                        }
                        break;

                    case KeyDirection.Left:
                        if (z.Right <= current.X + touchTolerance && z.CenterX < current.CenterX - 1)
                        {
                            int overlapY = Math.Min(current.Bottom, z.Bottom) - Math.Max(current.Y, z.Y);
                            if (overlapY > 4)
                            {
                                int gap = Math.Max(0, current.X - z.Right);
                                double orthoDist = Math.Abs(z.CenterY - current.CenterY);
                                candidates.Add((z, gap, orthoDist, overlapY));
                            }
                        }
                        break;

                    case KeyDirection.Down:
                        if (z.Y >= current.Bottom - touchTolerance && z.CenterY > current.CenterY + 1)
                        {
                            int overlapX = Math.Min(current.Right, z.Right) - Math.Max(current.X, z.X);
                            if (overlapX > 4)
                            {
                                int gap = Math.Max(0, z.Y - current.Bottom);
                                double orthoDist = Math.Abs(z.CenterX - current.CenterX);
                                candidates.Add((z, gap, orthoDist, overlapX));
                            }
                        }
                        break;

                    case KeyDirection.Up:
                        if (z.Bottom <= current.Y + touchTolerance && z.CenterY < current.CenterY - 1)
                        {
                            int overlapX = Math.Min(current.Right, z.Right) - Math.Max(current.X, z.X);
                            if (overlapX > 4)
                            {
                                int gap = Math.Max(0, current.Y - z.Bottom);
                                double orthoDist = Math.Abs(z.CenterX - current.CenterX);
                                candidates.Add((z, gap, orthoDist, overlapX));
                            }
                        }
                        break;
                }
            }

            if (candidates.Count == 0)
                return null;

            return candidates
                .OrderBy(c => c.EdgeGap)
                .ThenBy(c => c.OrthoDist)
                .ThenByDescending(c => c.OrthoOverlap)
                .First().Zone;
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
            if (targetPreset == null) return false;

            var targetZones = ResolveZonesForMonitor(targetPreset, targetMonitor);
            if (targetZones.Count == 0) return false;

            int targetZoneIndex = 0;
            if (_windowCurrentZoneMap.TryGetValue(hWnd, out int currentZoneId))
            {
                var curPreset = _settings.GetPresetForMonitor(currentMonitor);
                var curZones = ResolveZonesForMonitor(curPreset, currentMonitor);
                int curIdx = curZones.FindIndex(z => z.Id == currentZoneId);
                if (curIdx >= 0)
                {
                    targetZoneIndex = Math.Min(curIdx, targetZones.Count - 1);
                }
            }

            var targetZone = targetZones[targetZoneIndex];
            _windowCurrentZoneMap[hWnd] = targetZone.Id;

            if (NativeMethods.IsZoomed(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }

            ApplyWindowToZone(hWnd, targetZone, targetMonitor);
            return true;
        }

        public void ApplyWindowToZone(IntPtr hWnd, Zone targetZone, MonitorInfoItem monitor)
        {
            int monLeft = monitor.WorkArea.Left;
            int monTop = monitor.WorkArea.Top;

            int targetX = monLeft + targetZone.X;
            int targetY = monTop + targetZone.Y;
            int targetWidth = Math.Min(targetZone.Width, Math.Max(80, monitor.WorkArea.Width - targetZone.X));
            int targetHeight = Math.Min(targetZone.Height, Math.Max(80, monitor.WorkArea.Height - targetZone.Y));

            int actualX = targetX;
            int actualY = targetY;
            int actualW = targetWidth;
            int actualH = targetHeight;

            long windowStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE).ToInt64();
            bool hasDwmFrame = (windowStyle & (NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_BORDER)) != 0;

            if (_settings.DwmInvisibleBorderCompensation && hasDwmFrame)
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

            if (_settings.DisableWin11RoundedCorners)
            {
                int pref = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
                NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }

            const uint swpFlags = NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE;
            NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_TOP,
                actualX,
                actualY,
                actualW,
                actualH,
                swpFlags
            );

            // Second pass DWM alignment: align visible extended frame bounds cleanly to target rect
            if (_settings.DwmInvisibleBorderCompensation && hasDwmFrame)
            {
                if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out var postFrame, Marshal.SizeOf<NativeMethods.RECT>()) == 0)
                {
                    int deltaLeft = targetX - postFrame.Left;
                    int deltaTop = targetY - postFrame.Top;
                    int deltaRight = (targetX + targetWidth) - postFrame.Right;
                    int deltaBottom = (targetY + targetHeight) - postFrame.Bottom;

                    if ((deltaLeft != 0 || deltaTop != 0 || deltaRight != 0 || deltaBottom != 0) &&
                        Math.Abs(deltaLeft) <= 24 && Math.Abs(deltaTop) <= 24 &&
                        Math.Abs(deltaRight) <= 24 && Math.Abs(deltaBottom) <= 24)
                    {
                        int corrX = actualX + deltaLeft;
                        int corrY = actualY + deltaTop;
                        int corrW = actualW - deltaLeft + deltaRight;
                        int corrH = actualH - deltaTop + deltaBottom;

                        if (corrW > 50 && corrH > 50)
                        {
                            NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_TOP, corrX, corrY, corrW, corrH, swpFlags);
                        }
                    }
                }
            }

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
