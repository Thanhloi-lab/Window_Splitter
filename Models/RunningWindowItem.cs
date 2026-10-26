using System;
using System.IO;
using System.Windows.Media;

namespace WindowsSplitter.Models
{
    public class RunningWindowItem
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }
        public bool IsBorderless { get; set; }
        public string DisplayText => $"{Title} ({ProcessName})";
    }
}
