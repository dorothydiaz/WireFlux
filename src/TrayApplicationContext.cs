using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WireFlux
{
    public class TrayApplicationContext : ApplicationContext
    {
        private class NetworkSnapshot
        {
            public List<NetworkAdapterInfo> EthernetAdapters { get; set; }
            public List<NetworkAdapterInfo> WifiAdapters { get; set; }
            public AirplaneModeInfo Airplane { get; set; }
        }

        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _enableMenuItem;
        private readonly ToolStripMenuItem _disableMenuItem;
        private readonly ToolStripMenuItem _wifiMenuItem;
        private readonly ToolStripMenuItem _airplaneMenuItem;
        private readonly ToolStripMenuItem _speedTestMenuItem;
        private readonly ToolStripMenuItem _reconnectMenuItem;
        private readonly ToolStripMenuItem _flushDnsMenuItem;
        private readonly ToolStripMenuItem _copyIpMenuItem;
        private readonly ToolStripMenuItem _adaptersCplMenuItem;
        private readonly ToolStripMenuItem _refreshMenuItem;
        private readonly ToolStripMenuItem _openMenuItem;
        private readonly ToolStripMenuItem _exitMenuItem;

        private readonly SynchronizationContext _uiContext;
        private readonly EventWaitHandle _showWindowSignal;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private MainForm _mainForm;
        private List<NetworkAdapterInfo> _ethernetAdapters = new List<NetworkAdapterInfo>();
        private List<NetworkAdapterInfo> _wifiAdapters = new List<NetworkAdapterInfo>();
        private AirplaneModeInfo _airplaneInfo = new AirplaneModeInfo();
        private string _selectedAdapterName;
        private bool _isBusy;
        private bool _trayHintShown;
        private Icon _currentIcon;

        public TrayApplicationContext(bool startInBackground, EventWaitHandle showWindowSignal)
        {
            _showWindowSignal = showWindowSignal;

            _enableMenuItem = new ToolStripMenuItem("Enable Ethernet", null, (s, e) => ToggleEthernetAsync(true));
            _disableMenuItem = new ToolStripMenuItem("Disable Ethernet", null, (s, e) => ToggleEthernetAsync(false));

            _wifiMenuItem = new ToolStripMenuItem("Wi-Fi", null, (s, e) =>
            {
                var wifi = GetPrimaryWifiAdapter();
                if (wifi != null)
                {
                    bool currentlyEnabled = wifi.State != AdapterConnectionState.Disabled;
                    ToggleWifiAsync(!currentlyEnabled);
                }
            });

            _airplaneMenuItem = new ToolStripMenuItem("Airplane Mode", null, (s, e) =>
            {
                if (_airplaneInfo != null && _airplaneInfo.IsSupported)
                {
                    ToggleAirplaneModeAsync(!_airplaneInfo.IsEnabled);
                }
            });

            _speedTestMenuItem = new ToolStripMenuItem("Run Speed Test", null, (s, e) =>
            {
                ShowMainWindow();
                if (_mainForm != null && !_mainForm.IsDisposed)
                {
                    _mainForm.StartSpeedTestFromTray();
                }
            });

            _reconnectMenuItem = new ToolStripMenuItem("Reconnect Ethernet", null, (s, e) => ReconnectEthernetAsync());
            _flushDnsMenuItem = new ToolStripMenuItem("Flush DNS Cache", null, (s, e) => FlushDnsAsync());
            _copyIpMenuItem = new ToolStripMenuItem("Copy IP Address", null, (s, e) => CopyIpAddress());
            _adaptersCplMenuItem = new ToolStripMenuItem("Adapter Settings", null, (s, e) => OpenAdaptersControlPanel());
            _refreshMenuItem = new ToolStripMenuItem("Refresh Status", null, (s, e) => RefreshStatusAsync(true));

            _openMenuItem = new ToolStripMenuItem("Open WireFlux", null, (s, e) => ShowMainWindow());
            _openMenuItem.Font = new Font(_openMenuItem.Font, FontStyle.Bold);
            _exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication());

            AppTheme savedTheme = ThemeManager.LoadSavedTheme();

            _trayMenu = new ContextMenuStrip
            {
                Renderer = new ThemedMenuRenderer(savedTheme),
                ShowImageMargin = false
            };
            _trayMenu.Items.AddRange(new ToolStripItem[]
            {
                _enableMenuItem,
                _disableMenuItem,
                new ToolStripSeparator(),
                _wifiMenuItem,
                _airplaneMenuItem,
                new ToolStripSeparator(),
                _speedTestMenuItem,
                _reconnectMenuItem,
                _flushDnsMenuItem,
                _copyIpMenuItem,
                _adaptersCplMenuItem,
                _refreshMenuItem,
                new ToolStripSeparator(),
                _openMenuItem,
                _exitMenuItem
            });

            _currentIcon = IconGenerator.GetAppIcon();
            _trayIcon = new NotifyIcon
            {
                Icon = _currentIcon,
                Text = "WireFlux - Checking...",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => ShowMainWindow();

            _mainForm = new MainForm(
                initialTheme: savedTheme,
                onEnableRequested: () => ToggleEthernetAsync(true),
                onDisableRequested: () => ToggleEthernetAsync(false),
                onWifiToggleRequested: ToggleWifiAsync,
                onAirplaneToggleRequested: ToggleAirplaneModeAsync,
                onRefreshRequested: () => RefreshStatusAsync(true),
                onFlushDnsRequested: FlushDnsAsync,
                onCopyIpRequested: CopyIpAddress,
                onOpenAdaptersRequested: OpenAdaptersControlPanel,
                onAdapterSelected: OnAdapterSelected,
                onMinimizeToTray: OnMinimizedToTray,
                onThemeChanged: OnThemeChanged);
            _mainForm.Icon = _currentIcon;

            _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;

            StartShowWindowListener();

            if (!startInBackground)
            {
                ShowMainWindow();
            }

            RefreshStatusAsync(false);
        }

        private void StartShowWindowListener()
        {
            if (_showWindowSignal == null) return;

            Task.Run(() =>
            {
                var waitHandles = new WaitHandle[] { _showWindowSignal, _cts.Token.WaitHandle };
                while (!_cts.IsCancellationRequested)
                {
                    int signaled = WaitHandle.WaitAny(waitHandles);
                    if (signaled == 0)
                    {
                        _uiContext.Post(_ => ShowMainWindow(), null);
                    }
                    else
                    {
                        break;
                    }
                }
            });
        }

        private void OnNetworkChanged(object sender, EventArgs e)
        {
            _uiContext.Post(_ => RefreshStatusAsync(false), null);
        }

        private void OnAdapterSelected(string adapterName)
        {
            _selectedAdapterName = adapterName;
            UpdateUiState();
        }

        private void OnThemeChanged(AppTheme theme)
        {
            if (_trayMenu != null && !_trayMenu.IsDisposed)
            {
                _trayMenu.Renderer = new ThemedMenuRenderer(theme);
            }
        }

        private void OnMinimizedToTray()
        {
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _trayIcon.ShowBalloonTip(
                    2500,
                    "WireFlux",
                    "Still running in the system tray. Double-click the tray icon to reopen.",
                    ToolTipIcon.Info);
            }
        }

        private void ShowMainWindow()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
                return;

            if (!_mainForm.Visible)
            {
                _mainForm.Show();
            }

            if (_mainForm.WindowState == FormWindowState.Minimized)
            {
                _mainForm.WindowState = FormWindowState.Normal;
            }

            _mainForm.Activate();
            _mainForm.BringToFront();
        }

        private NetworkAdapterInfo GetSelectedAdapter()
        {
            if (_ethernetAdapters == null || _ethernetAdapters.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(_selectedAdapterName))
            {
                var match = _ethernetAdapters.FirstOrDefault(a =>
                    string.Equals(a.NetConnectionId, _selectedAdapterName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match;
            }

            _selectedAdapterName = _ethernetAdapters[0].NetConnectionId;
            return _ethernetAdapters[0];
        }

        private NetworkAdapterInfo GetPrimaryWifiAdapter()
        {
            if (_wifiAdapters == null || _wifiAdapters.Count == 0)
                return null;
            return _wifiAdapters[0];
        }

        private static NetworkSnapshot QuerySnapshot()
        {
            return new NetworkSnapshot
            {
                EthernetAdapters = NetworkAdapterService.GetEthernetAdapters(),
                WifiAdapters = NetworkAdapterService.GetWifiAdapters(),
                Airplane = AirplaneModeService.GetState()
            };
        }

        private void ApplySnapshot(NetworkSnapshot snap)
        {
            if (snap == null) return;
            _ethernetAdapters = snap.EthernetAdapters ?? new List<NetworkAdapterInfo>();
            _wifiAdapters = snap.WifiAdapters ?? new List<NetworkAdapterInfo>();
            _airplaneInfo = snap.Airplane ?? new AirplaneModeInfo();
        }

        private async void RefreshStatusAsync(bool userInitiated)
        {
            if (_isBusy) return;

            try
            {
                NetworkSnapshot snap = await Task.Run(() => QuerySnapshot());
                ApplySnapshot(snap);
                UpdateUiState();

                if (userInitiated && _mainForm != null && !_mainForm.IsDisposed)
                {
                    var current = GetSelectedAdapter();
                    _mainForm.SetMessage(
                        current != null
                            ? string.Format("Status refreshed: {0} - {1}", current.NetConnectionId, current.StatusText)
                            : "Status refreshed.",
                        false);
                }
            }
            catch (Exception ex)
            {
                if (_mainForm != null && !_mainForm.IsDisposed)
                {
                    _mainForm.SetMessage("Failed to refresh status: " + ex.Message, true);
                }
            }
        }

        private async void ToggleEthernetAsync(bool enable)
        {
            if (_isBusy) return;

            var adapter = GetSelectedAdapter();
            if (adapter == null)
            {
                if (_mainForm != null && !_mainForm.IsDisposed)
                    _mainForm.SetMessage("No Ethernet adapter available to toggle.", true);
                return;
            }

            _isBusy = true;
            UpdateUiState();
            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.SetMessage(enable ? "Enabling Ethernet..." : "Disabling Ethernet...", false);

            try
            {
                string targetName = adapter.NetConnectionId;
                ToggleResult result = await Task.Run(() => NetworkAdapterService.SetAdapterEnabled(targetName, enable, "Ethernet"));

                if (result.Success)
                {
                    await Task.Delay(700);
                }

                ApplySnapshot(await Task.Run(() => QuerySnapshot()));
                _isBusy = false;
                UpdateUiState();

                NotifyFeedback(result);
            }
            catch (Exception ex)
            {
                _isBusy = false;
                UpdateUiState();
                if (_mainForm != null && !_mainForm.IsDisposed)
                    _mainForm.SetMessage("Error: " + ex.Message, true);
            }
        }

        private async void ReconnectEthernetAsync()
        {
            if (_isBusy) return;

            var adapter = GetSelectedAdapter();
            if (adapter == null) return;

            _isBusy = true;
            UpdateUiState();
            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.SetMessage("Reconnecting Ethernet...", false);

            try
            {
                string targetName = adapter.NetConnectionId;
                ToggleResult offRes = await Task.Run(() => NetworkAdapterService.SetAdapterEnabled(targetName, false, "Ethernet"));
                if (!offRes.Success)
                {
                    _isBusy = false;
                    UpdateUiState();
                    NotifyFeedback(offRes);
                    return;
                }

                await Task.Delay(900);
                ToggleResult onRes = await Task.Run(() => NetworkAdapterService.SetAdapterEnabled(targetName, true, "Ethernet"));
                if (onRes.Success)
                {
                    onRes.Message = string.Format("Ethernet '{0}' reconnected.", targetName);
                    await Task.Delay(900);
                }

                ApplySnapshot(await Task.Run(() => QuerySnapshot()));
                _isBusy = false;
                UpdateUiState();
                NotifyFeedback(onRes);
            }
            catch (Exception ex)
            {
                _isBusy = false;
                UpdateUiState();
                if (_mainForm != null && !_mainForm.IsDisposed)
                    _mainForm.SetMessage("Error reconnecting: " + ex.Message, true);
            }
        }

        private async void ToggleWifiAsync(bool enable)
        {
            if (_isBusy) return;

            var wifi = GetPrimaryWifiAdapter();
            if (wifi == null)
            {
                UpdateUiState();
                if (_mainForm != null && !_mainForm.IsDisposed)
                    _mainForm.SetMessage("No Wi-Fi adapter detected on this PC.", true);
                return;
            }

            _isBusy = true;
            UpdateUiState();
            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.SetMessage(enable ? "Enabling Wi-Fi..." : "Disabling Wi-Fi...", false);

            try
            {
                string targetName = wifi.NetConnectionId;
                ToggleResult result = await Task.Run(() => NetworkAdapterService.SetAdapterEnabled(targetName, enable, "Wi-Fi"));

                if (result.Success)
                {
                    await Task.Delay(800);
                }

                ApplySnapshot(await Task.Run(() => QuerySnapshot()));
                _isBusy = false;
                UpdateUiState();
                NotifyFeedback(result);
            }
            catch (Exception ex)
            {
                _isBusy = false;
                UpdateUiState();
                if (_mainForm != null && !_mainForm.IsDisposed)
                    _mainForm.SetMessage("Error toggling Wi-Fi: " + ex.Message, true);
            }
        }

        private async void ToggleAirplaneModeAsync(bool enableAirplane)
        {
            if (_isBusy) return;

            _isBusy = true;
            UpdateUiState();
            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.SetMessage(enableAirplane ? "Turning on Airplane Mode..." : "Turning off Airplane Mode...", false);

            try
            {
                ToggleResult result = await Task.Run(() => AirplaneModeService.SetAirplaneMode(enableAirplane));
                if (result.Success)
                {
                    await Task.Delay(600);
                }

                ApplySnapshot(await Task.Run(() => QuerySnapshot()));
                _isBusy = false;
                UpdateUiState();
                NotifyFeedback(result);
            }
            catch (Exception ex)
            {
                _isBusy = false;
                UpdateUiState();
                if (_mainForm != null && !_mainForm.IsDisposed)
                    _mainForm.SetMessage("Error toggling Airplane Mode: " + ex.Message, true);
            }
        }

        private async void FlushDnsAsync()
        {
            if (_isBusy) return;

            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.SetMessage("Flushing DNS cache...", false);

            ToggleResult result = await Task.Run(() => NetworkDiagnosticsService.FlushDnsCache());
            NotifyFeedback(result);
        }

        private void CopyIpAddress()
        {
            var selected = GetSelectedAdapter();
            string ip = _mainForm != null ? _mainForm.LatestIPv4Address : null;
            if (string.IsNullOrEmpty(ip) && selected != null)
            {
                var metrics = NetworkDiagnosticsService.GetAdapterMetrics(selected.NetConnectionId);
                if (metrics != null) ip = metrics.IPv4Address;
            }

            if (string.IsNullOrEmpty(ip))
            {
                if (_mainForm != null && !_mainForm.IsDisposed)
                    _mainForm.SetMessage("No active IPv4 address available to copy.", true);
                return;
            }

            try
            {
                Clipboard.SetText(ip);
                if (_mainForm != null && !_mainForm.IsDisposed)
                    _mainForm.SetMessage("Copied IPv4 address to clipboard: " + ip, false);
            }
            catch (Exception ex)
            {
                if (_mainForm != null && !_mainForm.IsDisposed)
                    _mainForm.SetMessage("Clipboard error: " + ex.Message, true);
            }
        }

        private void OpenAdaptersControlPanel()
        {
            ToggleResult res = NetworkDiagnosticsService.OpenNetworkAdaptersControlPanel();
            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.SetMessage(res.Message, !res.Success);
        }

        private void NotifyFeedback(ToggleResult result)
        {
            if (result == null) return;

            if (_mainForm != null && !_mainForm.IsDisposed)
            {
                _mainForm.SetMessage(result.Message, !result.Success && !result.CancelledByUser);
            }

            if (!result.Success && !result.CancelledByUser && (_mainForm == null || !_mainForm.Visible))
            {
                _trayIcon.ShowBalloonTip(3000, "WireFlux", result.Message, ToolTipIcon.Warning);
            }
        }

        private void UpdateUiState()
        {
            var selected = GetSelectedAdapter();
            var wifi = GetPrimaryWifiAdapter();

            string tooltip = selected != null
                ? string.Format("WireFlux ({0}): {1}", selected.NetConnectionId, selected.StatusText)
                : "WireFlux: No Ethernet adapter";
            if (_airplaneInfo != null && _airplaneInfo.IsEnabled)
            {
                tooltip += " | Airplane ON";
            }
            _trayIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;

            _enableMenuItem.Enabled = !_isBusy && selected != null && selected.State == AdapterConnectionState.Disabled;
            _disableMenuItem.Enabled = !_isBusy && selected != null && selected.State != AdapterConnectionState.Disabled;
            _reconnectMenuItem.Enabled = !_isBusy && selected != null;

            if (wifi == null)
            {
                _wifiMenuItem.Text = "Wi-Fi (No Adapter)";
                _wifiMenuItem.Enabled = false;
            }
            else
            {
                bool wifiOn = wifi.State != AdapterConnectionState.Disabled;
                _wifiMenuItem.Text = wifiOn ? "Turn Off Wi-Fi" : "Turn On Wi-Fi";
                _wifiMenuItem.Enabled = !_isBusy;
            }

            if (_airplaneInfo == null || !_airplaneInfo.IsSupported)
            {
                _airplaneMenuItem.Text = "Airplane Mode (Unsupported)";
                _airplaneMenuItem.Enabled = false;
            }
            else
            {
                _airplaneMenuItem.Text = _airplaneInfo.IsEnabled
                    ? "Turn Off Airplane Mode"
                    : "Turn On Airplane Mode";
                _airplaneMenuItem.Enabled = !_isBusy;
            }

            _speedTestMenuItem.Enabled = !_isBusy;
            _flushDnsMenuItem.Enabled = !_isBusy;
            _copyIpMenuItem.Enabled = !_isBusy && selected != null && selected.State == AdapterConnectionState.Connected;
            _refreshMenuItem.Enabled = !_isBusy;

            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.UpdateView(_ethernetAdapters, selected, wifi, _airplaneInfo, _isBusy);
        }

        private void ExitApplication()
        {
            _cts.Cancel();
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            if (_mainForm != null && !_mainForm.IsDisposed)
            {
                _mainForm.AllowExitClose();
                _mainForm.Close();
                _mainForm.Dispose();
            }

            if (_currentIcon != null) _currentIcon.Dispose();
            if (_trayMenu != null) _trayMenu.Dispose();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
                if (_trayIcon != null) _trayIcon.Dispose();
                if (_trayMenu != null) _trayMenu.Dispose();
                if (_currentIcon != null) _currentIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
