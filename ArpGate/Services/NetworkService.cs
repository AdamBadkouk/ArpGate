using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ArpGate.Models;
using SharpPcap;
using SharpPcap.LibPcap;

namespace ArpGate.Services;

/// <summary>
/// Handles network interface discovery and selection
/// </summary>
public static class NetworkService
{
    /// <summary>
    /// Gets all available network interfaces with Npcap
    /// </summary>
    public static List<(LibPcapLiveDevice Device, NetworkInterfaceInfo Info)> GetAvailableInterfaces()
    {
        var result = new List<(LibPcapLiveDevice, NetworkInterfaceInfo)>();
        var devices = LibPcapLiveDeviceList.Instance;

        foreach (var device in devices)
        {
            try
            {
                // Try to get IP information from the device
                var addresses = device.Addresses;

                IPAddress? ipAddress = null;
                IPAddress? netmask = null;

                foreach (var addr in addresses)
                {
                    if (addr.Addr?.ipAddress?.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipAddress = addr.Addr.ipAddress;
                        netmask = addr.Netmask?.ipAddress;
                        break;
                    }
                }

                if (ipAddress == null || netmask == null) continue;

                // Get MAC address and gateway from .NET interfaces
                var macAddress = PhysicalAddress.None;
                var gateway = IPAddress.None;

                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var props = ni.GetIPProperties();
                    foreach (var unicast in props.UnicastAddresses)
                    {
                        if (unicast.Address.Equals(ipAddress))
                        {
                            macAddress = ni.GetPhysicalAddress();

                            if (props.GatewayAddresses.Count > 0)
                            {
                                gateway = props.GatewayAddresses
                                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                                    ?.Address ?? IPAddress.None;
                            }
                            break;
                        }
                    }
                }

                if (macAddress.Equals(PhysicalAddress.None)) continue;
                if (gateway.Equals(IPAddress.None)) continue;

                var info = new NetworkInterfaceInfo
                {
                    Name = device.Name,
                    Description = device.Description ?? "Unknown",
                    MacAddress = macAddress,
                    IpAddress = ipAddress,
                    SubnetMask = netmask,
                    GatewayAddress = gateway
                };

                result.Add((device, info));
            }
            catch
            {
                // Skip interfaces that can't be processed
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if Npcap is installed and available
    /// </summary>
    public static bool IsNpcapInstalled()
    {
        try
        {
            var devices = LibPcapLiveDeviceList.Instance;
            return devices.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}

