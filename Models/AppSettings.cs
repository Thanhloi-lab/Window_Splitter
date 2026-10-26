using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WindowsSplitter.Core;

namespace WindowsSplitter.Models
{
    public class AppSettings
    {
        public bool OverrideWindowsSnap { get; set; } = true;
        public bool DisableWin11RoundedCorners { get; set; } = true;
        public bool DwmInvisibleBorderCompensation { get; set; } = true;
        public int BorderCompensationPixels { get; set; } = 7;
        public bool ShowZoneOverlayOnSnap { get; set; } = true;
        public int ZoneOverlayDurationMs { get; set; } = 500;
        public bool StartMinimizedToTray { get; set; } = false;

        // Multi-monitor advanced options
        public bool EnableCrossMonitorWrap { get; set; } = true;
        public bool EnableWinShiftMonitorMove { get; set; } = true;

        public string SelectedPresetId { get; set; } = "preset_ultrawide_2k";
        public List<ZonePreset> Presets { get; set; } = new();
        public List<MonitorLayoutConfig> MonitorConfigs { get; set; } = new();
        public List<BorderlessAppRule> BorderlessWhitelist { get; set; } = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string GetSettingsFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "WindowsSplitter");
            if (!Directory.Exists(folder))
            {
                try
                {
                    Directory.CreateDirectory(folder);
                }
                catch
                {
                    folder = AppDomain.CurrentDomain.BaseDirectory;
                }
            }
            return Path.Combine(folder, "appsettings.json");
        }

        public static AppSettings Load()
        {
            string filePath = GetSettingsFilePath();
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        settings.EnsureDefaults();
                        return settings;
                    }
                }
                catch
                {
                    // Fallback to default on parse error
                }
            }

            var defaultSettings = new AppSettings();
            defaultSettings.EnsureDefaults();
            defaultSettings.Save();
            return defaultSettings;
        }

        public void EnsureDefaults()
        {
            if (Presets == null || Presets.Count == 0)
            {
                Presets = ZonePreset.CreateDefaultPresets(3440, 1440);
            }

            if (string.IsNullOrEmpty(SelectedPresetId) || Presets.All(p => p.Id != SelectedPresetId))
            {
                SelectedPresetId = Presets.FirstOrDefault()?.Id ?? "preset_ultrawide_2k";
            }

            if (MonitorConfigs == null)
            {
                MonitorConfigs = new List<MonitorLayoutConfig>();
            }

            if (BorderlessWhitelist == null)
            {
                BorderlessWhitelist = new List<BorderlessAppRule>();
            }

            if (BorderlessWhitelist.Count == 0)
            {
                BorderlessWhitelist.Add(new BorderlessAppRule
                {
                    ProcessName = "wwm.exe",
                    DisplayName = "Where Winds Meet (Game)",
                    TargetZoneId = 2,
                    TargetMonitorDevice = "", // Auto
                    CoverTaskbar = true,
                    ShowFloatingCaption = true,
                    AutoApply = true
                });

                BorderlessWhitelist.Add(new BorderlessAppRule
                {
                    ProcessName = "chrome.exe",
                    DisplayName = "Google Chrome",
                    TargetZoneId = 1,
                    TargetMonitorDevice = "",
                    CoverTaskbar = false,
                    ShowFloatingCaption = false,
                    AutoApply = false
                });

                BorderlessWhitelist.Add(new BorderlessAppRule
                {
                    ProcessName = "potplayer64.exe",
                    DisplayName = "PotPlayer (Media Player)",
                    TargetZoneId = 2,
                    TargetMonitorDevice = "",
                    CoverTaskbar = true,
                    ShowFloatingCaption = true,
                    AutoApply = false
                });
            }
        }

        public MonitorLayoutConfig GetOrCreateMonitorConfig(string deviceName, int width, int height)
        {
            var config = MonitorConfigs.FirstOrDefault(c => string.Equals(c.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (config == null)
            {
                config = new MonitorLayoutConfig(deviceName, width, height);
                MonitorConfigs.Add(config);
            }
            return config;
        }

        public ZonePreset GetPresetForMonitor(MonitorInfoItem monitor)
        {
            if (monitor == null || string.IsNullOrEmpty(monitor.DeviceName))
            {
                return GetActivePreset() ?? Presets.FirstOrDefault() ?? new ZonePreset();
            }

            var config = MonitorConfigs.FirstOrDefault(c => string.Equals(c.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (config != null)
            {
                var preset = config.GetActivePreset();
                if (preset != null) return preset;
            }

            // Create per-monitor layout automatically matching aspect ratio
            var newConfig = GetOrCreateMonitorConfig(monitor.DeviceName, monitor.Width, monitor.Height);
            return newConfig.GetActivePreset() ?? GetActivePreset() ?? new ZonePreset();
        }

        public void Save()
        {
            try
            {
                string filePath = GetSettingsFilePath();
                string json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public ZonePreset? GetActivePreset()
        {
            return Presets.FirstOrDefault(p => p.Id == SelectedPresetId) ?? Presets.FirstOrDefault();
        }
    }
}
