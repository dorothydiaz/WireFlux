using System;
using System.IO;
using System.Reflection;

namespace WireFluxInstaller
{
    internal static class ShortcutHelper
    {
        public static void CreateShortcut(
            string shortcutFilePath,
            string targetExePath,
            string arguments,
            string workingDirectory,
            string description,
            string iconLocation)
        {
            string parentDir = Path.GetDirectoryName(shortcutFilePath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("WScript.Shell COM component is unavailable.");

            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutFilePath });

            Type scType = shortcut.GetType();
            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExePath });

            if (!string.IsNullOrEmpty(arguments))
            {
                scType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
            }

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
            }

            if (!string.IsNullOrEmpty(description))
            {
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
            }

            if (!string.IsNullOrEmpty(iconLocation))
            {
                scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconLocation });
            }

            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
    }
}
