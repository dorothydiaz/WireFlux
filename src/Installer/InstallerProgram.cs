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

[assembly: AssemblyTitle("WireFlux Setup")]
[assembly: AssemblyDescription("WireFlux Installer for Windows")]
[assembly: AssemblyCompany("codilumi")]
[assembly: AssemblyProduct("WireFlux")]
[assembly: AssemblyCopyright("Copyright © 2026 codilumi. All rights reserved.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace WireFluxInstaller
{
    internal static class InstallerProgram
    {
        public const string AppVersion = "1.0.0";
        private const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WireFlux";
        private const string RunRegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        [STAThread]
        private static void Main(string[] args)
        {
            // Silent or elevated child install mode
            if (args != null && args.Length > 0 &&
                (string.Equals(args[0], "--silent", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "/S", StringComparison.OrdinalIgnoreCase)))
            {
                string defaultDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "WireFlux");
                string targetDir = args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : defaultDir;
                bool desktop = args.Length < 3 || args[2] != "0";
                bool startMenu = args.Length < 4 || args[3] != "0";
                bool startup = args.Length < 5 || args[4] != "0";

                try
                {
                    PerformInstall(targetDir, desktop, startMenu, startup, null);
                    Environment.ExitCode = 0;
                }
                catch
                {
                    Environment.ExitCode = 1;
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }

        public static bool NeedsElevationForPath(string targetDir)
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                {
                    if (new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator))
                        return false;
                }

                string probeDir = targetDir;
                while (!string.IsNullOrEmpty(probeDir) && !Directory.Exists(probeDir))
                {
                    probeDir = Path.GetDirectoryName(probeDir);
                }
                if (string.IsNullOrEmpty(probeDir))
                    return false;

                string testFile = Path.Combine(probeDir, ".wireflux_write_probe_" + Guid.NewGuid().ToString("N"));
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

        public static void PerformInstall(
            string installDir,
            bool createDesktopShortcut,
            bool createStartMenuShortcut,
            bool runAtStartup,
            Action<float, string> onProgress)
        {
            if (onProgress != null) onProgress(0.10f, "Stopping any running WireFlux instance...");

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

            if (onProgress != null) onProgress(0.25f, "Creating installation directory...");
            Directory.CreateDirectory(installDir);

            string exeDest = Path.Combine(installDir, "WireFlux.exe");
            string uninstDest = Path.Combine(installDir, "Uninstall.exe");

            if (onProgress != null) onProgress(0.45f, "Extracting WireFlux application...");
            ExtractResource("WireFlux.exe", exeDest);
            ExtractResource("Uninstall.exe", uninstDest);

            if (createDesktopShortcut)
            {
                if (onProgress != null) onProgress(0.68f, "Creating Desktop shortcut...");
                string desktopLnk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "WireFlux.lnk");
                ShortcutHelper.CreateShortcut(
                    desktopLnk,
                    exeDest,
                    null,
                    installDir,
                    "WireFlux — Network, Radio & Speed Hub",
                    exeDest + ",0");
            }

            if (createStartMenuShortcut)
            {
                if (onProgress != null) onProgress(0.80f, "Creating Start Menu shortcuts...");
                string startMenuFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "WireFlux");
                Directory.CreateDirectory(startMenuFolder);

                ShortcutHelper.CreateShortcut(
                    Path.Combine(startMenuFolder, "WireFlux.lnk"),
                    exeDest,
                    null,
                    installDir,
                    "WireFlux — Network, Radio & Speed Hub",
                    exeDest + ",0");

                ShortcutHelper.CreateShortcut(
                    Path.Combine(startMenuFolder, "Uninstall WireFlux.lnk"),
                    uninstDest,
                    null,
                    installDir,
                    "Uninstall WireFlux",
                    uninstDest + ",0");
            }

            if (onProgress != null) onProgress(0.90f, "Configuring Windows startup & Installed Apps...");
            using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(RunRegKey, true))
            {
                if (runKey != null)
                {
                    if (runKey.GetValue("EthernetTrayController") != null)
                        runKey.DeleteValue("EthernetTrayController", false);
                    if (runKey.GetValue("NetPulse") != null)
                        runKey.DeleteValue("NetPulse", false);

                    if (runAtStartup)
                    {
                        runKey.SetValue("WireFlux", string.Format("\"{0}\" --startup", exeDest), RegistryValueKind.String);
                    }
                    else if (runKey.GetValue("WireFlux") != null)
                    {
                        runKey.DeleteValue("WireFlux", false);
                    }
                }
            }

            long totalBytes = new FileInfo(exeDest).Length + new FileInfo(uninstDest).Length;
            int sizeKb = (int)Math.Max(64, totalBytes / 1024L);

            using (RegistryKey uninstKey = Registry.CurrentUser.CreateSubKey(UninstallRegKey))
            {
                if (uninstKey != null)
                {
                    uninstKey.SetValue("DisplayName", "WireFlux", RegistryValueKind.String);
                    uninstKey.SetValue("DisplayVersion", AppVersion, RegistryValueKind.String);
                    uninstKey.SetValue("Publisher", "codilumi", RegistryValueKind.String);
                    uninstKey.SetValue("DisplayIcon", exeDest + ",0", RegistryValueKind.String);
                    uninstKey.SetValue("InstallLocation", installDir, RegistryValueKind.String);
                    uninstKey.SetValue("UninstallString", string.Format("\"{0}\"", uninstDest), RegistryValueKind.String);
                    uninstKey.SetValue("QuietUninstallString", string.Format("\"{0}\" --silent", uninstDest), RegistryValueKind.String);
                    uninstKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
                    uninstKey.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
                    uninstKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    uninstKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }

            if (onProgress != null) onProgress(1.0f, "WireFlux installed successfully!");
        }

        private static void ExtractResource(string resourceName, string destinationPath)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("Missing embedded installer payload: " + resourceName);

                using (FileStream fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(fs);
                }
            }
        }
    }

    internal class InstallerForm : Form
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private readonly SetupCloseButton _closeHeaderBtn;
        private readonly TextBox _pathBox;
        private readonly SetupButton _browseBtn;
        private readonly SetupOptionRow _desktopRow;
        private readonly SetupOptionRow _startMenuRow;
        private readonly SetupOptionRow _startupRow;
        private readonly SetupOptionRow _launchRow;
        private readonly SetupPanel _progressPanel;
        private readonly Label _statusLabel;
        private readonly SetupButton _installBtn;
        private readonly SetupButton _cancelBtn;

        private float _progress;
        private float _displayedProgress;
        private readonly Timer _progTimer;
        private bool _installed;
        private bool _busy;

        public InstallerForm()
        {
            Text = "WireFlux Setup";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(480, 514);
            BackColor = SetupTheme.WindowBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            Icon appIcon = null;
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Icon = appIcon; } catch { }

            // Header
            var header = new SetupPanel
            {
                Location = new Point(1, 1),
                Size = new Size(ClientSize.Width - 2, 62),
                BackColor = SetupTheme.HeaderBg
            };
            header.MouseDown += DragWindow;
            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (appIcon != null)
                {
                    e.Graphics.DrawIcon(appIcon, new Rectangle(20, 15, 32, 32));
                }
                using (var pen = new Pen(SetupTheme.CardBorder, 1f))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var title = new Label
            {
                Text = "WireFlux Setup",
                Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold),
                ForeColor = SetupTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(62, 10)
            };
            title.MouseDown += DragWindow;

            var subtitle = new Label
            {
                Text = "Version 1.0.0  ·  Network, Radio & Speed Hub",
                Font = new Font("Segoe UI", 8.2f),
                ForeColor = SetupTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(63, 34)
            };
            subtitle.MouseDown += DragWindow;

            _closeHeaderBtn = new SetupCloseButton
            {
                Location = new Point(header.Width - 42, 16)
            };
            _closeHeaderBtn.Click += (s, e) => { if (!_busy) Close(); };

            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(_closeHeaderBtn);

            // Install Location Card
            var pathCard = new SetupPanel
            {
                Location = new Point(22, 78),
                Size = new Size(436, 74),
                BackColor = Color.Transparent
            };
            pathCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(SetupTheme.WindowBg);
                var r = new RectangleF(0.5f, 0.5f, pathCard.Width - 2f, pathCard.Height - 2f);
                using (var path = SetupTheme.CreateRoundedRect(r, 10f))
                using (var fill = new SolidBrush(SetupTheme.CardBg))
                using (var pen = new Pen(SetupTheme.CardBorder, 1f))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }

                using (var lblFont = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold))
                using (var lblBrush = new SolidBrush(SetupTheme.TextMuted))
                {
                    g.DrawString("Installation Folder", lblFont, lblBrush, new PointF(14f, 9f));
                }

                var boxBorder = new Rectangle(14, 31, 318, 28);
                using (var boxFill = new SolidBrush(SetupTheme.SurfaceBg))
                using (var boxPen = new Pen(SetupTheme.SurfaceBorder, 1f))
                {
                    g.FillRectangle(boxFill, boxBorder);
                    g.DrawRectangle(boxPen, boxBorder);
                }
            };

            string defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "WireFlux");

            _pathBox = new TextBox
            {
                Text = defaultDir,
                BorderStyle = BorderStyle.None,
                BackColor = SetupTheme.SurfaceBg,
                ForeColor = SetupTheme.TextPrimary,
                Font = new Font("Segoe UI", 8.8f),
                Location = new Point(22, 37),
                Size = new Size(302, 18)
            };

            _browseBtn = new SetupButton
            {
                Text = "Browse...",
                IsPrimary = false,
                ClearColor = SetupTheme.CardBg,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                Location = new Point(340, 29),
                Size = new Size(84, 32)
            };
            _browseBtn.Click += OnBrowseClicked;

            pathCard.Controls.Add(_pathBox);
            pathCard.Controls.Add(_browseBtn);

            // Option Rows
            _desktopRow = new SetupOptionRow("Create Desktop Shortcut", "Add a WireFlux icon to your Desktop", true)
            {
                Location = new Point(22, 164),
                Size = new Size(436, 44)
            };

            _startMenuRow = new SetupOptionRow("Create Start Menu Shortcut", "Pin WireFlux in the Windows Start Menu", true)
            {
                Location = new Point(22, 214),
                Size = new Size(436, 44)
            };

            _startupRow = new SetupOptionRow("Run at Windows Startup", "Auto-start silently in the system tray", true)
            {
                Location = new Point(22, 264),
                Size = new Size(436, 44)
            };

            _launchRow = new SetupOptionRow("Launch WireFlux when finished", "Open WireFlux right after setup completes", true)
            {
                Location = new Point(22, 314),
                Size = new Size(436, 44)
            };

            // Status + Progress bar
            _statusLabel = new Label
            {
                Text = "Ready to install WireFlux on your computer.",
                Font = new Font("Segoe UI", 8.3f),
                ForeColor = SetupTheme.TextSecondary,
                AutoSize = false,
                Size = new Size(436, 18),
                Location = new Point(24, 372)
            };

            _progressPanel = new SetupPanel
            {
                Location = new Point(22, 395),
                Size = new Size(436, 8),
                BackColor = SetupTheme.WindowBg
            };
            _progressPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(SetupTheme.WindowBg);
                var tr = new RectangleF(0.5f, 0.5f, _progressPanel.Width - 2f, _progressPanel.Height - 2f);
                using (var tp = SetupTheme.CreateRoundedRect(tr, 3f))
                using (var tb = new SolidBrush(SetupTheme.ToggleOffFill))
                {
                    g.FillPath(tb, tp);
                }

                if (_displayedProgress > 0.005f)
                {
                    float fw = Math.Max(6f, tr.Width * Math.Min(1f, _displayedProgress));
                    var fr = new RectangleF(tr.X, tr.Y, fw, tr.Height);
                    using (var fp = SetupTheme.CreateRoundedRect(fr, 3f))
                    using (var fb = new SolidBrush(_installed ? SetupTheme.AccentGreen : SetupTheme.AccentCyan))
                    {
                        g.FillPath(fb, fp);
                    }
                }
            };

            _progTimer = new Timer { Interval = 16 };
            _progTimer.Tick += (s, e) =>
            {
                _displayedProgress += (_progress - _displayedProgress) * 0.22f;
                if (Math.Abs(_progress - _displayedProgress) < 0.004f)
                {
                    _displayedProgress = _progress;
                    _progTimer.Stop();
                }
                _progressPanel.Invalidate();
            };

            // Bottom Action Buttons
            _cancelBtn = new SetupButton
            {
                Text = "Cancel",
                IsPrimary = false,
                Location = new Point(22, 420),
                Size = new Size(140, 42)
            };
            _cancelBtn.Click += (s, e) => { if (!_busy) Close(); };

            _installBtn = new SetupButton
            {
                Text = "Install WireFlux",
                IsPrimary = true,
                AccentColor = SetupTheme.AccentGreen,
                Location = new Point(174, 420),
                Size = new Size(284, 42)
            };
            _installBtn.Click += OnInstallOrFinishClicked;

            // Copyright footer
            var copyright = new Label
            {
                Text = "© 2026 codilumi · All rights reserved",
                Font = new Font("Segoe UI", 7.6f),
                ForeColor = SetupTheme.TextMuted,
                AutoSize = false,
                Size = new Size(436, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(22, 480)
            };

            Controls.Add(header);
            Controls.Add(pathCard);
            Controls.Add(_desktopRow);
            Controls.Add(_startMenuRow);
            Controls.Add(_startupRow);
            Controls.Add(_launchRow);
            Controls.Add(_statusLabel);
            Controls.Add(_progressPanel);
            Controls.Add(_cancelBtn);
            Controls.Add(_installBtn);
            Controls.Add(copyright);
        }

        private void OnBrowseClicked(object sender, EventArgs e)
        {
            if (_busy || _installed) return;
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Choose installation folder for WireFlux";
                dlg.SelectedPath = _pathBox.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                {
                    string chosen = dlg.SelectedPath;
                    if (!chosen.EndsWith("WireFlux", StringComparison.OrdinalIgnoreCase))
                    {
                        chosen = Path.Combine(chosen, "WireFlux");
                    }
                    _pathBox.Text = chosen;
                }
            }
        }

        private async void OnInstallOrFinishClicked(object sender, EventArgs e)
        {
            if (_busy) return;

            if (_installed)
            {
                if (_launchRow.Toggle.Checked)
                {
                    string installedExe = Path.Combine(_pathBox.Text.Trim(), "WireFlux.exe");
                    if (File.Exists(installedExe))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = installedExe,
                                WorkingDirectory = Path.GetDirectoryName(installedExe),
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    }
                }
                Close();
                return;
            }

            string targetDir = _pathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                _statusLabel.Text = "Please enter a valid installation folder.";
                _statusLabel.ForeColor = SetupTheme.AccentRed;
                return;
            }

            _busy = true;
            SetControlsInteractive(false);
            _statusLabel.ForeColor = SetupTheme.TextSecondary;

            bool desktop = _desktopRow.Toggle.Checked;
            bool startMenu = _startMenuRow.Toggle.Checked;
            bool startup = _startupRow.Toggle.Checked;

            try
            {
                if (InstallerProgram.NeedsElevationForPath(targetDir))
                {
                    SetProgress(0.35f, "Requesting administrator permission for protected folder...");
                    var psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = string.Format("--silent \"{0}\" {1} {2} {3}",
                            targetDir,
                            desktop ? "1" : "0",
                            startMenu ? "1" : "0",
                            startup ? "1" : "0"),
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    await Task.Run(() =>
                    {
                        using (var p = Process.Start(psi))
                        {
                            if (p == null || !p.WaitForExit(30000) || p.ExitCode != 0)
                                throw new InvalidOperationException("Elevated installation did not complete.");
                        }
                    });
                    SetProgress(1.0f, "WireFlux installed successfully!");
                }
                else
                {
                    await Task.Run(() =>
                    {
                        InstallerProgram.PerformInstall(targetDir, desktop, startMenu, startup, (prog, msg) =>
                        {
                            BeginInvoke(new Action(() => SetProgress(prog, msg)));
                        });
                    });
                }

                _installed = true;
                _busy = false;
                _closeHeaderBtn.Enabled = true;
                _statusLabel.ForeColor = SetupTheme.AccentGreen;
                _cancelBtn.Visible = false;
                _installBtn.Location = new Point(22, 420);
                _installBtn.Size = new Size(436, 42);
                _installBtn.Text = "Finish";
                _installBtn.AccentColor = SetupTheme.AccentCyan;
                _installBtn.Enabled = true;
                _launchRow.Toggle.Enabled = true;
            }
            catch (Win32Exception ex)
            {
                _busy = false;
                SetControlsInteractive(true);
                SetProgress(0f, ex.NativeErrorCode == 1223
                    ? "Administrator permission cancelled."
                    : ("Installation error: " + ex.Message));
                _statusLabel.ForeColor = SetupTheme.AccentRed;
            }
            catch (Exception ex)
            {
                _busy = false;
                SetControlsInteractive(true);
                SetProgress(0f, "Installation failed: " + ex.Message);
                _statusLabel.ForeColor = SetupTheme.AccentRed;
            }
        }

        private void SetControlsInteractive(bool enabled)
        {
            _pathBox.ReadOnly = !enabled;
            _browseBtn.Enabled = enabled;
            _closeHeaderBtn.Enabled = enabled;
            _desktopRow.Toggle.Enabled = enabled;
            _startMenuRow.Toggle.Enabled = enabled;
            _startupRow.Toggle.Enabled = enabled;
            _launchRow.Toggle.Enabled = enabled;
            _installBtn.Enabled = enabled;
            _cancelBtn.Enabled = enabled;
        }

        private void SetProgress(float value, string message)
        {
            _progress = value;
            _statusLabel.Text = message;
            _progTimer.Start();
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
