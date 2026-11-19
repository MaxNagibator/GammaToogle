using System.Text.Json;

namespace GammaToggle;

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
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GammaToggle");
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
