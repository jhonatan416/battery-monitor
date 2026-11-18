using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace BatteryMonitor.App.Tray;

/// <summary>Draws a small tray icon showing the percent number, colored by level.</summary>
internal static class BatteryIconRenderer
{
    public static Icon Render(int? percent, bool charging, bool stale)
    {
        int size = SystemInformation.SmallIconSize.Width; // 16 at 100 % scaling, larger on HiDPI
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            Color color = percent switch
            {
                null => Color.Gray,
                _ when charging => Color.DeepSkyBlue,
                <= 20 => Color.OrangeRed,
                <= 50 => Color.Gold,
                _ => Color.LimeGreen,
            };
            if (stale) color = Color.FromArgb(150, color);

            string text = percent switch { null => "?", 100 => "F", _ => percent.Value.ToString() };
            using var font = new Font("Segoe UI", size * (text.Length > 1 ? 0.56f : 0.7f), FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, brush, new RectangleF(-1, 0, size + 2, size), fmt);

            // Bottom bar: fill proportional to the level.
            if (percent is int p)
            {
                int w = Math.Max(1, (size - 2) * p / 100);
                g.FillRectangle(brush, 1, size - 2, w, 2);
            }
        }

        IntPtr hIcon = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
        finally { DestroyIcon(hIcon); }
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
