using System.Runtime.InteropServices;

namespace GammaToggle;

internal static class GammaController
{
    public static List<DisplayMonitor> GetMonitors() => Screen.AllScreens
        .Select(screen => new DisplayMonitor(
            screen.DeviceName,
            $"{screen.DeviceName} — {screen.Bounds.Width}×{screen.Bounds.Height}{(screen.Primary ? " (основной)" : "")}"))
        .ToList();

    public static bool Apply(double gamma, IReadOnlyCollection<string>? selectedMonitors = null)
    {
        var ramp = new GammaRamp { Red = new ushort[256], Green = new ushort[256], Blue = new ushort[256] };
        for (var index = 0; index < 256; index++)
        {
            var value = (ushort)Math.Clamp(
                (int)Math.Round(Math.Pow(index / 255.0, 1.0 / gamma) * 65535.0), 0, 65535);
            ramp.Red[index] = ramp.Green[index] = ramp.Blue[index] = value;
        }

        var success = true;
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info)) { success = false; return true; }
            if (selectedMonitors is { Count: > 0 }
                && !selectedMonitors.Contains(info.DeviceName, StringComparer.OrdinalIgnoreCase)) return true;
            var dc = NativeMethods.CreateDC("DISPLAY", info.DeviceName, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) { success = false; return true; }
            success &= NativeMethods.SetDeviceGammaRamp(dc, ref ramp);
            NativeMethods.DeleteDC(dc);
            return true;
        }, IntPtr.Zero);
        return success;
    }
}

internal sealed record DisplayMonitor(string DeviceName, string Label);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
internal struct MonitorInfoEx
{
    public int Size;
    public NativeRect Monitor;
    public NativeRect WorkArea;
    public uint Flags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential)]
internal struct GammaRamp
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Red;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Green;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Blue;
}

internal static class NativeMethods
{
    internal delegate bool MonitorEnumProc(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] internal static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll", CharSet = CharSet.Auto)] internal static extern IntPtr CreateDC(string driver, string device, string? output, IntPtr initData);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern bool SetDeviceGammaRamp(IntPtr dc, ref GammaRamp ramp);
}
