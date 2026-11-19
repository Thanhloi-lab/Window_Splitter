using System.Diagnostics;
using System.Runtime.InteropServices;
using WindowsSplitter.Models;

namespace WindowsSplitter.Core
{
    public class TopEdgeSnapGuard : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly ZoneEngine _zoneEngine;
        private readonly NativeMethods.LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;
        private bool _suppressNextButtonUp = false;

        public TopEdgeSnapGuard(AppSettings settings, ZoneEngine zoneEngine)
        {
            _settings = settings;
            _zoneEngine = zoneEngine;
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            IntPtr hMod = NativeMethods.GetModuleHandle(curModule?.ModuleName);
            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                _suppressNextButtonUp = false;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();

                if (msg == NativeMethods.WM_LBUTTONUP && _suppressNextButtonUp)
                {
                    _suppressNextButtonUp = false;
                    return (IntPtr)1;
                }

                if (msg == NativeMethods.WM_LBUTTONDOWN && _settings.PreventAccidentalTopEdgeMaximize)
                {
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    var pt = hookStruct.pt;

                    var monitor = FindMonitorAtPoint(pt);
                    if (monitor != null && pt.Y >= monitor.WorkArea.Top && pt.Y <= monitor.WorkArea.Top + 8)
                    {
                        IntPtr hitHwnd = NativeMethods.WindowFromPoint(pt);
                        if (hitHwnd != IntPtr.Zero)
                        {
                            IntPtr rootHwnd = NativeMethods.GetAncestor(hitHwnd, NativeMethods.GA_ROOT);
                            if (rootHwnd == IntPtr.Zero) rootHwnd = hitHwnd;

                            if (_zoneEngine.IsWindowSnappedAtTopEdge(rootHwnd, monitor) && !NativeMethods.IsZoomed(rootHwnd))
                            {
                                IntPtr packedPt = new IntPtr(unchecked((int)(((uint)(pt.Y & 0xFFFF) << 16) | (uint)(pt.X & 0xFFFF))));
                                NativeMethods.SendMessageTimeout(
                                    rootHwnd,
                                    NativeMethods.WM_NCHITTEST,
                                    IntPtr.Zero,
                                    packedPt,
                                    NativeMethods.SMTO_ABORTIFHUNG,
                                    30,
                                    out IntPtr hitTestResult);

                                if (hitTestResult.ToInt32() == NativeMethods.HTTOP)
                                {
                                    _suppressNextButtonUp = true;
                                    NativeMethods.SetForegroundWindow(rootHwnd);
                                    return (IntPtr)1;
                                }
                            }
                        }
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static MonitorInfoItem? FindMonitorAtPoint(NativeMethods.POINT pt)
        {
            foreach (var mon in MonitorManager.Instance.Monitors)
            {
                if (pt.X >= mon.Bounds.Left && pt.X < mon.Bounds.Right &&
                    pt.Y >= mon.Bounds.Top && pt.Y < mon.Bounds.Bottom)
                {
                    return mon;
                }
            }
            return null;
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        ~TopEdgeSnapGuard()
        {
            Stop();
        }
    }
}
