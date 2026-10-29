// Khorshid (خورشید): Persian (Solar Hijri) date in the system tray, a month
// calendar popup and an optional desktop widget.
// Build with build.ps1 (uses the C# compiler that ships with .NET Framework 4.x).

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Khorshid")]
[assembly: System.Reflection.AssemblyProduct("Khorshid")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace Khorshid
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool created;
            using (var mutex = new Mutex(true, "Khorshid.SingleInstance", out created))
            {
                if (!created) return;
                Application.Run(new TrayApp());
            }
        }
    }

    // ---------------------------------------------------------------- calendar data

    static class Persian
    {
        public static readonly PersianCalendar Cal = new PersianCalendar();
        static readonly HijriCalendar Hijri = new HijriCalendar();

        public static readonly string[] Months = {
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

        public static readonly string[] WeekDays = {
            "شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه" };

        public static readonly string[] WeekShort = { "ش", "ی", "د", "س", "چ", "پ", "ج" };

        static readonly string[] HijriMonths = {
            "محرم", "صفر", "ربیع‌الاول", "ربیع‌الثانی", "جمادی‌الاول", "جمادی‌الثانی",
            "رجب", "شعبان", "رمضان", "شوال", "ذی‌القعده", "ذی‌الحجه" };

        public static string Digits(int n) { return Digits(n.ToString(CultureInfo.InvariantCulture)); }

        public static string Digits(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] >= '0' && chars[i] <= '9') chars[i] = (char)('۰' + (chars[i] - '0'));
            return new string(chars);
        }

        // Column in a Saturday-first week: Saturday = 0 ... Friday = 6.
        public static int Col(DateTime d) { return ((int)d.DayOfWeek + 1) % 7; }

        public static int Year(DateTime d) { return Cal.GetYear(d); }
        public static int Month(DateTime d) { return Cal.GetMonth(d); }
        public static int Day(DateTime d) { return Cal.GetDayOfMonth(d); }

        public static string LongDate(DateTime d)
        {
            return WeekDays[Col(d)] + " " + Digits(Day(d)) + " " + Months[Month(d) - 1] + " " + Digits(Year(d));
        }

        public static string ShortDate(DateTime d)
        {
            return Digits(Day(d)) + " " + Months[Month(d) - 1] + " " + Digits(Year(d));
        }

        public static string HijriDate(DateTime d)
        {
            return Digits(Hijri.GetDayOfMonth(d)) + " " + HijriMonths[Hijri.GetMonth(d) - 1] + " " + Digits(Hijri.GetYear(d));
        }

        public static string GregorianDate(DateTime d)
        {
            return d.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture);
        }

        // Official holidays that fall on fixed Solar Hijri dates.
        public static string Holiday(int month, int day)
        {
            if (month == 1 && day >= 1 && day <= 4) return "عید نوروز";
            if (month == 1 && day == 12) return "روز جمهوری اسلامی";
            if (month == 1 && day == 13) return "روز طبیعت";
            if (month == 3 && day == 14) return "رحلت امام خمینی";
            if (month == 3 && day == 15) return "قیام ۱۵ خرداد";
            if (month == 11 && day == 22) return "پیروزی انقلاب اسلامی";
            if (month == 12 && day == 29) return "ملی شدن صنعت نفت";
            return null;
        }

        public static string Holiday(DateTime d) { return Holiday(Month(d), Day(d)); }
    }

    // ---------------------------------------------------------------- settings

    static class Settings
    {
        static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Khorshid");
        static readonly string FilePath = Path.Combine(Dir, "settings.ini");

        public static bool Widget;
        public static bool WidgetTopMost;
        public static int WidgetX = int.MinValue, WidgetY = int.MinValue;

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim(), val = line.Substring(eq + 1).Trim();
                    int n;
                    if (key == "widget") Widget = val == "1";
                    else if (key == "topmost") WidgetTopMost = val == "1";
                    else if (key == "x" && int.TryParse(val, out n)) WidgetX = n;
                    else if (key == "y" && int.TryParse(val, out n)) WidgetY = n;
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new[] {
                    "widget=" + (Widget ? "1" : "0"),
                    "topmost=" + (WidgetTopMost ? "1" : "0"),
                    "x=" + WidgetX,
                    "y=" + WidgetY });
            }
            catch { }
        }
    }

    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "Khorshid";

        // Store (MSIX) installs can't use the Run key; they declare a StartupTask that
        // the user switches on in Settings > Apps > Startup.
        public static readonly bool Packaged = Native.IsPackaged();

        public static bool Enabled
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(ValueName) != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(ValueName, false);
                }
            }
        }
    }

    // ---------------------------------------------------------------- drawing helpers

    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr handle);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);

        public static bool IsPackaged()
        {
            try
            {
                int length = 0;
                return GetCurrentPackageFullName(ref length, null) != 15700; // APPMODEL_ERROR_NO_PACKAGE
            }
            catch (EntryPointNotFoundException) { return false; }
        }

        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int CS_DROPSHADOW = 0x20000;
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int WM_NCLBUTTONDBLCLK = 0xA3;
        public const int HTCAPTION = 2;
        public const int WM_WINDOWPOSCHANGING = 0x46;
        public const int WM_MOUSEACTIVATE = 0x21;
        public const int MA_NOACTIVATE = 3;
        public const int SWP_NOZORDER = 0x4;
        public static readonly IntPtr HWND_BOTTOM = (IntPtr)1;

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS
        {
            public IntPtr hwnd, hwndInsertAfter;
            public int x, y, cx, cy, flags;
        }

        // Rounded corners on Windows 11; silently ignored on older versions.
        public static void RoundCorners(IntPtr hWnd)
        {
            try { int round = 2; DwmSetWindowAttribute(hWnd, 33, ref round, 4); }
            catch { }
        }
    }

    class Theme
    {
        public Color Bg, Text, Muted, Line, Hover, Holiday, TodayBg, TodayText;

        public static bool IsDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        // Set by the asset generator to render a fixed theme.
        public static bool? ForceDark = null;

        public static Theme Current()
        {
            if (ForceDark ?? IsDark())
                return new Theme {
                    Bg = Color.FromArgb(32, 32, 32), Text = Color.FromArgb(240, 240, 240),
                    Muted = Color.FromArgb(155, 155, 155), Line = Color.FromArgb(58, 58, 58),
                    Hover = Color.FromArgb(48, 48, 48), Holiday = Color.FromArgb(255, 110, 110),
                    TodayBg = Color.FromArgb(76, 194, 255), TodayText = Color.FromArgb(10, 10, 10) };
            return new Theme {
                Bg = Color.FromArgb(249, 249, 249), Text = Color.FromArgb(26, 26, 26),
                Muted = Color.FromArgb(110, 110, 110), Line = Color.FromArgb(224, 224, 224),
                Hover = Color.FromArgb(234, 234, 234), Holiday = Color.FromArgb(205, 45, 45),
                TodayBg = Color.FromArgb(0, 103, 192), TodayText = Color.White };
        }
    }

    enum Align { Left, Center, Right }

    static class Ui
    {
        public static readonly string FontName = PickFont();

        static string PickFont()
        {
            using (var fonts = new InstalledFontCollection())
                foreach (var f in fonts.Families)
                    if (f.Name == "Vazirmatn") return f.Name;
            return "Segoe UI";
        }

        // Sizes are in logical pixels; forms scale their Graphics by the DPI factor.
        public static Font F(float px, bool bold)
        {
            return new Font(FontName, px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public static void Text(Graphics g, string s, Font f, Color c, RectangleF r, Align align, bool rtl)
        {
            using (var sf = new StringFormat(StringFormatFlags.NoWrap))
            using (var brush = new SolidBrush(c))
            {
                if (rtl) sf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                if (align == Align.Center) sf.Alignment = StringAlignment.Center;
                else if ((align == Align.Right) == rtl) sf.Alignment = StringAlignment.Near;
                else sf.Alignment = StringAlignment.Far;
                g.DrawString(s, f, brush, r, sf);
            }
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var p = Round(r, radius)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Prepare(Graphics g, float scale)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.ScaleTransform(scale, scale);
        }

        public static float DpiScale(Control c)
        {
            using (var g = c.CreateGraphics()) return g.DpiX / 96f;
        }

        // Tray icon: today's Persian day number on the logo's sun gradient.
        public static Bitmap DayIcon(int size, int day)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                var plate = new RectangleF(0, 0, size - 1, size - 1);
                using (var brush = new LinearGradientBrush(RectangleF.Inflate(plate, 0, 1), Color.FromArgb(255, 150, 50), Color.FromArgb(222, 56, 46), 90f))
                using (var path = Round(plate, size * 0.22f))
                    g.FillPath(brush, path);

                string txt = Persian.Digits(day);
                float px = size * 0.85f;
                Font f = null;
                while (true)
                {
                    f = new Font(FontName, px, FontStyle.Bold, GraphicsUnit.Pixel);
                    var m = g.MeasureString(txt, f, PointF.Empty, StringFormat.GenericTypographic);
                    if (m.Width <= size - 3 || px <= 6) break;
                    f.Dispose();
                    px -= 0.5f;
                }
                using (f) Text(g, txt, f, Color.White, new RectangleF(0, size * 0.04f, size, size), Align.Center, true);
            }
            return bmp;
        }
    }

    // ---------------------------------------------------------------- tray app

    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray;
        readonly CalendarPopup popup = new CalendarPopup();
        readonly DesktopWidget widget = new DesktopWidget();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly ToolStripMenuItem miWidget, miStartup;
        DateTime shownDate = DateTime.MinValue, lastPushed = DateTime.MinValue;
        IntPtr iconHandle = IntPtr.Zero;

        public TrayApp()
        {
            Settings.Load();

            var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
            menu.Items.Add("نمایش تقویم", null, (s, e) => ShowPopup());
            miWidget = new ToolStripMenuItem("ویجت روی دسکتاپ") { CheckOnClick = true, Checked = Settings.Widget };
            miWidget.CheckedChanged += (s, e) => SetWidgetVisible(miWidget.Checked);
            menu.Items.Add(miWidget);
            if (Startup.Packaged)
            {
                miStartup = new ToolStripMenuItem("اجرای خودکار با ویندوز…");
                miStartup.Click += (s, e) => System.Diagnostics.Process.Start("ms-settings:startupapps");
            }
            else
            {
                miStartup = new ToolStripMenuItem("اجرا هنگام ورود به ویندوز");
                miStartup.Click += (s, e) => { Startup.Enabled = !Startup.Enabled; };
                menu.Opening += (s, e) => { miStartup.Checked = Startup.Enabled; };
            }
            menu.Items.Add(miStartup);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("خروج", null, (s, e) => Quit());

            tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) TogglePopup(); };

            widget.OpenCalendar += (s, e) => ShowPopup();
            widget.HideRequested += (s, e) => { miWidget.Checked = false; };

            timer.Interval = 15000;
            timer.Tick += (s, e) => RefreshDate();
            timer.Start();
            SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
            SystemEvents.TimeChanged += OnTimeChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            RefreshDate();
            if (Settings.Widget) widget.Show();
        }

        void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) { widget.ApplyTheme(); }
        void OnTimeChanged(object sender, EventArgs e) { RefreshDate(); }

        // After sleep or a locked session the shell may still be waking up; resend on the next tick.
        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) lastPushed = DateTime.MinValue; }
        void OnSessionSwitch(object sender, SessionSwitchEventArgs e) { if (e.Reason == SessionSwitchReason.SessionUnlock) lastPushed = DateTime.MinValue; }

        void RefreshDate()
        {
            // The shell can silently drop a tray icon update (NotifyIcon ignores the failure),
            // for example right after resuming from sleep, so resend it every few minutes too.
            DateTime today = DateTime.Today;
            if (today == shownDate && (DateTime.Now - lastPushed).TotalMinutes < 5) return;

            using (var bmp = Ui.DayIcon(SystemInformation.SmallIconSize.Width, Persian.Day(today)))
            {
                IntPtr old = iconHandle;
                iconHandle = bmp.GetHicon();
                tray.Icon = Icon.FromHandle(iconHandle);
                if (old != IntPtr.Zero) Native.DestroyIcon(old);
            }
            string tip = Persian.LongDate(today) + "\n" + today.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;

            widget.Invalidate();
            if (popup.Visible) popup.Invalidate();
            shownDate = today;
            lastPushed = DateTime.Now;
        }

        void TogglePopup()
        {
            // A click on the tray icon first deactivates (hides) an open popup; don't reopen it.
            if (popup.Visible || (DateTime.Now - popup.LastHidden).TotalMilliseconds < 300) { popup.HidePopup(); return; }
            ShowPopup();
        }

        void ShowPopup() { popup.ShowNear(Cursor.Position); }

        void SetWidgetVisible(bool visible)
        {
            Settings.Widget = visible;
            Settings.Save();
            if (visible) widget.Show(); else widget.Hide();
        }

        void Quit()
        {
            SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
            SystemEvents.TimeChanged -= OnTimeChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
            if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
            ExitThread();
        }
    }

    // ---------------------------------------------------------------- month calendar popup

    class CalendarPopup : Form
    {
        const float W = 340, H = 446, Pad = 16, GridTop = 148, RowH = 40;

        readonly float scale;
        Theme th = Theme.Current();
        int viewYear, viewMonth;
        int hoverDay = -1;      // 0-based day of the viewed month under the mouse
        int hoverButton;        // 1 = previous, 2 = next, 3 = today
        RectangleF prevRect, nextRect, todayRect;
        readonly RectangleF[] cells = new RectangleF[31];
        readonly Font fTitle = Ui.F(19, true), fSub = Ui.F(12.5f, false), fMonth = Ui.F(16, true),
            fMonthSub = Ui.F(11.5f, false), fWeek = Ui.F(12, false), fDay = Ui.F(16, false),
            fDaySmall = Ui.F(10, false), fFoot = Ui.F(12.5f, false);

        public DateTime LastHidden = DateTime.MinValue;

        public CalendarPopup()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            scale = Ui.DpiScale(this);
            ClientSize = new Size((int)(W * scale), (int)(H * scale));
            Deactivate += (s, e) => HidePopup();
            ResetToToday();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= Native.CS_DROPSHADOW;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        public void ResetToToday()
        {
            th = Theme.Current();
            BackColor = th.Bg;
            viewYear = Persian.Year(DateTime.Today);
            viewMonth = Persian.Month(DateTime.Today);
            hoverDay = -1;
            hoverButton = 0;
        }

        public void ShowNear(Point anchor)
        {
            ResetToToday();
            Rectangle wa = Screen.FromPoint(anchor).WorkingArea;
            int w = Width, h = Height, gap = (int)(12 * scale);
            int x = anchor.X >= wa.Right ? wa.Right - w - gap
                  : anchor.X < wa.Left ? wa.Left + gap
                  : Math.Max(wa.Left + gap, Math.Min(anchor.X - w / 2, wa.Right - w - gap));
            int y = anchor.Y >= wa.Bottom ? wa.Bottom - h - gap
                  : anchor.Y < wa.Top ? wa.Top + gap
                  : Math.Max(wa.Top + gap, Math.Min(anchor.Y - h / 2, wa.Bottom - h - gap));
            Location = new Point(x, y);
            Show();
            Activate();
            Native.SetForegroundWindow(Handle);
            Invalidate();
        }

        public void HidePopup()
        {
            if (!Visible) return;
            Hide();
            LastHidden = DateTime.Now;
        }

        void ShiftMonth(int delta)
        {
            int m = viewYear * 12 + (viewMonth - 1) + delta;
            viewYear = m / 12;
            viewMonth = m % 12 + 1;
            hoverDay = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Ui.Prepare(g, scale);
            g.Clear(th.Bg);
            DateTime today = DateTime.Today;
            float inner = W - 2 * Pad;

            // Today
            Ui.Text(g, Persian.LongDate(today), fTitle, th.Text, new RectangleF(Pad, 12, inner, 30), Align.Right, true);
            Ui.Text(g, Persian.HijriDate(today), fSub, th.Muted, new RectangleF(Pad + inner / 2, 42, inner / 2, 20), Align.Right, true);
            Ui.Text(g, today.ToString("ddd, d MMMM yyyy", CultureInfo.InvariantCulture), fSub, th.Muted,
                new RectangleF(Pad, 42, inner / 2 + 20, 20), Align.Left, false);
            using (var pen = new Pen(th.Line)) g.DrawLine(pen, Pad, 70, W - Pad, 70);

            // Month navigation (RTL: the right arrow goes back, the left arrow goes forward)
            nextRect = new RectangleF(Pad, 80, 34, 34);
            prevRect = new RectangleF(W - Pad - 34, 80, 34, 34);
            if (hoverButton == 1) Ui.FillRound(g, th.Hover, prevRect, 6);
            if (hoverButton == 2) Ui.FillRound(g, th.Hover, nextRect, 6);
            Chevron(g, prevRect, true);
            Chevron(g, nextRect, false);

            DateTime first = Persian.Cal.ToDateTime(viewYear, viewMonth, 1, 0, 0, 0, 0);
            int days = Persian.Cal.GetDaysInMonth(viewYear, viewMonth);
            DateTime last = first.AddDays(days - 1);
            Ui.Text(g, Persian.Months[viewMonth - 1] + " " + Persian.Digits(viewYear), fMonth, th.Text,
                new RectangleF(60, 78, W - 120, 22), Align.Center, true);
            Ui.Text(g, GregorianRange(first, last), fMonthSub, th.Muted, new RectangleF(60, 100, W - 120, 16), Align.Center, false);

            // Week header, Saturday on the right
            float cw = inner / 7;
            for (int c = 0; c < 7; c++)
                Ui.Text(g, Persian.WeekShort[c], fWeek, c == 6 ? th.Holiday : th.Muted,
                    new RectangleF(Pad + (6 - c) * cw, 122, cw, 22), Align.Center, true);

            // Days
            int startCol = Persian.Col(first);
            for (int i = 0; i < cells.Length; i++) cells[i] = RectangleF.Empty;
            for (int d = 1; d <= days; d++)
            {
                int idx = startCol + d - 1, c = idx % 7, r = idx / 7;
                var cell = new RectangleF(Pad + (6 - c) * cw, GridTop + r * RowH, cw, RowH);
                cells[d - 1] = cell;
                DateTime date = first.AddDays(d - 1);
                bool isToday = date == today;
                bool red = c == 6 || Persian.Holiday(viewMonth, d) != null;
                var box = RectangleF.Inflate(cell, -3, -2);

                if (isToday) Ui.FillRound(g, th.TodayBg, box, 8);
                else if (hoverDay == d - 1) Ui.FillRound(g, th.Hover, box, 8);

                Color main = isToday ? th.TodayText : red ? th.Holiday : th.Text;
                Color small = isToday ? th.TodayText : th.Muted;
                Ui.Text(g, Persian.Digits(d), fDay, main, new RectangleF(box.X, box.Y, box.Width, box.Height - 11), Align.Center, true);
                Ui.Text(g, date.Day.ToString(CultureInfo.InvariantCulture), fDaySmall, small,
                    new RectangleF(box.X, box.Bottom - 15, box.Width, 13), Align.Center, false);
            }

            // Footer: details of the hovered day (or today), plus a "today" button
            float footTop = GridTop + 6 * RowH + 4;
            using (var pen = new Pen(th.Line)) g.DrawLine(pen, Pad, footTop, W - Pad, footTop);

            int focusDay = hoverDay >= 0 ? hoverDay + 1
                : (viewYear == Persian.Year(today) && viewMonth == Persian.Month(today)) ? Persian.Day(today) : -1;
            if (focusDay > 0)
            {
                DateTime fd = first.AddDays(focusDay - 1);
                string holiday = Persian.Holiday(viewMonth, focusDay);
                var textRect = new RectangleF(100, footTop + 6, W - Pad - 100, 20);
                Ui.Text(g, Persian.LongDate(fd), fFoot, th.Text, textRect, Align.Right, true);
                textRect.Y += 20;
                if (holiday != null) Ui.Text(g, holiday, fFoot, th.Holiday, textRect, Align.Right, true);
                else Ui.Text(g, fd.ToString("d MMMM yyyy", CultureInfo.InvariantCulture), fFoot, th.Muted, textRect, Align.Right, false);
            }

            todayRect = new RectangleF(Pad, footTop + 10, 68, 30);
            Ui.FillRound(g, hoverButton == 3 ? th.Hover : th.Bg, todayRect, 15);
            using (var p = Ui.Round(todayRect, 15)) using (var pen = new Pen(th.Line)) g.DrawPath(pen, p);
            Ui.Text(g, "امروز", fFoot, th.Text, todayRect, Align.Center, true);
        }

        void Chevron(Graphics g, RectangleF r, bool pointRight)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, dx = pointRight ? 3.5f : -3.5f;
            using (var pen = new Pen(th.Text, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(cx - dx, cy - 7), new PointF(cx + dx, cy), new PointF(cx - dx, cy + 7) });
        }

        static string GregorianRange(DateTime a, DateTime b)
        {
            var inv = CultureInfo.InvariantCulture;
            if (a.Year != b.Year) return a.ToString("MMMM yyyy", inv) + " – " + b.ToString("MMMM yyyy", inv);
            if (a.Month != b.Month) return a.ToString("MMMM", inv) + " – " + b.ToString("MMMM yyyy", inv);
            return a.ToString("MMMM yyyy", inv);
        }

        PointF Logical(Point p) { return new PointF(p.X / scale, p.Y / scale); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            PointF p = Logical(e.Location);
            int day = -1;
            for (int i = 0; i < cells.Length; i++)
                if (!cells[i].IsEmpty && cells[i].Contains(p)) { day = i; break; }
            int button = prevRect.Contains(p) ? 1 : nextRect.Contains(p) ? 2 : todayRect.Contains(p) ? 3 : 0;
            Cursor = button != 0 ? Cursors.Hand : Cursors.Default;
            if (day != hoverDay || button != hoverButton)
            {
                hoverDay = day;
                hoverButton = button;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverDay = -1;
            hoverButton = 0;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            PointF p = Logical(e.Location);
            if (prevRect.Contains(p)) ShiftMonth(-1);
            else if (nextRect.Contains(p)) ShiftMonth(1);
            else if (todayRect.Contains(p)) { ResetToToday(); Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ShiftMonth(e.Delta > 0 ? -1 : 1);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) HidePopup();
            else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.PageDown) ShiftMonth(1);
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.PageUp) ShiftMonth(-1);
            else if (e.KeyCode == Keys.Home) { ResetToToday(); Invalidate(); }
        }
    }

    // ---------------------------------------------------------------- desktop widget

    class DesktopWidget : Form
    {
        const float W = 290, H = 118;

        readonly float scale;
        Theme th = Theme.Current();
        readonly Font fWeek = Ui.F(14, false), fBig = Ui.F(27, true), fSmall = Ui.F(12.5f, false);
        readonly ToolStripMenuItem miTopMost;

        public event EventHandler OpenCalendar;
        public event EventHandler HideRequested;

        public DesktopWidget()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = Settings.WidgetTopMost;
            Opacity = 0.96;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            scale = Ui.DpiScale(this);
            ClientSize = new Size((int)(W * scale), (int)(H * scale));
            BackColor = th.Bg;
            Location = InitialLocation();

            var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
            menu.Items.Add("نمایش تقویم", null, (s, e) => Raise(OpenCalendar));
            miTopMost = new ToolStripMenuItem("همیشه روی پنجره‌ها") { CheckOnClick = true, Checked = Settings.WidgetTopMost };
            miTopMost.CheckedChanged += (s, e) => { TopMost = Settings.WidgetTopMost = miTopMost.Checked; Settings.Save(); };
            menu.Items.Add(miTopMost);
            menu.Items.Add("پنهان کردن ویجت", null, (s, e) => Raise(HideRequested));
            ContextMenuStrip = menu;

            ResizeEnd += (s, e) => { Settings.WidgetX = Left; Settings.WidgetY = Top; Settings.Save(); };
        }

        void Raise(EventHandler handler) { if (handler != null) handler(this, EventArgs.Empty); }

        Point InitialLocation()
        {
            if (Settings.WidgetX != int.MinValue)
            {
                var p = new Point(Settings.WidgetX, Settings.WidgetY);
                foreach (var screen in Screen.AllScreens)
                    if (screen.WorkingArea.Contains(new Point(p.X + 20, p.Y + 20))) return p;
            }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Right - Width - (int)(24 * scale), wa.Top + (int)(24 * scale));
        }

        public void ApplyTheme()
        {
            th = Theme.Current();
            BackColor = th.Bg;
            Invalidate();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            // Drag the borderless window as if by its title bar.
            Native.ReleaseCapture();
            Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, (IntPtr)Native.HTCAPTION, IntPtr.Zero);
        }

        // Unless pinned on top, the widget sits on the desktop: it never takes focus
        // and stays below all other windows, even when shown, clicked or dragged.
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_NCLBUTTONDBLCLK) { Raise(OpenCalendar); return; }
            if (!Settings.WidgetTopMost)
            {
                if (m.Msg == Native.WM_MOUSEACTIVATE) { m.Result = (IntPtr)Native.MA_NOACTIVATE; return; }
                if (m.Msg == Native.WM_WINDOWPOSCHANGING)
                {
                    var pos = (Native.WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(Native.WINDOWPOS));
                    pos.hwndInsertAfter = Native.HWND_BOTTOM;
                    pos.flags &= ~Native.SWP_NOZORDER;
                    Marshal.StructureToPtr(pos, m.LParam, false);
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Ui.Prepare(g, scale);
            g.Clear(th.Bg);
            DateTime today = DateTime.Today;
            string holiday = Persian.Holiday(today);
            bool red = Persian.Col(today) == 6 || holiday != null;

            Ui.FillRound(g, red ? th.Holiday : th.TodayBg, new RectangleF(W - 12, 20, 4, H - 40), 2);
            float right = W - 22;
            Ui.Text(g, Persian.WeekDays[Persian.Col(today)], fWeek, red ? th.Holiday : th.Muted,
                new RectangleF(14, 12, right - 14, 24), Align.Right, true);
            Ui.Text(g, Persian.ShortDate(today), fBig, th.Text, new RectangleF(14, 36, right - 14, 42), Align.Right, true);
            Ui.Text(g, today.ToString("ddd, d MMM yyyy", CultureInfo.InvariantCulture), fSmall, th.Muted,
                new RectangleF(14, 84, 112, 20), Align.Left, false);
            Ui.Text(g, holiday ?? Persian.HijriDate(today), fSmall, holiday != null ? th.Holiday : th.Muted,
                new RectangleF(122, 84, right - 122, 20), Align.Right, true);
        }
    }
}
