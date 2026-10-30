using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WindowsSplitter.Models
{
    public class RunningWindowItem : INotifyPropertyChanged
    {
        private bool _isBorderless;

        public IntPtr Hwnd { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }

        public bool IsBorderless
        {
            get => _isBorderless;
            set
            {
                if (_isBorderless != value)
                {
                    _isBorderless = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActionText));
                    OnPropertyChanged(nameof(StatusBadge));
                    OnPropertyChanged(nameof(HasStatusBadge));
                }
            }
        }

        public string ActionText => IsBorderless 
            ? Core.LocalizationManager.GetString("Str_BtnRestoreWindow") 
            : Core.LocalizationManager.GetString("Str_BtnMakeBorderless");
        public string StatusBadge => IsBorderless 
            ? Core.LocalizationManager.GetString("Str_BadgeBorderless") 
            : "";
        public bool HasStatusBadge => IsBorderless;
        public string DisplayText => $"{Title} ({ProcessName})";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
