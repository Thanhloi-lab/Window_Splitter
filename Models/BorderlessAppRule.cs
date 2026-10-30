namespace WindowsSplitter.Models
{
    public class BorderlessAppRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Process executable name (e.g., "wwm.exe", "chrome.exe")
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Human-friendly display name (e.g., "Where Winds Meet")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Optional window title pattern / substring
        /// </summary>
        public string WindowTitlePattern { get; set; } = string.Empty;

        /// <summary>
        /// Auto apply borderless when window is detected or snapped
        /// </summary>
        public bool AutoApply { get; set; } = true;

        /// <summary>
        /// Target Monitor device name (empty = Auto/Current monitor, "primary" = Primary monitor, or specific device name)
        /// </summary>
        public string TargetMonitorDevice { get; set; } = string.Empty;

        /// <summary>
        /// Target Zone ID (0 = Fullscreen, 1 = Zone 1, 2 = Zone 2, etc.)
        /// </summary>
        public int TargetZoneId { get; set; } = 2;

        /// <summary>
        /// Height reaches screen height to cover the taskbar
        /// </summary>
        public bool CoverTaskbar { get; set; } = true;

        /// <summary>
        /// Floating [- [] X] Mini-Control Bar in top-right corner
        /// </summary>
        public bool ShowFloatingCaption { get; set; } = true;

        /// <summary>
        /// Disable Windows 11 rounded corners for seamless docking
        /// </summary>
        public bool RemoveRoundedCorners { get; set; } = true;

        public BorderlessAppRule Clone()
        {
            return new BorderlessAppRule
            {
                Id = this.Id,
                ProcessName = this.ProcessName,
                DisplayName = this.DisplayName,
                WindowTitlePattern = this.WindowTitlePattern,
                AutoApply = this.AutoApply,
                TargetMonitorDevice = this.TargetMonitorDevice,
                TargetZoneId = this.TargetZoneId,
                CoverTaskbar = this.CoverTaskbar,
                ShowFloatingCaption = this.ShowFloatingCaption,
                RemoveRoundedCorners = this.RemoveRoundedCorners
            };
        }
    }
}
