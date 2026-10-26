using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WindowsSplitter.Models;

namespace WindowsSplitter.Core
{
    public static class WindowEnumerator
    {
        private const uint WM_GETICON = 0x007F;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int ICON_SMALL2 = 2;
        private const int GCL_HICONSM = -34;
        private const int GCL_HICON = -14;

        [DllImport("user32.dll", EntryPoint = "GetClassLong")]
        private static extern IntPtr GetClassLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
        private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

        private static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetClassLongPtr64(hWnd, nIndex) : GetClassLong32(hWnd, nIndex);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        public static List<RunningWindowItem> GetRunningWindows(BorderlessEngine borderlessEngine)
        {
            var list = new List<RunningWindowItem>();
            IntPtr shellWnd = NativeMethods.GetShellWindow();
            IntPtr desktopWnd = NativeMethods.GetDesktopWindow();

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == shellWnd || hWnd == desktopWnd)
                    return true;

                if (!NativeMethods.IsWindowVisible(hWnd))
                    return true;

                // Check title length
                string title = NativeMethods.GetWindowTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                // Check window style
                long style = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE).ToInt64();
                long exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();

                // Skip tool windows unless they are borderless managed windows
                if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 && !borderlessEngine.IsBorderless(hWnd))
                    return true;

                string className = NativeMethods.GetWindowClassName(hWnd);
                if (className == "Shell_TrayWnd" || className == "Windows.UI.Core.CoreWindow" || className.Contains("Tooltip"))
                    return true;

                string exePath = NativeMethods.GetProcessExecutablePath(hWnd);
                string exeName = string.IsNullOrEmpty(exePath) ? "Unknown" : Path.GetFileName(exePath);

                // Exclude current app
                if (exeName.Equals("WindowsSplitter.exe", StringComparison.OrdinalIgnoreCase))
                    return true;

                var item = new RunningWindowItem
                {
                    Hwnd = hWnd,
                    Title = title,
                    ProcessName = exeName,
                    ExecutablePath = exePath,
                    IsBorderless = borderlessEngine.IsBorderless(hWnd),
                    Icon = GetWindowIcon(hWnd, exePath)
                };

                list.Add(item);
                return true;
            }, IntPtr.Zero);

            return list;
        }

        public static BitmapSource? GetWindowIcon(IntPtr hWnd, string exePath)
        {
            try
            {
                // Try WM_GETICON
                IntPtr hIcon = NativeMethods.SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero);
                if (hIcon == IntPtr.Zero)
                    hIcon = NativeMethods.SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero);
                if (hIcon == IntPtr.Zero)
                    hIcon = NativeMethods.SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);

                // Try Class Icon
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hWnd, GCL_HICONSM);
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hWnd, GCL_HICON);

                // Try ExtractIconEx from executable file
                if (hIcon == IntPtr.Zero && !string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    IntPtr[] smallIcons = new IntPtr[1];
                    if (ExtractIconEx(exePath, 0, null!, smallIcons, 1) > 0 && smallIcons[0] != IntPtr.Zero)
                    {
                        hIcon = smallIcons[0];
                    }
                }

                if (hIcon != IntPtr.Zero)
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
                // Ignore icon extraction failure
            }

            return null;
        }
    }
}
