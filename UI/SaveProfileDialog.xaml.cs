using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<Zone> _zonesToSave;

        public SaveProfileDialog(string defaultName, int screenWidth, int screenHeight, List<Zone> currentZones, string monitorName = "")
        {
            InitializeComponent();

            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _zonesToSave = currentZones.Select(z => z.Clone()).ToList();

            TxtProfileName.Text = defaultName;
            TxtProfileName.SelectAll();
            TxtProfileName.Focus();

            bool isEn = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
            string z1Label = isEn ? "Zone 1" : "Vùng 1";
            string z2Label = isEn ? "Zone 2" : "Vùng 2";

            if (_zonesToSave.Count >= 2)
            {
                TxtDimensionsSummary.Text = $"{z1Label}: {_zonesToSave[0].Width} × {_zonesToSave[0].Height} px  |  {z2Label}: {_zonesToSave[1].Width} × {_zonesToSave[1].Height} px";
            }
            else if (_zonesToSave.Count == 1)
            {
                TxtDimensionsSummary.Text = $"{z1Label}: {_zonesToSave[0].Width} × {_zonesToSave[0].Height} px";
            }

            string monLabel = LocalizationManager.GetString("Str_MonitorSummaryLabel");
            TxtMonitorSummary.Text = $"{monLabel} {monitorName} ({screenWidth} × {screenHeight})";
            TxtProfileDesc.Text = isEn 
                ? $"Custom Profile for {screenWidth} × {screenHeight}" 
                : $"Profile tùy chỉnh cho màn hình {screenWidth} × {screenHeight}";
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

            ResultPreset = new ZonePreset(
                newId,
                name,
                desc,
                _screenWidth,
                _screenHeight,
                _zonesToSave,
                isDefault: false
            );

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
