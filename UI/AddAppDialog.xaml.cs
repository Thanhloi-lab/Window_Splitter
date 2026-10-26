using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WindowsSplitter.Core;
using WindowsSplitter.Models;

namespace WindowsSplitter.UI
{
    public partial class AddAppDialog : Window
    {
        public BorderlessAppRule? ResultRule { get; private set; }

        public AddAppDialog(string initialExeName = "", string initialTitle = "", string initialMonitor = "")
        {
            InitializeComponent();

            PopulateMonitorList(initialMonitor);

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

            var autoItem = new ComboBoxItem { Content = "🖥️ Tự động (Theo cửa sổ đang mở)", Tag = "" };
            CmbTargetMonitor.Items.Add(autoItem);

            var primaryItem = new ComboBoxItem { Content = "🖥️ Màn hình chính (Primary Monitor)", Tag = "primary" };
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

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Tệp thực thi (*.exe)|*.exe|Tất cả tệp (*.*)|*.*",
                Title = "Chọn file thực thi của Game hoặc Ứng dụng"
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
                MessageBox.Show(this, "Vui lòng nhập tên file thực thi (.exe)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
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
