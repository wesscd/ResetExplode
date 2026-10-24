using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ResetExplode;

/// <summary>APIs oficiais do Windows; não edita máscaras binárias de preferências.</summary>
public static class WindowsVisualEffects
{
    [StructLayout(LayoutKind.Sequential)]
    private struct AnimationInfo { public uint Size; public int Enabled; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool GetBool(uint action, uint param, out int value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SetBool(uint action, uint param, IntPtr value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool Animation(uint action, uint param, ref AnimationInfo value, uint flags);

    public static bool ClientAnimations()
    {
        if (!GetBool(0x1042, 0, out int value, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return value != 0;
    }
    public static bool MinimizeAnimations()
    {
        var value = new AnimationInfo { Size = (uint)Marshal.SizeOf<AnimationInfo>() };
        if (!Animation(0x0048, value.Size, ref value, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return value.Enabled != 0;
    }
    public static void SetClient(bool enabled)
    {
        if (!SetBool(0x1043, 0, enabled ? new IntPtr(1) : IntPtr.Zero, 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public static void SetMinimize(bool enabled)
    {
        var value = new AnimationInfo { Size = (uint)Marshal.SizeOf<AnimationInfo>(), Enabled = enabled ? 1 : 0 };
        if (!Animation(0x0049, value.Size, ref value, 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
