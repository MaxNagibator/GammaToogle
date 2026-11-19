using Microsoft.Win32;

namespace GammaToggle;

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
        tray.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left) OpenSettings();
        };
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

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        GammaController.Apply(gamma, settings.Monitors);

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

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    public event Action? HotkeyPressed;
    public HotkeyWindow() => CreateHandle(new CreateParams());
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey) HotkeyPressed?.Invoke();
        base.WndProc(ref message);
    }
    public void Dispose() => DestroyHandle();
}
