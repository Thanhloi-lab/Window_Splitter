using System.ComponentModel;
using System.Globalization;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
        private readonly TopEdgeSnapGuard _topEdgeSnapGuard;
        private ZoneOverlayWindow? _overlayWindow;

        private List<RunningWindowItem> _allRunningWindows = new();
        private MonitorInfoItem? _currentSelectedMonitor;
        private ZonePreset? _workingPreset;
        private int _selectedZoneId = 1;
        private bool _usePercentUnit = false;
        private double _previewScale = 1.0;
        private double _previewHostWidth = 960;
        private double _previewHostHeight = 212;
        private SplitDividerInfo? _draggingDivider;
        private Point _dragStartPoint;
        private int _dragStartLinePos;
        private bool _isUpdatingUi = true;
        private bool _isReallyExiting = false;

        public MainWindow()
        {
            _isUpdatingUi = true;

            _settings = AppSettings.Load();
            _zoneEngine = new ZoneEngine(_settings);
            _borderlessEngine = new BorderlessEngine(_settings, _zoneEngine);
            _winEventHookService = new WinEventHookService(_borderlessEngine);
            _keyboardHook = new LowLevelKeyboardHook
            {
                OverrideWindowsSnap = _settings.OverrideWindowsSnap,
                EnableWinShiftMonitorMove = _settings.EnableWinShiftMonitorMove
            };
            _topEdgeSnapGuard = new TopEdgeSnapGuard(_settings, _zoneEngine);

            InitializeComponent();

            _borderlessEngine.BorderlessStateChanged += (hWnd, isBorderless) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    var item = _allRunningWindows.FirstOrDefault(window => window.Hwnd == hWnd);
                    if (item != null)
                    {
                        item.IsBorderless = isBorderless;
                    }

                    if (isBorderless)
                    {
                        new BorderlessNoticeWindow().ShowForWindow(hWnd);
                    }
                });
            };

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

                        // Re-apply borderless inside the snapped zone when already borderless.
                        var curZone = _zoneEngine.GetWindowCurrentZone(activeHwnd);

                        if (_borderlessEngine.IsBorderless(activeHwnd))
                        {
                            _borderlessEngine.ApplyBorderless(activeHwnd, curZone);
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
                            _borderlessEngine.ApplyBorderless(activeHwnd, curZone);
                        }
                    }
                });
            };

            // Win + Ctrl + B: toggle borderless mode for the active window.
            _keyboardHook.BorderlessTogglePressed += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    var activeHwnd = NativeMethods.GetForegroundWindow();
                    _borderlessEngine.ToggleBorderless(activeHwnd);
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
            _topEdgeSnapGuard.Start();

            CanvasZoneEditor.MouseMove += CanvasZoneEditor_MouseMove;
            CanvasZoneEditor.MouseLeftButtonUp += CanvasZoneEditor_MouseLeftButtonUp;
            CanvasZoneEditor.LostMouseCapture += CanvasZoneEditor_LostMouseCapture;

            // Populate UI
            RefreshMonitorSelector();
            LoadSettingsToUi();
            RefreshRunningAppsList();

            _isUpdatingUi = false;
            SyncWorkingPresetFromSelection();
            UpdateZoneVisualDisplay();

            // Start in the tray when explicitly requested or configured in Settings.
            string[] args = Environment.GetCommandLineArgs();
            bool hasMinimizedArgument = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            if (hasMinimizedArgument || _settings.StartMinimizedToTray)
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
                    RefreshPresetsForCurrentMonitor();
                    SyncWorkingPresetFromSelection();
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
            if (target != null)
            {
                TxtMonitorDpiBadge.Text = $"DPI: {(int)Math.Round(target.DpiScaleX * 100)}%";
            }

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
                SyncWorkingPresetFromSelection();
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
                ChkPreventTopEdgeMaximize.IsChecked = _settings.PreventAccidentalTopEdgeMaximize;
                ChkStartWithWindows.IsChecked = _settings.StartWithWindows || StartupManager.IsRunAtStartupEnabled();
                ChkStartMinimized.IsChecked = _settings.StartMinimizedToTray;
                ChkCloseToTray.IsChecked = _settings.CloseToTray;
                RefreshPresetsForCurrentMonitor();

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

        private ZonePreset? GetCurrentMonitorActivePreset()
        {
            if (_settings == null) return null;
            if (_currentSelectedMonitor == null) return _settings.GetActivePreset();
            return _settings.GetPresetForMonitor(_currentSelectedMonitor);
        }

        private (int Width, int Height) GetPreviewMonitorDimensions()
        {
            if (_currentSelectedMonitor != null && _currentSelectedMonitor.WorkArea.Width > 0 && _currentSelectedMonitor.WorkArea.Height > 0)
            {
                return (_currentSelectedMonitor.WorkArea.Width, _currentSelectedMonitor.WorkArea.Height);
            }
            if (_currentSelectedMonitor != null && _currentSelectedMonitor.Width > 0 && _currentSelectedMonitor.Height > 0)
            {
                return (_currentSelectedMonitor.Width, _currentSelectedMonitor.Height);
            }
            return (1920, 1080);
        }

        private void SyncWorkingPresetFromSelection()
        {
            var preset = GetCurrentMonitorActivePreset();
            if (preset == null)
            {
                _workingPreset = null;
                return;
            }

            var (monW, monH) = GetPreviewMonitorDimensions();
            _workingPreset = preset.IsDefault ? preset.Clone() : preset;
            _workingPreset.EnsureSplitTreeAndSync(monW, monH);

            if (_workingPreset.Zones.All(z => z.Id != _selectedZoneId))
            {
                _selectedZoneId = _workingPreset.Zones.FirstOrDefault()?.Id ?? 1;
            }
        }

        private void UpdateZoneVisualDisplay()
        {
            if (_settings == null) return;
            var activePreset = GetCurrentMonitorActivePreset();
            if (activePreset == null) return;

            if (_workingPreset == null || _workingPreset.Id != activePreset.Id)
            {
                SyncWorkingPresetFromSelection();
            }

            if (_workingPreset == null || _workingPreset.Zones.Count == 0) return;

            var (monW, monH) = GetPreviewMonitorDimensions();
            _workingPreset.EnsureSplitTreeAndSync(monW, monH);

            TxtPresetDescription.Text = activePreset.Description;

            if (activePreset.IsDefault)
            {
                TxtPresetTypeBadge.Text = LocalizationManager.GetString("Str_BadgeDefault");
                TxtPresetTypeBadge.Foreground = Brushes.LightGray;
                BadgePresetType.Background = (Brush)new BrushConverter().ConvertFromString("#333333")!;

                BtnDeleteProfile.Visibility = Visibility.Collapsed;
                BtnSaveZoneSettings.IsEnabled = false;
                BtnSaveZoneSettings.Content = LocalizationManager.GetString("Str_BtnCannotOverwriteDefault");
                BtnSaveZoneSettings.ToolTip = LocalizationManager.GetString("Str_SavePresetHintDefault");
                TxtSavePresetHint.Visibility = Visibility.Visible;
            }
            else
            {
                TxtPresetTypeBadge.Text = LocalizationManager.GetString("Str_BadgeCustom");
                TxtPresetTypeBadge.Foreground = (Brush)new BrushConverter().ConvertFromString("#81C784")!;
                BadgePresetType.Background = (Brush)new BrushConverter().ConvertFromString("#1B5E20")!;

                BtnDeleteProfile.Visibility = Visibility.Visible;
                BtnSaveZoneSettings.IsEnabled = true;
                BtnSaveZoneSettings.Content = LocalizationManager.GetString("Str_BtnUpdateCurrentProfile");
                BtnSaveZoneSettings.ToolTip = LocalizationManager.GetString("Str_BtnUpdateCurrentProfileTip");
                TxtSavePresetHint.Visibility = Visibility.Collapsed;
            }

            bool wasUpdating = _isUpdatingUi;
            _isUpdatingUi = true;

            SliderZoneGap.Value = _workingPreset.ZoneGap;
            TxtZoneGapValue.Text = $"{_workingPreset.ZoneGap} px";

            bool isEn = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
            int count = _workingPreset.Zones.Count;
            TxtZoneCountBadge.Text = isEn ? $"{count} Zones" : $"{count} Vùng";
            BtnRemoveZone.IsEnabled = count > 1;

            CmbSelectedZone.ItemsSource = _workingPreset.Zones;
            CmbSelectedZone.DisplayMemberPath = "LocalizedName";
            CmbSelectedZone.SelectedValuePath = "Id";
            if (_workingPreset.Zones.All(z => z.Id != _selectedZoneId))
            {
                _selectedZoneId = _workingPreset.Zones[0].Id;
            }
            CmbSelectedZone.SelectedValue = _selectedZoneId;

            _isUpdatingUi = wasUpdating;

            RenderZoneCanvas();
            PopulateSelectedZoneInspector();
        }

        private void RenderZoneCanvas()
        {
            if (_workingPreset == null) return;

            var (monW, monH) = GetPreviewMonitorDimensions();
            _workingPreset.EnsureSplitTreeAndSync(monW, monH);

            double availW = Math.Max(120, _previewHostWidth);
            double availH = Math.Max(80, _previewHostHeight);
            _previewScale = Math.Min(availW / monW, availH / monH);

            CanvasZoneEditor.Width = Math.Max(60, monW * _previewScale);
            CanvasZoneEditor.Height = Math.Max(40, monH * _previewScale);
            CanvasZoneEditor.Children.Clear();

            var selectedBg = (Brush)new BrushConverter().ConvertFromString("#330078D4")!;
            var selectedBorder = (Brush)new BrushConverter().ConvertFromString("#4CC2FF")!;
            var normalBg = (Brush)new BrushConverter().ConvertFromString("#221E3557")!;
            var normalBorder = (Brush)new BrushConverter().ConvertFromString("#3B6BA5")!;

            foreach (var zone in _workingPreset.Zones)
            {
                bool isSelected = zone.Id == _selectedZoneId;
                double zx = zone.X * _previewScale;
                double zy = zone.Y * _previewScale;
                double zw = Math.Max(16, zone.Width * _previewScale);
                double zh = Math.Max(16, zone.Height * _previewScale);

                var labelStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                labelStack.Children.Add(new TextBlock
                {
                    Text = zone.LocalizedName,
                    FontSize = zw < 90 || zh < 55 ? 10 : 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = isSelected ? selectedBorder : Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                if (zw >= 72 && zh >= 48)
                {
                    labelStack.Children.Add(new TextBlock
                    {
                        Text = $"{zone.Width} x {zone.Height}",
                        FontSize = 10,
                        Foreground = Brushes.LightGray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }

                if (zw >= 90 && zh >= 66)
                {
                    labelStack.Children.Add(new TextBlock
                    {
                        Text = $"{zone.PercentWidth:0.#}% x {zone.PercentHeight:0.#}%",
                        FontSize = 10,
                        Foreground = Brushes.Silver,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                }

                var zoneTile = new Border
                {
                    Width = zw,
                    Height = zh,
                    Background = isSelected ? selectedBg : normalBg,
                    BorderBrush = isSelected ? selectedBorder : normalBorder,
                    BorderThickness = new Thickness(isSelected ? 2 : 1),
                    CornerRadius = new CornerRadius(3),
                    Cursor = Cursors.Hand,
                    Tag = zone.Id,
                    Child = labelStack
                };

                zoneTile.MouseLeftButtonDown += (s, e) =>
                {
                    if (s is Border b && b.Tag is int zId)
                    {
                        _selectedZoneId = zId;
                        UpdateZoneVisualDisplay();
                        e.Handled = true;
                    }
                };

                Canvas.SetLeft(zoneTile, zx);
                Canvas.SetTop(zoneTile, zy);
                CanvasZoneEditor.Children.Add(zoneTile);
            }

            if (_workingPreset.SplitRoot != null)
            {
                var dividers = _workingPreset.SplitRoot.GetDividers(monW, monH, _workingPreset.ZoneGap, minLeafPx: 80);
                var dividerIdleBrush = (Brush)new BrushConverter().ConvertFromString("#40FFFFFF")!;
                var dividerHoverBrush = (Brush)new BrushConverter().ConvertFromString("#CC4CC2FF")!;

                foreach (var div in dividers)
                {
                    var handle = new Border
                    {
                        Background = Brushes.Transparent,
                        Tag = div,
                        Cursor = div.Orientation == SplitOrientation.Vertical ? Cursors.SizeWE : Cursors.SizeNS
                    };

                    var lineVisual = new Border
                    {
                        Background = dividerIdleBrush,
                        CornerRadius = new CornerRadius(1)
                    };

                    if (div.Orientation == SplitOrientation.Vertical)
                    {
                        handle.Width = 10;
                        handle.Height = Math.Max(12, (div.SpanEnd - div.SpanStart) * _previewScale);
                        lineVisual.Width = 2;
                        lineVisual.HorizontalAlignment = HorizontalAlignment.Center;
                        Canvas.SetLeft(handle, div.LinePos * _previewScale - 5);
                        Canvas.SetTop(handle, div.SpanStart * _previewScale);
                    }
                    else
                    {
                        handle.Width = Math.Max(12, (div.SpanEnd - div.SpanStart) * _previewScale);
                        handle.Height = 10;
                        lineVisual.Height = 2;
                        lineVisual.VerticalAlignment = VerticalAlignment.Center;
                        Canvas.SetLeft(handle, div.SpanStart * _previewScale);
                        Canvas.SetTop(handle, div.LinePos * _previewScale - 5);
                    }

                    handle.Child = lineVisual;
                    handle.MouseEnter += (_, _) => lineVisual.Background = dividerHoverBrush;
                    handle.MouseLeave += (_, _) => lineVisual.Background = dividerIdleBrush;
                    handle.MouseLeftButtonDown += (s, e) =>
                    {
                        if (s is Border b && b.Tag is SplitDividerInfo info)
                        {
                            _draggingDivider = info;
                            _dragStartPoint = e.GetPosition(CanvasZoneEditor);
                            _dragStartLinePos = info.LinePos;
                            CanvasZoneEditor.CaptureMouse();
                            e.Handled = true;
                        }
                    };

                    Canvas.SetZIndex(handle, 10);
                    CanvasZoneEditor.Children.Add(handle);
                }
            }
        }

        private void CanvasZoneEditor_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingDivider == null || _workingPreset == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var curPt = e.GetPosition(CanvasZoneEditor);
            double scale = Math.Max(0.01, _previewScale);

            int deltaPx = _draggingDivider.Orientation == SplitOrientation.Vertical
                ? (int)Math.Round((curPt.X - _dragStartPoint.X) / scale)
                : (int)Math.Round((curPt.Y - _dragStartPoint.Y) / scale);

            int avail = _draggingDivider.ParentSize - _draggingDivider.Gap;
            if (avail <= 0) return;

            int minPos = _draggingDivider.ParentStart + _draggingDivider.MinFirstSize + _draggingDivider.Gap / 2;
            int maxPos = _draggingDivider.ParentStart + avail - _draggingDivider.MinSecondSize + _draggingDivider.Gap / 2;
            int newLinePos = minPos <= maxPos
                ? Math.Clamp(_dragStartLinePos + deltaPx, minPos, maxPos)
                : _draggingDivider.ParentStart + avail / 2;

            int newFirstSize = newLinePos - _draggingDivider.ParentStart - _draggingDivider.Gap / 2;
            _draggingDivider.Node.SplitRatio = Math.Clamp(newFirstSize / (double)avail, 0.02, 0.98);

            var (monW, monH) = GetPreviewMonitorDimensions();
            _workingPreset.EnsureSplitTreeAndSync(monW, monH);

            RenderZoneCanvas();
            PopulateSelectedZoneInspector();
        }

        private void CanvasZoneEditor_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingDivider != null)
            {
                _draggingDivider = null;
                if (CanvasZoneEditor.IsMouseCaptured)
                    CanvasZoneEditor.ReleaseMouseCapture();
            }
        }

        private void CanvasZoneEditor_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _draggingDivider = null;
        }

        private void ZonePreviewHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _previewHostWidth = Math.Max(120, e.NewSize.Width - 24);
            _previewHostHeight = Math.Max(80, e.NewSize.Height - 24);
            if (!_isUpdatingUi)
            {
                RenderZoneCanvas();
            }
        }

        private void PopulateSelectedZoneInspector()
        {
            if (_workingPreset == null) return;
            var zone = _workingPreset.Zones.FirstOrDefault(z => z.Id == _selectedZoneId) ?? _workingPreset.Zones.FirstOrDefault();
            if (zone == null) return;

            bool wasUpdating = _isUpdatingUi;
            _isUpdatingUi = true;

            if (_usePercentUnit)
            {
                LblCoordX.Text = LocalizationManager.GetString("Str_CoordPctX");
                LblCoordY.Text = LocalizationManager.GetString("Str_CoordPctY");
                LblCoordW.Text = LocalizationManager.GetString("Str_CoordPctWidth");
                LblCoordH.Text = LocalizationManager.GetString("Str_CoordPctHeight");

                TxtZoneX.Text = zone.PercentX.ToString("0.##", CultureInfo.InvariantCulture);
                TxtZoneY.Text = zone.PercentY.ToString("0.##", CultureInfo.InvariantCulture);
                TxtZoneW.Text = zone.PercentWidth.ToString("0.##", CultureInfo.InvariantCulture);
                TxtZoneH.Text = zone.PercentHeight.ToString("0.##", CultureInfo.InvariantCulture);
            }
            else
            {
                LblCoordX.Text = LocalizationManager.GetString("Str_CoordX");
                LblCoordY.Text = LocalizationManager.GetString("Str_CoordY");
                LblCoordW.Text = LocalizationManager.GetString("Str_CoordWidth");
                LblCoordH.Text = LocalizationManager.GetString("Str_CoordHeight");

                TxtZoneX.Text = zone.X.ToString(CultureInfo.InvariantCulture);
                TxtZoneY.Text = zone.Y.ToString(CultureInfo.InvariantCulture);
                TxtZoneW.Text = zone.Width.ToString(CultureInfo.InvariantCulture);
                TxtZoneH.Text = zone.Height.ToString(CultureInfo.InvariantCulture);
            }

            _isUpdatingUi = wasUpdating;
        }

        private void CmbPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _settings == null) return;

            if (CmbPresets.SelectedItem is ZonePreset preset && _currentSelectedMonitor != null)
            {
                var config = _settings.GetOrCreateMonitorConfig(_currentSelectedMonitor.DeviceName, _currentSelectedMonitor.Width, _currentSelectedMonitor.Height);
                config.SelectedPresetId = preset.Id;
                _settings.Save();
                _selectedZoneId = 1;
                SyncWorkingPresetFromSelection();
                UpdateZoneVisualDisplay();
            }
        }

        private void CmbSelectedZone_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _workingPreset == null) return;
            if (CmbSelectedZone.SelectedValue is int zoneId)
            {
                _selectedZoneId = zoneId;
                RenderZoneCanvas();
                PopulateSelectedZoneInspector();
            }
        }

        private void UnitMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _usePercentUnit = RdoUnitPercent.IsChecked == true;
            PopulateSelectedZoneInspector();
        }

        private void BtnSplitVertical_Click(object sender, RoutedEventArgs e)
        {
            SplitSelectedZone(SplitOrientation.Vertical);
        }

        private void BtnSplitHorizontal_Click(object sender, RoutedEventArgs e)
        {
            SplitSelectedZone(SplitOrientation.Horizontal);
        }

        private void SplitSelectedZone(SplitOrientation orientation)
        {
            if (_workingPreset?.SplitRoot == null) return;
            var (monW, monH) = GetPreviewMonitorDimensions();

            if (_workingPreset.SplitRoot.SplitLeaf(_selectedZoneId, orientation, 0.5))
            {
                _workingPreset.EnsureSplitTreeAndSync(monW, monH);
                UpdateZoneVisualDisplay();
            }
        }

        private void BtnRemoveZone_Click(object sender, RoutedEventArgs e)
        {
            if (_workingPreset?.SplitRoot == null || _workingPreset.SplitRoot.CountLeaves() <= 1)
                return;

            var (monW, monH) = GetPreviewMonitorDimensions();
            if (_workingPreset.SplitRoot.RemoveLeaf(_selectedZoneId))
            {
                _workingPreset.EnsureSplitTreeAndSync(monW, monH);
                _selectedZoneId = Math.Clamp(_selectedZoneId, 1, _workingPreset.Zones.Count);
                UpdateZoneVisualDisplay();
            }
        }

        private void SliderZoneGap_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _workingPreset == null) return;

            int newGap = (int)Math.Round(SliderZoneGap.Value);
            _workingPreset.ZoneGap = newGap;
            TxtZoneGapValue.Text = $"{newGap} px";

            var (monW, monH) = GetPreviewMonitorDimensions();
            _workingPreset.EnsureSplitTreeAndSync(monW, monH);
            RenderZoneCanvas();
            PopulateSelectedZoneInspector();
        }

        private void ZoneInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyZoneBoundsFromInputs();
                e.Handled = true;
            }
        }

        private void BtnApplyZoneBounds_Click(object sender, RoutedEventArgs e)
        {
            ApplyZoneBoundsFromInputs();
        }

        private static bool TryParseFlexibleDouble(string? text, out double value)
        {
            string normalized = (text ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private void ApplyZoneBoundsFromInputs()
        {
            if (_workingPreset?.SplitRoot == null) return;
            if (!TryParseFlexibleDouble(TxtZoneX.Text, out double rawX) ||
                !TryParseFlexibleDouble(TxtZoneY.Text, out double rawY) ||
                !TryParseFlexibleDouble(TxtZoneW.Text, out double rawW) ||
                !TryParseFlexibleDouble(TxtZoneH.Text, out double rawH))
            {
                return;
            }

            var (monW, monH) = GetPreviewMonitorDimensions();

            int pxX, pxY, pxW, pxH;
            if (_usePercentUnit)
            {
                pxX = (int)Math.Round((Math.Clamp(rawX, 0, 100) / 100.0) * monW);
                pxY = (int)Math.Round((Math.Clamp(rawY, 0, 100) / 100.0) * monH);
                pxW = (int)Math.Round((Math.Clamp(rawW, 1, 100) / 100.0) * monW);
                pxH = (int)Math.Round((Math.Clamp(rawH, 1, 100) / 100.0) * monH);
            }
            else
            {
                pxX = (int)Math.Round(rawX);
                pxY = (int)Math.Round(rawY);
                pxW = (int)Math.Round(rawW);
                pxH = (int)Math.Round(rawH);
            }

            _workingPreset.SplitRoot.TryApplyZoneBounds(
                _selectedZoneId,
                pxX, pxY, pxW, pxH,
                monW, monH,
                _workingPreset.ZoneGap,
                minLeafPx: 80);

            _workingPreset.EnsureSplitTreeAndSync(monW, monH);
            RenderZoneCanvas();
            PopulateSelectedZoneInspector();
        }

        private void BtnSaveAsNewProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSelectedMonitor == null || _settings == null || _workingPreset == null) return;

            var config = _settings.GetOrCreateMonitorConfig(_currentSelectedMonitor.DeviceName, _currentSelectedMonitor.Width, _currentSelectedMonitor.Height);
            var activePreset = GetCurrentMonitorActivePreset();
            var (monW, monH) = GetPreviewMonitorDimensions();
            _workingPreset.EnsureSplitTreeAndSync(monW, monH);

            string defaultName = activePreset != null && !activePreset.IsDefault
                ? $"{activePreset.Name} (Copy)"
                : $"Profile {_workingPreset.Zones.Count}-Zone";

            var dlg = new SaveProfileDialog(
                defaultName,
                monW,
                monH,
                _workingPreset.Zones,
                _currentSelectedMonitor.FriendlyName,
                _workingPreset.SplitRoot,
                _workingPreset.ZoneGap)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.ResultPreset != null)
            {
                config.Presets.Add(dlg.ResultPreset);
                config.SelectedPresetId = dlg.ResultPreset.Id;
                _settings.Save();

                RefreshPresetsForCurrentMonitor(dlg.ResultPreset.Id);
                SyncWorkingPresetFromSelection();
                UpdateZoneVisualDisplay();

                string msg = LocalizationManager.GetString("Msg_ProfileSaveSuccess", dlg.ResultPreset.Name);
                string title = LocalizationManager.GetString("Msg_Notice");
                MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSaveZoneSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null || _currentSelectedMonitor == null || _workingPreset == null) return;
            var preset = GetCurrentMonitorActivePreset();
            if (preset == null) return;

            if (preset.IsDefault)
            {
                BtnSaveAsNewProfile_Click(sender, e);
                return;
            }

            ApplyZoneBoundsFromInputs();
            var (monW, monH) = GetPreviewMonitorDimensions();
            preset.EnsureSplitTreeAndSync(monW, monH);

            _settings.Save();
            RefreshPresetsForCurrentMonitor(preset.Id);
            SyncWorkingPresetFromSelection();
            UpdateZoneVisualDisplay();

            string msg = LocalizationManager.GetString("Msg_ProfileUpdateSuccess", preset.Name);
            string title = LocalizationManager.GetString("Msg_Notice");
            MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
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
                SyncWorkingPresetFromSelection();
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
            if (enable && (!success || !StartupManager.IsTaskSchedulerEnabled()))
            {
                MessageBox.Show(this, LocalizationManager.GetString("Msg_TaskSchedulerFailed"), LocalizationManager.GetString("Msg_Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnDetectScreen_Click(object sender, RoutedEventArgs e)
        {
            MonitorManager.Instance.RefreshMonitors();
            RefreshMonitorSelector();
            RefreshPresetsForCurrentMonitor();
            SyncWorkingPresetFromSelection();
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

            bool startMinimizedChanged = ReferenceEquals(sender, ChkStartMinimized);

            _settings.OverrideWindowsSnap = ChkOverrideSnap.IsChecked ?? true;
            _settings.EnableWinShiftMonitorMove = ChkWinShiftMonitorMove.IsChecked ?? true;
            _settings.EnableCrossMonitorWrap = ChkCrossMonitorWrap.IsChecked ?? true;
            _settings.DwmInvisibleBorderCompensation = ChkInvisibleBorderComp.IsChecked ?? true;
            _settings.DisableWin11RoundedCorners = ChkDisableRoundedCorners.IsChecked ?? true;
            _settings.ShowZoneOverlayOnSnap = ChkShowZoneOverlay.IsChecked ?? true;
            _settings.PreventAccidentalTopEdgeMaximize = ChkPreventTopEdgeMaximize.IsChecked ?? true;
            _settings.StartMinimizedToTray = ChkStartMinimized.IsChecked ?? false;
            _settings.CloseToTray = ChkCloseToTray.IsChecked ?? false;

            _keyboardHook.OverrideWindowsSnap = _settings.OverrideWindowsSnap;
            _keyboardHook.EnableWinShiftMonitorMove = _settings.EnableWinShiftMonitorMove;
            UpdateHookStatusText();

            _settings.Save();

            if (startMinimizedChanged &&
                (_settings.StartWithWindows || StartupManager.IsRunAtStartupEnabled()))
            {
                bool success = StartupManager.SetRunAtStartup(true, _settings.StartMinimizedToTray);
                if (!success || !StartupManager.IsTaskSchedulerEnabled())
                {
                    MessageBox.Show(this, LocalizationManager.GetString("Msg_TaskSchedulerFailed"),
                        LocalizationManager.GetString("Msg_Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        #region Borderless Manager Actions

        private void RefreshRunningAppsList()
        {
            try
            {
                _allRunningWindows = WindowEnumerator.GetRunningWindows(
                    _borderlessEngine,
                    new WindowInteropHelper(this).Handle);
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

        #endregion

        #region System Tray and Window Lifecycle

        private void BtnMinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            ShowTrayBalloonTip();
        }

        private void ShowTrayBalloonTip()
        {
            TrayIcon.ShowBalloonTip(
                "Windows Splitter",
                LocalizationManager.GetString("Msg_BalloonTipContent"),
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
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
            _settings.OverrideWindowsSnap = ChkOverrideSnap.IsChecked == true;
            ChkOverrideSnap.IsChecked = _settings.OverrideWindowsSnap;
            _keyboardHook.OverrideWindowsSnap = _settings.OverrideWindowsSnap;
            TxtHookStatus.Text = _settings.OverrideWindowsSnap
                ? LocalizationManager.GetString("Str_BadgeHookEnabled")
                : LocalizationManager.GetString("Str_BadgeHookDisabled");
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
                ShowTrayBalloonTip();
                return;
            }

            // Cleanup: unhook and restore all borderless windows to standard state
            _keyboardHook.Dispose();
            _winEventHookService.Dispose();
            _topEdgeSnapGuard.Dispose();
            _borderlessEngine.CleanUp();
            _settings?.Save();

            base.OnClosing(e);
        }

        #endregion
    }
}
