using DFCMAD.Core.Models;
using DFCMAD.Core.Services;
using DFCMAD.WindowsAudio.Notifications;
using DFCMAD.WindowsAudio.PolicyConfig;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;

namespace DFCMAD.WindowsAudio.CoreAudio;

public sealed class CoreAudioDeviceService : IAudioDeviceService, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly IDefaultAudioDeviceSetter _defaultDeviceSetter;
    private readonly ILogger<CoreAudioDeviceService> _logger;
    private readonly AudioNotificationClient _notificationClient = new();
    private bool _disposed;

    public CoreAudioDeviceService(IDefaultAudioDeviceSetter defaultDeviceSetter, ILogger<CoreAudioDeviceService> logger)
    {
        _defaultDeviceSetter = defaultDeviceSetter;
        _logger = logger;
        _notificationClient.DeviceChanged += (_, args) =>
        {
            _logger.LogInformation("Audio device change detected: {ChangeKind} {EndpointId} {State}", args.ChangeKind, args.EndpointId, args.State);
            DeviceChanged?.Invoke(this, args);
        };
        _notificationClient.DefaultDeviceChanged += (_, args) =>
        {
            _logger.LogInformation("Default audio device changed: {Direction} {Role} {EndpointId}", args.Direction, args.Role, args.EndpointId);
            DefaultDeviceChanged?.Invoke(this, args);
        };

        try
        {
            _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio notification callback registration failed. Manual refresh still works.");
        }
    }

    public event EventHandler<AudioDeviceChangedEventArgs>? DeviceChanged;
    public event EventHandler<DefaultAudioDeviceChangedEventArgs>? DefaultDeviceChanged;

    public Task<IReadOnlyList<AudioDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var devices = new List<AudioDeviceInfo>();
            devices.AddRange(Enumerate(AudioDirection.Output, cancellationToken));
            devices.AddRange(Enumerate(AudioDirection.Input, cancellationToken));
            _logger.LogInformation("Device list refreshed. Count: {Count}", devices.Count);
            return (IReadOnlyList<AudioDeviceInfo>)devices
                .OrderBy(device => device.Direction)
                .ThenByDescending(device => device.State == DFCMAD.Core.Models.AudioDeviceState.Active)
                .ThenBy(device => device.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }, cancellationToken);
    }

    public Task<AudioDeviceInfo?> GetDefaultDeviceAsync(AudioDirection direction, AudioRole role, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var device = _enumerator.GetDefaultAudioEndpoint(direction.ToNAudio(), role.ToNAudio());
                return ReadDevice(device, direction, isDefault: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "No default {Direction} endpoint for {Role}.", direction, role);
                return null;
            }
        }, cancellationToken);
    }

    public Task SetDefaultDeviceAsync(string endpointId, AudioDirection direction, IReadOnlyCollection<AudioRole> roles, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _defaultDeviceSetter.SetDefaultDevice(endpointId, direction, roles);
            _logger.LogInformation("Set default {Direction} endpoint {EndpointId} for roles {Roles}.", direction, endpointId, string.Join(", ", roles));
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unregister audio notification callback.");
        }

        _enumerator.Dispose();
    }

    private IEnumerable<AudioDeviceInfo> Enumerate(AudioDirection direction, CancellationToken cancellationToken)
    {
        var collection = _enumerator.EnumerateAudioEndPoints(direction.ToNAudio(), NAudio.CoreAudioApi.DeviceState.All);
        var defaultIds = GetDefaultEndpointIds(direction);
        foreach (var device in collection)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                yield return ReadDevice(device, direction, isDefault: defaultIds.Contains(device.ID));
            }
            finally
            {
                device.Dispose();
            }
        }
    }

    private HashSet<string> GetDefaultEndpointIds(AudioDirection direction)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in new[] { AudioRole.Console, AudioRole.Multimedia, AudioRole.Communications })
        {
            try
            {
                using var device = _enumerator.GetDefaultAudioEndpoint(direction.ToNAudio(), role.ToNAudio());
                if (!string.IsNullOrWhiteSpace(device.ID))
                {
                    ids.Add(device.ID);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read default {Direction} endpoint for {Role}.", direction, role);
            }
        }

        return ids;
    }

    private static AudioDeviceInfo ReadDevice(MMDevice device, AudioDirection direction, bool isDefault)
    {
        return new AudioDeviceInfo
        {
            EndpointId = device.ID,
            FriendlyName = device.FriendlyName,
            Direction = direction,
            State = device.State.ToModel(),
            IsDefault = isDefault,
            InterfaceFriendlyName = TryGetString(device, PropertyKeys.PKEY_DeviceInterface_FriendlyName),
            DeviceDescription = TryGetString(device, PropertyKeys.PKEY_Device_DeviceDesc) ?? device.DeviceFriendlyName,
            FormFactor = TryGetUInt(device, PropertyKeys.PKEY_AudioEndpoint_FormFactor)?.ToString(),
            IconPath = device.IconPath,
            ContainerId = device.InstanceId
        };
    }

    private static string? TryGetString(MMDevice device, PropertyKey key)
    {
        try
        {
            return device.Properties.TryGetValue<string>(key, out var value) ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private static uint? TryGetUInt(MMDevice device, PropertyKey key)
    {
        try
        {
            if (device.Properties.TryGetValue<uint>(key, out var value))
            {
                return value;
            }

            if (device.Properties.TryGetValue<int>(key, out var intValue))
            {
                return unchecked((uint)intValue);
            }
        }
        catch
        {
        }

        return null;
    }
}
