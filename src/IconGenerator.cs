using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WireFlux
{
    public static class IconGenerator
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon GetAppIcon()
        {
            try
            {
                Icon associated = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (associated != null)
                {
                    return associated;
                }
            }
            catch
            {
            }

            return CreateFallbackIcon();
        }

        private static Icon CreateFallbackIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var bgBrush = new SolidBrush(Color.FromArgb(16, 26, 44)))
                using (var borderPen = new Pen(Color.FromArgb(0, 216, 255), 2f))
                {
                    g.FillEllipse(bgBrush, 2, 2, 28, 28);
                    g.DrawEllipse(borderPen, 2, 2, 28, 28);
                }

                using (var pulsePen = new Pen(Color.FromArgb(16, 224, 148), 2.2f))
                {
                    pulsePen.StartCap = LineCap.Round;
                    pulsePen.EndCap = LineCap.Round;
                    pulsePen.LineJoin = LineJoin.Round;
                    var pts = new[]
                    {
                        new PointF(6f, 16f),
                        new PointF(11f, 16f),
                        new PointF(14f, 10f),
                        new PointF(18f, 22f),
                        new PointF(21f, 16f),
                        new PointF(26f, 16f)
                    };
                    g.DrawLines(pulsePen, pts);
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(hIcon))
                    {
                        return (Icon)temp.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }
    }
}
