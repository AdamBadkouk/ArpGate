﻿using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using ArpGate.Models;

namespace ArpGate.Services;

/// <summary>
/// Manages blocking and unblocking of network devices using ARP spoofing
/// </summary>
public class BlockingService : IAsyncDisposable, IDisposable
{
    private readonly ArpService _arpService;
    private readonly NetworkDevice _gateway;
    private readonly NetworkInterfaceInfo _interfaceInfo;
    private readonly ConcurrentDictionary<string, BlockedDeviceInfo> _blockedDevices = new();
    private CancellationTokenSource? _spoofCts;
    private Task? _spoofTask;
    private bool _disposed;

    public IReadOnlyDictionary<string, BlockedDeviceInfo> BlockedDevices => _blockedDevices;
    public bool IsRunning => _spoofTask != null && !_spoofTask.IsCompleted;

    public event EventHandler<string>? LogMessage;

    public BlockingService(ArpService arpService, NetworkDevice gateway, NetworkInterfaceInfo interfaceInfo)
    {
        _arpService = arpService;
        _gateway = gateway;
        _interfaceInfo = interfaceInfo;
    }

    /// <summary>
    /// Starts the ARP spoofing loop
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;

        _spoofCts = new CancellationTokenSource();
        _spoofTask = Task.Run(() => SpoofLoopAsync(_spoofCts.Token));
        Log("Blocking service started");
    }

    /// <summary>
    /// Stops the ARP spoofing and restores all devices
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;

        Log("Stopping blocking service...");

        // Restore all blocked devices first
        foreach (var blocked in _blockedDevices.Values)
        {
            await RestoreDeviceAsync(blocked.Device);
        }

        _spoofCts?.Cancel();

        try
        {
            if (_spoofTask != null)
                await _spoofTask;
        }
        catch (OperationCanceledException) { }

        _blockedDevices.Clear();
        Log("Blocking service stopped");
    }

    /// <summary>
    /// Blocks a device by ARP spoofing
    /// </summary>
    public void BlockDevice(NetworkDevice device)
    {
        if (device.IsGateway)
        {
            Log("Cannot block the gateway!");
            return;
        }

        // Prevent self-blocking
        if (_interfaceInfo != null && device.IpAddress.Equals(_interfaceInfo.IpAddress))
        {
            Log("Cannot block yourself!");
            return;
        }

        // Validate MAC address before blocking
        if (device.MacAddress == null || device.MacAddress.Equals(PhysicalAddress.None))
        {
            Log($"Cannot block {device.IpAddress}: MAC address not known");
            return;
        }

        var key = device.MacAddressString;

        if (_blockedDevices.ContainsKey(key))
        {
            Log($"Device {device.IpAddress} is already blocked");
            return;
        }

        var info = new BlockedDeviceInfo
        {
            Device = device,
            BlockedAt = DateTime.Now
        };

        if (_blockedDevices.TryAdd(key, info))
        {
            device.IsBlocked = true;
            Log($"Blocking device: {device.IpAddress} ({device.MacAddressString})");

            // Immediately send spoof packets
            SendSpoofPackets(device);
        }
    }

    /// <summary>
    /// Unblocks a device and restores its ARP cache
    /// </summary>
    public async Task UnblockDeviceAsync(NetworkDevice device)
    {
        var key = device.MacAddressString;

        if (_blockedDevices.TryRemove(key, out _))
        {
            device.IsBlocked = false;
            Log($"Unblocking device: {device.IpAddress}");
            await RestoreDeviceAsync(device);
        }
    }

    /// <summary>
    /// Sends ARP spoof packets to poison target and gateway
    /// </summary>
    private void SendSpoofPackets(NetworkDevice target)
    {
        // Tell target that we are the gateway
        _arpService.SendArpSpoof(target, _gateway);

        // Tell gateway that we are the target
        _arpService.SendArpSpoof(_gateway, target);
    }

    /// <summary>
    /// Restores the device's ARP cache with correct information
    /// </summary>
    private async Task RestoreDeviceAsync(NetworkDevice device)
    {
        try
        {
            Log($"Restoring ARP cache for {device.IpAddress}");

            // Send multiple restore packets to ensure it takes effect
            for (int i = 0; i < 5; i++)
            {
                // Tell target the real gateway MAC
                _arpService.SendArpRestore(device, _gateway);

                // Tell gateway the real target MAC
                _arpService.SendArpRestore(_gateway, device);

                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            Log($"Error restoring device {device.IpAddress}: {ex.Message}");
        }
    }

    /// <summary>
    /// Continuous loop that sends spoof packets to all blocked devices
    /// </summary>
    private async Task SpoofLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // Take snapshot to avoid collection modification during iteration
            var snapshot = _blockedDevices.Values.ToArray();
            
            foreach (var blocked in snapshot)
            {
                if (token.IsCancellationRequested) break;

                try
                {
                    SendSpoofPackets(blocked.Device);
                    blocked.IncrementPackets(2);
                }
                catch (Exception ex)
                {
                    Log($"Error spoofing {blocked.Device.IpAddress}: {ex.Message}");
                }
            }

            try
            {
                // Send spoof packets every 1-2 seconds
                await Task.Delay(1500, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void Log(string message) => LogMessage?.Invoke(this, message);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync();
        _spoofCts?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Synchronously stop and restore - best effort for non-async disposal
        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort cleanup
        }
        _spoofCts?.Dispose();
    }
}

public class BlockedDeviceInfo
{
    public required NetworkDevice Device { get; init; }
    public DateTime BlockedAt { get; init; }
    private int _packetsSent;
    public int PacketsSent => _packetsSent;
    public void IncrementPackets(int count) => Interlocked.Add(ref _packetsSent, count);
}

