using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WindowsSplitter.Core;

namespace WindowsSplitter.UI
{
    public partial class BorderlessNoticeWindow : Window
    {
        private readonly DispatcherTimer _dismissTimer;

        public BorderlessNoticeWindow()
        {
            InitializeComponent();
            TxtTitle.Text = LocalizationManager.GetString("Str_BorderlessNoticeTitle");
            TxtHint.Text = LocalizationManager.GetString("Str_BorderlessNoticeHint");

            _dismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(2800)
            };
            _dismissTimer.Tick += DismissTimer_Tick;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW |
                       NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOPMOST;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
        }

        public void ShowForWindow(IntPtr targetHwnd)
        {
            if (!NativeMethods.GetWindowRect(targetHwnd, out var rect))
                return;

            var monitor = MonitorManager.Instance.GetMonitorForWindow(targetHwnd);
            int width = (int)Math.Round(Width * monitor.DpiScaleX);
            int height = (int)Math.Round(Height * monitor.DpiScaleY);
            int left = rect.Left + (rect.Width - width) / 2;
            int top = rect.Top + (rect.Height - height) / 3;

            Show();
            NativeMethods.SetWindowPos(
                new WindowInteropHelper(this).Handle,
                NativeMethods.HWND_TOPMOST,
                left,
                top,
                width,
                height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            BeginAnimation(OpacityProperty, new DoubleAnimation(0.82, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            _dismissTimer.Start();
        }

        private void DismissTimer_Tick(object? sender, EventArgs e)
        {
            _dismissTimer.Stop();
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(350));
            fadeOut.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }

        protected override void OnClosed(EventArgs e)
        {
            _dismissTimer.Stop();
            base.OnClosed(e);
        }
    }
}
