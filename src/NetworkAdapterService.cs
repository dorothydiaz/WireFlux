using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Security.Principal;

namespace WireFlux
{
    public enum AdapterConnectionState
    {
        Unknown,
        Disabled,
        EnabledDisconnected,
        Connected
    }

    public class NetworkAdapterInfo
    {
        public string NetConnectionId { get; set; }
        public string Description { get; set; }
        public bool? NetEnabled { get; set; }
        public int? NetConnectionStatus { get; set; }
        public string AdapterType { get; set; }
        public bool PhysicalAdapter { get; set; }

        public AdapterConnectionState State
        {
            get
            {
                if (NetEnabled == false)
                {
                    return AdapterConnectionState.Disabled;
                }

                if (NetEnabled == true)
                {
                    if (NetConnectionStatus == 2)
                    {
                        return AdapterConnectionState.Connected;
                    }
                    return AdapterConnectionState.EnabledDisconnected;
                }

                return AdapterConnectionState.Unknown;
            }
        }

        public string StatusText
        {
            get
            {
                switch (State)
                {
                    case AdapterConnectionState.Connected:
                        return "Connected (Enabled)";
                    case AdapterConnectionState.EnabledDisconnected:
                        if (NetConnectionStatus == 7)
                            return "Enabled (Cable Disconnected)";
                        if (NetConnectionStatus == 1)
                            return "Enabled (Connecting...)";
                        return "Enabled (Disconnected)";
                    case AdapterConnectionState.Disabled:
                        return "Disabled";
                    default:
                        return "Unknown";
                }
            }
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Description)
                ? NetConnectionId
                : string.Format("{0} ({1})", NetConnectionId, Description);
        }
    }

    public class ToggleResult
    {
        public bool Success { get; set; }
        public bool CancelledByUser { get; set; }
        public string Message { get; set; }
    }

    public static class NetworkAdapterService
    {
        private static readonly string[] EthernetExcludedKeywords = new[]
        {
            "wi-fi", "wifi", "wireless", "wlan", "802.11",
            "bluetooth", "virtual", "vmware", "virtualbox",
            "hyper-v", "vethernet", "loopback", "vpn", "tap-",
            "wireguard", "zerotier", "tailscale", "hamachi", "wintun"
        };

        private static readonly string[] WifiExcludedKeywords = new[]
        {
            "virtual", "vmware", "virtualbox", "hyper-v", "vethernet",
            "bluetooth", "wi-fi direct", "vwifi", "hosted network",
            "loopback", "vpn", "tap-", "wireguard", "zerotier", "tailscale", "wintun"
        };

        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public static List<NetworkAdapterInfo> GetEthernetAdapters()
        {
            var physicalEthernet = new List<NetworkAdapterInfo>();
            var fallbackEthernet = new List<NetworkAdapterInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT NetConnectionID, Name, NetEnabled, NetConnectionStatus, AdapterType, PhysicalAdapter FROM Win32_NetworkAdapter WHERE NetConnectionID IS NOT NULL"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string netConnectionId = obj["NetConnectionID"] as string;
                        if (string.IsNullOrWhiteSpace(netConnectionId))
                            continue;

                        string description = obj["Name"] as string ?? string.Empty;
                        string adapterType = obj["AdapterType"] as string ?? string.Empty;
                        bool physical = obj["PhysicalAdapter"] != null && Convert.ToBoolean(obj["PhysicalAdapter"]);
                        bool? netEnabled = obj["NetEnabled"] != null ? (bool?)Convert.ToBoolean(obj["NetEnabled"]) : null;
                        int? netStatus = obj["NetConnectionStatus"] != null ? (int?)Convert.ToInt32(obj["NetConnectionStatus"]) : null;

                        var info = new NetworkAdapterInfo
                        {
                            NetConnectionId = netConnectionId,
                            Description = description,
                            AdapterType = adapterType,
                            PhysicalAdapter = physical,
                            NetEnabled = netEnabled,
                            NetConnectionStatus = netStatus
                        };

                        if (IsExcluded(netConnectionId, description, EthernetExcludedKeywords))
                            continue;

                        bool looksLikeEthernet =
                            netConnectionId.IndexOf("ethernet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            netConnectionId.IndexOf("local area", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            description.IndexOf("ethernet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            description.IndexOf("gigabit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            description.IndexOf("lan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            adapterType.IndexOf("802.3", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (physical && looksLikeEthernet)
                        {
                            physicalEthernet.Add(info);
                        }
                        else if (physical || looksLikeEthernet)
                        {
                            fallbackEthernet.Add(info);
                        }
                    }
                }
            }
            catch
            {
            }

            var result = physicalEthernet.Count > 0 ? physicalEthernet : fallbackEthernet;

            return result
                .OrderByDescending(a => string.Equals(a.NetConnectionId, "Ethernet", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(a => a.NetConnectionId.IndexOf("Ethernet", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenBy(a => a.NetConnectionId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<NetworkAdapterInfo> GetWifiAdapters()
        {
            var wifiAdapters = new List<NetworkAdapterInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT NetConnectionID, Name, NetEnabled, NetConnectionStatus, AdapterType, PhysicalAdapter FROM Win32_NetworkAdapter WHERE NetConnectionID IS NOT NULL"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string netConnectionId = obj["NetConnectionID"] as string;
                        if (string.IsNullOrWhiteSpace(netConnectionId))
                            continue;

                        string description = obj["Name"] as string ?? string.Empty;
                        if (IsExcluded(netConnectionId, description, WifiExcludedKeywords))
                            continue;

                        string combined = (netConnectionId + " " + description).ToLowerInvariant();
                        bool isWifi =
                            combined.Contains("wi-fi") ||
                            combined.Contains("wifi") ||
                            combined.Contains("wireless") ||
                            combined.Contains("wlan") ||
                            combined.Contains("802.11");

                        if (!isWifi)
                            continue;

                        string adapterType = obj["AdapterType"] as string ?? string.Empty;
                        bool physical = obj["PhysicalAdapter"] != null && Convert.ToBoolean(obj["PhysicalAdapter"]);
                        bool? netEnabled = obj["NetEnabled"] != null ? (bool?)Convert.ToBoolean(obj["NetEnabled"]) : null;
                        int? netStatus = obj["NetConnectionStatus"] != null ? (int?)Convert.ToInt32(obj["NetConnectionStatus"]) : null;

                        wifiAdapters.Add(new NetworkAdapterInfo
                        {
                            NetConnectionId = netConnectionId,
                            Description = description,
                            AdapterType = adapterType,
                            PhysicalAdapter = physical,
                            NetEnabled = netEnabled,
                            NetConnectionStatus = netStatus
                        });
                    }
                }
            }
            catch
            {
            }

            return wifiAdapters
                .OrderByDescending(a => a.PhysicalAdapter)
                .ThenByDescending(a => string.Equals(a.NetConnectionId, "Wi-Fi", StringComparison.OrdinalIgnoreCase))
                .ThenBy(a => a.NetConnectionId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsExcluded(string netConnectionId, string description, string[] keywords)
        {
            string combined = (netConnectionId + " " + description).ToLowerInvariant();
            foreach (string keyword in keywords)
            {
                if (combined.Contains(keyword))
                    return true;
            }
            return false;
        }

        public static ToggleResult SetAdapterEnabled(string adapterName, bool enable, string kindLabel = "Adapter")
        {
            if (string.IsNullOrWhiteSpace(adapterName))
            {
                return new ToggleResult
                {
                    Success = false,
                    Message = string.Format("No {0} selected.", kindLabel)
                };
            }

            string action = enable ? "enabled" : "disabled";
            string safeName = adapterName.Replace("\"", string.Empty);
            string arguments = string.Format("interface set interface name=\"{0}\" admin={1}", safeName, action);

            bool isAdmin = IsRunningAsAdministrator();
            var psi = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = arguments,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            if (isAdmin)
            {
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
            }
            else
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
            }

            try
            {
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        return new ToggleResult
                        {
                            Success = false,
                            Message = "Failed to start netsh.exe."
                        };
                    }

                    string stdOut = string.Empty;
                    string stdErr = string.Empty;

                    if (isAdmin)
                    {
                        stdOut = process.StandardOutput.ReadToEnd();
                        stdErr = process.StandardError.ReadToEnd();
                    }

                    if (!process.WaitForExit(15000))
                    {
                        return new ToggleResult
                        {
                            Success = false,
                            Message = "Timed out waiting for network adapter change."
                        };
                    }

                    if (process.ExitCode == 0)
                    {
                        return new ToggleResult
                        {
                            Success = true,
                            Message = string.Format("{0} '{1}' {2}.", kindLabel, adapterName, action)
                        };
                    }

                    string details = !string.IsNullOrWhiteSpace(stdErr) ? stdErr.Trim() : stdOut.Trim();
                    return new ToggleResult
                    {
                        Success = false,
                        Message = string.IsNullOrWhiteSpace(details)
                            ? string.Format("netsh exited with code {0}.", process.ExitCode)
                            : details
                    };
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

                return new ToggleResult
                {
                    Success = false,
                    Message = string.Format("Error toggling {0}: {1}", kindLabel, ex.Message)
                };
            }
            catch (Exception ex)
            {
                return new ToggleResult
                {
                    Success = false,
                    Message = string.Format("Error toggling {0}: {1}", kindLabel, ex.Message)
                };
            }
        }
    }
}
