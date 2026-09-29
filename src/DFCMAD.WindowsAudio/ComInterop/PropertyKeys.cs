namespace DFCMAD.WindowsAudio.ComInterop;

internal static class PropertyKeys
{
    public static readonly PropertyKey DeviceFriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
    public static readonly PropertyKey DeviceDescription = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);
    public static readonly PropertyKey DeviceInterfaceFriendlyName = new(new Guid("026E516E-B814-414B-83CD-856D6FEF4822"), 2);
    public static readonly PropertyKey AudioEndpointFormFactor = new(new Guid("1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E"), 0);
    public static readonly PropertyKey DeviceIconPath = new(new Guid("259ABFFC-50A7-47CE-AF08-68C9A7D73366"), 12);
    public static readonly PropertyKey DeviceContainerId = new(new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);
}
