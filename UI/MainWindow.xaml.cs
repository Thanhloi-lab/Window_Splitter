using System.ComponentModel;
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
        private bool _isUpdatingUi = true;
        private bool _isReallyExiting = false;

        public MainWindow()
        {
            _isUpdatingUi = true;

            _settings = AppSettings.Load();
            _zoneEngine = new ZoneEngine(_settings);
            _borderlessEngine = new BorderlessEngine(_settings, _zoneEngine);
            _winEventHookService = new WinEventHookService(_settings, _borderlessEngine, _zoneEngine);
            _keyboardHook = new LowLevelKeyboardHook
            {
                OverrideWindowsSnap = _settings.OverrideWindowsSnap
            };

            InitializeComponent();

            // Setup Admin indicator & Language events
            UpdateAdminIndicator();
            LocalizationManager.LanguageChanged += OnLanguageChanged;

            // Setup Hotkey Snap Handler: Win + Arrow (Zones)
            _keyboardHook.HotKeyPressed += (direction) =>
            {
                Dispatcher.Invoke(() =>
                {
                    IntPtr activeHwnd = NativeMethods.GetForegroundWindow();
                    if (activeHwnd != IntPtr.Zero)
                    {
                        // 1. Snap window according to pressed arrow direction
                        _zoneEngine.SnapWindow(activeHwnd, direction);

                        // 2. If it is already borderless or has a whitelist rule, re-apply borderless inside the snapped zone
                        var rule = _borderlessEngine.FindRuleForWindow(activeHwnd);
                        var curZone = _zoneEngine.GetWindowCurrentZone(activeHwnd);

                        if (_borderlessEngine.IsBorderless(activeHwnd))
                        {
                            _borderlessEngine.ApplyBorderless(activeHwnd, rule, curZone);
                        }
                        else if (rule != null && rule.AutoApply)
                        {
                            _borderlessEngine.ApplyBorderless(activeHwnd, rule, curZone);
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

                        if (_borderlessEngine.IsBorderless(activeHwnd))
                        {
                            var curZone = _zoneEngine.GetWindowCurrentZone(activeHwnd);
                            var rule = _borderlessEngine.FindRuleForWindow(activeHwnd);
                            _borderlessEngine.ApplyBorderless(activeHwnd, rule, curZone);
                        }
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

            _isUpdatingUi = false;
            UpdateZoneVisualDisplay();

            // Check if launched with autostart/minimized flags or setting
            string[] args = Environment.GetCommandLineArgs();
            bool isAutoStart = args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase) || a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            if (isAutoStart || _settings.StartMinimizedToTray)
            {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            }
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
            if (App.WM_SHOW_SPLITTER != 0 && msg == (int)App.WM_SHOW_SPLITTER)
            {
                Dispatcher.Invoke(() =>
                {
                    this.Show();
                    if (this.WindowState == WindowState.Minimized)
                    {
                        this.WindowState = WindowState.Normal;
                    }
                    this.Activate();
                    this.Focus();
                });
                handled = true;
                return IntPtr.Zero;
            }

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

        private void UpdateAdminIndicator()
        {
            bool isAdmin = CheckIfRunningAsAdmin();
            if (!isAdmin)
            {
                BadgeAdmin.Background = System.Windows.Media.Brushes.DarkRed;
                BadgeAdmin.BorderBrush = System.Windows.Media.Brushes.Red;
                TxtBadgeAdmin.Text = LocalizationManager.GetString("Str_BadgeNonAdmin");
                TxtBadgeAdmin.Foreground = System.Windows.Media.Brushes.Yellow;
            }
            else
            {
                BadgeAdmin.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D3B26")!;
                BadgeAdmin.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2D6A3E")!;
                TxtBadgeAdmin.Text = LocalizationManager.GetString("Str_BadgeAdmin");
                TxtBadgeAdmin.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#57D97A")!;
            }
        }

        private void UpdateHookStatusText()
        {
            string onText = LocalizationManager.GetString("Str_BadgeHookEnabled");
            string offText = LocalizationManager.GetString("Str_BadgeHookDisabled");
            TxtHookStatus.Text = _settings.OverrideWindowsSnap ? onText : offText;
            MenuHookStatus.IsChecked = _settings.OverrideWindowsSnap;
        }

        private void RefreshMonitorSelector()
        {
            bool wasUpdating = _isUpdatingUi;
            _isUpdatingUi = true;

            var monitors = MonitorManager.Instance.Monitors;
            CmbMonitors.ItemsSource = null;
            CmbMonitors.ItemsSource = monitors;
            CmbMonitors.DisplayMemberPath = "FriendlyName";

            // Duplicate Mode indicator
            if (MonitorManager.Instance.IsDuplicateMode)
            {
                BadgeDuplicateMode.Visibility = Visibility.Visible;
                TxtMonitorTopologyTag.Text = LocalizationManager.GetString("Msg_ModeDuplicate");
            }
            else
            {
                BadgeDuplicateMode.Visibility = Visibility.Collapsed;
                TxtMonitorTopologyTag.Text = monitors.Count > 1 
                    ? LocalizationManager.GetString("Msg_MultiDisplayExtend", monitors.Count) 
                    : LocalizationManager.GetString("Msg_SingleDisplay");
            }

            // Select primary or first
            var target = MonitorManager.Instance.GetPrimaryMonitor() ?? monitors.FirstOrDefault();
            CmbMonitors.SelectedItem = target;
            _currentSelectedMonitor = target;

            _isUpdatingUi = wasUpdating;
        }

        private void RefreshPresetsForCurrentMonitor(string? selectedIdToKeep = null)
        {
            if (_currentSelectedMonitor == null || _settings == null) return;

            var config = _settings.GetOrCreateMonitorConfig(_currentSelectedMonitor.DeviceName, _currentSelectedMonitor.Width, _currentSelectedMonitor.Height);

            bool wasUpdating = _isUpdatingUi;
            _isUpdatingUi = true;

            string targetId = selectedIdToKeep ?? config.SelectedPresetId;

            CmbPresets.ItemsSource = null;
            CmbPresets.ItemsSource = config.Presets;
            CmbPresets.DisplayMemberPath = "DisplayName";
            CmbPresets.SelectedValuePath = "Id";
            CmbPresets.SelectedValue = targetId;

            if (CmbPresets.SelectedIndex < 0 && config.Presets.Count > 0)
            {
                CmbPresets.SelectedIndex = 0;
                config.SelectedPresetId = config.Presets[0].Id;
            }

            _isUpdatingUi = wasUpdating;
        }

        private void CmbMonitors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _settings == null) return;

            if (CmbMonitors.SelectedItem is MonitorInfoItem monitor)
            {
                _currentSelectedMonitor = monitor;
                TxtMonitorDpiBadge.Text = $"DPI: {(int)Math.Round(monitor.DpiScaleX * 100)}%";

                RefreshPresetsForCurrentMonitor();
                UpdateZoneVisualDisplay();
            }
        }

        private void LoadSettingsToUi()
        {
            bool wasUpdating = _isUpdatingUi;
            _isUpdatingUi = true;

            if (_settings != null)
            {
                // Behavior & Multi-monitor options
                ChkOverrideSnap.IsChecked = _settings.OverrideWindowsSnap;
                ChkWinShiftMonitorMove.IsChecked = _settings.EnableWinShiftMonitorMove;
                ChkCrossMonitorWrap.IsChecked = _settings.EnableCrossMonitorWrap;
                ChkInvisibleBorderComp.IsChecked = _settings.DwmInvisibleBorderCompensation;
                ChkDisableRoundedCorners.IsChecked = _settings.DisableWin11RoundedCorners;
                ChkShowZoneOverlay.IsChecked = _settings.ShowZoneOverlayOnSnap;
                ChkStartWithWindows.IsChecked = _settings.StartWithWindows || StartupManager.IsRunAtStartupEnabled();
                ChkStartMinimized.IsChecked = _settings.StartMinimizedToTray;
                ChkCloseToTray.IsChecked = _settings.CloseToTray;
                ChkDefaultCoverTaskbar.IsChecked = _settings.DefaultCoverTaskbar;

                RefreshPresetsForCurrentMonitor();

                // Whitelist
                RefreshWhitelistUi();

                // Language & Status UI
                SyncLanguageSelectorUi();
                UpdateHookStatusText();
                UpdateAdminIndicator();
            }

            _isUpdatingUi = wasUpdating;
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _settings == null) return;
            if (sender is ComboBox cmb && cmb.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string selectedLang = item.Tag.ToString() ?? LocalizationManager.LangEnglish;
                if (selectedLang != LocalizationManager.CurrentLanguage)
                {
                    _settings.Language = selectedLang;
                    _settings.Save();
                    LocalizationManager.SetLanguage(selectedLang, true);
                }
            }
        }

        private void OnLanguageChanged()
        {
            Dispatcher.Invoke(() =>
            {
                bool wasUpdating = _isUpdatingUi;
                _isUpdatingUi = true;

                SyncLanguageSelectorUi();
                UpdateAdminIndicator();
                UpdateHookStatusText();
                RefreshMonitorSelector();
                RefreshPresetsForCurrentMonitor();
                UpdateZoneVisualDisplay();
                RefreshRunningAppsList();
                RefreshWhitelistUi();

                _isUpdatingUi = wasUpdating;
            });
        }

        private void SyncLanguageSelectorUi()
        {
            string currentLang = LocalizationManager.CurrentLanguage;
            SelectLanguageInComboBox(CmbLanguageHeader, currentLang);
            SelectLanguageInComboBox(CmbLanguageSettings, currentLang);
        }

        private static void SelectLanguageInComboBox(ComboBox? cmb, string langCode)
        {
            if (cmb == null) return;
            foreach (var item in cmb.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), langCode, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = item;
                    break;
                }
            }
        }

        private void RefreshWhitelistUi()
        {
            ListWhitelist.ItemsSource = null;
            if (_settings != null)
            {
                ListWhitelist.ItemsSource = _settings.BorderlessWhitelist;
            }
        }

        private ZonePreset? GetCurrentMonitorActivePreset()
        {
            if (_settings == null) return null;
            if (_currentSelectedMonitor == null) return _settings.GetActivePreset();
            return _settings.GetPresetForMonitor(_currentSelectedMonitor);
        }

        private void UpdateZoneVisualDisplay()
        {
            if (_settings == null) return;
            var preset = GetCurrentMonitorActivePreset();
            if (preset == null || preset.Zones.Count == 0) return;

            TxtPresetDescription.Text = preset.Description;

            // Visual feedback: Default Template vs Custom Profile
            if (preset.IsDefault)
            {
                TxtPresetTypeBadge.Text = LocalizationManager.GetString("Str_BadgeDefault");
                TxtPresetTypeBadge.Foreground = System.Windows.Media.Brushes.LightGray;
                BadgePresetType.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#333333")!;

                BtnDeleteProfile.Visibility = Visibility.Collapsed;
                BtnSaveZoneSettings.IsEnabled = false;
                BtnSaveZoneSettings.Content = LocalizationManager.GetString("Str_BtnCannotOverwriteDefault");
                BtnSaveZoneSettings.ToolTip = LocalizationManager.GetString("Str_SavePresetHintDefault");
                TxtSavePresetHint.Visibility = Visibility.Visible;
            }
            else
            {
                TxtPresetTypeBadge.Text = LocalizationManager.GetString("Str_BadgeCustom");
                TxtPresetTypeBadge.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#81C784")!;
                BadgePresetType.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1B5E20")!;

                BtnDeleteProfile.Visibility = Visibility.Visible;
                BtnSaveZoneSettings.IsEnabled = true;
                BtnSaveZoneSettings.Content = LocalizationManager.GetString("Str_BtnUpdateCurrentProfile");
                BtnSaveZoneSettings.ToolTip = LocalizationManager.GetString("Str_BtnUpdateCurrentProfileTip");
                TxtSavePresetHint.Visibility = Visibility.Collapsed;
            }

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

                SliderZoneSplit.Minimum = 50;
                SliderZoneSplit.Maximum = Math.Max(100, totalW - 50);
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
            if (_isUpdatingUi || _settings == null) return;

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
            if (_isUpdatingUi || _settings == null) return;

            var preset = GetCurrentMonitorActivePreset();
            if (preset == null || preset.Zones.Count < 2) return;

            int newZ1Width = (int)Math.Round(SliderZoneSplit.Value);
            int totalWidth = _currentSelectedMonitor?.Width ?? (preset.Zones[0].Width + preset.Zones[1].Width);
            if (totalWidth <= 0) totalWidth = preset.ScreenWidth;

            int newZ2Width = Math.Max(50, totalWidth - newZ1Width);

            // If it is a custom profile, update in-memory immediately.
            // If it is a default preset, do NOT mutate default preset in-memory, just update UI preview!
            if (!preset.IsDefault)
            {
                preset.Zones[0].Width = newZ1Width;
                preset.Zones[1].X = newZ1Width;
                preset.Zones[1].Width = newZ2Width;
            }

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
            if (_isUpdatingUi || _settings == null) return;

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
                // Only mutate in-memory if custom profile!
                if (!preset.IsDefault)
                {
                    preset.Zones[0].X = z1x;
                    preset.Zones[0].Y = z1y;
                    preset.Zones[0].Width = z1w;
                    preset.Zones[0].Height = z1h;

                    preset.Zones[1].X = z2x;
                    preset.Zones[1].Y = z2y;
                    preset.Zones[1].Width = z2w;
                    preset.Zones[1].Height = z2h;
                }

                ColVisualZone1.Width = new GridLength(Math.Max(1, z1w), GridUnitType.Star);
                ColVisualZone2.Width = new GridLength(Math.Max(1, z2w), GridUnitType.Star);

                TxtVisualZone1Size.Text = $"{z1w} × {z1h} px";
                TxtVisualZone2Size.Text = $"{z2w} × {z2h} px";
            }
        }

        private void BtnSaveAsNewProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSelectedMonitor == null || _settings == null) return;

            var config = _settings.GetOrCreateMonitorConfig(_currentSelectedMonitor.DeviceName, _currentSelectedMonitor.Width, _currentSelectedMonitor.Height);
            var activePreset = GetCurrentMonitorActivePreset();

            // Read current values from textboxes
            int z1x = int.TryParse(TxtZone1X.Text, out int parsedZ1X) ? parsedZ1X : 0;
            int z1y = int.TryParse(TxtZone1Y.Text, out int parsedZ1Y) ? parsedZ1Y : 0;
            int z1w = int.TryParse(TxtZone1W.Text, out int parsedZ1W) ? parsedZ1W : (int)(config.ScreenWidth / 3.0);
            int z1h = int.TryParse(TxtZone1H.Text, out int parsedZ1H) ? parsedZ1H : config.ScreenHeight;

            int z2x = int.TryParse(TxtZone2X.Text, out int parsedZ2X) ? parsedZ2X : z1w;
            int z2y = int.TryParse(TxtZone2Y.Text, out int parsedZ2Y) ? parsedZ2Y : 0;
            int z2w = int.TryParse(TxtZone2W.Text, out int parsedZ2W) ? parsedZ2W : (config.ScreenWidth - z1w);
            int z2h = int.TryParse(TxtZone2H.Text, out int parsedZ2H) ? parsedZ2H : config.ScreenHeight;

            var zones = new List<Zone>
            {
                new Zone(1, "Vùng 1 (Trái)", z1x, z1y, z1w, z1h),
                new Zone(2, "Vùng 2 (Phải)", z2x, z2y, z2w, z2h)
            };

            string defaultName = activePreset != null && !activePreset.IsDefault
                ? $"{activePreset.Name} (Copy)"
                : $"Profile {z1w}x{z2w}";

            var dlg = new SaveProfileDialog(defaultName, config.ScreenWidth, config.ScreenHeight, zones, _currentSelectedMonitor.FriendlyName)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.ResultPreset != null)
            {
                config.Presets.Add(dlg.ResultPreset);
                config.SelectedPresetId = dlg.ResultPreset.Id;
                _settings.Save();

                RefreshPresetsForCurrentMonitor(dlg.ResultPreset.Id);
                UpdateZoneVisualDisplay();

                string msg = LocalizationManager.GetString("Msg_ProfileSaveSuccess", dlg.ResultPreset.Name);
                string title = LocalizationManager.GetString("Msg_Notice");
                MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSaveZoneSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null || _currentSelectedMonitor == null) return;
            var preset = GetCurrentMonitorActivePreset();
            if (preset == null) return;

            if (preset.IsDefault)
            {
                // Cannot overwrite default preset! Redirect to Save as new profile
                BtnSaveAsNewProfile_Click(sender, e);
                return;
            }

            // Save changes to custom profile
            if (int.TryParse(TxtZone1X.Text, out int z1x) &&
                int.TryParse(TxtZone1Y.Text, out int z1y) &&
                int.TryParse(TxtZone1W.Text, out int z1w) &&
                int.TryParse(TxtZone1H.Text, out int z1h) &&
                int.TryParse(TxtZone2X.Text, out int z2x) &&
                int.TryParse(TxtZone2Y.Text, out int z2y) &&
                int.TryParse(TxtZone2W.Text, out int z2w) &&
                int.TryParse(TxtZone2H.Text, out int z2h))
            {
                if (preset.Zones.Count >= 2)
                {
                    preset.Zones[0].X = z1x;
                    preset.Zones[0].Y = z1y;
                    preset.Zones[0].Width = z1w;
                    preset.Zones[0].Height = z1h;

                    preset.Zones[1].X = z2x;
                    preset.Zones[1].Y = z2y;
                    preset.Zones[1].Width = z2w;
                    preset.Zones[1].Height = z2h;
                }

                _settings.Save();
                RefreshPresetsForCurrentMonitor(preset.Id);
                UpdateZoneVisualDisplay();

                string msg = LocalizationManager.GetString("Msg_ProfileUpdateSuccess", preset.Name);
                string title = LocalizationManager.GetString("Msg_Notice");
                MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null || _currentSelectedMonitor == null) return;
            var config = _settings.GetOrCreateMonitorConfig(_currentSelectedMonitor.DeviceName, _currentSelectedMonitor.Width, _currentSelectedMonitor.Height);
            var preset = GetCurrentMonitorActivePreset();

            if (preset == null) return;

            if (preset.IsDefault)
            {
                MessageBox.Show(this, LocalizationManager.GetString("Msg_CannotDeleteDefault"), LocalizationManager.GetString("Msg_Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string confirmMsg = LocalizationManager.GetString("Msg_ConfirmDeleteProfile", preset.Name);
            string confirmTitle = LocalizationManager.GetString("Msg_ConfirmDeleteTitle");
            var confirm = MessageBox.Show(this, confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                config.Presets.Remove(preset);
                var fallback = config.Presets.FirstOrDefault();
                config.SelectedPresetId = fallback?.Id ?? "";
                _settings.Save();

                RefreshPresetsForCurrentMonitor(fallback?.Id);
                UpdateZoneVisualDisplay();

                MessageBox.Show(this, LocalizationManager.GetString("Msg_ProfileDeleteSuccess", preset.Name), LocalizationManager.GetString("Msg_Notice"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ChkStartWithWindows_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _settings == null) return;
            bool enable = ChkStartWithWindows.IsChecked ?? false;
            _settings.StartWithWindows = enable;
            _settings.Save();

            bool success = StartupManager.SetRunAtStartup(enable, _settings.StartMinimizedToTray);
            if (enable && !success)
            {
                MessageBox.Show(this, LocalizationManager.GetString("Msg_TaskSchedulerFailed"), LocalizationManager.GetString("Msg_Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnDetectScreen_Click(object sender, RoutedEventArgs e)
        {
            MonitorManager.Instance.RefreshMonitors();
            RefreshMonitorSelector();
            UpdateZoneVisualDisplay();

            int count = MonitorManager.Instance.Monitors.Count;
            string mode = MonitorManager.Instance.IsDuplicateMode 
                ? LocalizationManager.GetString("Msg_ModeDuplicate") 
                : LocalizationManager.GetString("Msg_ModeExtend");
            MessageBox.Show(this, LocalizationManager.GetString("Msg_MonitorRefreshed", count, mode), LocalizationManager.GetString("Msg_Notice"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SettingChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _settings == null) return;

            _settings.OverrideWindowsSnap = ChkOverrideSnap.IsChecked ?? true;
            _settings.EnableWinShiftMonitorMove = ChkWinShiftMonitorMove.IsChecked ?? true;
            _settings.EnableCrossMonitorWrap = ChkCrossMonitorWrap.IsChecked ?? true;
            _settings.DwmInvisibleBorderCompensation = ChkInvisibleBorderComp.IsChecked ?? true;
            _settings.DisableWin11RoundedCorners = ChkDisableRoundedCorners.IsChecked ?? true;
            _settings.ShowZoneOverlayOnSnap = ChkShowZoneOverlay.IsChecked ?? true;
            _settings.StartMinimizedToTray = ChkStartMinimized.IsChecked ?? false;
            _settings.CloseToTray = ChkCloseToTray.IsChecked ?? false;

            _keyboardHook.OverrideWindowsSnap = _settings.OverrideWindowsSnap;
            UpdateHookStatusText();

            _settings.Save();
        }

        private void ChkDefaultCoverTaskbar_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _settings == null) return;
            _settings.DefaultCoverTaskbar = ChkDefaultCoverTaskbar.IsChecked ?? true;
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
            }
        }

        private void BtnAddToWhitelist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RunningWindowItem item)
            {
                var curMon = MonitorManager.Instance.GetMonitorForWindow(item.Hwnd);
                var curZone = _zoneEngine.GetWindowCurrentZone(item.Hwnd);
                int zoneId = curZone?.Id ?? 2;
                var dlg = new AddAppDialog(item.ProcessName, item.Title, curMon.DeviceName, zoneId)
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
            if (_isUpdatingUi || _settings == null) return;
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
                    string msg = LocalizationManager.GetString("Msg_AppliedBorderlessSuccess", match.Title);
                    string title = LocalizationManager.GetString("Msg_Notice");
                    MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string msg = LocalizationManager.GetString("Msg_AppWindowNotFound", rule.ProcessName);
                    string title = LocalizationManager.GetString("Msg_Notice");
                    MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    match.IsBorderless = _borderlessEngine.IsBorderless(match.Hwnd);
                    string msg = LocalizationManager.GetString("Msg_RestoredWindowSuccess", match.Title);
                    string title = LocalizationManager.GetString("Msg_Notice");
                    MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnDeleteWhitelistRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BorderlessAppRule rule)
            {
                string confirmMsg = LocalizationManager.GetString("Msg_ConfirmDeleteRule", rule.DisplayName, rule.ProcessName);
                string confirmTitle = LocalizationManager.GetString("Msg_ConfirmDeleteTitle");
                var res = MessageBox.Show(this, confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                LocalizationManager.GetString("Msg_BalloonTipContent"),
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
            if (MainTabControl != null)
            {
                MainTabControl.SelectedIndex = 2; // Switch directly to Settings tab
            }
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void MenuToggleHook_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
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
            if (!_isReallyExiting && _settings?.CloseToTray == true)
            {
                e.Cancel = true;
                this.Hide();
                TrayIcon.ShowBalloonTip("WindowsSplitter", "Ứng dụng đang chạy ngầm trong khay hệ thống.", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                return;
            }

            // Cleanup: unhook and restore all borderless windows to standard state
            _keyboardHook.Dispose();
            _winEventHookService.Dispose();
            _borderlessEngine.CleanUp();
            _settings?.Save();

            base.OnClosing(e);
        }

        #endregion
    }
}
