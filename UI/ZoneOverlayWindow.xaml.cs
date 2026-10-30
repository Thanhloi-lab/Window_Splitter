using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using WindowsSplitter.Core;
using WindowsSplitter.Models;

namespace WindowsSplitter.UI
{
    public partial class ZoneOverlayWindow : Window
    {
        public ZoneOverlayWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            IntPtr hWnd = helper.Handle;

            // Set WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_TOPMOST
            long exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW |
                       NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOPMOST;
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
        }

        public async void ShowForZone(Zone zone, NativeMethods.RECT rect, int durationMs = 500)
        {
            TxtZoneName.Text = zone.Name;
            TxtZoneDimensions.Text = $"{rect.Width} × {rect.Height} px";

            this.Show();

            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                // Position directly using Win32 Physical Pixels to align accurately on multi-monitors
                NativeMethods.SetWindowPos(
                    helper.Handle,
                    NativeMethods.HWND_TOPMOST,
                    rect.Left,
                    rect.Top,
                    rect.Width,
                    rect.Height,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW
                );
            }

            // Fade In
            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            OverlayContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            await Task.Delay(durationMs);

            // Fade Out
            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, e) =>
            {
                this.Hide();
            };
            OverlayContainer.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}
