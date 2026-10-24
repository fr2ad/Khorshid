// Renders the README screenshots, Store screenshots, app icon and MSIX logos.
// Built and run by tools\make-assets.ps1; compiled together with Khorshid.cs so it
// draws the real calendar and widget.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Khorshid.Tools
{
    static class AssetGen
    {
        static readonly Color SunTop = Color.FromArgb(255, 176, 59), SunBottom = Color.FromArgb(226, 64, 52);

        [STAThread]
        static void Main(string[] args)
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            string root = args.Length > 0 ? args[0] : ".";
            string docs = Dir(root, "docs"), assets = Dir(root, "assets"), msix = Dir(root, @"packaging\Assets");

            // Screenshots
            foreach (bool dark in new[] { false, true })
            {
                Theme.ForceDark = dark;
                string suffix = dark ? "dark" : "light";
                using (var popup = Capture(new CalendarPopup())) popup.Save(Path.Combine(docs, "calendar-" + suffix + ".png"));
                using (var widget = Capture(new DesktopWidget())) widget.Save(Path.Combine(docs, "widget-" + suffix + ".png"));
                using (var shot = StoreScreenshot(dark)) shot.Save(Path.Combine(docs, "screenshot-" + suffix + ".png"));
            }
            Theme.ForceDark = null;
            using (var tray = Ui.DayIcon(64, Persian.Day(DateTime.Today))) tray.Save(Path.Combine(docs, "tray-icon.png"));

            // App icon and logo
            using (var logo = Logo(512, true)) logo.Save(Path.Combine(assets, "logo.png"));
            WriteIco(Path.Combine(assets, "Khorshid.ico"), new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 });

            // MSIX visual assets (names follow the MRT scale/targetsize qualifiers)
            SaveLogo(msix, "Square44x44Logo.scale-100.png", 44, 44, true, 1f);
            SaveLogo(msix, "Square44x44Logo.scale-200.png", 88, 88, true, 1f);
            foreach (int t in new[] { 16, 24, 32, 48, 256 })
            {
                SaveLogo(msix, "Square44x44Logo.targetsize-" + t + ".png", t, t, true, 1f);
                SaveLogo(msix, "Square44x44Logo.altform-unplated_targetsize-" + t + ".png", t, t, false, 1f);
            }
            SaveLogo(msix, "Square150x150Logo.scale-100.png", 150, 150, true, 0.62f);
            SaveLogo(msix, "Square150x150Logo.scale-200.png", 300, 300, true, 0.62f);
            SaveLogo(msix, "Wide310x150Logo.scale-100.png", 310, 150, true, 0.62f);
            SaveLogo(msix, "Wide310x150Logo.scale-200.png", 620, 300, true, 0.62f);
            SaveLogo(msix, "StoreLogo.scale-100.png", 50, 50, true, 1f);
            SaveLogo(msix, "StoreLogo.scale-200.png", 100, 100, true, 1f);

            Console.WriteLine("Assets written to " + Path.GetFullPath(root));
        }

        static string Dir(string root, string sub)
        {
            string d = Path.Combine(root, sub);
            Directory.CreateDirectory(d);
            return d;
        }

        static Bitmap Capture(Form f)
        {
            using (f)
            {
                var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height);
                f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.ClientSize));
                return bmp;
            }
        }

        // ------------------------------------------------------------ logo

        // A white sun on a warm rounded square (plated) or a warm sun alone (unplated).
        static void DrawLogo(Graphics g, RectangleF r, bool plated)
        {
            float size = Math.Min(r.Width, r.Height);
            var box = new RectangleF(r.X + (r.Width - size) / 2, r.Y + (r.Height - size) / 2, size, size);
            Brush sun;
            if (plated)
            {
                using (var bg = new LinearGradientBrush(box, SunTop, SunBottom, 90f))
                using (var path = Ui.Round(box, size * 0.22f))
                    g.FillPath(bg, path);
                sun = new SolidBrush(Color.White);
            }
            else
            {
                sun = new LinearGradientBrush(box, SunTop, SunBottom, 90f);
            }

            using (sun)
            {
                float cx = box.X + size / 2, cy = box.Y + size / 2;
                float core = size * (plated ? 0.17f : 0.24f);
                float inner = core * 1.45f, outer = core * (plated ? 2.05f : 1.95f);
                g.FillEllipse(sun, cx - core, cy - core, core * 2, core * 2);
                using (var pen = new Pen(sun, size * (plated ? 0.065f : 0.09f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(pen, cx + (float)Math.Cos(a) * inner, cy + (float)Math.Sin(a) * inner,
                                        cx + (float)Math.Cos(a) * outer, cy + (float)Math.Sin(a) * outer);
                    }
            }
        }

        static Bitmap Logo(int size, bool plated) { return Logo(size, size, plated, 1f); }

        static Bitmap Logo(int w, int h, bool plated, float fill)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                float s = Math.Min(w, h) * fill;
                float margin = s < 32 ? 0 : s / 32f;   // keep tiny icons edge to edge
                DrawLogo(g, new RectangleF((w - s) / 2 + margin, (h - s) / 2 + margin, s - 2 * margin, s - 2 * margin), plated);
            }
            return bmp;
        }

        static void SaveLogo(string dir, string name, int w, int h, bool plated, float fill)
        {
            using (var bmp = Logo(w, h, plated, fill)) bmp.Save(Path.Combine(dir, name), ImageFormat.Png);
        }

        // ICO with PNG-compressed entries (supported since Windows Vista).
        static void WriteIco(string path, int[] sizes)
        {
            var images = new List<byte[]>();
            foreach (int s in sizes)
                using (var bmp = Logo(s, true))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    images.Add(ms.ToArray());
                }

            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(images[i].Length); w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var img in images) w.Write(img);
            }
        }

        // ------------------------------------------------------------ store screenshot

        // 1920x1080 scene: title, desktop widget, and the calendar opened from the tray.
        static Bitmap StoreScreenshot(bool dark)
        {
            const int W = 1920, H = 1080, TaskbarH = 72;
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var popup = Capture(new CalendarPopup()))
            using (var widget = Capture(new DesktopWidget()))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                Color top = dark ? Color.FromArgb(24, 28, 42) : Color.FromArgb(255, 240, 224);
                Color bottom = dark ? Color.FromArgb(62, 30, 40) : Color.FromArgb(255, 205, 178);
                using (var bg = new LinearGradientBrush(new Rectangle(-2, -2, W + 4, H + 4), top, bottom, 60f))
                    g.FillRectangle(bg, 0, 0, W, H);

                // Taskbar with the tray icon
                using (var bar = new SolidBrush(dark ? Color.FromArgb(230, 28, 28, 28) : Color.FromArgb(235, 243, 243, 243)))
                    g.FillRectangle(bar, 0, H - TaskbarH, W, TaskbarH);
                int iconSize = 36, iconX = W - 260;
                using (var tray = Ui.DayIcon(iconSize, Persian.Day(DateTime.Today)))
                    g.DrawImage(tray, iconX, H - TaskbarH + (TaskbarH - iconSize) / 2);
                Color barText = dark ? Color.FromArgb(235, 235, 235) : Color.FromArgb(30, 30, 30);
                using (var f = new Font("Segoe UI", 15, GraphicsUnit.Pixel))
                {
                    var t = DateTime.Today;
                    Ui.Text(g, "12:30 PM", f, barText, new RectangleF(W - 200, H - TaskbarH + 12, 170, 24), Align.Right, false);
                    Ui.Text(g, t.ToString("M/d/yyyy", CultureInfo.InvariantCulture), f, barText,
                        new RectangleF(W - 200, H - TaskbarH + 36, 170, 24), Align.Right, false);
                }

                // Calendar popup above the tray icon
                int px = W - popup.Width - 40, py = H - TaskbarH - popup.Height - 20;
                DrawCard(g, popup, px, py, dark);

                // Title block and widget on the left
                Color title = dark ? Color.White : Color.FromArgb(40, 24, 20);
                Color sub = dark ? Color.FromArgb(200, 200, 210) : Color.FromArgb(110, 70, 60);
                var left = new RectangleF(80, 0, px - 160, 0);
                using (var logo = Logo(150, true))
                    g.DrawImage(logo, left.X + (left.Width - 150) / 2, 150);
                using (var fTitle = Ui.F(110, true)) using (var fSub = Ui.F(40, false)) using (var fEn = new Font("Segoe UI", 30, GraphicsUnit.Pixel))
                {
                    Ui.Text(g, "خورشید", fTitle, title, new RectangleF(left.X, 310, left.Width, 140), Align.Center, true);
                    Ui.Text(g, "تقویم شمسی در نوار وظیفه ویندوز", fSub, sub, new RectangleF(left.X, 450, left.Width, 60), Align.Center, true);
                    Ui.Text(g, "Persian calendar for the Windows taskbar", fEn, sub, new RectangleF(left.X, 510, left.Width, 50), Align.Center, false);
                }
                DrawCard(g, widget, (int)(left.X + (left.Width - widget.Width) / 2), 620, dark);
            }
            return bmp;
        }

        static void DrawCard(Graphics g, Bitmap img, int x, int y, bool dark)
        {
            float radius = 14;
            for (int i = 1; i <= 14; i++)
                using (var shadow = new SolidBrush(Color.FromArgb(dark ? 9 : 6, 0, 0, 0)))
                using (var path = Ui.Round(new RectangleF(x - i, y - i + 8, img.Width + 2 * i, img.Height + 2 * i), radius + i))
                    g.FillPath(shadow, path);
            using (var brush = new TextureBrush(img))
            using (var path = Ui.Round(new RectangleF(x, y, img.Width, img.Height), radius))
            {
                brush.TranslateTransform(x, y);
                g.FillPath(brush, path);
            }
        }
    }
}
