using System.Globalization;

namespace GammaToggle;

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

    private void CaptureHotkey(object? sender, KeyEventArgs eventArgs)
    {
        eventArgs.SuppressKeyPress = true;
        eventArgs.Handled = true;
        if (eventArgs.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu) return;
        var modifiers = HotkeyModifiers.None;
        if (eventArgs.Control) modifiers |= HotkeyModifiers.Control;
        if (eventArgs.Shift) modifiers |= HotkeyModifiers.Shift;
        if (eventArgs.Alt) modifiers |= HotkeyModifiers.Alt;
        if (modifiers == HotkeyModifiers.None) return;
        hotkey = new Hotkey(eventArgs.KeyCode, modifiers);
        hotkeyBox.Text = hotkey.ToString();
    }

    private void Save(object? sender, EventArgs eventArgs)
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

        var selectedMonitors = monitors.Where((_, index) => monitorsBox.GetItemChecked(index))
            .Select(monitor => monitor.DeviceName).ToList();
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
