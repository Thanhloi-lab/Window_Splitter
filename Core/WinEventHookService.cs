using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using WindowsSplitter.Models;

namespace WindowsSplitter.Core
{
    public class WinEventHookService : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly BorderlessEngine _borderlessEngine;
        private readonly ZoneEngine _zoneEngine;
        private readonly DispatcherTimer _pollTimer;
        private readonly HashSet<IntPtr> _autoAppliedHwnds = new();

        private IntPtr _foregroundHook = IntPtr.Zero;
        private readonly NativeMethods.WinEventDelegate _winEventDelegate;

        public WinEventHookService(AppSettings settings, BorderlessEngine borderlessEngine, ZoneEngine zoneEngine)
        {
            _settings = settings;
            _borderlessEngine = borderlessEngine;
            _zoneEngine = zoneEngine;

            _winEventDelegate = WinEventCallback;

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _pollTimer.Tick += (s, e) => CheckOpenWindowsForAutoApply();
        }

        public void Start()
        {
            if (_foregroundHook == IntPtr.Zero)
            {
                const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
                _foregroundHook = NativeMethods.SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero,
                    _winEventDelegate,
                    0,
                    0,
                    0 // WINEVENT_OUTOFCONTEXT
                );
            }

            _pollTimer.Start();
        }

        public void Stop()
        {
            if (_foregroundHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_foregroundHook);
                _foregroundHook = IntPtr.Zero;
            }

            _pollTimer.Stop();
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hwnd))
                return;

            CheckAndApplyWindow(hwnd);
        }

        public void CheckOpenWindowsForAutoApply()
        {
            if (_settings.BorderlessWhitelist.Count == 0) return;

            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                if (NativeMethods.IsWindowVisible(hwnd) && !NativeMethods.IsIconic(hwnd))
                {
                    CheckAndApplyWindow(hwnd);
                }
                return true;
            }, IntPtr.Zero);
        }

        private void CheckAndApplyWindow(IntPtr hwnd)
        {
            if (_autoAppliedHwnds.Contains(hwnd))
                return;

            var rule = _borderlessEngine.FindRuleForWindow(hwnd);
            if (rule != null && rule.AutoApply)
            {
                _autoAppliedHwnds.Add(hwnd);
                _borderlessEngine.ApplyBorderless(hwnd, rule);
            }
        }

        public void ResetWindowCache(IntPtr hwnd)
        {
            _autoAppliedHwnds.Remove(hwnd);
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
