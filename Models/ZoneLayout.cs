using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

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
        public int ScreenWidth { get; set; } = 3440;
        public int ScreenHeight { get; set; } = 1440;
        public List<Zone> Zones { get; set; } = new();

        public ZonePreset() { }

        public ZonePreset(string id, string name, string description, int screenWidth, int screenHeight, List<Zone> zones)
        {
            Id = id;
            Name = name;
            Description = description;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            Zones = zones;
        }

        [JsonIgnore]
        public int Height
        {
            get => ScreenHeight;
            set => ScreenHeight = value;
        }

        public static List<ZonePreset> CreateDefaultPresets(int screenWidth = 3440, int screenHeight = 1440)
        {
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
                    }
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
                    }
                ));

                return presets;
            }

            // Case 2: Ultrawide Monitor (Aspect ratio >= 2.0, e.g. 21:9 or 32:9)
            bool isUltrawide = screenHeight > 0 && ((double)screenWidth / screenHeight) >= 2.0;
            if (isUltrawide)
            {
                int zone1Width = (int)Math.Round(screenWidth * (880.0 / 3440.0));
                int zone2Width = screenWidth - zone1Width;
                if (screenWidth == 3440)
                {
                    zone1Width = 880;
                    zone2Width = 2560;
                }

                presets.Add(new ZonePreset(
                    "preset_ultrawide_2k",
                    "Ultrawide 1/3 - 2/3 (Chuẩn 2K)",
                    $"Vùng 1: {zone1Width}px | Vùng 2: {zone2Width}px (2K QHD Gaming)",
                    screenWidth,
                    screenHeight,
                    new List<Zone>
                    {
                        new Zone(1, "Vùng 1 (Trái - Web/Chat)", 0, 0, zone1Width, screenHeight),
                        new Zone(2, "Vùng 2 (Phải - Game 2K)", zone1Width, 0, zone2Width, screenHeight)
                    }
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
                    }
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
                    }
                ));

                int sideWidth = Math.Max(100, (screenWidth - 2560) / 2);
                int centerWidth = screenWidth - (sideWidth * 2);
                presets.Add(new ZonePreset(
                    "preset_center_focus",
                    "Trung tâm 2K + 2 Cánh",
                    $"Cánh trái {sideWidth}px | Trung tâm {centerWidth}px | Cánh phải {sideWidth}px",
                    screenWidth,
                    screenHeight,
                    new List<Zone>
                    {
                        new Zone(1, "Cánh Trái", 0, 0, sideWidth, screenHeight),
                        new Zone(2, "Trung Tâm (Game 2K)", sideWidth, 0, centerWidth, screenHeight),
                        new Zone(3, "Cánh Phải", sideWidth + centerWidth, 0, sideWidth, screenHeight)
                    }
                ));

                return presets;
            }

            // Case 3: Standard 16:9 / 16:10 Monitor (1920x1080, 2560x1440, etc.)
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
                }
            ));

            int leftOneThird = (int)Math.Round(screenWidth / 3.0);
            presets.Add(new ZonePreset(
                "preset_standard_1_3",
                "1/3 Trái - 2/3 Phải",
                $"Vùng trái {leftOneThird}px | Vùng phải {screenWidth - leftOneThird}px",
                screenWidth,
                screenHeight,
                new List<Zone>
                {
                    new Zone(1, "Cột Trái (33%)", 0, 0, leftOneThird, screenHeight),
                    new Zone(2, "Vùng Phải (67%)", leftOneThird, 0, screenWidth - leftOneThird, screenHeight)
                }
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
                }
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
            ScreenWidth = width;
            ScreenHeight = height;
            Presets = ZonePreset.CreateDefaultPresets(width, height);
            SelectedPresetId = Presets.FirstOrDefault()?.Id ?? "";
        }

        public ZonePreset? GetActivePreset()
        {
            return Presets.FirstOrDefault(p => p.Id == SelectedPresetId) ?? Presets.FirstOrDefault();
        }
    }
}
