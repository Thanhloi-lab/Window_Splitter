using System.IO;
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
        public bool CloseToTray { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        public string Language { get; set; } = "en";

        // Multi-monitor advanced options
        public bool EnableCrossMonitorWrap { get; set; } = true;
        public bool EnableWinShiftMonitorMove { get; set; } = true;
        public bool DefaultCoverTaskbar { get; set; } = true;

        public string SelectedPresetId { get; set; } = "";
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
                        // Clean up legacy dummy apps if any
                        settings.BorderlessWhitelist?.RemoveAll(r => r.ProcessName == "wwm.exe" || r.ProcessName == "chrome.exe" || r.ProcessName == "potplayer64.exe");

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
            var primary = MonitorManager.Instance.GetPrimaryMonitor();
            int screenW = primary != null && primary.Width > 0 ? primary.Width : 1920;
            int screenH = primary != null && primary.Height > 0 ? primary.Height : 1080;

            var defaultPresets = ZonePreset.CreateDefaultPresets(screenW, screenH);

            if (Presets == null || Presets.Count == 0)
            {
                Presets = defaultPresets;
            }
            else
            {
                // Ensure all default presets exist and are marked IsDefault = true
                foreach (var def in defaultPresets)
                {
                    var existing = Presets.FirstOrDefault(p => p.Id == def.Id);
                    if (existing == null)
                    {
                        Presets.Insert(0, def);
                    }
                    else
                    {
                        existing.IsDefault = true;
                    }
                }
            }

            if (string.IsNullOrEmpty(SelectedPresetId) || Presets.All(p => p.Id != SelectedPresetId))
            {
                SelectedPresetId = Presets.FirstOrDefault()?.Id ?? "";
            }

            if (MonitorConfigs == null)
            {
                MonitorConfigs = new List<MonitorLayoutConfig>();
            }
            else
            {
                foreach (var config in MonitorConfigs)
                {
                    var monDefaults = ZonePreset.CreateDefaultPresets(config.ScreenWidth, config.ScreenHeight);
                    if (config.Presets == null || config.Presets.Count == 0)
                    {
                        config.Presets = monDefaults;
                    }
                    else
                    {
                        foreach (var def in monDefaults)
                        {
                            var existing = config.Presets.FirstOrDefault(p => p.Id == def.Id);
                            if (existing == null)
                            {
                                config.Presets.Insert(0, def);
                            }
                            else
                            {
                                existing.IsDefault = true;
                            }
                        }
                    }
                }
            }

            if (BorderlessWhitelist == null)
            {
                BorderlessWhitelist = new List<BorderlessAppRule>();
            }
        }

        public MonitorLayoutConfig GetOrCreateMonitorConfig(string deviceName, int width, int height)
        {
            if (MonitorConfigs == null)
            {
                MonitorConfigs = new List<MonitorLayoutConfig>();
            }

            var config = MonitorConfigs.FirstOrDefault(c => string.Equals(c.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (config == null)
            {
                config = new MonitorLayoutConfig(deviceName, width > 0 ? width : 1920, height > 0 ? height : 1080);
                MonitorConfigs.Add(config);
            }
            return config;
        }

        public ZonePreset GetPresetForMonitor(MonitorInfoItem monitor)
        {
            if (MonitorConfigs == null)
            {
                MonitorConfigs = new List<MonitorLayoutConfig>();
            }

            if (monitor == null || string.IsNullOrEmpty(monitor.DeviceName))
            {
                return GetActivePreset() ?? Presets?.FirstOrDefault() ?? new ZonePreset();
            }

            var config = MonitorConfigs.FirstOrDefault(c => string.Equals(c.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (config != null)
            {
                var preset = config.GetActivePreset();
                if (preset != null) return preset;
            }

            // Create per-monitor layout automatically matching aspect ratio
            int w = monitor.Width > 0 ? monitor.Width : 1920;
            int h = monitor.Height > 0 ? monitor.Height : 1080;
            var newConfig = GetOrCreateMonitorConfig(monitor.DeviceName, w, h);
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
            if (Presets == null || Presets.Count == 0)
            {
                var primary = MonitorManager.Instance.GetPrimaryMonitor();
                int screenW = primary != null && primary.Width > 0 ? primary.Width : 1920;
                int screenH = primary != null && primary.Height > 0 ? primary.Height : 1080;
                Presets = ZonePreset.CreateDefaultPresets(screenW, screenH);
            }
            return Presets.FirstOrDefault(p => p.Id == SelectedPresetId) ?? Presets.FirstOrDefault();
        }
    }
}
