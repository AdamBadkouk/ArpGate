using System.Net;
using System.Net.NetworkInformation;

namespace NetKick.Models;

/// <summary>
/// Represents a network interface with its configuration
/// </summary>
public class NetworkInterfaceInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required PhysicalAddress MacAddress { get; init; }
    public required IPAddress IpAddress { get; init; }
    public required IPAddress SubnetMask { get; init; }
    public required IPAddress GatewayAddress { get; init; }
    public int InterfaceIndex { get; init; }

    public string MacAddressString => BitConverter.ToString(MacAddress.GetAddressBytes()).Replace("-", ":");

    /// <summary>
    /// Calculates the network address from IP and subnet mask
    /// </summary>
    public IPAddress NetworkAddress
    {
        get
        {
            var ipBytes = IpAddress.GetAddressBytes();
            var maskBytes = SubnetMask.GetAddressBytes();
            var networkBytes = new byte[4];

            for (int i = 0; i < 4; i++)
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);

            return new IPAddress(networkBytes);
        }
    }

    /// <summary>
    /// Calculates the broadcast address
    /// </summary>
    public IPAddress BroadcastAddress
    {
        get
        {
            var ipBytes = IpAddress.GetAddressBytes();
            var maskBytes = SubnetMask.GetAddressBytes();
            var broadcastBytes = new byte[4];

            for (int i = 0; i < 4; i++)
                broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);

            return new IPAddress(broadcastBytes);
        }
    }

    /// <summary>
    /// Gets the CIDR prefix length
    /// </summary>
    public int PrefixLength
    {
        get
        {
            var maskBytes = SubnetMask.GetAddressBytes();
            int prefix = 0;
            foreach (var b in maskBytes)
            {
                for (int i = 7; i >= 0; i--)
                {
                    if ((b & (1 << i)) != 0)
                        prefix++;
                    else
                        return prefix;
                }
            }
            return prefix;
        }
    }

    /// <summary>
    /// Enumerates all host IPs in the subnet
    /// </summary>
    public IEnumerable<IPAddress> GetAllHostAddresses()
    {
        var networkBytes = NetworkAddress.GetAddressBytes();
        var broadcastBytes = BroadcastAddress.GetAddressBytes();

        var start = BitConverter.ToUInt32(networkBytes.Reverse().ToArray(), 0) + 1;
        var end = BitConverter.ToUInt32(broadcastBytes.Reverse().ToArray(), 0);

        for (uint i = start; i < end; i++)
        {
            var bytes = BitConverter.GetBytes(i).Reverse().ToArray();
            yield return new IPAddress(bytes);
        }
    }

    public override string ToString() =>
        $"{Description} ({IpAddress}/{PrefixLength})";
}

