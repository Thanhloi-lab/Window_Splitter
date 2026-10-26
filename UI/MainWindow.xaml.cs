using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WindowsSplitter.Core;
using WindowsSplitter.Models;

namespace WindowsSplitter.UI
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly ZoneEngine _zoneEngine;
        private readonly BorderlessEngine _borderlessEngine;
        private readonly WinEventHookService _winEventHookService;
        private readonly LowLevelKeyboardHook _keyboardHook;
        private ZoneOverlayWindow? _overlayWindow;

        private List<RunningWindowItem> _allRunningWindows = new();
        private MonitorInfoItem? _currentSelectedMonitor;
        private bool _isUpdatingUi = false;
        private bool _isReallyExiting = false;

        public MainWindow()
        {
            InitializeComponent();

            _settings = AppSettings.Load();
            _zoneEngine = new ZoneEngine(_settings);
            _borderlessEngine = new BorderlessEngine(_settings);
            _winEventHookService = new WinEventHookService(_settings, _borderlessEngine, _zoneEngine);
            _keyboardHook = new LowLevelKeyboardHook
            {
                OverrideWindowsSnap = _settings.OverrideWindowsSnap
            };

            // Setup Admin indicator
            bool isAdmin = CheckIfRunningAsAdmin();
            if (!isAdmin)
            {
                BadgeAdmin.Background = System.Windows.Media.Brushes.DarkRed;
                BadgeAdmin.BorderBrush = System.Windows.Media.Brushes.Red;
                if (BadgeAdmin.Child is TextBlock tb)
                {
                    tb.Text = "⚠️ Non-Admin Mode (Re-run as Admin for Games)";
                    tb.Foreground = System.Windows.Media.Brushes.Yellow;
                }
            }

            // Setup Hotkey Snap Handler: Win + Arrow (Zones)
            _keyboardHook.HotKeyPressed += (direction) =>
            {
                Dispatcher.Invoke(() =>
                {
                    IntPtr activeHwnd = NativeMethods.GetForegroundWindow();
                    if (activeHwnd != IntPtr.Zero)
                    {
                        var rule = _borderlessEngine.FindRuleForWindow(activeHwnd);
                        if (rule != null && !_borderlessEngine.IsBorderless(activeHwnd))
                        {
                            _borderlessEngine.ApplyBorderless(activeHwnd, rule);
                        }
                        else
                        {
                            _zoneEngine.SnapWindow(activeHwnd, direction);
                        }
                    }
                });
            };

            // Setup Hotkey Monitor Move Handler: Win + Shift + Arrow (Cross Monitor)
            _keyboardHook.HotKeyPressedWithShift += (direction) =>
            {
                if (!_settings.EnableWinShiftMonitorMove) return;

                Dispatcher.Invoke(() =>
                {
                    IntPtr activeHwnd = NativeMethods.GetForegroundWindow();
                    if (activeHwnd != IntPtr.Zero)
                    {
                        _zoneEngine.MoveWindowToMonitor(activeHwnd, direction);
                    }
                });
            };

            // Setup Visual Overlay Handler
            _zoneEngine.WindowSnapped += (hWnd, zone, rect) =>
            {
                if (_settings.ShowZoneOverlayOnSnap)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_overlayWindow == null)
                        {
                            _overlayWindow = new ZoneOverlayWindow();
                        }
                        _overlayWindow.ShowForZone(zone, rect, _settings.ZoneOverlayDurationMs);
                    });
                }
            };

            // Start background services
            _keyboardHook.Start();
            _winEventHookService.Start();

            // Populate UI
            RefreshMonitorSelector();
            LoadSettingsToUi();
            RefreshRunningAppsList();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Hook window messages to listen for WM_DISPLAYCHANGE (Win+P or monitor topology changes)
            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)NativeMethods.WM_DISPLAYCHANGE)
            {
                // Topology changed: re-scan all monitors dynamically
                MonitorManager.Instance.RefreshMonitors();
                Dispatcher.Invoke(() =>
                {
                    RefreshMonitorSelector();
                    UpdateZoneVisualDisplay();
                });
            }

            return IntPtr.Zero;
        }

        private static bool CheckIfRunningAsAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void RefreshMonitorSelector()
        {
            _isUpdatingUi = true;

            var monitors = MonitorManager.Instance.Monitors;
            CmbMonitors.ItemsSource = null;
            CmbMonitors.ItemsSource = monitors;
            CmbMonitors.DisplayMemberPath = "FriendlyName";

            // Duplicate Mode indicator
            if (MonitorManager.Instance.IsDuplicateMode)
            {
                BadgeDuplicateMode.Visibility = Visibility.Visible;
                TxtMonitorTopologyTag.Text = "Nhân bản (Duplicate Mode)";
            }
            else
            {
                BadgeDuplicateMode.Visibility = Visibility.Collapsed;
                TxtMonitorTopologyTag.Text = monitors.Count > 1 ? $"Mở rộng ({monitors.Count} Màn hình - Extend)" : "Màn hình đơn (Single Display)";
            }

            // Select primary or first
            var target = MonitorManager.Instance.GetPrimaryMonitor() ?? monitors.FirstOrDefault();
            CmbMonitors.SelectedItem = target;
            _currentSelectedMonitor = target;

            _isUpdatingUi = false;
        }

        private void CmbMonitors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;

            if (CmbMonitors.SelectedItem is MonitorInfoItem monitor)
            {
                _currentSelectedMonitor = monitor;
                TxtMonitorDpiBadge.Text = $"DPI: {(int)Math.Round(monitor.DpiScaleX * 100)}%";

                var config = _settings.GetOrCreateMonitorConfig(monitor.DeviceName, monitor.Width, monitor.Height);

                _isUpdatingUi = true;
                CmbPresets.ItemsSource = null;
                CmbPresets.ItemsSource = config.Presets;
                CmbPresets.DisplayMemberPath = "Name";
                CmbPresets.SelectedValuePath = "Id";
                CmbPresets.SelectedValue = config.SelectedPresetId;

                if (CmbPresets.SelectedIndex < 0 && config.Presets.Count > 0)
                {
                    CmbPresets.SelectedIndex = 0;
                }
                _isUpdatingUi = false;

                UpdateZoneVisualDisplay();
            }
        }

        private void LoadSettingsToUi()
        {
            _isUpdatingUi = true;

            // Behavior & Multi-monitor options
            ChkOverrideSnap.IsChecked = _settings.OverrideWindowsSnap;
            ChkWinShiftMonitorMove.IsChecked = _settings.EnableWinShiftMonitorMove;
            ChkCrossMonitorWrap.IsChecked = _settings.EnableCrossMonitorWrap;
            ChkInvisibleBorderComp.IsChecked = _settings.DwmInvisibleBorderCompensation;
            ChkDisableRoundedCorners.IsChecked = _settings.DisableWin11RoundedCorners;
            ChkShowZoneOverlay.IsChecked = _settings.ShowZoneOverlayOnSnap;
            ChkStartMinimized.IsChecked = _settings.StartMinimizedToTray;

            if (_currentSelectedMonitor != null)
            {
                var config = _settings.GetOrCreateMonitorConfig(_currentSelectedMonitor.DeviceName, _currentSelectedMonitor.Width, _currentSelectedMonitor.Height);
                CmbPresets.ItemsSource = config.Presets;
                CmbPresets.DisplayMemberPath = "Name";
                CmbPresets.SelectedValuePath = "Id";
                CmbPresets.SelectedValue = config.SelectedPresetId;
            }

            // Whitelist
            RefreshWhitelistUi();

            _isUpdatingUi = false;
            UpdateZoneVisualDisplay();
        }

        private void RefreshWhitelistUi()
        {
            ListWhitelist.ItemsSource = null;
            ListWhitelist.ItemsSource = _settings.BorderlessWhitelist;
        }

        private ZonePreset? GetCurrentMonitorActivePreset()
        {
            if (_currentSelectedMonitor == null) return _settings.GetActivePreset();
            return _settings.GetPresetForMonitor(_currentSelectedMonitor);
        }

        private void UpdateZoneVisualDisplay()
        {
            var preset = GetCurrentMonitorActivePreset();
            if (preset == null || preset.Zones.Count == 0) return;

            TxtPresetDescription.Text = preset.Description;

            if (preset.Zones.Count >= 2)
            {
                var z1 = preset.Zones[0];
                var z2 = preset.Zones[1];

                TxtZone1X.Text = z1.X.ToString();
                TxtZone1Y.Text = z1.Y.ToString();
                TxtZone1W.Text = z1.Width.ToString();
                TxtZone1H.Text = z1.Height.ToString();

                TxtZone2X.Text = z2.X.ToString();
                TxtZone2Y.Text = z2.Y.ToString();
                TxtZone2W.Text = z2.Width.ToString();
                TxtZone2H.Text = z2.Height.ToString();

                int totalW = z1.Width + z2.Width;
                if (_currentSelectedMonitor != null && _currentSelectedMonitor.Width > 0)
                {
                    totalW = _currentSelectedMonitor.Width;
                }

                SliderZoneSplit.Maximum = Math.Max(totalW - 100, 1000);
                SliderZoneSplit.Value = z1.Width;

                double pct1 = (z1.Width / (double)totalW) * 100.0;
                double pct2 = (z2.Width / (double)totalW) * 100.0;
                TxtSliderRatioValue.Text = $" {z1.Width}px / {z2.Width}px ({pct1:0.#}% / {pct2:0.#}%)";

                ColVisualZone1.Width = new GridLength(Math.Max(1, z1.Width), GridUnitType.Star);
                ColVisualZone2.Width = new GridLength(Math.Max(1, z2.Width), GridUnitType.Star);

                TxtVisualZone1Size.Text = $"{z1.Width} × {z1.Height} px";
                TxtVisualZone2Size.Text = $"{z2.Width} × {z2.Height} px";
            }
        }

        private void CmbPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;

            if (CmbPresets.SelectedItem is ZonePreset preset && _currentSelectedMonitor != null)
            {
                var config = _settings.GetOrCreateMonitorConfig(_currentSelectedMonitor.DeviceName, _currentSelectedMonitor.Width, _currentSelectedMonitor.Height);
                config.SelectedPresetId = preset.Id;
                _settings.Save();
                UpdateZoneVisualDisplay();
            }
        }

        private void SliderZoneSplit_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi) return;

            var preset = GetCurrentMonitorActivePreset();
            if (preset == null || preset.Zones.Count < 2) return;

            int newZ1Width = (int)Math.Round(SliderZoneSplit.Value);
            int totalWidth = _currentSelectedMonitor?.Width ?? (preset.Zones[0].Width + preset.Zones[1].Width);
            if (totalWidth <= 0) totalWidth = preset.ScreenWidth;

            int newZ2Width = Math.Max(100, totalWidth - newZ1Width);

            preset.Zones[0].Width = newZ1Width;
            preset.Zones[1].X = newZ1Width;
            preset.Zones[1].Width = newZ2Width;

            _isUpdatingUi = true;
            TxtZone1W.Text = newZ1Width.ToString();
            TxtZone2X.Text = newZ1Width.ToString();
            TxtZone2W.Text = newZ2Width.ToString();

            double pct1 = (newZ1Width / (double)totalWidth) * 100.0;
            double pct2 = (newZ2Width / (double)totalWidth) * 100.0;
            TxtSliderRatioValue.Text = $" {newZ1Width}px / {newZ2Width}px ({pct1:0.#}% / {pct2:0.#}%)";

            ColVisualZone1.Width = new GridLength(newZ1Width, GridUnitType.Star);
            ColVisualZone2.Width = new GridLength(newZ2Width, GridUnitType.Star);

            TxtVisualZone1Size.Text = $"{newZ1Width} × {preset.Zones[0].Height} px";
            TxtVisualZone2Size.Text = $"{newZ2Width} × {preset.Zones[1].Height} px";
            _isUpdatingUi = false;
        }

        private void ZoneInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;

            var preset = GetCurrentMonitorActivePreset();
            if (preset == null || preset.Zones.Count < 2) return;

            if (int.TryParse(TxtZone1X.Text, out int z1x) &&
                int.TryParse(TxtZone1Y.Text, out int z1y) &&
                int.TryParse(TxtZone1W.Text, out int z1w) &&
                int.TryParse(TxtZone1H.Text, out int z1h) &&
                int.TryParse(TxtZone2X.Text, out int z2x) &&
                int.TryParse(TxtZone2Y.Text, out int z2y) &&
                int.TryParse(TxtZone2W.Text, out int z2w) &&
                int.TryParse(TxtZone2H.Text, out int z2h))
            {
                preset.Zones[0].X = z1x;
                preset.Zones[0].Y = z1y;
                preset.Zones[0].Width = z1w;
                preset.Zones[0].Height = z1h;

                preset.Zones[1].X = z2x;
                preset.Zones[1].Y = z2y;
                preset.Zones[1].Width = z2w;
                preset.Zones[1].Height = z2h;

                ColVisualZone1.Width = new GridLength(Math.Max(1, z1w), GridUnitType.Star);
                ColVisualZone2.Width = new GridLength(Math.Max(1, z2w), GridUnitType.Star);

                TxtVisualZone1Size.Text = $"{z1w} × {z1h} px";
                TxtVisualZone2Size.Text = $"{z2w} × {z2h} px";
            }
        }

        private void BtnSaveZoneSettings_Click(object sender, RoutedEventArgs e)
        {
            _settings.Save();
            MessageBox.Show(this, "Đã lưu cài đặt bố cục vùng cho màn hình này thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDetectScreen_Click(object sender, RoutedEventArgs e)
        {
            MonitorManager.Instance.RefreshMonitors();
            RefreshMonitorSelector();
            UpdateZoneVisualDisplay();

            int count = MonitorManager.Instance.Monitors.Count;
            string mode = MonitorManager.Instance.IsDuplicateMode ? "Duplicate (Nhân bản)" : "Extend (Mở rộng)";
            MessageBox.Show(this, $"Đã làm mới danh sách màn hình: Tìm thấy {count} màn hình ({mode}).", "Quét màn hình", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SettingChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;

            _settings.OverrideWindowsSnap = ChkOverrideSnap.IsChecked ?? true;
            _settings.EnableWinShiftMonitorMove = ChkWinShiftMonitorMove.IsChecked ?? true;
            _settings.EnableCrossMonitorWrap = ChkCrossMonitorWrap.IsChecked ?? true;
            _settings.DwmInvisibleBorderCompensation = ChkInvisibleBorderComp.IsChecked ?? true;
            _settings.DisableWin11RoundedCorners = ChkDisableRoundedCorners.IsChecked ?? true;
            _settings.ShowZoneOverlayOnSnap = ChkShowZoneOverlay.IsChecked ?? true;
            _settings.StartMinimizedToTray = ChkStartMinimized.IsChecked ?? false;

            _keyboardHook.OverrideWindowsSnap = _settings.OverrideWindowsSnap;
            TxtHookStatus.Text = _settings.OverrideWindowsSnap ? "⚡ Win+Arrow: BẬT" : "⚪ Win+Arrow: TẮT";
            MenuHookStatus.IsChecked = _settings.OverrideWindowsSnap;

            _settings.Save();
        }

        #region Borderless Manager Actions

        private void RefreshRunningAppsList()
        {
            try
            {
                _allRunningWindows = WindowEnumerator.GetRunningWindows(_borderlessEngine);
                ApplyFilterToRunningApps();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to enumerate windows: {ex.Message}");
            }
        }

        private void ApplyFilterToRunningApps()
        {
            string query = TxtFilterRunningApps.Text?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                ListRunningApps.ItemsSource = _allRunningWindows;
            }
            else
            {
                ListRunningApps.ItemsSource = _allRunningWindows.Where(w =>
                    w.Title.ToLowerInvariant().Contains(query) ||
                    w.ProcessName.ToLowerInvariant().Contains(query)
                ).ToList();
            }
        }

        private void BtnRefreshRunningApps_Click(object sender, RoutedEventArgs e)
        {
            RefreshRunningAppsList();
        }

        private void TxtFilterRunningApps_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilterToRunningApps();
        }

        private void BtnQuickBorderless_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RunningWindowItem item)
            {
                _borderlessEngine.ToggleBorderless(item.Hwnd);
                item.IsBorderless = _borderlessEngine.IsBorderless(item.Hwnd);
                btn.Content = item.IsBorderless ? "Khôi phục" : "Bỏ viền";
            }
        }

        private void BtnAddToWhitelist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RunningWindowItem item)
            {
                var curMon = MonitorManager.Instance.GetMonitorForWindow(item.Hwnd);
                var dlg = new AddAppDialog(item.ProcessName, item.Title, curMon.DeviceName)
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true && dlg.ResultRule != null)
                {
                    _settings.BorderlessWhitelist.Add(dlg.ResultRule);
                    _settings.Save();
                    RefreshWhitelistUi();
                }
            }
        }

        private void BtnAddAppManual_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddAppDialog
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.ResultRule != null)
            {
                _settings.BorderlessWhitelist.Add(dlg.ResultRule);
                _settings.Save();
                RefreshWhitelistUi();
            }
        }

        private void WhitelistSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _settings.Save();
        }

        private void BtnApplyWhitelistNow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BorderlessAppRule rule)
            {
                RefreshRunningAppsList();
                var match = _allRunningWindows.FirstOrDefault(w =>
                    string.Equals(w.ProcessName, rule.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                    w.ProcessName.Contains(rule.ProcessName, StringComparison.OrdinalIgnoreCase)
                );

                if (match != null)
                {
                    _borderlessEngine.ApplyBorderless(match.Hwnd, rule);
                    MessageBox.Show(this, $"Đã áp dụng Borderless thành công cho: {match.Title}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(this, $"Không tìm thấy cửa sổ đang chạy của '{rule.ProcessName}'.\nVui lòng mở ứng dụng trước khi áp dụng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnRestoreWhitelistWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BorderlessAppRule rule)
            {
                RefreshRunningAppsList();
                var match = _allRunningWindows.FirstOrDefault(w =>
                    string.Equals(w.ProcessName, rule.ProcessName, StringComparison.OrdinalIgnoreCase)
                );

                if (match != null)
                {
                    _borderlessEngine.RestoreWindow(match.Hwnd);
                    MessageBox.Show(this, $"Đã khôi phục viền gốc cho: {match.Title}", "Khôi phục thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnDeleteWhitelistRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BorderlessAppRule rule)
            {
                var res = MessageBox.Show(this, $"Bạn có chắc muốn xóa '{rule.DisplayName} ({rule.ProcessName})' khỏi Whitelist?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _settings.BorderlessWhitelist.Remove(rule);
                    _settings.Save();
                    RefreshWhitelistUi();
                }
            }
        }

        #endregion

        #region System Tray and Window Lifecycle

        private void BtnMinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            TrayIcon.ShowBalloonTip(
                "Windows Splitter",
                "Ứng dụng đang chạy ngầm trong khay hệ thống.\nBấm phím Win + Arrow để chia vùng, Win + Shift + Arrow để chuyển màn hình!",
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info
            );
        }

        private void TrayIcon_DoubleClick(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
        }

        private void MenuOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void MenuToggleHook_Click(object sender, RoutedEventArgs e)
        {
            _settings.OverrideWindowsSnap = MenuHookStatus.IsChecked;
            ChkOverrideSnap.IsChecked = _settings.OverrideWindowsSnap;
            _keyboardHook.OverrideWindowsSnap = _settings.OverrideWindowsSnap;
            TxtHookStatus.Text = _settings.OverrideWindowsSnap ? "⚡ Win+Arrow: BẬT" : "⚪ Win+Arrow: TẮT";
            _settings.Save();
        }

        private void MenuSnapZone1_Click(object sender, RoutedEventArgs e)
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                _zoneEngine.SnapWindow(fg, KeyDirection.Left);
            }
        }

        private void MenuSnapZone2_Click(object sender, RoutedEventArgs e)
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                _zoneEngine.SnapWindow(fg, KeyDirection.Right);
            }
        }

        private void MenuMoveOtherMonitor_Click(object sender, RoutedEventArgs e)
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                _zoneEngine.MoveWindowToMonitor(fg, KeyDirection.Right);
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            _isReallyExiting = true;
            this.Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isReallyExiting)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }

            // Cleanup
            _keyboardHook.Dispose();
            _winEventHookService.Dispose();
            _borderlessEngine.CleanUp();
            _settings.Save();

            base.OnClosing(e);
        }

        #endregion
    }
}
