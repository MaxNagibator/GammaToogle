using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace GammaToggle;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "GammaToggle.SingleInstance", out var first);
        if (!first) { MessageBox.Show("Gamma Toggle уже запущен — ищи значок в трее.", "Gamma Toggle"); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new GammaContext());
    }
}

internal sealed class GammaContext : ApplicationContext
{
    private const int HotkeyId = 0x4741;
    private const uint ModNoRepeat = 0x4000;
    private readonly HotkeyWindow window = new();
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private AppSettings settings;
    private double gamma = 1.0;

    public GammaContext()
    {
        settings = SettingsStore.Load();
        window.HotkeyPressed += Toggle;
        tray = new NotifyIcon { Icon = AppIcon.Create(), ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Toggle();
        BuildMenu();
        if (!RegisterHotkey(settings.Hotkey))
            MessageBox.Show($"Не удалось занять {settings.Hotkey}. Выбери другое сочетание в настройках.",
                "Gamma Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        ApplyGamma(settings.Levels[0]);
    }

    private void BuildMenu()
    {
        menu.Items.Clear();
        foreach (var level in settings.Levels)
        {
            var captured = level;
            menu.Items.Add($"Гамма — {level:0.00}", null, (_, _) => ApplyGamma(captured));
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Настройки…", null, (_, _) => OpenSettings());
        menu.Items.Add("Выход", null, (_, _) => Exit());
    }

    private void Toggle()
    {
        var current = settings.Levels.FindIndex(level => Math.Abs(level - gamma) < 0.001);
        ApplyGamma(settings.Levels[(current + 1) % settings.Levels.Count]);
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(settings);
        if (dialog.ShowDialog() != DialogResult.OK || dialog.Result is null) return;
        var updated = dialog.Result;
        NativeMethods.UnregisterHotKey(window.Handle, HotkeyId);
        if (!RegisterHotkey(updated.Hotkey))
        {
            RegisterHotkey(settings.Hotkey);
            MessageBox.Show($"Сочетание {updated.Hotkey} уже используется. Настройки не изменены.",
                "Gamma Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        settings = updated;
        SettingsStore.Save(settings);
        BuildMenu();
        GammaController.Apply(1.0);
        ApplyGamma(settings.Levels[0]);
    }

    private bool RegisterHotkey(Hotkey hotkey) => NativeMethods.RegisterHotKey(
        window.Handle, HotkeyId, (uint)hotkey.Modifiers | ModNoRepeat, (uint)hotkey.Key);

    private void ApplyGamma(double value)
    {
        gamma = value;
        GammaController.Apply(value, settings.Monitors);
        tray.Text = $"Gamma Toggle — {value:0.00} — {settings.Hotkey}";
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => GammaController.Apply(gamma, settings.Monitors);

    private void Exit()
    {
        GammaController.Apply(1.0);
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        NativeMethods.UnregisterHotKey(window.Handle, HotkeyId);
        tray.Visible = false;
        tray.Icon?.Dispose();
        tray.Dispose();
        window.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && tray.Visible) Exit();
        base.Dispose(disposing);
    }
}

internal sealed class SettingsForm : Form
{
    private readonly TextBox levelsBox = new();
    private readonly TextBox hotkeyBox = new();
    private readonly CheckedListBox monitorsBox = new();
    private readonly List<DisplayMonitor> monitors = GammaController.GetMonitors();
    private Hotkey hotkey;
    public AppSettings? Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        hotkey = current.Hotkey;
        Text = "Настройки Gamma Toggle";
        Icon = AppIcon.Create();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 360);
        Font = new Font("Segoe UI", 9F);

        var levelsLabel = new Label { Text = "Ступени гаммы через запятую:", AutoSize = true, Location = new Point(20, 18) };
        levelsBox.SetBounds(20, 43, 380, 28);
        levelsBox.Text = string.Join(", ", current.Levels.Select(x => x.ToString("0.00", CultureInfo.InvariantCulture)));
        var hint = new Label { Text = "Например: 1.00, 2.00, 3.00  (от 0.30 до 5.00)", AutoSize = true, ForeColor = SystemColors.GrayText, Location = new Point(20, 73) };
        var hotkeyLabel = new Label { Text = "Горячая клавиша:", AutoSize = true, Location = new Point(20, 105) };
        hotkeyBox.SetBounds(160, 100, 240, 28);
        hotkeyBox.ReadOnly = true;
        hotkeyBox.Text = hotkey.ToString();
        hotkeyBox.KeyDown += CaptureHotkey;

        var monitorsLabel = new Label { Text = "Применять к мониторам:", AutoSize = true, Location = new Point(20, 143) };
        monitorsBox.SetBounds(20, 168, 380, 120);
        monitorsBox.CheckOnClick = true;
        for (var index = 0; index < monitors.Count; index++)
        {
            var monitor = monitors[index];
            var selected = current.Monitors is not { Count: > 0 }
                || current.Monitors.Contains(monitor.DeviceName, StringComparer.OrdinalIgnoreCase);
            monitorsBox.Items.Add(monitor.Label, selected);
        }

        var save = new Button { Text = "Сохранить" };
        save.SetBounds(214, 308, 90, 32);
        save.Click += Save;
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(310, 308, 90, 32);
        Controls.AddRange([levelsLabel, levelsBox, hint, hotkeyLabel, hotkeyBox, monitorsLabel, monitorsBox, save, cancel]);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void CaptureHotkey(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu) return;
        var modifiers = HotkeyModifiers.None;
        if (e.Control) modifiers |= HotkeyModifiers.Control;
        if (e.Shift) modifiers |= HotkeyModifiers.Shift;
        if (e.Alt) modifiers |= HotkeyModifiers.Alt;
        if (modifiers == HotkeyModifiers.None) return;
        hotkey = new Hotkey(e.KeyCode, modifiers);
        hotkeyBox.Text = hotkey.ToString();
    }

    private void Save(object? sender, EventArgs e)
    {
        var pieces = levelsBox.Text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
        var levels = new List<double>();
        foreach (var piece in pieces)
        {
            if (!double.TryParse(piece.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || value is < 0.30 or > 5.00)
            {
                MessageBox.Show($"Не могу понять ступень «{piece}». Используй числа от 0.30 до 5.00.",
                    "Gamma Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            levels.Add(value);
        }
        if (levels.Count < 2)
        {
            MessageBox.Show("Добавь хотя бы две ступени гаммы.", "Gamma Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var selectedMonitors = monitors
            .Where((_, index) => monitorsBox.GetItemChecked(index))
            .Select(monitor => monitor.DeviceName)
            .ToList();
        if (selectedMonitors.Count == 0)
        {
            MessageBox.Show("Отметь хотя бы один монитор.", "Gamma Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Result = new AppSettings(levels, hotkey, selectedMonitors);
        DialogResult = DialogResult.OK;
        Close();
    }
}

[Flags]
internal enum HotkeyModifiers : uint { None = 0, Alt = 0x0001, Control = 0x0002, Shift = 0x0004 }

internal sealed record Hotkey(Keys Key, HotkeyModifiers Modifiers)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(Key is >= Keys.NumPad0 and <= Keys.NumPad9 ? $"Num{Key - Keys.NumPad0}" : Key.ToString());
        return string.Join(" + ", parts);
    }
}

internal sealed record AppSettings(List<double> Levels, Hotkey Hotkey, List<string>? Monitors = null)
{
    public static AppSettings Default => new([1.00, 2.00, 3.00], new Hotkey(Keys.NumPad0, HotkeyModifiers.Control));
}

internal static class SettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GammaToggle");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (loaded is not null
                    && loaded.Levels is { Count: >= 2 }
                    && loaded.Levels.All(level => level is >= 0.30 and <= 5.00)
                    && loaded.Hotkey.Modifiers != HotkeyModifiers.None
                    && loaded.Hotkey.Key != Keys.None)
                    return loaded;
            }
        }
        catch { }
        return AppSettings.Default;
    }
    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal static class AppIcon
{
    public static Icon Create()
    {
        const int size = 64;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);
        using var background = new SolidBrush(Color.FromArgb(54, 91, 255));
        graphics.FillEllipse(background, 0.5F, 0.5F, size - 1F, size - 1F);
        using var font = new Font("Segoe UI", 42F, FontStyle.Bold, GraphicsUnit.Pixel);
        using var text = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        graphics.DrawString("γ", font, text, new RectangleF(0, -2, size, size), format);
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { NativeMethods.DestroyIcon(handle); }
    }
}

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    public event Action? HotkeyPressed;
    public HotkeyWindow() => CreateHandle(new CreateParams());
    protected override void WndProc(ref Message m) { if (m.Msg == WmHotkey) HotkeyPressed?.Invoke(); base.WndProc(ref m); }
    public void Dispose() => DestroyHandle();
}

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
        for (var i = 0; i < 256; i++)
        {
            var value = (ushort)Math.Clamp((int)Math.Round(Math.Pow(i / 255.0, 1.0 / gamma) * 65535.0), 0, 65535);
            ramp.Red[i] = ramp.Green[i] = ramp.Blue[i] = value;
        }
        var success = true;
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info)) { success = false; return true; }
            if (selectedMonitors is { Count: > 0 }
                && !selectedMonitors.Contains(info.DeviceName, StringComparer.OrdinalIgnoreCase))
                return true;
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
    public int Size; public NativeRect Monitor; public NativeRect WorkArea; public uint Flags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
}
[StructLayout(LayoutKind.Sequential)] internal struct NativeRect { public int Left, Top, Right, Bottom; }
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
