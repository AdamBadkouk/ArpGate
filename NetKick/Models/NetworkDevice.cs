using System.Net;
using System.Net.NetworkInformation;

namespace NetKick.Models;

/// <summary>
/// Represents a device discovered on the network
/// </summary>
public class NetworkDevice
{
    public required IPAddress IpAddress { get; init; }
    public required PhysicalAddress MacAddress { get; init; }
    public string? Hostname { get; set; }
    public string? Vendor { get; set; }
    public bool IsGateway { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime DiscoveredAt { get; init; } = DateTime.Now;
    public DateTime LastSeen { get; set; } = DateTime.Now;

    public string MacAddressString => BitConverter.ToString(MacAddress.GetAddressBytes()).Replace("-", ":");

    public string DisplayName => Hostname ?? IpAddress.ToString();

    public override string ToString() =>
        $"{IpAddress,-15} | {MacAddressString,-17} | {Hostname ?? "Unknown",-20} | {(IsGateway ? "Gateway" : "Device")}";

    public override bool Equals(object? obj) =>
        obj is NetworkDevice device && MacAddress.Equals(device.MacAddress);

    public override int GetHashCode() => MacAddress.GetHashCode();
}

