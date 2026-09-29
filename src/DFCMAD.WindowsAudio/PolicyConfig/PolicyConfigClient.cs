using System.Runtime.InteropServices;
using DFCMAD.Core.Models;
using DFCMAD.WindowsAudio.ComInterop;
using DFCMAD.WindowsAudio.CoreAudio;

namespace DFCMAD.WindowsAudio.PolicyConfig;

public interface IDefaultAudioDeviceSetter
{
    void SetDefaultDevice(string endpointId, AudioDirection direction, IReadOnlyCollection<AudioRole> roles);
}

public sealed class PolicyConfigDefaultAudioDeviceSetter : IDefaultAudioDeviceSetter
{
    private readonly object _gate = new();

    public void SetDefaultDevice(string endpointId, AudioDirection direction, IReadOnlyCollection<AudioRole> roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        lock (_gate)
        {
            object? clientObject = null;
            try
            {
                clientObject = new PolicyConfigClientCom();
                var policyConfig = (IPolicyConfig)clientObject;
                foreach (var role in roles)
                {
                    var hr = policyConfig.SetDefaultEndpoint(endpointId, role.ToNative());
                    HResult.ThrowIfFailed(hr, $"SetDefaultEndpoint {direction}/{role}");
                }
            }
            finally
            {
                if (clientObject is not null)
                {
                    Marshal.FinalReleaseComObject(clientObject);
                }
            }
        }
    }
}

// Windows intentionally does not publish a supported default-endpoint setter.
// This narrow COM adapter uses the long-standing PolicyConfig interface and is
// isolated so it can be replaced without touching enforcement or UI code.
[ComImport]
[Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal sealed class PolicyConfigClientCom
{
}

[ComImport]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig]
    int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);

    [PreserveSig]
    int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bDefault, out IntPtr ppFormat);

    [PreserveSig]
    int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);

    [PreserveSig]
    int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);

    [PreserveSig]
    int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);

    [PreserveSig]
    int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref long pmftPeriod);

    [PreserveSig]
    int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);

    [PreserveSig]
    int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);

    [PreserveSig]
    int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, out PropVariant pv);

    [PreserveSig]
    int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, ref PropVariant pv);

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole role);

    [PreserveSig]
    int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bVisible);
}
