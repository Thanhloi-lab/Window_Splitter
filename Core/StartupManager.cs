using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace WindowsSplitter.Core
{
    public static class StartupManager
    {
        public const string TaskName = "WindowsSplitter_Elevated";
        public const string RegKeyName = "WindowsSplitter";
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsRunAtStartupEnabled()
        {
            // 1. Check Task Scheduler (Primary elevated method)
            if (IsTaskSchedulerEnabled())
                return true;

            // 2. Check Registry (Fallback)
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                return key?.GetValue(RegKeyName) != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsTaskSchedulerEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{TaskName}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                proc.WaitForExit(2000);
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool SetRunAtStartup(bool enable, bool startMinimized = true)
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WindowsSplitter.exe");

            if (!enable)
            {
                // Remove Task Scheduler task
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"{TaskName}\" /F",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(3000);
                }
                catch { }

                // Remove Registry key
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                    key?.DeleteValue(RegKeyName, false);
                }
                catch { }

                return true;
            }
            else
            {
                string args = startMinimized ? "--autostart --minimized" : "--autostart";
                bool taskSuccess = false;

                // 1. Create Task Scheduler task with HIGHEST runlevel for Admin privileges without UAC prompt
                try
                {
                    string trParam = $"\\\"{exePath}\\\" {args}";
                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Create /TN \"{TaskName}\" /TR \"{trParam}\" /SC ONLOGON /RL HIGHEST /F",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.WaitForExit(4000);
                        taskSuccess = proc.ExitCode == 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to create startup task: {ex.Message}");
                }

                if (taskSuccess)
                {
                    // Clean up registry fallback if task scheduler succeeded, avoiding duplicate launch and UAC prompts
                    try
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                        key?.DeleteValue(RegKeyName, false);
                    }
                    catch { }
                }
                else
                {
                    // Fallback to Registry Run key only if schtasks failed
                    try
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                        if (key != null)
                        {
                            key.SetValue(RegKeyName, $"\"{exePath}\" {args}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to set registry run key: {ex.Message}");
                    }
                }

                return taskSuccess;
            }
        }
    }
}
