using System.Runtime.InteropServices;

namespace DFCMAD.WindowsAudio.ComInterop;

internal enum EDataFlow
{
    eRender = 0,
    eCapture = 1,
    eAll = 2
}

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2
}

[Flags]
internal enum DeviceState : uint
{
    Active = 0x00000001,
    Disabled = 0x00000002,
    NotPresent = 0x00000004,
    Unplugged = 0x00000008,
    All = 0x0000000F
}

[Flags]
internal enum ClsCtx : uint
{
    InprocServer = 0x1,
    InprocHandler = 0x2,
    LocalServer = 0x4,
    All = InprocServer | InprocHandler | LocalServer
}

internal enum Stgm
{
    Read = 0x00000000
}

internal enum Vartype : ushort
{
    Empty = 0,
    UInt = 19,
    Lpwstr = 31,
    Clsid = 72
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct PropertyKey
{
    public PropertyKey(Guid fmtid, uint pid)
    {
        Fmtid = fmtid;
        Pid = pid;
    }

    public readonly Guid Fmtid;
    public readonly uint Pid;
}

[StructLayout(LayoutKind.Explicit)]
internal struct PropVariant
{
    [FieldOffset(0)]
    public ushort Vt;
    [FieldOffset(2)]
    private readonly ushort _wReserved1;
    [FieldOffset(4)]
    private readonly ushort _wReserved2;
    [FieldOffset(6)]
    private readonly ushort _wReserved3;
    [FieldOffset(8)]
    private readonly IntPtr _data;

    public string? GetString()
    {
        return Vt == (ushort)Vartype.Lpwstr && _data != IntPtr.Zero
            ? Marshal.PtrToStringUni(_data)
            : null;
    }

    public uint? GetUInt()
    {
        return Vt == (ushort)Vartype.UInt ? (uint)_data.ToInt64() : null;
    }

    public Guid? GetGuid()
    {
        return Vt == (ushort)Vartype.Clsid && _data != IntPtr.Zero
            ? Marshal.PtrToStructure<Guid>(_data)
            : null;
    }
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal sealed class MMDeviceEnumerator
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState dwStateMask, out IntPtr ppDevices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IntPtr ppEndpoint);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IntPtr ppDevice);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-C0C4C2F6E83C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig]
    int GetCount(out uint pcDevices);

    [PreserveSig]
    int Item(uint nDevice, out IntPtr ppDevice);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, ClsCtx dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);

    [PreserveSig]
    int OpenPropertyStore(Stgm stgmAccess, out IntPtr ppProperties);

    [PreserveSig]
    int GetId(out IntPtr ppstrId);

    [PreserveSig]
    int GetState(out DeviceState pdwState);
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig]
    int GetCount(out uint cProps);

    [PreserveSig]
    int GetAt(uint iProp, out PropertyKey pkey);

    [PreserveSig]
    int GetValue(ref PropertyKey key, out PropVariant pv);

    [PreserveSig]
    int SetValue(ref PropertyKey key, ref PropVariant propvar);

    [PreserveSig]
    int Commit();
}

[ComImport]
[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    [PreserveSig]
    int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, DeviceState dwNewState);

    [PreserveSig]
    int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);

    [PreserveSig]
    int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);

    [PreserveSig]
    int OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string? pwstrDefaultDeviceId);

    [PreserveSig]
    int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PropertyKey key);
}

internal static class HResult
{
    public static void ThrowIfFailed(int hr, string operation)
    {
        if (hr < 0)
        {
            throw new COMException($"{operation} failed with HRESULT 0x{hr:X8}.", hr);
        }
    }
}

internal static partial class NativeMethods
{
    [DllImport("ole32.dll")]
    internal static extern int PropVariantClear(ref PropVariant pvar);
}
