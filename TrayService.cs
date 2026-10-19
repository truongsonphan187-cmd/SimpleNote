using System;
using Microsoft.Win32;

namespace SimpleNote
{
    public static class TrayService
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "SimpleNote";

        /// <summary>
        /// Checks whether SimpleNote is configured to start automatically with Windows in HKCU\...\Run.
        /// </summary>
        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                var val = key?.GetValue(AppRegistryName);
                return val != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enables or disables SimpleNote startup with Windows via the HKCU\...\Run registry key.
        /// </summary>
        public static void SetStartupEnabled(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppRegistryName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppRegistryName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update startup registry: {ex.Message}");
            }
        }
    }
}
