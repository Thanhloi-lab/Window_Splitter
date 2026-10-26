using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsSplitter.Core
{
    public enum KeyDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    public class LowLevelKeyboardHook : IDisposable
    {
        private IntPtr _hookId = IntPtr.Zero;
        private readonly NativeMethods.LowLevelKeyboardProc _proc;
        private bool _isWinDown = false;

        public bool OverrideWindowsSnap { get; set; } = true;
        public bool IsRunning => _hookId != IntPtr.Zero;

        public event Action<KeyDirection>? HotKeyPressed;
        public event Action<KeyDirection>? HotKeyPressedWithShift;

        public LowLevelKeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookId == IntPtr.Zero)
            {
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule;
                IntPtr hMod = NativeMethods.GetModuleHandle(curModule?.ModuleName);
                _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, hMod, 0);
            }
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                _isWinDown = false;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                uint vkCode = hookStruct.vkCode;
                int message = wParam.ToInt32();

                // Track Win key state
                if (vkCode == NativeMethods.VK_LWIN || vkCode == NativeMethods.VK_RWIN)
                {
                    if (message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN)
                    {
                        _isWinDown = true;
                    }
                    else if (message == NativeMethods.WM_KEYUP || message == NativeMethods.WM_SYSKEYUP)
                    {
                        _isWinDown = false;
                    }
                }

                // Check physical Win key state via GetAsyncKeyState for maximum reliability
                bool isWinPhysicallyPressed = _isWinDown ||
                    (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 ||
                    (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

                if (isWinPhysicallyPressed && (message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN))
                {
                    bool isCtrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                    bool isShift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;

                    if (!isCtrl)
                    {
                        KeyDirection? direction = vkCode switch
                        {
                            NativeMethods.VK_LEFT => KeyDirection.Left,
                            NativeMethods.VK_RIGHT => KeyDirection.Right,
                            NativeMethods.VK_UP => KeyDirection.Up,
                            NativeMethods.VK_DOWN => KeyDirection.Down,
                            _ => null
                        };

                        if (direction.HasValue && OverrideWindowsSnap)
                        {
                            if (isShift)
                            {
                                // Win + Shift + Arrow: Move between monitors
                                HotKeyPressedWithShift?.Invoke(direction.Value);
                                return (IntPtr)1;
                            }
                            else
                            {
                                // Win + Arrow: Move between zones
                                HotKeyPressed?.Invoke(direction.Value);
                                return (IntPtr)1;
                            }
                        }
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        ~LowLevelKeyboardHook()
        {
            Stop();
        }
    }
}
