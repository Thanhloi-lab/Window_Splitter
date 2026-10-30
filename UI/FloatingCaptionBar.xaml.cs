using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WindowsSplitter.Core;

namespace WindowsSplitter.UI
{
    public partial class FloatingCaptionBar : Window
    {
        private readonly IntPtr _targetHWnd;
        private readonly Action<IntPtr>? _onToggleBorderless;
        private readonly DispatcherTimer _hoverCheckTimer;
        private bool _isExpanded = false;

        private int _physicalX = 0;
        private int _physicalY = 0;
        private int _physicalW = 114;
        private int _physicalH = 36;

        public IntPtr TargetHWnd => _targetHWnd;

        public FloatingCaptionBar(IntPtr targetHWnd, Action<IntPtr>? onToggleBorderless = null)
        {
            InitializeComponent();
            _targetHWnd = targetHWnd;
            _onToggleBorderless = onToggleBorderless;

            _hoverCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _hoverCheckTimer.Tick += HoverCheckTimer_Tick;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            IntPtr hWnd = helper.Handle;

            // Set WS_EX_NOACTIVATE and WS_EX_TOOLWINDOW and WS_EX_TOPMOST
            long exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST;
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            _hoverCheckTimer.Start();
        }

        public void UpdatePosition(int physicalRight, int physicalTop, double dpiScaleX = 1.0, double dpiScaleY = 1.0)
        {
            _physicalW = (int)Math.Round(114 * dpiScaleX);
            _physicalH = (int)Math.Round(36 * dpiScaleY);
            _physicalX = physicalRight - _physicalW;
            _physicalY = physicalTop;

            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                // Position directly using Win32 Physical Pixels to guarantee Per-Monitor DPI accuracy
                NativeMethods.SetWindowPos(
                    helper.Handle,
                    NativeMethods.HWND_TOPMOST,
                    _physicalX,
                    _physicalY,
                    _physicalW,
                    _physicalH,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW
                );
            }
            else
            {
                // Fallback to WPF DIPs before handle is created
                this.Left = _physicalX / dpiScaleX;
                this.Top = _physicalY / dpiScaleY;
            }
        }

        private void HoverCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (_targetHWnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(_targetHWnd) || NativeMethods.IsIconic(_targetHWnd))
            {
                if (this.Visibility != Visibility.Hidden)
                    this.Visibility = Visibility.Hidden;
                return;
            }

            if (this.Visibility != Visibility.Visible)
                this.Visibility = Visibility.Visible;

            NativeMethods.GetCursorPos(out var cursorPos);

            // Compare directly in physical pixels
            bool isCursorOverBar = (cursorPos.X >= _physicalX && cursorPos.X <= _physicalX + _physicalW &&
                                    cursorPos.Y >= _physicalY && cursorPos.Y <= _physicalY + _physicalH + 10);

            // Alt key trigger
            bool isAltDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;

            bool shouldExpand = isCursorOverBar || isAltDown;

            if (shouldExpand != _isExpanded)
            {
                _isExpanded = shouldExpand;
                AnimateState(_isExpanded);
            }
        }

        private void AnimateState(bool expand)
        {
            double targetY = expand ? 0 : -24;
            double targetOpacity = expand ? 1.0 : 0.15;

            var slideAnim = new DoubleAnimation(targetY, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var opacityAnim = new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            SlideTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideAnim);
            RootContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            if (_targetHWnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(_targetHWnd, NativeMethods.SW_MINIMIZE);
            }
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (_targetHWnd != IntPtr.Zero)
            {
                _onToggleBorderless?.Invoke(_targetHWnd);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_targetHWnd != IntPtr.Zero)
            {
                NativeMethods.PostMessage(_targetHWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _hoverCheckTimer.Stop();
            base.OnClosed(e);
        }
    }
}
