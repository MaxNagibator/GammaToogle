using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GammaToggle;

internal static class AppIcon
{
    public static Icon Create()
    {
        const int size = 64;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
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
