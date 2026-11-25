using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WireFlux
{
    public class AirplaneModeInfo
    {
        public bool IsSupported { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class AdapterLiveMetrics
    {
        public string IPv4Address { get; set; }
        public string GatewayAddress { get; set; }
        public string LinkSpeedText { get; set; }
        public long BytesReceived { get; set; }
        public long BytesSent { get; set; }
    }

    public static class AirplaneModeService
    {
        private static readonly Guid ClsidRadioManagementApi = new Guid("581333f6-28db-41be-bc7a-ff201f12f3f6");
        private const string RadioRegistryPath = @"SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState";

        [ComImport]
        [Guid("db3afbfb-08e6-46c6-aa70-bf9a34c30ab7")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IRadioManager
        {
            [PreserveSig]
            int IsRMSupported(out uint pdwState);

            [PreserveSig]
            int GetUIRadioInstances([MarshalAs(UnmanagedType.IUnknown)] out object ppInstances);

            [PreserveSig]
            int GetSystemRadioState(out int pbEnabled, out int param2, out int param3);

            [PreserveSig]
            int SetSystemRadioState(int bEnabled);

            [PreserveSig]
            int Refresh();

            [PreserveSig]
            int OnHardwareSliderChange(int param1, int param2);
        }

        public static AirplaneModeInfo GetState()
        {
            IRadioManager rm = null;
            try
            {
                Type comType = Type.GetTypeFromCLSID(ClsidRadioManagementApi);
                if (comType != null)
                {
                    rm = Activator.CreateInstance(comType) as IRadioManager;
                    if (rm != null)
                    {
                        uint supported;
                        int hrSup = rm.IsRMSupported(out supported);
                        if (hrSup == 0 && supported != 0)
                        {
                            int radiosEnabled, p2, p3;
                            int hrState = rm.GetSystemRadioState(out radiosEnabled, out p2, out p3);
                            if (hrState == 0)
                            {
                                return new AirplaneModeInfo
                                {
                                    IsSupported = true,
                                    IsEnabled = (radiosEnabled == 0)
                                };
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (rm != null)
                {
                    try { Marshal.ReleaseComObject(rm); } catch { }
                }
            }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RadioRegistryPath, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(null);
                        if (val != null)
                        {
                            int state = Convert.ToInt32(val);
                            return new AirplaneModeInfo
                            {
                                IsSupported = true,
                                IsEnabled = (state != 0)
                            };
                        }
                    }
                }
            }
            catch
            {
            }

            return new AirplaneModeInfo { IsSupported = false, IsEnabled = false };
        }

        public static int SetStateDirectCom(bool enableAirplaneMode)
        {
            IRadioManager rm = null;
            try
            {
                Type comType = Type.GetTypeFromCLSID(ClsidRadioManagementApi);
                if (comType == null)
                    return -1;

                rm = Activator.CreateInstance(comType) as IRadioManager;
                if (rm == null)
                    return -1;

                int targetRadiosEnabled = enableAirplaneMode ? 0 : 1;
                return rm.SetSystemRadioState(targetRadiosEnabled);
            }
            catch (COMException comEx)
            {
                return comEx.ErrorCode;
            }
            catch
            {
                return -1;
            }
            finally
            {
                if (rm != null)
                {
                    try { Marshal.ReleaseComObject(rm); } catch { }
                }
            }
        }

        public static ToggleResult SetAirplaneMode(bool enableAirplaneMode)
        {
            int hr = SetStateDirectCom(enableAirplaneMode);
            if (hr == 0)
            {
                return new ToggleResult
                {
                    Success = true,
                    Message = enableAirplaneMode
                        ? "Airplane Mode turned on."
                        : "Airplane Mode turned off."
                };
            }

            if (!NetworkAdapterService.IsRunningAsAdministrator())
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = "--set-airplane " + (enableAirplaneMode ? "on" : "off"),
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null && proc.WaitForExit(12000) && proc.ExitCode == 0)
                        {
                            return new ToggleResult
                            {
                                Success = true,
                                Message = enableAirplaneMode
                                    ? "Airplane Mode turned on."
                                    : "Airplane Mode turned off."
                            };
                        }
                    }
                }
                catch (Win32Exception ex)
                {
                    if (ex.NativeErrorCode == 1223)
                    {
                        return new ToggleResult
                        {
                            Success = false,
                            CancelledByUser = true,
                            Message = "Administrator permission request was cancelled."
                        };
                    }
                }
                catch (Exception ex)
                {
                    return new ToggleResult
                    {
                        Success = false,
                        Message = "Failed to toggle Airplane Mode: " + ex.Message
                    };
                }
            }

            return new ToggleResult
            {
                Success = false,
                Message = string.Format("Could not change Airplane Mode (0x{0:X8}).", hr)
            };
        }
    }

    public static class NetworkDiagnosticsService
    {
        public static AdapterLiveMetrics GetAdapterMetrics(string netConnectionId)
        {
            if (string.IsNullOrWhiteSpace(netConnectionId))
                return null;

            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => string.Equals(n.Name, netConnectionId, StringComparison.OrdinalIgnoreCase));

                if (nic == null || nic.OperationalStatus != OperationalStatus.Up)
                    return null;

                var ipProps = nic.GetIPProperties();
                string ipv4 = null;
                foreach (var uni in ipProps.UnicastAddresses)
                {
                    if (uni.Address != null &&
                        uni.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(uni.Address))
                    {
                        ipv4 = uni.Address.ToString();
                        break;
                    }
                }

                string gateway = null;
                foreach (var gw in ipProps.GatewayAddresses)
                {
                    if (gw.Address != null &&
                        gw.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !gw.Address.Equals(IPAddress.Any))
                    {
                        gateway = gw.Address.ToString();
                        break;
                    }
                }

                long bytesRx = 0;
                long bytesTx = 0;
                try
                {
                    var stats = nic.GetIPv4Statistics();
                    bytesRx = stats.BytesReceived;
                    bytesTx = stats.BytesSent;
                }
                catch
                {
                }

                return new AdapterLiveMetrics
                {
                    IPv4Address = ipv4,
                    GatewayAddress = gateway,
                    LinkSpeedText = FormatLinkSpeed(nic.Speed),
                    BytesReceived = bytesRx,
                    BytesSent = bytesTx
                };
            }
            catch
            {
                return null;
            }
        }

        public static string FormatLinkSpeed(long bitsPerSecond)
        {
            if (bitsPerSecond <= 0)
                return null;
            if (bitsPerSecond >= 1000000000L)
                return string.Format("{0:0.#} Gbps", bitsPerSecond / 1000000000.0);
            if (bitsPerSecond >= 1000000L)
                return string.Format("{0:0} Mbps", bitsPerSecond / 1000000.0);
            if (bitsPerSecond >= 1000L)
                return string.Format("{0:0} Kbps", bitsPerSecond / 1000.0);
            return bitsPerSecond + " bps";
        }

        public static string FormatByteRate(double bytesPerSec)
        {
            if (bytesPerSec < 0) bytesPerSec = 0;
            if (bytesPerSec >= 1048576.0)
                return string.Format("{0:0.0} MB/s", bytesPerSec / 1048576.0);
            if (bytesPerSec >= 1024.0)
                return string.Format("{0:0} KB/s", bytesPerSec / 1024.0);
            return string.Format("{0:0} B/s", bytesPerSec);
        }

        public static int? MeasureLatencyMs(string fallbackGateway)
        {
            string[] targets = string.IsNullOrWhiteSpace(fallbackGateway)
                ? new[] { "1.1.1.1", "8.8.8.8" }
                : new[] { "1.1.1.1", fallbackGateway };

            foreach (string host in targets)
            {
                try
                {
                    using (var ping = new Ping())
                    {
                        PingReply reply = ping.Send(host, 1000);
                        if (reply != null && reply.Status == IPStatus.Success)
                        {
                            return (int)reply.RoundtripTime;
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public static ToggleResult FlushDnsCache()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        return new ToggleResult { Success = false, Message = "Could not run ipconfig." };
                    }

                    proc.StandardOutput.ReadToEnd();
                    string err = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(8000);

                    if (proc.ExitCode == 0)
                    {
                        return new ToggleResult
                        {
                            Success = true,
                            Message = "DNS resolver cache flushed successfully."
                        };
                    }

                    return new ToggleResult
                    {
                        Success = false,
                        Message = string.IsNullOrWhiteSpace(err) ? "Failed to flush DNS cache." : err.Trim()
                    };
                }
            }
            catch (Exception ex)
            {
                return new ToggleResult
                {
                    Success = false,
                    Message = "Error flushing DNS: " + ex.Message
                };
            }
        }

        public static ToggleResult OpenNetworkAdaptersControlPanel()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ncpa.cpl",
                    UseShellExecute = true
                });
                return new ToggleResult
                {
                    Success = true,
                    Message = "Opened Windows Network Connections."
                };
            }
            catch (Exception ex)
            {
                return new ToggleResult
                {
                    Success = false,
                    Message = "Could not open ncpa.cpl: " + ex.Message
                };
            }
        }
    }
}
