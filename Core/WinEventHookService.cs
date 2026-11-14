namespace WindowsSplitter.Core
{
    public class WinEventHookService : IDisposable
    {
        private readonly BorderlessEngine _borderlessEngine;
        private IntPtr _foregroundHook = IntPtr.Zero;
        private readonly NativeMethods.WinEventDelegate _winEventDelegate;

        public WinEventHookService(BorderlessEngine borderlessEngine)
        {
            _borderlessEngine = borderlessEngine;
            _winEventDelegate = WinEventCallback;
        }

        public void Start()
        {
            if (_foregroundHook != IntPtr.Zero)
                return;

            const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
            _foregroundHook = NativeMethods.SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                0);
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd != IntPtr.Zero && NativeMethods.IsWindowVisible(hwnd) &&
                _borderlessEngine.IsBorderless(hwnd) && _borderlessEngine.IsCoveringTaskbar(hwnd))
            {
                _borderlessEngine.BringAboveTaskbar(hwnd);
            }
        }

        public void Stop()
        {
            if (_foregroundHook == IntPtr.Zero)
                return;

            NativeMethods.UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
