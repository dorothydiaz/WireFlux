using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace WireFlux
{
    public class MainForm : Form
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private readonly Action _onEnableRequested;
        private readonly Action _onDisableRequested;
        private readonly Action<bool> _onWifiToggleRequested;
        private readonly Action<bool> _onAirplaneToggleRequested;
        private readonly Action _onRefreshRequested;
        private readonly Action _onFlushDnsRequested;
        private readonly Action _onCopyIpRequested;
        private readonly Action _onOpenAdaptersRequested;
        private readonly Action<string> _onAdapterSelected;
        private readonly Action _onMinimizeToTray;
        private readonly Action<AppTheme> _onThemeChanged;

        private DoubleBufferedPanel _headerPanel;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private DoubleBufferedPanel _hudCard;
        private TechRadarIndicator _radarIndicator;
        private Label _statusTag;
        private Label _trafficLabel;
        private Label _statusBadgeLabel;
        private Label _statusDetailLabel;
        private Label _adapterDescLabel;
        private Label _adapterCaption;
        private TechAdapterSelector _adapterSelector;

        private TechButton _enableButton;
        private TechButton _disableButton;

        private SpeedTestCard _speedTestCard;
        private CancellationTokenSource _speedTestCts;

        private TechToggleRow _wifiRow;
        private TechToggleRow _airplaneRow;

        private TechButton _refreshButton;
        private TechButton _flushDnsButton;
        private TechButton _copyIpButton;
        private TechButton _adaptersButton;

        private TechToggleRow _startupRow;

        private Label _terminalLabel;
        private Label _pingLabel;
        private Label _copyrightLabel;
        private TechChromeButton _themeBtn;
        private TechChromeButton _minBtn;
        private TechChromeButton _closeBtn;
        private ToolTip _toolTip;
        private Timer _pulseTimer;
        private Timer _telemetryTimer;

        private AppTheme _theme;
        private bool _isUpdatingUi;
        private bool _allowClose;
        private bool _lastMessageIsError;
        private readonly Stopwatch _animClock = new Stopwatch();
        private AdapterConnectionState _currentState = AdapterConnectionState.Unknown;
        private Color _stateAccent;

        private string _activeAdapterName;
        private string _latestIpv4;
        private string _latestGateway;
        private long _prevRxBytes = -1;
        private long _prevTxBytes = -1;
        private DateTime _prevSampleUtc = DateTime.MinValue;
        private int _telemetryTickCount;
        private bool _pingInFlight;

        public string LatestIPv4Address { get { return _latestIpv4; } }

        public MainForm(
            AppTheme initialTheme,
            Action onEnableRequested,
            Action onDisableRequested,
            Action<bool> onWifiToggleRequested,
            Action<bool> onAirplaneToggleRequested,
            Action onRefreshRequested,
            Action onFlushDnsRequested,
            Action onCopyIpRequested,
            Action onOpenAdaptersRequested,
            Action<string> onAdapterSelected,
            Action onMinimizeToTray,
            Action<AppTheme> onThemeChanged)
        {
            _theme = initialTheme ?? AppTheme.Dark;
            _stateAccent = _theme.AccentCyan;
            _onEnableRequested = onEnableRequested;
            _onDisableRequested = onDisableRequested;
            _onWifiToggleRequested = onWifiToggleRequested;
            _onAirplaneToggleRequested = onAirplaneToggleRequested;
            _onRefreshRequested = onRefreshRequested;
            _onFlushDnsRequested = onFlushDnsRequested;
            _onCopyIpRequested = onCopyIpRequested;
            _onOpenAdaptersRequested = onOpenAdaptersRequested;
            _onAdapterSelected = onAdapterSelected;
            _onMinimizeToTray = onMinimizeToTray;
            _onThemeChanged = onThemeChanged;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            InitializeUi();
            ApplyTheme();
            StartTimers();
        }

        public void AllowExitClose()
        {
            _allowClose = true;
        }

        public void StartSpeedTestFromTray()
        {
            if (!_speedTestCard.IsRunning)
            {
                ToggleSpeedTest();
            }
        }

        private void StartTimers()
        {
            _animClock.Start();
            _pulseTimer = new Timer { Interval = 16 };
            _pulseTimer.Tick += (s, e) =>
            {
                if (!Visible || _radarIndicator == null || _radarIndicator.IsDisposed) return;
                if (_currentState == AdapterConnectionState.Disabled || _currentState == AdapterConnectionState.Unknown)
                    return;
                float progress = (float)(_animClock.Elapsed.TotalSeconds / 2.0);
                _radarIndicator.AnimationProgress = progress;
            };
            _pulseTimer.Start();

            _telemetryTimer = new Timer { Interval = 1000 };
            _telemetryTimer.Tick += (s, e) => SampleLiveTelemetry();
            _telemetryTimer.Start();
        }

        private void SampleLiveTelemetry()
        {
            if (!Visible || IsDisposed) return;

            if (_currentState != AdapterConnectionState.Connected || string.IsNullOrEmpty(_activeAdapterName))
            {
                _latestIpv4 = null;
                _latestGateway = null;
                _prevRxBytes = -1;
                _prevTxBytes = -1;
                _trafficLabel.Text = string.Empty;
                _pingLabel.Text = "Offline";
                _pingLabel.ForeColor = _theme.TextMuted;
                return;
            }

            AdapterLiveMetrics metrics = NetworkDiagnosticsService.GetAdapterMetrics(_activeAdapterName);
            if (metrics != null)
            {
                _latestIpv4 = metrics.IPv4Address;
                _latestGateway = metrics.GatewayAddress;

                if (!string.IsNullOrEmpty(metrics.IPv4Address))
                {
                    _statusDetailLabel.Text = string.IsNullOrEmpty(metrics.LinkSpeedText)
                        ? ("IPv4: " + metrics.IPv4Address)
                        : string.Format("{0}  ·  {1}", metrics.IPv4Address, metrics.LinkSpeedText);
                }

                DateTime now = DateTime.UtcNow;
                if (_prevRxBytes >= 0 && _prevTxBytes >= 0 && _prevSampleUtc != DateTime.MinValue)
                {
                    double elapsed = (now - _prevSampleUtc).TotalSeconds;
                    if (elapsed > 0.2)
                    {
                        double rxRate = Math.Max(0, metrics.BytesReceived - _prevRxBytes) / elapsed;
                        double txRate = Math.Max(0, metrics.BytesSent - _prevTxBytes) / elapsed;
                        _trafficLabel.Text = string.Format("↓ {0}   ↑ {1}",
                            NetworkDiagnosticsService.FormatByteRate(rxRate),
                            NetworkDiagnosticsService.FormatByteRate(txRate));
                    }
                }

                _prevRxBytes = metrics.BytesReceived;
                _prevTxBytes = metrics.BytesSent;
                _prevSampleUtc = now;
            }

            _telemetryTickCount++;
            if (!_speedTestCard.IsRunning && (_telemetryTickCount == 1 || _telemetryTickCount % 4 == 0) && !_pingInFlight)
            {
                _pingInFlight = true;
                string gw = _latestGateway;
                Task.Run(() => NetworkDiagnosticsService.MeasureLatencyMs(gw))
                    .ContinueWith(t =>
                    {
                        _pingInFlight = false;
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke(new Action(() =>
                        {
                            if (IsDisposed) return;
                            int? ms = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                            if (ms.HasValue)
                            {
                                _pingLabel.Text = string.Format("Ping: {0} ms", ms.Value);
                                _pingLabel.ForeColor = ms.Value < 50
                                    ? _theme.AccentGreen
                                    : (ms.Value < 120 ? _theme.AccentAmber : _theme.AccentRed);
                            }
                            else
                            {
                                _pingLabel.Text = "Ping: --";
                                _pingLabel.ForeColor = _theme.TextMuted;
                            }
                        }));
                    });
            }
        }

        private async void ToggleSpeedTest()
        {
            if (_speedTestCard.IsRunning)
            {
                if (_speedTestCts != null)
                {
                    _speedTestCts.Cancel();
                }
                return;
            }

            _speedTestCts = new CancellationTokenSource();
            _speedTestCard.IsRunning = true;
            SetMessage("Running internet download & upload speed test...", false);

            try
            {
                var finalInfo = await SpeedTestService.RunAsync(
                    progress =>
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke(new Action(() =>
                        {
                            if (IsDisposed) return;
                            _speedTestCard.UpdateProgress(progress);
                            if (progress.LatencyMs.HasValue)
                            {
                                _pingLabel.Text = string.Format("Ping: {0} ms", progress.LatencyMs.Value);
                            }
                        }));
                    },
                    _speedTestCts.Token);

                if (!IsDisposed)
                {
                    if (finalInfo.Stage == SpeedTestStage.Completed)
                    {
                        SetMessage(string.Format(
                            "Speed test complete: ↓ {0:0.0} Mbps  ·  ↑ {1:0.0} Mbps",
                            finalInfo.DownloadMbps ?? 0,
                            finalInfo.UploadMbps ?? 0), false);
                    }
                    else if (finalInfo.Stage == SpeedTestStage.Cancelled)
                    {
                        SetMessage("Speed test cancelled.", false);
                    }
                    else if (finalInfo.Stage == SpeedTestStage.Failed)
                    {
                        SetMessage(finalInfo.StatusText, true);
                    }
                }
            }
            finally
            {
                if (_speedTestCts != null)
                {
                    _speedTestCts.Dispose();
                    _speedTestCts = null;
                }
                if (!IsDisposed)
                {
                    _speedTestCard.IsRunning = false;
                }
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

        private void InitializeUi()
        {
            Text = "WireFlux";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(480, 678);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            DoubleBuffered = true;

            _toolTip = new ToolTip();

            // Header bar
            _headerPanel = new DoubleBufferedPanel
            {
                Location = new Point(1, 1),
                Size = new Size(ClientSize.Width - 2, 54),
                BackColor = Color.Transparent
            };
            _headerPanel.MouseDown += DragWindow;
            _headerPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using (var bg = new SolidBrush(_theme.HeaderGradStart))
                {
                    g.FillRectangle(bg, _headerPanel.ClientRectangle);
                }

                using (var pen = new Pen(_theme.CardBorder, 1f))
                {
                    g.DrawLine(pen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
                }
            };

            _titleLabel = new Label
            {
                Text = "WireFlux",
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(22, 8)
            };
            _titleLabel.MouseDown += DragWindow;

            _subtitleLabel = new Label
            {
                Text = "Network, Radio & Speed Hub",
                Font = new Font("Segoe UI", 8.2f),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(23, 31)
            };
            _subtitleLabel.MouseDown += DragWindow;

            _themeBtn = new TechChromeButton
            {
                IconKind = _theme.IsDark ? ChromeIconKind.Sun : ChromeIconKind.Moon,
                Location = new Point(ClientSize.Width - 114, 12)
            };
            _themeBtn.Click += (s, e) => ToggleTheme();

            _minBtn = new TechChromeButton
            {
                IconKind = ChromeIconKind.Minimize,
                Location = new Point(ClientSize.Width - 78, 12)
            };
            _minBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;

            _closeBtn = new TechChromeButton
            {
                IconKind = ChromeIconKind.Close,
                Location = new Point(ClientSize.Width - 42, 12)
            };
            _closeBtn.Click += (s, e) => Close();

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_subtitleLabel);
            _headerPanel.Controls.Add(_themeBtn);
            _headerPanel.Controls.Add(_minBtn);
            _headerPanel.Controls.Add(_closeBtn);

            // Primary Ethernet Status Card
            _hudCard = new DoubleBufferedPanel
            {
                Location = new Point(22, 68),
                Size = new Size(436, 134),
                BackColor = Color.Transparent
            };
            _hudCard.Paint += OnHudCardPaint;

            _radarIndicator = new TechRadarIndicator
            {
                Location = new Point(16, 15),
                Size = new Size(72, 72)
            };

            _statusTag = new Label
            {
                Text = "Ethernet",
                Font = new Font("Segoe UI", 8.2f),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(102, 12)
            };

            _trafficLabel = new Label
            {
                Text = string.Empty,
                Font = new Font("Consolas", 8f),
                AutoSize = false,
                Size = new Size(216, 16),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Location = new Point(204, 12)
            };

            _statusBadgeLabel = new Label
            {
                Text = "Checking...",
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(100, 28)
            };

            _statusDetailLabel = new Label
            {
                Text = "Reading network adapter state",
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = false,
                Size = new Size(318, 18),
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Location = new Point(102, 53)
            };

            _adapterDescLabel = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI", 8f),
                AutoSize = false,
                Size = new Size(318, 16),
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Location = new Point(102, 72)
            };

            _adapterCaption = new Label
            {
                Text = "Adapter",
                Font = new Font("Segoe UI", 8.2f),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(18, 100)
            };

            _adapterSelector = new TechAdapterSelector
            {
                Location = new Point(78, 95),
                Size = new Size(340, 26)
            };
            _adapterSelector.AdapterSelected += (adapterName) =>
            {
                if (_onAdapterSelected != null)
                    _onAdapterSelected(adapterName);
            };

            _hudCard.Controls.Add(_radarIndicator);
            _hudCard.Controls.Add(_statusTag);
            _hudCard.Controls.Add(_trafficLabel);
            _hudCard.Controls.Add(_statusBadgeLabel);
            _hudCard.Controls.Add(_statusDetailLabel);
            _hudCard.Controls.Add(_adapterDescLabel);
            _hudCard.Controls.Add(_adapterCaption);
            _hudCard.Controls.Add(_adapterSelector);

            // Primary Ethernet Buttons
            _enableButton = new TechButton
            {
                Text = "Enable Ethernet",
                IconKind = TechButtonIcon.PowerOn,
                IsPrimary = true,
                Location = new Point(22, 212),
                Size = new Size(212, 40)
            };
            _enableButton.Click += (s, e) => { if (_onEnableRequested != null) _onEnableRequested(); };

            _disableButton = new TechButton
            {
                Text = "Disable Ethernet",
                IconKind = TechButtonIcon.PowerOff,
                IsPrimary = true,
                Location = new Point(246, 212),
                Size = new Size(212, 40)
            };
            _disableButton.Click += (s, e) => { if (_onDisableRequested != null) _onDisableRequested(); };

            // Internet Download & Upload Speed Test Card
            _speedTestCard = new SpeedTestCard
            {
                Location = new Point(22, 262),
                Size = new Size(436, 104)
            };
            _speedTestCard.StartOrStopClicked += (s, e) => ToggleSpeedTest();

            // Wi-Fi & Airplane Mode Toggle Rows
            _wifiRow = new TechToggleRow(FeatureRowIcon.Wifi, "Wi-Fi Connection", "Checking wireless adapter...")
            {
                Location = new Point(22, 376),
                Size = new Size(436, 48)
            };
            _wifiRow.Toggled += (s, e) =>
            {
                if (_isUpdatingUi) return;
                if (_onWifiToggleRequested != null)
                    _onWifiToggleRequested(_wifiRow.Toggle.Checked);
            };

            _airplaneRow = new TechToggleRow(FeatureRowIcon.Airplane, "Airplane Mode", "Checking system radio state...")
            {
                Location = new Point(22, 430),
                Size = new Size(436, 48)
            };
            _airplaneRow.Toggled += (s, e) =>
            {
                if (_isUpdatingUi) return;
                if (_onAirplaneToggleRequested != null)
                    _onAirplaneToggleRequested(_airplaneRow.Toggle.Checked);
            };

            // Quick Network Utilities (2x2 Grid)
            _refreshButton = new TechButton
            {
                Text = "Refresh Status",
                IconKind = TechButtonIcon.Refresh,
                IsPrimary = false,
                Location = new Point(22, 488),
                Size = new Size(212, 35)
            };
            _refreshButton.Click += (s, e) => { if (_onRefreshRequested != null) _onRefreshRequested(); };

            _flushDnsButton = new TechButton
            {
                Text = "Flush DNS",
                IconKind = TechButtonIcon.FlushDns,
                IsPrimary = false,
                Location = new Point(246, 488),
                Size = new Size(212, 35)
            };
            _flushDnsButton.Click += (s, e) => { if (_onFlushDnsRequested != null) _onFlushDnsRequested(); };

            _copyIpButton = new TechButton
            {
                Text = "Copy IP Address",
                IconKind = TechButtonIcon.Copy,
                IsPrimary = false,
                Location = new Point(22, 528),
                Size = new Size(212, 35)
            };
            _copyIpButton.Click += (s, e) => { if (_onCopyIpRequested != null) _onCopyIpRequested(); };

            _adaptersButton = new TechButton
            {
                Text = "Adapter Settings",
                IconKind = TechButtonIcon.Adapters,
                IsPrimary = false,
                Location = new Point(246, 528),
                Size = new Size(212, 35)
            };
            _adaptersButton.Click += (s, e) => { if (_onOpenAdaptersRequested != null) _onOpenAdaptersRequested(); };

            // Windows Startup Row
            _startupRow = new TechToggleRow(FeatureRowIcon.Startup, "Run at Windows Startup", "Starts silently in the system tray")
            {
                Location = new Point(22, 573),
                Size = new Size(436, 48)
            };
            _startupRow.Toggle.SetCheckedSilent(StartupManager.IsRunAtStartupEnabled());
            _startupRow.Toggled += OnStartupToggleChanged;

            // Minimalist Footer + Live Ping
            _terminalLabel = new Label
            {
                Text = "Closing this window minimizes to the system tray.",
                Font = new Font("Segoe UI", 8.2f),
                AutoSize = false,
                Size = new Size(344, 20),
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Location = new Point(24, 630)
            };

            _pingLabel = new Label
            {
                Text = "Ping: --",
                Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(86, 20),
                TextAlign = ContentAlignment.TopRight,
                BackColor = Color.Transparent,
                Location = new Point(370, 630)
            };

            _copyrightLabel = new Label
            {
                Text = "© 2026 codilumi · All rights reserved",
                Font = new Font("Segoe UI", 7.6f),
                AutoSize = false,
                Size = new Size(436, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Location = new Point(22, 653)
            };

            Controls.Add(_headerPanel);
            Controls.Add(_hudCard);
            Controls.Add(_enableButton);
            Controls.Add(_disableButton);
            Controls.Add(_speedTestCard);
            Controls.Add(_wifiRow);
            Controls.Add(_airplaneRow);
            Controls.Add(_refreshButton);
            Controls.Add(_flushDnsButton);
            Controls.Add(_copyIpButton);
            Controls.Add(_adaptersButton);
            Controls.Add(_startupRow);
            Controls.Add(_terminalLabel);
            Controls.Add(_pingLabel);
            Controls.Add(_copyrightLabel);
        }

        private void ToggleTheme()
        {
            _theme = _theme.IsDark ? AppTheme.Light : AppTheme.Dark;
            ThemeManager.SaveTheme(_theme);
            ApplyTheme();
            if (_onThemeChanged != null)
                _onThemeChanged(_theme);
            SetMessage(_theme.IsDark ? "Switched to Dark Mode." : "Switched to Light Mode.", false);
        }

        private void ApplyTheme()
        {
            bool lockRedraw = IsHandleCreated;
            if (lockRedraw)
                SendMessage(Handle, WM_SETREDRAW, 0, 0);

            SuspendLayout();
            try
            {
                BackColor = _theme.WindowBg;
                _stateAccent = GetStateAccent(_currentState);

                _titleLabel.ForeColor = _theme.TextPrimary;
                _subtitleLabel.ForeColor = _theme.TextMuted;

                _themeBtn.Theme = _theme;
                _themeBtn.IconKind = _theme.IsDark ? ChromeIconKind.Sun : ChromeIconKind.Moon;
                _themeBtn.HoverAccent = _theme.AccentCyan;
                _toolTip.SetToolTip(_themeBtn, _theme.IsDark ? "Switch to Light Mode" : "Switch to Dark Mode");

                _minBtn.Theme = _theme;
                _minBtn.HoverAccent = _theme.AccentCyan;
                _toolTip.SetToolTip(_minBtn, "Minimize");

                _closeBtn.Theme = _theme;
                _closeBtn.HoverAccent = _theme.AccentRed;
                _toolTip.SetToolTip(_closeBtn, "Minimize to System Tray");

                _statusTag.ForeColor = _theme.TextMuted;
                _trafficLabel.ForeColor = _theme.TextMuted;
                _statusBadgeLabel.ForeColor = _stateAccent;
                _statusDetailLabel.ForeColor = _theme.TextSecondary;
                _adapterDescLabel.ForeColor = _theme.TextMuted;
                _adapterCaption.ForeColor = _theme.TextMuted;

                _adapterSelector.Theme = _theme;
                _radarIndicator.Theme = _theme;
                _radarIndicator.AccentColor = _stateAccent;
                _radarIndicator.ConnectionState = _currentState;

                _enableButton.Theme = _theme;
                _enableButton.AccentColor = _theme.AccentGreen;

                _disableButton.Theme = _theme;
                _disableButton.AccentColor = _theme.AccentRed;

                _speedTestCard.Theme = _theme;

                _wifiRow.Theme = _theme;
                _wifiRow.IconAccent = _theme.AccentGreen;

                _airplaneRow.Theme = _theme;
                _airplaneRow.IconAccent = _theme.AccentAmber;

                _refreshButton.Theme = _theme;
                _refreshButton.AccentColor = _theme.AccentCyan;

                _flushDnsButton.Theme = _theme;
                _flushDnsButton.AccentColor = _theme.AccentCyan;

                _copyIpButton.Theme = _theme;
                _copyIpButton.AccentColor = _theme.AccentCyan;

                _adaptersButton.Theme = _theme;
                _adaptersButton.AccentColor = _theme.AccentCyan;

                _startupRow.Theme = _theme;
                _startupRow.IconAccent = _theme.AccentCyan;

                _terminalLabel.ForeColor = _lastMessageIsError ? _theme.AccentRed : _theme.TextMuted;
                _pingLabel.ForeColor = _theme.TextMuted;
                _copyrightLabel.ForeColor = TechButton.BlendColor(_theme.TextMuted, _theme.WindowBg, 0.22f);
            }
            finally
            {
                ResumeLayout(true);
                if (lockRedraw)
                {
                    SendMessage(Handle, WM_SETREDRAW, 1, 0);
                    Refresh();
                }
                else
                {
                    Invalidate(true);
                }
            }
        }

        private Color GetStateAccent(AdapterConnectionState state)
        {
            switch (state)
            {
                case AdapterConnectionState.Connected:
                    return _theme.AccentGreen;
                case AdapterConnectionState.EnabledDisconnected:
                    return _theme.AccentAmber;
                case AdapterConnectionState.Disabled:
                    return _theme.AccentRed;
                default:
                    return _theme.AccentCyan;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            using (var borderPen = new Pen(_theme.CardBorder, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }
        }

        private void OnHudCardPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(_theme.WindowBg);

            var rect = new RectangleF(0.5f, 0.5f, _hudCard.Width - 2f, _hudCard.Height - 2f);
            float radius = 12f;

            using (var cardPath = HudDrawing.CreateRoundedRect(rect, radius))
            {
                using (var fill = new SolidBrush(_theme.CardBgTop))
                {
                    g.FillPath(fill, cardPath);
                }

                using (var borderPen = new Pen(_theme.CardBorder, 1f))
                {
                    g.DrawPath(borderPen, cardPath);
                }
            }
        }

        private void OnStartupToggleChanged(object sender, EventArgs e)
        {
            if (_isUpdatingUi) return;

            bool desired = _startupRow.Toggle.Checked;
            string error;
            if (!StartupManager.SetRunAtStartup(desired, out error))
            {
                _startupRow.Toggle.SetCheckedSilent(!desired);
                SetMessage(error, true);
            }
            else
            {
                SetMessage(desired
                    ? "Enabled automatic background startup with Windows."
                    : "Disabled automatic startup with Windows.", false);
            }
        }

        public void UpdateView(
            List<NetworkAdapterInfo> ethernetAdapters,
            NetworkAdapterInfo selectedEthernet,
            NetworkAdapterInfo primaryWifi,
            AirplaneModeInfo airplaneInfo,
            bool isBusy)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateView(ethernetAdapters, selectedEthernet, primaryWifi, airplaneInfo, isBusy)));
                return;
            }

            _isUpdatingUi = true;
            try
            {
                _adapterSelector.SetAdapters(ethernetAdapters, selectedEthernet, isBusy);

                if (selectedEthernet == null)
                {
                    _activeAdapterName = null;
                    _currentState = AdapterConnectionState.Unknown;
                    _stateAccent = _theme.TextMuted;
                    _statusBadgeLabel.Text = "No Adapter";
                    _statusDetailLabel.Text = "No physical Ethernet interface detected";
                    _adapterDescLabel.Text = "Connect an Ethernet adapter and refresh";
                    _trafficLabel.Text = string.Empty;
                    _enableButton.Enabled = false;
                    _disableButton.Enabled = false;
                }
                else
                {
                    if (!string.Equals(_activeAdapterName, selectedEthernet.NetConnectionId, StringComparison.OrdinalIgnoreCase))
                    {
                        _prevRxBytes = -1;
                        _prevTxBytes = -1;
                        _activeAdapterName = selectedEthernet.NetConnectionId;
                    }

                    _currentState = selectedEthernet.State;
                    _stateAccent = GetStateAccent(_currentState);

                    switch (selectedEthernet.State)
                    {
                        case AdapterConnectionState.Connected:
                            _statusBadgeLabel.Text = "Connected";
                            break;
                        case AdapterConnectionState.EnabledDisconnected:
                            _statusBadgeLabel.Text = "Disconnected";
                            break;
                        case AdapterConnectionState.Disabled:
                            _statusBadgeLabel.Text = "Disabled";
                            break;
                        default:
                            _statusBadgeLabel.Text = "Unknown";
                            break;
                    }

                    _statusDetailLabel.Text = selectedEthernet.StatusText;
                    _adapterDescLabel.Text = selectedEthernet.Description;
                    _enableButton.Enabled = !isBusy && selectedEthernet.State == AdapterConnectionState.Disabled;
                    _disableButton.Enabled = !isBusy && selectedEthernet.State != AdapterConnectionState.Disabled;
                }

                if (primaryWifi == null)
                {
                    _wifiRow.Subtitle = "No Wi-Fi adapter detected on this PC";
                    _wifiRow.Toggle.Enabled = false;
                    _wifiRow.Toggle.SetCheckedSilent(false);
                }
                else
                {
                    bool wifiEnabled = primaryWifi.State != AdapterConnectionState.Disabled;
                    _wifiRow.Subtitle = string.Format("{0} · {1}", primaryWifi.NetConnectionId, primaryWifi.StatusText);
                    _wifiRow.Toggle.Enabled = !isBusy;
                    _wifiRow.Toggle.SetCheckedSilent(wifiEnabled);
                }

                if (airplaneInfo == null || !airplaneInfo.IsSupported)
                {
                    _airplaneRow.Subtitle = "Not supported on this system";
                    _airplaneRow.Toggle.Enabled = false;
                    _airplaneRow.Toggle.SetCheckedSilent(false);
                }
                else
                {
                    _airplaneRow.Subtitle = airplaneInfo.IsEnabled
                        ? "On · Wireless radios disabled"
                        : "Off · Wireless radios active";
                    _airplaneRow.Toggle.Enabled = !isBusy;
                    _airplaneRow.Toggle.SetCheckedSilent(airplaneInfo.IsEnabled);
                }

                _statusBadgeLabel.ForeColor = _stateAccent;
                _radarIndicator.AccentColor = _stateAccent;
                _radarIndicator.ConnectionState = _currentState;

                _refreshButton.Enabled = !isBusy;
                _flushDnsButton.Enabled = !isBusy;
                _copyIpButton.Enabled = !isBusy && _currentState == AdapterConnectionState.Connected;
                _adaptersButton.Enabled = true;

                _startupRow.Toggle.SetCheckedSilent(StartupManager.IsRunAtStartupEnabled());

                SampleLiveTelemetry();
                Invalidate(true);
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        public void SetMessage(string message, bool isError)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetMessage(message, isError)));
                return;
            }

            _lastMessageIsError = isError;
            string clean = (message ?? string.Empty).Trim();
            _terminalLabel.Text = clean;
            _terminalLabel.ForeColor = isError ? _theme.AccentRed : _theme.TextMuted;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (_onMinimizeToTray != null) _onMinimizeToTray();
                return;
            }

            if (_speedTestCts != null)
            {
                _speedTestCts.Cancel();
                _speedTestCts.Dispose();
                _speedTestCts = null;
            }

            if (_pulseTimer != null)
            {
                _pulseTimer.Stop();
                _pulseTimer.Dispose();
            }

            if (_telemetryTimer != null)
            {
                _telemetryTimer.Stop();
                _telemetryTimer.Dispose();
            }

            if (_toolTip != null)
            {
                _toolTip.Dispose();
            }

            base.OnFormClosing(e);
        }
    }
}
