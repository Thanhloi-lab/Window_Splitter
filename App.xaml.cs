using System.Windows;
using Wpf.Ui.Appearance;

namespace WindowsSplitter
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            }
            catch
            {
                // Fallback to merged dictionary in App.xaml
            }
        }
    }
}
