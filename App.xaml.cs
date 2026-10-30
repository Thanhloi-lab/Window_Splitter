using System.Windows;
using WindowsSplitter.Core;
using Wpf.Ui.Appearance;

namespace WindowsSplitter
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private const string AppMutexName = "WindowsSplitter_SingleInstance_Mutex";
        public static uint WM_SHOW_SPLITTER { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            WM_SHOW_SPLITTER = NativeMethods.RegisterWindowMessage("WindowsSplitter_ShowMainWindow_Msg");

            _singleInstanceMutex = new Mutex(true, AppMutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another instance is already running! Broadcast message to show existing window
                NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, WM_SHOW_SPLITTER, IntPtr.Zero, IntPtr.Zero);
                Shutdown();
                return;
            }

            base.OnStartup(e);
            try
            {
                var settings = Models.AppSettings.Load();
                LocalizationManager.Initialize(settings.Language);
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            }
            catch
            {
                // Fallback to merged dictionary in App.xaml
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch { }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
            base.OnExit(e);
        }
    }
}
