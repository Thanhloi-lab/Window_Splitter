using System.Windows;
using WindowsSplitter.Core;
using WindowsSplitter.Models;

namespace WindowsSplitter.UI
{
    public partial class SaveProfileDialog : Window
    {
        public ZonePreset? ResultPreset { get; private set; }

        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private readonly int _zoneGap;
        private readonly ZoneSplitNode? _splitRoot;
        private readonly List<Zone> _zonesToSave;

        public SaveProfileDialog(
            string defaultName,
            int screenWidth,
            int screenHeight,
            List<Zone> currentZones,
            string monitorName = "",
            ZoneSplitNode? splitRoot = null,
            int zoneGap = 0)
        {
            InitializeComponent();

            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _zoneGap = zoneGap;
            _splitRoot = splitRoot?.Clone();
            _zonesToSave = currentZones.Select(z => z.Clone()).ToList();

            TxtProfileName.Text = defaultName;
            TxtProfileName.SelectAll();
            TxtProfileName.Focus();

            bool isEn = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
            TxtDimensionsSummary.Text = string.Join("  |  ",
                _zonesToSave.Select(z => $"{z.LocalizedName}: {z.Width} x {z.Height} px ({z.PercentWidth:0.#}% x {z.PercentHeight:0.#}%)"));

            string monLabel = LocalizationManager.GetString("Str_MonitorSummaryLabel");
            TxtMonitorSummary.Text = $"{monLabel} {monitorName} ({screenWidth} x {screenHeight})";
            TxtProfileDesc.Text = isEn
                ? $"Custom Profile for {screenWidth} x {screenHeight} ({_zonesToSave.Count} Zones)"
                : $"Profile tùy chỉnh cho màn hình {screenWidth} x {screenHeight} ({_zonesToSave.Count} Vùng)";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtProfileName.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, LocalizationManager.GetString("Msg_EnterProfileName"), LocalizationManager.GetString("Msg_MissingInfo"), MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtProfileName.Focus();
                return;
            }

            string desc = TxtProfileDesc.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(desc))
            {
                desc = TxtDimensionsSummary.Text;
            }

            string newId = "custom_" + Guid.NewGuid().ToString("N")[..8];

            ResultPreset = _splitRoot != null
                ? new ZonePreset(newId, name, desc, _screenWidth, _screenHeight, _splitRoot, isDefault: false, zoneGap: _zoneGap)
                : new ZonePreset(newId, name, desc, _screenWidth, _screenHeight, _zonesToSave, isDefault: false) { ZoneGap = _zoneGap };

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
