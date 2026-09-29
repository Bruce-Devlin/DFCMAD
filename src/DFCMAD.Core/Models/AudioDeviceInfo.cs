namespace DFCMAD.Core.Models;

public sealed record AudioDeviceInfo
{
    public required string EndpointId { get; init; }
    public required string FriendlyName { get; init; }
    public required AudioDirection Direction { get; init; }
    public required AudioDeviceState State { get; init; }
    public bool IsDefault { get; init; }
    public bool IsPreferred { get; init; }
    public string? InterfaceFriendlyName { get; init; }
    public string? DeviceDescription { get; init; }
    public string? FormFactor { get; init; }
    public string? IconPath { get; init; }
    public string? ContainerId { get; init; }

    public bool IsAvailable => State == AudioDeviceState.Active;

    public string DisplayName => string.IsNullOrWhiteSpace(FriendlyName) ? EndpointId : FriendlyName;
}
