using System.Text.Json.Serialization;
using WindowsSplitter.Core;

namespace WindowsSplitter.Models
{
    public enum SplitOrientation
    {
        None = 0,
        Vertical = 1,   // Splits Left | Right
        Horizontal = 2  // Splits Top / Bottom
    }

    public class SplitDividerInfo
    {
        public ZoneSplitNode Node { get; set; } = null!;
        public SplitOrientation Orientation { get; set; }
        public int LinePos { get; set; }
        public int SpanStart { get; set; }
        public int SpanEnd { get; set; }
        public int ParentStart { get; set; }
        public int ParentSize { get; set; }
        public int MinFirstSize { get; set; }
        public int MinSecondSize { get; set; }
        public int Gap { get; set; }
    }

    public class ZoneSplitNode
    {
        public SplitOrientation Orientation { get; set; } = SplitOrientation.None;
        public double SplitRatio { get; set; } = 0.5;
        public int ZoneId { get; set; } = 1;
        public ZoneSplitNode? First { get; set; }
        public ZoneSplitNode? Second { get; set; }

        [JsonIgnore]
        public bool IsLeaf => Orientation == SplitOrientation.None || (First == null && Second == null);

        public static ZoneSplitNode CreateLeaf(int zoneId = 1) => new()
        {
            Orientation = SplitOrientation.None,
            ZoneId = zoneId
        };

        public static ZoneSplitNode CreateSplit(SplitOrientation orientation, double ratio, ZoneSplitNode first, ZoneSplitNode second) => new()
        {
            Orientation = orientation,
            SplitRatio = Math.Clamp(ratio, 0.05, 0.95),
            First = first,
            Second = second
        };

        public ZoneSplitNode Clone()
        {
            return new ZoneSplitNode
            {
                Orientation = Orientation,
                SplitRatio = SplitRatio,
                ZoneId = ZoneId,
                First = First?.Clone(),
                Second = Second?.Clone()
            };
        }

        public int CountLeaves()
        {
            if (IsLeaf) return 1;
            return (First?.CountLeaves() ?? 0) + (Second?.CountLeaves() ?? 0);
        }

        public int GetMinSize(SplitOrientation axis, int zoneGap, int minLeafPx = 80)
        {
            if (IsLeaf || First == null || Second == null)
                return minLeafPx;

            int firstMin = First.GetMinSize(axis, zoneGap, minLeafPx);
            int secondMin = Second.GetMinSize(axis, zoneGap, minLeafPx);

            if (Orientation == axis)
            {
                return firstMin + Math.Max(0, zoneGap) + secondMin;
            }
            return Math.Max(firstMin, secondMin);
        }

        public List<Zone> ComputeZones(int totalWidth, int totalHeight, int zoneGap = 0)
        {
            totalWidth = Math.Max(100, totalWidth);
            totalHeight = Math.Max(100, totalHeight);
            zoneGap = Math.Clamp(zoneGap, 0, 80);

            var collected = new List<(ZoneSplitNode Leaf, int X, int Y, int W, int H, double PctX, double PctY, double PctW, double PctH)>();
            CollectLeaves(0, 0, totalWidth, totalHeight, 0.0, 0.0, 100.0, 100.0, zoneGap, collected);

            // Order Zone 1..N from top-to-bottom, left-to-right
            var ordered = collected
                .OrderBy(item => item.Y)
                .ThenBy(item => item.X)
                .ToList();

            var zones = new List<Zone>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var item = ordered[i];
                int id = i + 1;
                item.Leaf.ZoneId = id;
                zones.Add(new Zone(id, $"Zone {id}", item.X, item.Y, item.W, item.H)
                {
                    PercentX = Math.Round(item.PctX, 2),
                    PercentY = Math.Round(item.PctY, 2),
                    PercentWidth = Math.Round(item.PctW, 2),
                    PercentHeight = Math.Round(item.PctH, 2)
                });
            }

            return zones;
        }

        private void CollectLeaves(
            int x, int y, int w, int h,
            double pctX, double pctY, double pctW, double pctH,
            int zoneGap,
            List<(ZoneSplitNode Leaf, int X, int Y, int W, int H, double PctX, double PctY, double PctW, double PctH)> output)
        {
            if (IsLeaf || First == null || Second == null)
            {
                output.Add((this, x, y, w, h, pctX, pctY, pctW, pctH));
                return;
            }

            double r = Math.Clamp(SplitRatio, 0.05, 0.95);

            if (Orientation == SplitOrientation.Vertical)
            {
                int gap = Math.Clamp(zoneGap, 0, Math.Max(0, w - 40));
                int availW = w - gap;
                int min1 = Math.Min(availW - 20, First.GetMinSize(SplitOrientation.Vertical, zoneGap, 20));
                int min2 = Math.Min(availW - 20, Second.GetMinSize(SplitOrientation.Vertical, zoneGap, 20));
                int w1 = (int)Math.Round(availW * r);
                if (min1 + min2 <= availW)
                {
                    w1 = Math.Clamp(w1, min1, availW - min2);
                }
                else
                {
                    w1 = Math.Clamp(w1, 1, Math.Max(1, availW - 1));
                }
                int w2 = Math.Max(1, availW - w1);

                double pctGap = w > 0 ? (gap / (double)w) * pctW : 0;
                double availPctW = Math.Max(0.1, pctW - pctGap);
                double pw1 = availPctW * (availW > 0 ? w1 / (double)availW : r);
                double pw2 = Math.Max(0.1, availPctW - pw1);

                First.CollectLeaves(x, y, w1, h, pctX, pctY, pw1, pctH, zoneGap, output);
                Second.CollectLeaves(x + w1 + gap, y, w2, h, pctX + pw1 + pctGap, pctY, pw2, pctH, zoneGap, output);
            }
            else
            {
                int gap = Math.Clamp(zoneGap, 0, Math.Max(0, h - 40));
                int availH = h - gap;
                int min1 = Math.Min(availH - 20, First.GetMinSize(SplitOrientation.Horizontal, zoneGap, 20));
                int min2 = Math.Min(availH - 20, Second.GetMinSize(SplitOrientation.Horizontal, zoneGap, 20));
                int h1 = (int)Math.Round(availH * r);
                if (min1 + min2 <= availH)
                {
                    h1 = Math.Clamp(h1, min1, availH - min2);
                }
                else
                {
                    h1 = Math.Clamp(h1, 1, Math.Max(1, availH - 1));
                }
                int h2 = Math.Max(1, availH - h1);

                double pctGap = h > 0 ? (gap / (double)h) * pctH : 0;
                double availPctH = Math.Max(0.1, pctH - pctGap);
                double ph1 = availPctH * (availH > 0 ? h1 / (double)availH : r);
                double ph2 = Math.Max(0.1, availPctH - ph1);

                First.CollectLeaves(x, y, w, h1, pctX, pctY, pctW, ph1, zoneGap, output);
                Second.CollectLeaves(x, y + h1 + gap, w, h2, pctX, pctY + ph1 + pctGap, pctW, ph2, zoneGap, output);
            }
        }

        public List<SplitDividerInfo> GetDividers(int totalWidth, int totalHeight, int zoneGap = 0, int minLeafPx = 80)
        {
            var dividers = new List<SplitDividerInfo>();
            CollectDividers(0, 0, Math.Max(100, totalWidth), Math.Max(100, totalHeight), Math.Clamp(zoneGap, 0, 80), minLeafPx, dividers);
            return dividers;
        }

        private void CollectDividers(int x, int y, int w, int h, int zoneGap, int minLeafPx, List<SplitDividerInfo> dividers)
        {
            if (IsLeaf || First == null || Second == null)
                return;

            double r = Math.Clamp(SplitRatio, 0.05, 0.95);

            if (Orientation == SplitOrientation.Vertical)
            {
                int gap = Math.Clamp(zoneGap, 0, Math.Max(0, w - 40));
                int availW = w - gap;
                int min1 = First.GetMinSize(SplitOrientation.Vertical, zoneGap, minLeafPx);
                int min2 = Second.GetMinSize(SplitOrientation.Vertical, zoneGap, minLeafPx);
                int w1 = (int)Math.Round(availW * r);
                if (min1 + min2 <= availW)
                    w1 = Math.Clamp(w1, min1, availW - min2);
                else
                    w1 = Math.Clamp(w1, 1, Math.Max(1, availW - 1));
                int w2 = Math.Max(1, availW - w1);

                dividers.Add(new SplitDividerInfo
                {
                    Node = this,
                    Orientation = SplitOrientation.Vertical,
                    LinePos = x + w1 + gap / 2,
                    SpanStart = y,
                    SpanEnd = y + h,
                    ParentStart = x,
                    ParentSize = w,
                    MinFirstSize = min1,
                    MinSecondSize = min2,
                    Gap = gap
                });

                First.CollectDividers(x, y, w1, h, zoneGap, minLeafPx, dividers);
                Second.CollectDividers(x + w1 + gap, y, w2, h, zoneGap, minLeafPx, dividers);
            }
            else
            {
                int gap = Math.Clamp(zoneGap, 0, Math.Max(0, h - 40));
                int availH = h - gap;
                int min1 = First.GetMinSize(SplitOrientation.Horizontal, zoneGap, minLeafPx);
                int min2 = Second.GetMinSize(SplitOrientation.Horizontal, zoneGap, minLeafPx);
                int h1 = (int)Math.Round(availH * r);
                if (min1 + min2 <= availH)
                    h1 = Math.Clamp(h1, min1, availH - min2);
                else
                    h1 = Math.Clamp(h1, 1, Math.Max(1, availH - 1));
                int h2 = Math.Max(1, availH - h1);

                dividers.Add(new SplitDividerInfo
                {
                    Node = this,
                    Orientation = SplitOrientation.Horizontal,
                    LinePos = y + h1 + gap / 2,
                    SpanStart = x,
                    SpanEnd = x + w,
                    ParentStart = y,
                    ParentSize = h,
                    MinFirstSize = min1,
                    MinSecondSize = min2,
                    Gap = gap
                });

                First.CollectDividers(x, y, w, h1, zoneGap, minLeafPx, dividers);
                Second.CollectDividers(x, y + h1 + gap, w, h2, zoneGap, minLeafPx, dividers);
            }
        }

        public bool SplitLeaf(int targetZoneId, SplitOrientation orientation, double ratio = 0.5)
        {
            if (orientation == SplitOrientation.None) return false;

            if (IsLeaf)
            {
                if (ZoneId != targetZoneId) return false;

                First = CreateLeaf(ZoneId);
                Second = CreateLeaf(ZoneId + 1);
                Orientation = orientation;
                SplitRatio = Math.Clamp(ratio, 0.1, 0.9);
                return true;
            }

            return (First?.SplitLeaf(targetZoneId, orientation, ratio) ?? false)
                || (Second?.SplitLeaf(targetZoneId, orientation, ratio) ?? false);
        }

        public bool RemoveLeaf(int targetZoneId)
        {
            if (IsLeaf || First == null || Second == null)
                return false;

            if (First.IsLeaf && First.ZoneId == targetZoneId)
            {
                CopyFrom(Second);
                return true;
            }

            if (Second.IsLeaf && Second.ZoneId == targetZoneId)
            {
                CopyFrom(First);
                return true;
            }

            return First.RemoveLeaf(targetZoneId) || Second.RemoveLeaf(targetZoneId);
        }

        private void CopyFrom(ZoneSplitNode other)
        {
            Orientation = other.Orientation;
            SplitRatio = other.SplitRatio;
            ZoneId = other.ZoneId;
            First = other.First;
            Second = other.Second;
        }

        public bool TryApplyZoneBounds(
            int targetZoneId,
            int newX, int newY, int newW, int newH,
            int totalWidth, int totalHeight, int zoneGap = 0, int minLeafPx = 80)
        {
            var path = new List<(ZoneSplitNode Split, bool IsFirstChild, int X, int Y, int W, int H)>();
            if (!FindPathWithBounds(targetZoneId, 0, 0, totalWidth, totalHeight, zoneGap, minLeafPx, path))
                return false;

            // Adjust Left edge (nearest Vertical split where target is Second child)
            var leftAnchor = path.LastOrDefault(p => p.Split.Orientation == SplitOrientation.Vertical && !p.IsFirstChild);
            var rightAnchor = path.LastOrDefault(p => p.Split.Orientation == SplitOrientation.Vertical && p.IsFirstChild);
            var topAnchor = path.LastOrDefault(p => p.Split.Orientation == SplitOrientation.Horizontal && !p.IsFirstChild);
            var bottomAnchor = path.LastOrDefault(p => p.Split.Orientation == SplitOrientation.Horizontal && p.IsFirstChild);

            bool changed = false;

            // Compute current bounds of target zone to detect which edges actually changed
            var currentZones = ComputeZones(totalWidth, totalHeight, zoneGap);
            var curZone = currentZones.FirstOrDefault(z => z.Id == targetZoneId);
            if (curZone == null) return false;

            if (leftAnchor.Split != null && newX != curZone.X)
            {
                changed |= SetSplitPosition(leftAnchor.Split, leftAnchor.X, leftAnchor.W, newX - zoneGap / 2, zoneGap, minLeafPx);
            }
            else if (rightAnchor.Split != null && newW != curZone.Width)
            {
                changed |= SetSplitPosition(rightAnchor.Split, rightAnchor.X, rightAnchor.W, curZone.X + newW + zoneGap / 2, zoneGap, minLeafPx);
            }

            // Re-evaluate if width changed via leftAnchor while right edge moved
            if (leftAnchor.Split != null && newX == curZone.X && newW != curZone.Width && rightAnchor.Split == null)
            {
                int desiredLeft = (curZone.X + curZone.Width) - newW;
                changed |= SetSplitPosition(leftAnchor.Split, leftAnchor.X, leftAnchor.W, desiredLeft - zoneGap / 2, zoneGap, minLeafPx);
            }

            if (topAnchor.Split != null && newY != curZone.Y)
            {
                changed |= SetSplitPosition(topAnchor.Split, topAnchor.Y, topAnchor.H, newY - zoneGap / 2, zoneGap, minLeafPx);
            }
            else if (bottomAnchor.Split != null && newH != curZone.Height)
            {
                changed |= SetSplitPosition(bottomAnchor.Split, bottomAnchor.Y, bottomAnchor.H, curZone.Y + newH + zoneGap / 2, zoneGap, minLeafPx);
            }

            if (topAnchor.Split != null && newY == curZone.Y && newH != curZone.Height && bottomAnchor.Split == null)
            {
                int desiredTop = (curZone.Y + curZone.Height) - newH;
                changed |= SetSplitPosition(topAnchor.Split, topAnchor.Y, topAnchor.H, desiredTop - zoneGap / 2, zoneGap, minLeafPx);
            }

            return changed;
        }

        private static bool SetSplitPosition(ZoneSplitNode split, int parentStart, int parentSize, int desiredLinePos, int zoneGap, int minLeafPx)
        {
            if (split.First == null || split.Second == null) return false;
            int gap = Math.Clamp(zoneGap, 0, Math.Max(0, parentSize - 40));
            int avail = parentSize - gap;
            if (avail <= 0) return false;

            int min1 = split.First.GetMinSize(split.Orientation, zoneGap, minLeafPx);
            int min2 = split.Second.GetMinSize(split.Orientation, zoneGap, minLeafPx);

            int desiredFirstSize = desiredLinePos - parentStart - gap / 2;
            if (min1 + min2 <= avail)
                desiredFirstSize = Math.Clamp(desiredFirstSize, min1, avail - min2);
            else
                desiredFirstSize = Math.Clamp(desiredFirstSize, 1, Math.Max(1, avail - 1));

            split.SplitRatio = Math.Clamp(desiredFirstSize / (double)avail, 0.02, 0.98);
            return true;
        }

        private bool FindPathWithBounds(
            int targetZoneId, int x, int y, int w, int h, int zoneGap, int minLeafPx,
            List<(ZoneSplitNode Split, bool IsFirstChild, int X, int Y, int W, int H)> path)
        {
            if (IsLeaf || First == null || Second == null)
                return ZoneId == targetZoneId;

            double r = Math.Clamp(SplitRatio, 0.05, 0.95);
            if (Orientation == SplitOrientation.Vertical)
            {
                int gap = Math.Clamp(zoneGap, 0, Math.Max(0, w - 40));
                int availW = w - gap;
                int min1 = Math.Min(availW - 20, First.GetMinSize(SplitOrientation.Vertical, zoneGap, 20));
                int min2 = Math.Min(availW - 20, Second.GetMinSize(SplitOrientation.Vertical, zoneGap, 20));
                int w1 = (int)Math.Round(availW * r);
                if (min1 + min2 <= availW) w1 = Math.Clamp(w1, min1, availW - min2);
                int w2 = Math.Max(1, availW - w1);

                path.Add((this, true, x, y, w, h));
                if (First.FindPathWithBounds(targetZoneId, x, y, w1, h, zoneGap, minLeafPx, path))
                    return true;
                path.RemoveAt(path.Count - 1);

                path.Add((this, false, x, y, w, h));
                if (Second.FindPathWithBounds(targetZoneId, x + w1 + gap, y, w2, h, zoneGap, minLeafPx, path))
                    return true;
                path.RemoveAt(path.Count - 1);
            }
            else
            {
                int gap = Math.Clamp(zoneGap, 0, Math.Max(0, h - 40));
                int availH = h - gap;
                int min1 = Math.Min(availH - 20, First.GetMinSize(SplitOrientation.Horizontal, zoneGap, 20));
                int min2 = Math.Min(availH - 20, Second.GetMinSize(SplitOrientation.Horizontal, zoneGap, 20));
                int h1 = (int)Math.Round(availH * r);
                if (min1 + min2 <= availH) h1 = Math.Clamp(h1, min1, availH - min2);
                int h2 = Math.Max(1, availH - h1);

                path.Add((this, true, x, y, w, h));
                if (First.FindPathWithBounds(targetZoneId, x, y, w, h1, zoneGap, minLeafPx, path))
                    return true;
                path.RemoveAt(path.Count - 1);

                path.Add((this, false, x, y, w, h));
                if (Second.FindPathWithBounds(targetZoneId, x, y + h1 + gap, w, h2, zoneGap, minLeafPx, path))
                    return true;
                path.RemoveAt(path.Count - 1);
            }

            return false;
        }

        public static ZoneSplitNode ReconstructFromZones(List<Zone> zones, int screenWidth, int screenHeight)
        {
            if (zones == null || zones.Count == 0)
                return CreateLeaf(1);

            if (zones.Count == 1)
                return CreateLeaf(zones[0].Id);

            int minX = zones.Min(z => z.X);
            int minY = zones.Min(z => z.Y);
            int maxR = Math.Max(minX + 1, zones.Max(z => z.X + z.Width));
            int maxB = Math.Max(minY + 1, zones.Max(z => z.Y + z.Height));

            return BuildGuillotineTree(zones, minX, minY, maxR - minX, maxB - minY);
        }

        private static ZoneSplitNode BuildGuillotineTree(List<Zone> subset, int bx, int by, int bw, int bh)
        {
            if (subset.Count <= 1)
                return CreateLeaf(subset.FirstOrDefault()?.Id ?? 1);

            const int tol = 8;

            // Try vertical guillotine cuts
            var xCuts = subset.Select(z => z.X + z.Width).Where(c => c > bx + tol && c < bx + bw - tol).Distinct().OrderBy(c => c);
            foreach (int cut in xCuts)
            {
                var left = subset.Where(z => z.X + z.Width <= cut + tol).ToList();
                var right = subset.Where(z => z.X >= cut - tol).ToList();
                if (left.Count > 0 && right.Count > 0 && left.Count + right.Count == subset.Count)
                {
                    double ratio = (cut - bx) / (double)Math.Max(1, bw);
                    return CreateSplit(
                        SplitOrientation.Vertical,
                        ratio,
                        BuildGuillotineTree(left, bx, by, cut - bx, bh),
                        BuildGuillotineTree(right, cut, by, bx + bw - cut, bh));
                }
            }

            // Try horizontal guillotine cuts
            var yCuts = subset.Select(z => z.Y + z.Height).Where(c => c > by + tol && c < by + bh - tol).Distinct().OrderBy(c => c);
            foreach (int cut in yCuts)
            {
                var top = subset.Where(z => z.Y + z.Height <= cut + tol).ToList();
                var bottom = subset.Where(z => z.Y >= cut - tol).ToList();
                if (top.Count > 0 && bottom.Count > 0 && top.Count + bottom.Count == subset.Count)
                {
                    double ratio = (cut - by) / (double)Math.Max(1, bh);
                    return CreateSplit(
                        SplitOrientation.Horizontal,
                        ratio,
                        BuildGuillotineTree(top, bx, by, bw, cut - by),
                        BuildGuillotineTree(bottom, bx, cut, bw, by + bh - cut));
                }
            }

            // Fallback: split evenly vertically
            int mid = subset.Count / 2;
            var ordered = subset.OrderBy(z => z.X).ThenBy(z => z.Y).ToList();
            return CreateSplit(
                SplitOrientation.Vertical,
                0.5,
                BuildGuillotineTree(ordered.Take(mid).ToList(), bx, by, bw / 2, bh),
                BuildGuillotineTree(ordered.Skip(mid).ToList(), bx + bw / 2, by, bw - bw / 2, bh));
        }
    }

    public class Zone
    {
        public int Id { get; set; } = 1;
        public string Name { get; set; } = "Zone 1";
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public int Width { get; set; } = 880;
        public int Height { get; set; } = 1440;

        public double PercentX { get; set; } = 0;
        public double PercentY { get; set; } = 0;
        public double PercentWidth { get; set; } = 50;
        public double PercentHeight { get; set; } = 100;

        [JsonIgnore]
        public int Right => X + Width;

        [JsonIgnore]
        public int Bottom => Y + Height;

        [JsonIgnore]
        public double CenterX => X + Width / 2.0;

        [JsonIgnore]
        public double CenterY => Y + Height / 2.0;

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
                return isEn ? $"Zone {Id}" : $"Vùng {Id}";
            }
        }

        public Zone Clone()
        {
            return new Zone(Id, Name, X, Y, Width, Height)
            {
                PercentX = PercentX,
                PercentY = PercentY,
                PercentWidth = PercentWidth,
                PercentHeight = PercentHeight
            };
        }
    }

    public class ZonePreset
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ScreenWidth { get; set; } = 1920;
        public int ScreenHeight { get; set; } = 1080;
        public int ZoneGap { get; set; } = 0;
        public bool IsDefault { get; set; } = false;
        public ZoneSplitNode? SplitRoot { get; set; }
        public List<Zone> Zones { get; set; } = new();

        public ZonePreset() { }

        public ZonePreset(
            string id,
            string name,
            string description,
            int screenWidth,
            int screenHeight,
            ZoneSplitNode splitRoot,
            bool isDefault = false,
            int zoneGap = 0)
        {
            Id = id;
            Name = name;
            Description = description;
            ScreenWidth = screenWidth > 0 ? screenWidth : 1920;
            ScreenHeight = screenHeight > 0 ? screenHeight : 1080;
            ZoneGap = zoneGap;
            IsDefault = isDefault;
            SplitRoot = splitRoot;
            Zones = splitRoot.ComputeZones(ScreenWidth, ScreenHeight, ZoneGap);
        }

        public ZonePreset(string id, string name, string description, int screenWidth, int screenHeight, List<Zone> zones, bool isDefault = false)
        {
            Id = id;
            Name = name;
            Description = description;
            ScreenWidth = screenWidth > 0 ? screenWidth : 1920;
            ScreenHeight = screenHeight > 0 ? screenHeight : 1080;
            IsDefault = isDefault;
            Zones = zones;
            SplitRoot = ZoneSplitNode.ReconstructFromZones(zones, ScreenWidth, ScreenHeight);
            SyncPercentagesFromPixels();
        }

        public void EnsureSplitTreeAndSync(int? overrideWidth = null, int? overrideHeight = null)
        {
            if (overrideWidth.HasValue && overrideWidth.Value > 0) ScreenWidth = overrideWidth.Value;
            if (overrideHeight.HasValue && overrideHeight.Value > 0) ScreenHeight = overrideHeight.Value;
            if (ScreenWidth <= 0) ScreenWidth = 1920;
            if (ScreenHeight <= 0) ScreenHeight = 1080;

            if (SplitRoot == null)
            {
                SplitRoot = ZoneSplitNode.ReconstructFromZones(Zones, ScreenWidth, ScreenHeight);
            }

            Zones = SplitRoot.ComputeZones(ScreenWidth, ScreenHeight, ZoneGap);
        }

        private void SyncPercentagesFromPixels()
        {
            double sw = Math.Max(1, ScreenWidth);
            double sh = Math.Max(1, ScreenHeight);
            foreach (var z in Zones)
            {
                z.PercentX = Math.Round((z.X / sw) * 100.0, 2);
                z.PercentY = Math.Round((z.Y / sh) * 100.0, 2);
                z.PercentWidth = Math.Round((z.Width / sw) * 100.0, 2);
                z.PercentHeight = Math.Round((z.Height / sh) * 100.0, 2);
            }
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
                    "preset_priority_grid" => isEn ? "Priority Grid (25% / 50% / 25% Stacked)" : "Priority Grid (25% / 50% / 25% Xếp tầng)",
                    "preset_grid_2x2" => isEn ? "2x2 Grid (4 Zones)" : "Lưới 2x2 (4 Vùng)",
                    "preset_asymmetric_5" => isEn ? "Asymmetric 5-Zone Layout" : "Bố cục 5 Vùng lệch tâm",
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
                return IsDefault ? $"{LocalizedName} ({defTag})" : $"{Name} ({customTag})";
            }
        }

        [JsonIgnore]
        public bool CanDelete => !IsDefault;

        [JsonIgnore]
        public bool CanOverwrite => !IsDefault;

        public ZonePreset Clone()
        {
            var clonedRoot = SplitRoot?.Clone();
            var cloned = new ZonePreset
            {
                Id = this.Id,
                Name = this.Name,
                Description = this.Description,
                ScreenWidth = this.ScreenWidth,
                ScreenHeight = this.ScreenHeight,
                ZoneGap = this.ZoneGap,
                IsDefault = this.IsDefault,
                SplitRoot = clonedRoot,
                Zones = this.Zones.Select(z => z.Clone()).ToList()
            };
            cloned.EnsureSplitTreeAndSync();
            return cloned;
        }

        public static List<ZonePreset> CreateDefaultPresets(int screenWidth = 0, int screenHeight = 0)
        {
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
                    $"2 Zones | {halfH}px",
                    screenWidth,
                    screenHeight,
                    ZoneSplitNode.CreateSplit(SplitOrientation.Horizontal, 0.5, ZoneSplitNode.CreateLeaf(1), ZoneSplitNode.CreateLeaf(2)),
                    isDefault: true
                ));

                int thirdH = screenHeight / 3;
                presets.Add(new ZonePreset(
                    "preset_vert_triple",
                    "Chia ba Tầng (33% / 33% / 33%)",
                    $"3 Zones | ~{thirdH}px",
                    screenWidth,
                    screenHeight,
                    ZoneSplitNode.CreateSplit(
                        SplitOrientation.Horizontal,
                        1.0 / 3.0,
                        ZoneSplitNode.CreateLeaf(1),
                        ZoneSplitNode.CreateSplit(SplitOrientation.Horizontal, 0.5, ZoneSplitNode.CreateLeaf(2), ZoneSplitNode.CreateLeaf(3))),
                    isDefault: true
                ));

                presets.Add(CreateGrid2x2Preset(screenWidth, screenHeight));
                return presets;
            }

            // Case 2: Ultrawide Monitor (Aspect ratio >= 2.0, e.g. 21:9 or 32:9)
            bool isUltrawide = screenHeight > 0 && ((double)screenWidth / screenHeight) >= 2.0;
            if (isUltrawide)
            {
                double ratio13 = 880.0 / 3440.0;
                int zone1Width = (int)Math.Round(screenWidth * ratio13);
                int zone2Width = screenWidth - zone1Width;
                string title = (screenWidth == 3440 && screenHeight == 1440)
                    ? "Ultrawide 1/3 - 2/3 (Chuẩn 2K)"
                    : $"Ultrawide 1/3 - 2/3 ({zone1Width}px / {zone2Width}px)";

                presets.Add(new ZonePreset(
                    "preset_ultrawide_2k",
                    title,
                    $"Zone 1: {zone1Width}px | Zone 2: {zone2Width}px",
                    screenWidth,
                    screenHeight,
                    ZoneSplitNode.CreateSplit(SplitOrientation.Vertical, ratio13, ZoneSplitNode.CreateLeaf(1), ZoneSplitNode.CreateLeaf(2)),
                    isDefault: true
                ));

                int halfWidth = screenWidth / 2;
                presets.Add(new ZonePreset(
                    "preset_half_half",
                    "Chia đôi cân bằng (50% / 50%)",
                    $"2 Zones | {halfWidth}px",
                    screenWidth,
                    screenHeight,
                    ZoneSplitNode.CreateSplit(SplitOrientation.Vertical, 0.5, ZoneSplitNode.CreateLeaf(1), ZoneSplitNode.CreateLeaf(2)),
                    isDefault: true
                ));

                int thirdWidth = screenWidth / 3;
                presets.Add(new ZonePreset(
                    "preset_triple",
                    "Chia ba cột đều nhau (33% / 33% / 33%)",
                    $"3 Zones | ~{thirdWidth}px",
                    screenWidth,
                    screenHeight,
                    ZoneSplitNode.CreateSplit(
                        SplitOrientation.Vertical,
                        1.0 / 3.0,
                        ZoneSplitNode.CreateLeaf(1),
                        ZoneSplitNode.CreateSplit(SplitOrientation.Vertical, 0.5, ZoneSplitNode.CreateLeaf(2), ZoneSplitNode.CreateLeaf(3))),
                    isDefault: true
                ));

                int sideWidth = Math.Max(100, (screenWidth - 2560) / 2);
                int centerWidth = screenWidth - (sideWidth * 2);
                if (centerWidth > 500)
                {
                    double sideRatio = sideWidth / (double)screenWidth;
                    double rightRatio = centerWidth / (double)(screenWidth - sideWidth);
                    presets.Add(new ZonePreset(
                        "preset_center_focus",
                        "Trung tâm + 2 Cánh",
                        $"3 Zones | {sideWidth}px / {centerWidth}px / {sideWidth}px",
                        screenWidth,
                        screenHeight,
                        ZoneSplitNode.CreateSplit(
                            SplitOrientation.Vertical,
                            sideRatio,
                            ZoneSplitNode.CreateLeaf(1),
                            ZoneSplitNode.CreateSplit(SplitOrientation.Vertical, rightRatio, ZoneSplitNode.CreateLeaf(2), ZoneSplitNode.CreateLeaf(3))),
                        isDefault: true
                    ));
                }

                // 2D Priority Grid (Left 25% | Center 50% | Right 25% Top/Bottom)
                presets.Add(new ZonePreset(
                    "preset_priority_grid",
                    "Priority Grid (25% / 50% / 25% Xếp tầng)",
                    "4 Zones | Left 25% | Center 50% | Right 25% (Top/Bottom)",
                    screenWidth,
                    screenHeight,
                    ZoneSplitNode.CreateSplit(
                        SplitOrientation.Vertical,
                        0.25,
                        ZoneSplitNode.CreateLeaf(1),
                        ZoneSplitNode.CreateSplit(
                            SplitOrientation.Vertical,
                            2.0 / 3.0,
                            ZoneSplitNode.CreateLeaf(2),
                            ZoneSplitNode.CreateSplit(SplitOrientation.Horizontal, 0.5, ZoneSplitNode.CreateLeaf(3), ZoneSplitNode.CreateLeaf(4)))),
                    isDefault: true
                ));

                presets.Add(CreateGrid2x2Preset(screenWidth, screenHeight));
                presets.Add(CreateAsymmetric5Preset(screenWidth, screenHeight));
                return presets;
            }

            // Case 3: Standard 16:9 / 16:10 Monitor
            int leftOneThird = (int)Math.Round(screenWidth / 3.0);
            int rightTwoThird = screenWidth - leftOneThird;
            presets.Add(new ZonePreset(
                "preset_standard_1_3",
                "1/3 Trái - 2/3 Phải (33% / 67%)",
                $"Zone 1: {leftOneThird}px | Zone 2: {rightTwoThird}px",
                screenWidth,
                screenHeight,
                ZoneSplitNode.CreateSplit(SplitOrientation.Vertical, 1.0 / 3.0, ZoneSplitNode.CreateLeaf(1), ZoneSplitNode.CreateLeaf(2)),
                isDefault: true
            ));

            int standardHalf = screenWidth / 2;
            presets.Add(new ZonePreset(
                "preset_standard_half",
                "Chia đôi cân bằng (50% / 50%)",
                $"2 Zones | {standardHalf}px",
                screenWidth,
                screenHeight,
                ZoneSplitNode.CreateSplit(SplitOrientation.Vertical, 0.5, ZoneSplitNode.CreateLeaf(1), ZoneSplitNode.CreateLeaf(2)),
                isDefault: true
            ));

            int leftTwoThird = (int)Math.Round(screenWidth * 2.0 / 3.0);
            int rightOneThird = screenWidth - leftTwoThird;
            presets.Add(new ZonePreset(
                "preset_standard_2_3",
                "2/3 Trái - 1/3 Phải (67% / 33%)",
                $"Zone 1: {leftTwoThird}px | Zone 2: {rightOneThird}px",
                screenWidth,
                screenHeight,
                ZoneSplitNode.CreateSplit(SplitOrientation.Vertical, 2.0 / 3.0, ZoneSplitNode.CreateLeaf(1), ZoneSplitNode.CreateLeaf(2)),
                isDefault: true
            ));

            int standardThird = screenWidth / 3;
            presets.Add(new ZonePreset(
                "preset_standard_triple",
                "Chia ba cột đều nhau (33% / 33% / 33%)",
                $"3 Zones | ~{standardThird}px",
                screenWidth,
                screenHeight,
                ZoneSplitNode.CreateSplit(
                    SplitOrientation.Vertical,
                    1.0 / 3.0,
                    ZoneSplitNode.CreateLeaf(1),
                    ZoneSplitNode.CreateSplit(SplitOrientation.Vertical, 0.5, ZoneSplitNode.CreateLeaf(2), ZoneSplitNode.CreateLeaf(3))),
                isDefault: true
            ));

            presets.Add(new ZonePreset(
                "preset_priority_grid",
                "Priority Grid (65% / 35% Xếp tầng)",
                "3 Zones | Left 65% | Right 35% (Top/Bottom)",
                screenWidth,
                screenHeight,
                ZoneSplitNode.CreateSplit(
                    SplitOrientation.Vertical,
                    0.65,
                    ZoneSplitNode.CreateLeaf(1),
                    ZoneSplitNode.CreateSplit(SplitOrientation.Horizontal, 0.5, ZoneSplitNode.CreateLeaf(2), ZoneSplitNode.CreateLeaf(3))),
                isDefault: true
            ));

            presets.Add(CreateGrid2x2Preset(screenWidth, screenHeight));
            presets.Add(CreateAsymmetric5Preset(screenWidth, screenHeight));
            return presets;
        }

        private static ZonePreset CreateGrid2x2Preset(int screenWidth, int screenHeight)
        {
            return new ZonePreset(
                "preset_grid_2x2",
                "Lưới 2x2 (4 Vùng)",
                "4 Zones | 2x2 (50% x 50%)",
                screenWidth,
                screenHeight,
                ZoneSplitNode.CreateSplit(
                    SplitOrientation.Vertical,
                    0.5,
                    ZoneSplitNode.CreateSplit(SplitOrientation.Horizontal, 0.5, ZoneSplitNode.CreateLeaf(1), ZoneSplitNode.CreateLeaf(3)),
                    ZoneSplitNode.CreateSplit(SplitOrientation.Horizontal, 0.5, ZoneSplitNode.CreateLeaf(2), ZoneSplitNode.CreateLeaf(4))),
                isDefault: true
            );
        }

        private static ZonePreset CreateAsymmetric5Preset(int screenWidth, int screenHeight)
        {
            return new ZonePreset(
                "preset_asymmetric_5",
                "Bố cục 5 Vùng lệch tâm",
                "5 Zones | Top bar, Left wing, Center 2-stack, Right column",
                screenWidth,
                screenHeight,
                ZoneSplitNode.CreateSplit(
                    SplitOrientation.Vertical,
                    0.80,
                    ZoneSplitNode.CreateSplit(
                        SplitOrientation.Horizontal,
                        0.22,
                        ZoneSplitNode.CreateLeaf(1),
                        ZoneSplitNode.CreateSplit(
                            SplitOrientation.Vertical,
                            0.26,
                            ZoneSplitNode.CreateLeaf(3),
                            ZoneSplitNode.CreateSplit(SplitOrientation.Horizontal, 0.5, ZoneSplitNode.CreateLeaf(4), ZoneSplitNode.CreateLeaf(5)))),
                    ZoneSplitNode.CreateLeaf(2)),
                isDefault: true
            );
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
