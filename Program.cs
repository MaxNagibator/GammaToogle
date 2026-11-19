namespace GammaToggle;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "GammaToggle.SingleInstance", out var first);
        if (!first)
        {
            MessageBox.Show("Gamma Toggle уже запущен — ищи значок в трее.", "Gamma Toggle");
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new GammaContext());
    }
}
