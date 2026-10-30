using System.Text.Json.Serialization;
using WindowsSplitter.Core;

namespace WindowsSplitter.Models
{
    public class Zone
    {
        public int Id { get; set; } = 1;
        public string Name { get; set; } = "Zone 1";
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public int Width { get; set; } = 880;
        public int Height { get; set; } = 1440;

        public Zone() { }

        public Zone(int id, string name, int x, int y, int width, int height)
        {
            Id = id;
            Name = name;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        [JsonIgnore]
        public string LocalizedName
        {
            get
            {
                bool isEn = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
                if (!isEn) return Name;
                if (Name.Contains("Vùng 1") || Name.Contains("Nửa Trên") || Name.Contains("Tầng Trên") || Name.Contains("Cánh Trái") || Name.Contains("Cột Trái"))
                    return $"Zone 1 ({ (Id == 1 ? "Left" : "Top") })";
                if (Name.Contains("Vùng 2") || Name.Contains("Nửa Dưới") || Name.Contains("Tầng Dưới") || Name.Contains("Cánh Phải") || Name.Contains("Cột Phải"))
                    return $"Zone {Id} (Right)";
                if (Name.Contains("Giữa") || Name.Contains("Trung Tâm"))
                    return "Zone 2 (Center)";
                return Name;
            }
        }

        public Zone Clone()
        {
            return new Zone(Id, Name, X, Y, Width, Height);
        }
    }

    public class ZonePreset
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ScreenWidth { get; set; } = 1920;
        public int ScreenHeight { get; set; } = 1080;
        public bool IsDefault { get; set; } = false;
        public List<Zone> Zones { get; set; } = new();

        public ZonePreset() { }

        public ZonePreset(string id, string name, string description, int screenWidth, int screenHeight, List<Zone> zones, bool isDefault = false)
        {
            Id = id;
            Name = name;
            Description = description;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            Zones = zones;
            IsDefault = isDefault;
        }

        [JsonIgnore]
        public int Height
        {
            get => ScreenHeight;
            set => ScreenHeight = value;
        }

        [JsonIgnore]
        public string LocalizedName
        {
            get
            {
                if (!IsDefault) return Name;
                bool isEn = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
                return Id switch
                {
                    "preset_ultrawide_2k" => isEn ? "Ultrawide 1/3 - 2/3 (2K Standard)" : "Ultrawide 1/3 - 2/3 (Chuẩn 2K)",
                    "preset_half_half" => isEn ? "Balanced Half & Half (50% / 50%)" : "Chia đôi cân bằng (50% / 50%)",
                    "preset_triple" => isEn ? "Triple Equal Columns (33% / 33% / 33%)" : "Chia ba cột đều nhau (33% / 33% / 33%)",
                    "preset_center_focus" => isEn ? "Center Focus + 2 Wings" : "Trung tâm + 2 Cánh",
                    "preset_standard_1_3" => isEn ? "1/3 Left - 2/3 Right (33% / 67%)" : "1/3 Trái - 2/3 Phải (33% / 67%)",
                    "preset_standard_half" => isEn ? "Balanced Half & Half (50% / 50%)" : "Chia đôi cân bằng (50% / 50%)",
                    "preset_standard_2_3" => isEn ? "2/3 Left - 1/3 Right (67% / 33%)" : "2/3 Trái - 1/3 Phải (67% / 33%)",
                    "preset_standard_triple" => isEn ? "Triple Equal Columns (33% / 33% / 33%)" : "Chia ba cột đều nhau (33% / 33% / 33%)",
                    "preset_vert_half" => isEn ? "Top - Bottom Split (50% / 50%)" : "Chia đôi Trên - Dưới (50% / 50%)",
                    "preset_vert_triple" => isEn ? "Triple Stack (33% / 33% / 33%)" : "Chia ba Tầng (33% / 33% / 33%)",
                    _ => Name
                };
            }
        }

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                bool isEn = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
                string defTag = isEn ? "Default" : "Mặc định";
                string customTag = isEn ? "Custom" : "Tùy chỉnh";
                return IsDefault ? $"{LocalizedName} ({defTag})" : $"⭐ {Name} ({customTag})";
            }
        }

        [JsonIgnore]
        public bool CanDelete => !IsDefault;

        [JsonIgnore]
        public bool CanOverwrite => !IsDefault;

        public ZonePreset Clone()
        {
            return new ZonePreset
            {
                Id = this.Id,
                Name = this.Name,
                Description = this.Description,
                ScreenWidth = this.ScreenWidth,
                ScreenHeight = this.ScreenHeight,
                IsDefault = this.IsDefault,
                Zones = this.Zones.Select(z => z.Clone()).ToList()
            };
        }

        public static List<ZonePreset> CreateDefaultPresets(int screenWidth = 0, int screenHeight = 0)
        {
            // Auto-detect resolution from Windows primary monitor if not specified
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                var primary = MonitorManager.Instance.GetPrimaryMonitor();
                screenWidth = primary != null && primary.Width > 0 ? primary.Width : 1920;
                screenHeight = primary != null && primary.Height > 0 ? primary.Height : 1080;
            }

            var presets = new List<ZonePreset>();

            // Case 1: Vertical / Rotated Monitor (Height > Width)
            if (screenHeight > screenWidth)
            {
                int halfH = screenHeight / 2;
                presets.Add(new ZonePreset(
                    "preset_vert_half",
                    "Chia đôi Trên - Dưới (50% / 50%)",
                    $"Mỗi vùng cao {halfH}px",
                    screenWidth,
                    screenHeight,
                    new List<Zone>
                    {
                        new Zone(1, "Nửa Trên", 0, 0, screenWidth, halfH),
                        new Zone(2, "Nửa Dưới", 0, halfH, screenWidth, screenHeight - halfH)
                    },
                    isDefault: true
                ));

                int thirdH = screenHeight / 3;
                presets.Add(new ZonePreset(
                    "preset_vert_triple",
                    "Chia ba Tầng (33% / 33% / 33%)",
                    $"Mỗi tầng cao ~{thirdH}px",
                    screenWidth,
                    screenHeight,
                    new List<Zone>
                    {
                        new Zone(1, "Tầng Trên", 0, 0, screenWidth, thirdH),
                        new Zone(2, "Tầng Giữa", 0, thirdH, screenWidth, thirdH),
                        new Zone(3, "Tầng Dưới", 0, thirdH * 2, screenWidth, screenHeight - (thirdH * 2))
                    },
                    isDefault: true
                ));

                return presets;
            }

            // Case 2: Ultrawide Monitor (Aspect ratio >= 2.0, e.g. 21:9 or 32:9)
            bool isUltrawide = screenHeight > 0 && ((double)screenWidth / screenHeight) >= 2.0;
            if (isUltrawide)
            {
                int zone1Width = (int)Math.Round(screenWidth * (880.0 / 3440.0));
                int zone2Width = screenWidth - zone1Width;
                string title = (screenWidth == 3440 && screenHeight == 1440)
                    ? "Ultrawide 1/3 - 2/3 (Chuẩn 2K)"
                    : $"Ultrawide 1/3 - 2/3 ({zone1Width}px / {zone2Width}px)";

                presets.Add(new ZonePreset(
                    "preset_ultrawide_2k",
                    title,
                    $"Vùng 1: {zone1Width}px | Vùng 2: {zone2Width}px",
                    screenWidth,
                    screenHeight,
                    new List<Zone>
                    {
                        new Zone(1, "Vùng 1 (Trái)", 0, 0, zone1Width, screenHeight),
                        new Zone(2, "Vùng 2 (Phải)", zone1Width, 0, zone2Width, screenHeight)
                    },
                    isDefault: true
                ));

                int halfWidth = screenWidth / 2;
                presets.Add(new ZonePreset(
                    "preset_half_half",
                    "Chia đôi cân bằng (50% / 50%)",
                    $"Mỗi vùng rộng {halfWidth}px",
                    screenWidth,
                    screenHeight,
                    new List<Zone>
                    {
                        new Zone(1, "Vùng Trái (50%)", 0, 0, halfWidth, screenHeight),
                        new Zone(2, "Vùng Phải (50%)", halfWidth, 0, screenWidth - halfWidth, screenHeight)
                    },
                    isDefault: true
                ));

                int thirdWidth = screenWidth / 3;
                presets.Add(new ZonePreset(
                    "preset_triple",
                    "Chia ba cột đều nhau (33% / 33% / 33%)",
                    $"Mỗi cột rộng ~{thirdWidth}px",
                    screenWidth,
                    screenHeight,
                    new List<Zone>
                    {
                        new Zone(1, "Cột Trái", 0, 0, thirdWidth, screenHeight),
                        new Zone(2, "Cột Giữa", thirdWidth, 0, thirdWidth, screenHeight),
                        new Zone(3, "Cột Phải", thirdWidth * 2, 0, screenWidth - (thirdWidth * 2), screenHeight)
                    },
                    isDefault: true
                ));

                int sideWidth = Math.Max(100, (screenWidth - 2560) / 2);
                int centerWidth = screenWidth - (sideWidth * 2);
                if (centerWidth > 500)
                {
                    presets.Add(new ZonePreset(
                        "preset_center_focus",
                        "Trung tâm + 2 Cánh",
                        $"Cánh trái {sideWidth}px | Trung tâm {centerWidth}px | Cánh phải {sideWidth}px",
                        screenWidth,
                        screenHeight,
                        new List<Zone>
                        {
                            new Zone(1, "Cánh Trái", 0, 0, sideWidth, screenHeight),
                            new Zone(2, "Trung Tâm", sideWidth, 0, centerWidth, screenHeight),
                            new Zone(3, "Cánh Phải", sideWidth + centerWidth, 0, sideWidth, screenHeight)
                        },
                        isDefault: true
                    ));
                }

                return presets;
            }

            // Case 3: Standard 16:9 / 16:10 Monitor (1920x1080, 2560x1440, etc.)
            int leftOneThird = (int)Math.Round(screenWidth / 3.0);
            int rightTwoThird = screenWidth - leftOneThird;
            presets.Add(new ZonePreset(
                "preset_standard_1_3",
                "1/3 Trái - 2/3 Phải (33% / 67%)",
                $"Vùng 1 (Trái): {leftOneThird}px | Vùng 2 (Phải): {rightTwoThird}px",
                screenWidth,
                screenHeight,
                new List<Zone>
                {
                    new Zone(1, "Vùng 1 (Trái 33%)", 0, 0, leftOneThird, screenHeight),
                    new Zone(2, "Vùng 2 (Phải 67%)", leftOneThird, 0, rightTwoThird, screenHeight)
                },
                isDefault: true
            ));

            int standardHalf = screenWidth / 2;
            presets.Add(new ZonePreset(
                "preset_standard_half",
                "Chia đôi cân bằng (50% / 50%)",
                $"Mỗi vùng rộng {standardHalf}px",
                screenWidth,
                screenHeight,
                new List<Zone>
                {
                    new Zone(1, "Nửa Trái (50%)", 0, 0, standardHalf, screenHeight),
                    new Zone(2, "Nửa Phải (50%)", standardHalf, 0, screenWidth - standardHalf, screenHeight)
                },
                isDefault: true
            ));

            int leftTwoThird = (int)Math.Round(screenWidth * 2.0 / 3.0);
            int rightOneThird = screenWidth - leftTwoThird;
            presets.Add(new ZonePreset(
                "preset_standard_2_3",
                "2/3 Trái - 1/3 Phải (67% / 33%)",
                $"Vùng 1 (Trái): {leftTwoThird}px | Vùng 2 (Phải): {rightOneThird}px",
                screenWidth,
                screenHeight,
                new List<Zone>
                {
                    new Zone(1, "Vùng 1 (Trái 67%)", 0, 0, leftTwoThird, screenHeight),
                    new Zone(2, "Vùng 2 (Phải 33%)", leftTwoThird, 0, rightOneThird, screenHeight)
                },
                isDefault: true
            ));

            int standardThird = screenWidth / 3;
            presets.Add(new ZonePreset(
                "preset_standard_triple",
                "Chia ba cột đều nhau (33% / 33% / 33%)",
                $"Mỗi cột rộng ~{standardThird}px",
                screenWidth,
                screenHeight,
                new List<Zone>
                {
                    new Zone(1, "Cột Trái", 0, 0, standardThird, screenHeight),
                    new Zone(2, "Cột Giữa", standardThird, 0, standardThird, screenHeight),
                    new Zone(3, "Cột Phải", standardThird * 2, 0, screenWidth - (standardThird * 2), screenHeight)
                },
                isDefault: true
            ));

            return presets;
        }
    }

    public class MonitorLayoutConfig
    {
        public string DeviceName { get; set; } = string.Empty;
        public int ScreenWidth { get; set; } = 1920;
        public int ScreenHeight { get; set; } = 1080;
        public string SelectedPresetId { get; set; } = string.Empty;
        public List<ZonePreset> Presets { get; set; } = new();

        public MonitorLayoutConfig() { }

        public MonitorLayoutConfig(string deviceName, int width, int height)
        {
            DeviceName = deviceName;
            ScreenWidth = width > 0 ? width : 1920;
            ScreenHeight = height > 0 ? height : 1080;
            Presets = ZonePreset.CreateDefaultPresets(ScreenWidth, ScreenHeight);
            SelectedPresetId = Presets.FirstOrDefault()?.Id ?? "";
        }

        public ZonePreset? GetActivePreset()
        {
            return Presets.FirstOrDefault(p => p.Id == SelectedPresetId) ?? Presets.FirstOrDefault();
        }
    }
}
