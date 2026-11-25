using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("WireFlux Uninstaller")]
[assembly: AssemblyDescription("Uninstalls WireFlux from your computer")]
[assembly: AssemblyCompany("codilumi")]
[assembly: AssemblyProduct("WireFlux")]
[assembly: AssemblyCopyright("Copyright © 2026 codilumi. All rights reserved.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace WireFluxInstaller
{
    internal static class UninstallerProgram
    {
        public const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WireFlux";
        private const string RunRegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string SettingsRegKey = @"Software\WireFlux";

        [STAThread]
        private static void Main(string[] args)
        {
            bool silent = args != null && args.Any(a =>
                string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase));

            if (silent)
            {
                bool keepPrefs = args != null && args.Any(a =>
                    string.Equals(a, "--keep-prefs", StringComparison.OrdinalIgnoreCase));
                PerformUninstall(!keepPrefs);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UninstallerForm());
        }

        public static bool NeedsElevationForPath(string targetDir)
        {
            try
            {
                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                    return false;

                using (var id = WindowsIdentity.GetCurrent())
                {
                    if (new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator))
                        return false;
                }

                string testFile = Path.Combine(targetDir, ".wireflux_uninst_probe_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(testFile, "ok");
                File.Delete(testFile);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void PerformUninstall(bool removePreferences)
        {
            string currentExe = Application.ExecutablePath;
            string currentDir = Path.GetDirectoryName(currentExe);
            string registeredInstallDir = null;

            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallRegKey, false))
                {
                    if (k != null)
                        registeredInstallDir = k.GetValue("InstallLocation") as string;
                }
            }
            catch { }

            // 1. Stop running WireFlux instances
            try
            {
                foreach (var proc in Process.GetProcessesByName("WireFlux"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                    catch { }
                }
            }
            catch { }

            // 2. Remove Desktop shortcut
            try
            {
                string desktopLnk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "WireFlux.lnk");
                if (File.Exists(desktopLnk)) File.Delete(desktopLnk);
            }
            catch { }

            // 3. Remove Start Menu folder
            try
            {
                string startMenuDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "WireFlux");
                if (Directory.Exists(startMenuDir))
                {
                    Directory.Delete(startMenuDir, true);
                }
            }
            catch { }

            // 4. Remove Startup & Uninstall Registry entries (works for both installed & portable runs)
            try
            {
                using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(RunRegKey, true))
                {
                    if (runKey != null)
                    {
                        foreach (string name in new[] { "WireFlux", "NetPulse", "EthernetTrayController" })
                        {
                            if (runKey.GetValue(name) != null)
                                runKey.DeleteValue(name, false);
                        }
                    }
                }
            }
            catch { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegKey, false);
            }
            catch { }

            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(UninstallRegKey, false);
            }
            catch { }

            if (removePreferences)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(SettingsRegKey, false);
                }
                catch { }
            }

            // 5. If a separate installed copy exists in InstallLocation, remove its WireFlux files too
            try
            {
                if (!string.IsNullOrEmpty(registeredInstallDir) &&
                    Directory.Exists(registeredInstallDir) &&
                    !string.Equals(registeredInstallDir.TrimEnd('\\'), (currentDir ?? string.Empty).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string knownFile in new[] { "WireFlux.exe", "Uninstall.exe" })
                    {
                        string p = Path.Combine(registeredInstallDir, knownFile);
                        if (File.Exists(p))
                        {
                            try { File.Delete(p); } catch { }
                        }
                    }
                    try
                    {
                        if (Directory.GetFileSystemEntries(registeredInstallDir).Length == 0)
                            Directory.Delete(registeredInstallDir, false);
                    }
                    catch { }
                }
            }
            catch { }

            // 6. Safely remove only WireFlux.exe & Uninstall.exe in current directory (never touch unrelated files in portable folders)
            try
            {
                if (!string.IsNullOrEmpty(currentDir) && Directory.Exists(currentDir))
                {
                    string siblingApp = Path.Combine(currentDir, "WireFlux.exe");
                    if (File.Exists(siblingApp) && !string.Equals(siblingApp, currentExe, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(siblingApp); } catch { }
                    }

                    // rmdir without /s only removes the folder if it is completely empty
                    string cmdArgs = string.Format(
                        "/c ping 127.0.0.1 -n 2 > nul & del /f /q \"{0}\" & rmdir \"{1}\" 2>nul",
                        currentExe,
                        currentDir);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = cmdArgs,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }
            catch { }
        }
    }

    internal class UninstallerForm : Form
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private readonly SetupCloseButton _closeHeaderBtn;
        private readonly SetupOptionRow _removePrefsRow;
        private readonly SetupButton _uninstallBtn;
        private readonly SetupButton _cancelBtn;
        private readonly Label _statusLabel;
        private bool _busy;

        public UninstallerForm()
        {
            Text = "Uninstall WireFlux";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(452, 276);
            BackColor = SetupTheme.WindowBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            Icon appIcon = null;
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Icon = appIcon; } catch { }

            var header = new SetupPanel
            {
                Location = new Point(1, 1),
                Size = new Size(ClientSize.Width - 2, 60),
                BackColor = SetupTheme.HeaderBg
            };
            header.MouseDown += DragWindow;
            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (appIcon != null)
                {
                    e.Graphics.DrawIcon(appIcon, new Rectangle(20, 14, 32, 32));
                }
                using (var pen = new Pen(SetupTheme.CardBorder, 1f))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var title = new Label
            {
                Text = "Uninstall WireFlux",
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                ForeColor = SetupTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(62, 9)
            };
            title.MouseDown += DragWindow;

            var sub = new Label
            {
                Text = "Remove WireFlux from your computer",
                Font = new Font("Segoe UI", 8.2f),
                ForeColor = SetupTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(63, 33)
            };
            sub.MouseDown += DragWindow;

            _closeHeaderBtn = new SetupCloseButton
            {
                Location = new Point(header.Width - 42, 15)
            };
            _closeHeaderBtn.Click += (s, e) => { if (!_busy) Close(); };

            header.Controls.Add(title);
            header.Controls.Add(sub);
            header.Controls.Add(_closeHeaderBtn);

            _statusLabel = new Label
            {
                Text = "Are you sure you want to uninstall WireFlux and remove its shortcuts?",
                Font = new Font("Segoe UI", 9f),
                ForeColor = SetupTheme.TextSecondary,
                AutoSize = false,
                Size = new Size(408, 22),
                Location = new Point(22, 76)
            };

            _removePrefsRow = new SetupOptionRow(
                "Remove saved preferences",
                "Also delete saved theme & user configuration",
                false)
            {
                Location = new Point(22, 112),
                Size = new Size(408, 48)
            };
            _removePrefsRow.Toggle.AccentColor = SetupTheme.AccentRed;

            _cancelBtn = new SetupButton
            {
                Text = "Cancel",
                IsPrimary = false,
                Location = new Point(22, 180),
                Size = new Size(196, 40)
            };
            _cancelBtn.Click += (s, e) => { if (!_busy) Close(); };

            _uninstallBtn = new SetupButton
            {
                Text = "Uninstall",
                IsPrimary = true,
                AccentColor = SetupTheme.AccentRed,
                Location = new Point(234, 180),
                Size = new Size(196, 40)
            };
            _uninstallBtn.Click += OnUninstallClicked;

            var copy = new Label
            {
                Text = "© 2026 codilumi · All rights reserved",
                Font = new Font("Segoe UI", 7.6f),
                ForeColor = SetupTheme.TextMuted,
                AutoSize = false,
                Size = new Size(408, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(22, 242)
            };

            Controls.Add(header);
            Controls.Add(_statusLabel);
            Controls.Add(_removePrefsRow);
            Controls.Add(_cancelBtn);
            Controls.Add(_uninstallBtn);
            Controls.Add(copy);
        }

        private async void OnUninstallClicked(object sender, EventArgs e)
        {
            if (_busy) return;
            _busy = true;
            _uninstallBtn.Enabled = false;
            _cancelBtn.Enabled = false;
            _closeHeaderBtn.Enabled = false;
            _removePrefsRow.Toggle.Enabled = false;
            _removePrefsRow.Cursor = Cursors.Default;
            _statusLabel.ForeColor = SetupTheme.TextSecondary;
            _statusLabel.Text = "Uninstalling WireFlux...";

            bool removePrefs = _removePrefsRow.Toggle.Checked;
            string installDir = Path.GetDirectoryName(Application.ExecutablePath);

            try
            {
                if (UninstallerProgram.NeedsElevationForPath(installDir))
                {
                    _statusLabel.Text = "Requesting administrator permission...";
                    var psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = removePrefs ? "--silent" : "--silent --keep-prefs",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    await Task.Run(() =>
                    {
                        using (var p = Process.Start(psi))
                        {
                            if (p == null)
                                throw new InvalidOperationException("Elevated uninstall could not start.");
                        }
                    });
                }
                else
                {
                    await Task.Run(() => UninstallerProgram.PerformUninstall(removePrefs));
                }

                _statusLabel.ForeColor = SetupTheme.AccentGreen;
                _statusLabel.Text = "WireFlux has been uninstalled.";
                await Task.Delay(500);
                Close();
            }
            catch (Win32Exception ex)
            {
                _busy = false;
                _uninstallBtn.Enabled = true;
                _cancelBtn.Enabled = true;
                _closeHeaderBtn.Enabled = true;
                _removePrefsRow.Toggle.Enabled = true;
                _removePrefsRow.Cursor = Cursors.Hand;
                _statusLabel.ForeColor = SetupTheme.AccentRed;
                _statusLabel.Text = ex.NativeErrorCode == 1223
                    ? "Administrator permission cancelled."
                    : ("Uninstall error: " + ex.Message);
            }
            catch (Exception ex)
            {
                _busy = false;
                _uninstallBtn.Enabled = true;
                _cancelBtn.Enabled = true;
                _closeHeaderBtn.Enabled = true;
                _removePrefsRow.Toggle.Enabled = true;
                _removePrefsRow.Cursor = Cursors.Hand;
                _statusLabel.ForeColor = SetupTheme.AccentRed;
                _statusLabel.Text = "Uninstall failed: " + ex.Message;
            }
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(SetupTheme.CardBorder, 1f))
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }
}
