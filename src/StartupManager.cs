using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WireFlux
{
    public static class StartupManager
    {
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppValueName = "WireFlux";
        private static readonly string[] LegacyValueNames = new[] { "NetPulse", "EthernetTrayController" };
        public const string StartupArgument = "--startup";

        public static bool IsRunAtStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false))
                {
                    if (key == null)
                        return false;

                    string str = key.GetValue(AppValueName) as string;
                    if (string.IsNullOrWhiteSpace(str))
                        return false;

                    string exePath = Application.ExecutablePath;
                    return str.IndexOf(exePath, StringComparison.OrdinalIgnoreCase) >= 0 &&
                           str.IndexOf(StartupArgument, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetRunAtStartup(bool enable, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null)
                    {
                        errorMessage = "Could not open Windows startup registry key.";
                        return false;
                    }

                    // Clean up legacy app names if present
                    foreach (string legacy in LegacyValueNames)
                    {
                        if (key.GetValue(legacy) != null)
                        {
                            key.DeleteValue(legacy, false);
                        }
                    }

                    if (enable)
                    {
                        string exePath = Application.ExecutablePath;
                        string command = string.Format("\"{0}\" {1}", exePath, StartupArgument);
                        key.SetValue(AppValueName, command, RegistryValueKind.String);
                    }
                    else
                    {
                        if (key.GetValue(AppValueName) != null)
                        {
                            key.DeleteValue(AppValueName, false);
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to update startup setting: " + ex.Message;
                return false;
            }
        }
    }
}
