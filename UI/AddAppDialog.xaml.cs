using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WindowsSplitter.Core;
using WindowsSplitter.Models;

namespace WindowsSplitter.UI
{
    public partial class AddAppDialog : Window
    {
        private readonly AppSettings _settings;
        public BorderlessAppRule? ResultRule { get; private set; }

        public AddAppDialog(string initialExeName = "", string initialTitle = "", string initialMonitor = "", int initialZoneId = 2)
        {
            InitializeComponent();

            _settings = AppSettings.Load();

            PopulateMonitorList(initialMonitor);
            PopulateZoneList(initialZoneId);

            if (!string.IsNullOrEmpty(initialExeName))
            {
                TxtProcessName.Text = initialExeName;
            }

            if (!string.IsNullOrEmpty(initialTitle))
            {
                TxtDisplayName.Text = initialTitle;
            }
            else if (!string.IsNullOrEmpty(initialExeName))
            {
                TxtDisplayName.Text = Path.GetFileNameWithoutExtension(initialExeName);
            }
        }

        private void PopulateMonitorList(string selectedMonitor)
        {
            CmbTargetMonitor.Items.Clear();

            bool isEn = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
            string autoText = isEn ? "🖥️ Auto (Follow active window)" : "🖥️ Tự động (Theo cửa sổ đang mở)";
            string primaryText = isEn ? "🖥️ Primary Monitor" : "🖥️ Màn hình chính (Primary Monitor)";

            var autoItem = new ComboBoxItem { Content = autoText, Tag = "" };
            CmbTargetMonitor.Items.Add(autoItem);

            var primaryItem = new ComboBoxItem { Content = primaryText, Tag = "primary" };
            CmbTargetMonitor.Items.Add(primaryItem);

            foreach (var m in MonitorManager.Instance.Monitors)
            {
                var monItem = new ComboBoxItem
                {
                    Content = m.FriendlyName,
                    Tag = m.DeviceName
                };
                CmbTargetMonitor.Items.Add(monItem);
            }

            // Set selection
            var match = CmbTargetMonitor.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Tag?.ToString(), selectedMonitor, StringComparison.OrdinalIgnoreCase));
            CmbTargetMonitor.SelectedItem = match ?? autoItem;
        }

        private void CmbTargetMonitor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int currentSelectedZone = 2;
            if (CmbTargetZone.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int parsed))
            {
                currentSelectedZone = parsed;
            }
            PopulateZoneList(currentSelectedZone);
        }

        private void PopulateZoneList(int targetZoneId)
        {
            CmbTargetZone.Items.Clear();

            // Item 0: Fullscreen
            string fsLabel = LocalizationManager.GetString("Str_ZoneFullscreen");
            var fullscreenItem = new ComboBoxItem { Content = fsLabel, Tag = 0 };
            CmbTargetZone.Items.Add(fullscreenItem);

            // Determine target monitor to get actual zones
            MonitorInfoItem? targetMonitor = null;
            if (CmbTargetMonitor.SelectedItem is ComboBoxItem monItem && monItem.Tag != null)
            {
                string dev = monItem.Tag.ToString() ?? "";
                if (dev.Equals("primary", StringComparison.OrdinalIgnoreCase))
                {
                    targetMonitor = MonitorManager.Instance.GetPrimaryMonitor();
                }
                else if (!string.IsNullOrEmpty(dev))
                {
                    targetMonitor = MonitorManager.Instance.GetMonitorByDevice(dev);
                }
            }

            targetMonitor ??= MonitorManager.Instance.GetPrimaryMonitor() ?? new MonitorInfoItem();
            var preset = _settings.GetPresetForMonitor(targetMonitor);

            ComboBoxItem? selectedItem = null;
            if (preset != null && preset.Zones.Count > 0)
            {
                foreach (var z in preset.Zones)
                {
                    var zoneItem = new ComboBoxItem
                    {
                        Content = $"{z.LocalizedName} ({z.Width} × {z.Height} px)",
                        Tag = z.Id
                    };
                    CmbTargetZone.Items.Add(zoneItem);

                    if (z.Id == targetZoneId)
                    {
                        selectedItem = zoneItem;
                    }
                }
            }

            if (targetZoneId == 0)
            {
                CmbTargetZone.SelectedItem = fullscreenItem;
            }
            else
            {
                CmbTargetZone.SelectedItem = selectedItem ?? CmbTargetZone.Items.OfType<ComboBoxItem>().Skip(1).FirstOrDefault() ?? fullscreenItem;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            bool isEn = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
            var ofd = new OpenFileDialog
            {
                Filter = isEn ? "Executable files (*.exe)|*.exe|All files (*.*)|*.*" : "Tệp thực thi (*.exe)|*.exe|Tất cả tệp (*.*)|*.*",
                Title = isEn ? "Select Game or Application Executable" : "Chọn file thực thi của Game hoặc Ứng dụng"
            };

            if (ofd.ShowDialog(this) == true)
            {
                TxtProcessName.Text = Path.GetFileName(ofd.FileName);
                if (string.IsNullOrWhiteSpace(TxtDisplayName.Text))
                {
                    TxtDisplayName.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string processName = TxtProcessName.Text.Trim();
            if (string.IsNullOrWhiteSpace(processName))
            {
                bool isEn = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
                MessageBox.Show(this, 
                    isEn ? "Please enter an executable file name (.exe)!" : "Vui lòng nhập tên file thực thi (.exe)!", 
                    LocalizationManager.GetString("Msg_MissingInfo"), 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processName += ".exe";
            }

            string displayName = TxtDisplayName.Text.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = Path.GetFileNameWithoutExtension(processName);
            }

            string targetMonitor = "";
            if (CmbTargetMonitor.SelectedItem is ComboBoxItem monItem && monItem.Tag != null)
            {
                targetMonitor = monItem.Tag.ToString() ?? "";
            }

            int targetZone = 2;
            if (CmbTargetZone.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int parsedZone))
            {
                targetZone = parsedZone;
            }

            ResultRule = new BorderlessAppRule
            {
                ProcessName = processName,
                DisplayName = displayName,
                TargetMonitorDevice = targetMonitor,
                TargetZoneId = targetZone,
                AutoApply = ChkAutoApply.IsChecked ?? true,
                CoverTaskbar = ChkCoverTaskbar.IsChecked ?? true,
                ShowFloatingCaption = ChkShowFloatingCaption.IsChecked ?? true,
                RemoveRoundedCorners = ChkRemoveRoundedCorners.IsChecked ?? true
            };

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
