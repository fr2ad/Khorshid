// Khorshid (خورشید): Persian (Solar Hijri) date in the system tray, a month
// calendar popup and an optional desktop widget.
// Build with build.ps1 (uses the C# compiler that ships with .NET Framework 4.x).

using System;
using System.Collections.Generic;
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
            int y, m, day;
            Lunar(d, out y, out m, out day);
            return Digits(day) + " " + HijriMonths[m - 1] + " " + Digits(y);
        }

        // First day of each lunar month in Iran's official calendar (Persian year, month, day),
        // from 1 Ramadan 1447. Iran starts lunar months by moon sighting, so outside this table
        // Windows' arithmetic HijriCalendar is used and may be off by a day. Extend it yearly.
        const int LunarFirstYear = 1447, LunarFirstMonth = 9;
        static readonly int[,] LunarStarts = {
            { 1404, 11, 30 },   // Ramadan 1447
            { 1405, 1, 1 }, { 1405, 1, 30 }, { 1405, 2, 28 },
            { 1405, 3, 26 },    // Muharram 1448
            { 1405, 4, 25 }, { 1405, 5, 23 }, { 1405, 6, 22 }, { 1405, 7, 20 }, { 1405, 8, 20 },
            { 1405, 9, 20 }, { 1405, 10, 20 }, { 1405, 11, 19 }, { 1405, 12, 19 } };

        static DateTime LunarStart(int i)
        {
            return Cal.ToDateTime(LunarStarts[i, 0], LunarStarts[i, 1], LunarStarts[i, 2], 0, 0, 0, 0);
        }

        public static void Lunar(DateTime d, out int year, out int month, out int day)
        {
            d = d.Date;
            int last = LunarStarts.GetLength(0) - 1;
            // The last listed month is known to have at least 29 days.
            if (d >= LunarStart(0) && d < LunarStart(last).AddDays(29))
            {
                int i = last;
                while (LunarStart(i) > d) i--;
                int k = LunarFirstMonth - 1 + i;
                year = LunarFirstYear + k / 12;
                month = k % 12 + 1;
                day = (d - LunarStart(i)).Days + 1;
                return;
            }
            year = Hijri.GetYear(d);
            month = Hijri.GetMonth(d);
            day = Hijri.GetDayOfMonth(d);
        }

        public static string GregorianDate(DateTime d)
        {
            return d.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture);
        }

        // Fridays and official holidays.
        public static bool IsOff(DateTime d) { return Col(d) == 6 || Occasions.IsHoliday(d); }
    }

    // ---------------------------------------------------------------- occasions

    // Occasions from Iran's official calendar, as "month/day text"; a leading "!" marks an
    // official holiday. Lunar day 0 means the last day of that month.
    static class Occasions
    {
        static readonly string[] Solar = {
            "1/1 !عید نوروز", "1/2 !عید نوروز", "1/3 !عید نوروز", "1/4 !عید نوروز",
            "1/6 زادروز زرتشت", "1/12 !روز جمهوری اسلامی ایران", "1/13 !روز طبیعت",
            "1/20 روز ملی فناوری هسته‌ای", "1/25 بزرگداشت عطار نیشابوری", "1/29 روز ارتش",
            "2/1 بزرگداشت سعدی", "2/3 بزرگداشت شیخ بهایی، روز معماری", "2/10 روز ملی خلیج فارس",
            "2/12 روز معلم", "2/25 بزرگداشت فردوسی، روز پاسداشت زبان فارسی",
            "2/27 روز ارتباطات و روابط عمومی", "2/28 بزرگداشت خیام",
            "3/1 بزرگداشت ملاصدرا", "3/3 سالروز آزادسازی خرمشهر", "3/14 !رحلت امام خمینی",
            "3/15 !قیام ۱۵ خرداد", "3/29 درگذشت دکتر علی شریعتی", "3/31 شهادت دکتر مصطفی چمران",
            "4/7 روز قوه قضاییه", "4/14 روز قلم", "4/22 بزرگداشت خوارزمی",
            "5/8 بزرگداشت شیخ شهاب‌الدین سهروردی", "5/14 صدور فرمان مشروطیت", "5/17 روز خبرنگار",
            "6/1 بزرگداشت ابوعلی سینا، روز پزشک", "6/2 آغاز هفته دولت", "6/4 روز کارمند",
            "6/5 بزرگداشت زکریای رازی، روز داروسازی", "6/13 بزرگداشت ابوریحان بیرونی، روز تعاون",
            "6/17 قیام ۱۷ شهریور", "6/21 روز سینما", "6/27 روز شعر و ادب فارسی، بزرگداشت شهریار",
            "6/31 آغاز هفته دفاع مقدس",
            "7/7 روز آتش‌نشانی و ایمنی، بزرگداشت شمس", "7/8 بزرگداشت مولوی", "7/13 روز نیروی انتظامی",
            "7/15 روز روستا و عشایر", "7/20 بزرگداشت حافظ", "7/26 روز تربیت بدنی و ورزش",
            "8/1 بزرگداشت ابوالفضل بیهقی", "8/8 روز نوجوان", "8/13 روز دانش‌آموز",
            "8/24 روز کتاب، کتابخوانی و کتابدار",
            "9/5 روز بسیج مستضعفان", "9/7 روز نیروی دریایی", "9/10 روز مجلس", "9/12 روز قانون اساسی",
            "9/16 روز دانشجو", "9/25 روز پژوهش", "9/26 روز حمل‌ونقل و رانندگان", "9/30 شب یلدا",
            "10/4 بزرگداشت رودکی", "10/13 شهادت سردار قاسم سلیمانی", "10/19 قیام ۱۹ دی مردم قم",
            "10/20 شهادت امیرکبیر",
            "11/1 بزرگداشت خاقانی", "11/12 بازگشت امام خمینی به ایران، آغاز دهه فجر",
            "11/19 روز نیروی هوایی", "11/22 !پیروزی انقلاب اسلامی ایران", "11/29 قیام مردم تبریز",
            "12/5 بزرگداشت خواجه نصیرالدین طوسی، روز مهندسی", "12/15 روز درختکاری",
            "12/21 بزرگداشت نظامی گنجوی", "12/22 روز بزرگداشت شهدا", "12/25 بزرگداشت پروین اعتصامی",
            "12/29 !روز ملی شدن صنعت نفت ایران" };

        static readonly string[] Lunar = {
            "1/1 آغاز سال هجری قمری", "1/9 !تاسوعای حسینی", "1/10 !عاشورای حسینی",
            "1/12 شهادت امام زین‌العابدین (ع)",
            "2/20 !اربعین حسینی", "2/28 !رحلت حضرت رسول اکرم (ص)، شهادت امام حسن مجتبی (ع)",
            "2/0 !شهادت امام رضا (ع)",
            "3/1 هجرت حضرت رسول اکرم (ص) به مدینه", "3/8 !شهادت امام حسن عسکری (ع)",
            "3/12 ولادت حضرت رسول اکرم (ص) به روایت اهل سنت، آغاز هفته وحدت",
            "3/17 !ولادت حضرت رسول اکرم (ص) و امام جعفر صادق (ع)",
            "4/8 ولادت امام حسن عسکری (ع)", "4/10 وفات حضرت معصومه (س)",
            "5/5 ولادت حضرت زینب (س)، روز پرستار",
            "6/3 !شهادت حضرت فاطمه زهرا (س)", "6/13 وفات حضرت ام‌البنین (س)",
            "6/20 ولادت حضرت فاطمه زهرا (س)، روز زن",
            "7/1 ولادت امام محمد باقر (ع)", "7/3 شهادت امام علی‌النقی (ع)", "7/10 ولادت امام محمد تقی (ع)",
            "7/13 !ولادت امام علی (ع)، روز پدر", "7/15 وفات حضرت زینب (س)",
            "7/25 شهادت امام موسی کاظم (ع)", "7/27 !مبعث حضرت رسول اکرم (ص)",
            "8/3 ولادت امام حسین (ع)، روز پاسدار", "8/4 ولادت حضرت ابوالفضل (ع)، روز جانباز",
            "8/5 ولادت امام زین‌العابدین (ع)", "8/15 !ولادت حضرت قائم (عج)",
            "9/10 وفات حضرت خدیجه (س)", "9/15 ولادت امام حسن مجتبی (ع)، روز اکرام", "9/18 شب قدر",
            "9/19 ضربت خوردن حضرت علی (ع)", "9/20 شب قدر", "9/21 !شهادت حضرت علی (ع)", "9/22 شب قدر",
            "10/1 !عید سعید فطر", "10/2 !تعطیل به مناسبت عید سعید فطر", "10/25 !شهادت امام جعفر صادق (ع)",
            "11/1 ولادت حضرت معصومه (س)، روز دختران", "11/11 ولادت امام رضا (ع)",
            "11/29 شهادت امام محمد تقی (ع)",
            "12/1 سالروز ازدواج حضرت علی (ع) و حضرت فاطمه (س)", "12/7 شهادت امام محمد باقر (ع)",
            "12/9 روز عرفه", "12/10 !عید سعید قربان", "12/15 ولادت امام علی‌النقی (ع)",
            "12/18 !عید سعید غدیر خم", "12/20 ولادت امام موسی کاظم (ع)", "12/24 روز مباهله" };

        static readonly string[] Gregorian = {
            "1/1 آغاز سال میلادی", "5/1 روز جهانی کار و کارگر", "12/25 ولادت حضرت عیسی مسیح (ع)" };

        static readonly Dictionary<int, string> solar = Index(Solar), lunar = Index(Lunar), gregorian = Index(Gregorian);

        static Dictionary<int, string> Index(string[] table)
        {
            var map = new Dictionary<int, string>();
            foreach (string entry in table)
            {
                int slash = entry.IndexOf('/'), space = entry.IndexOf(' ');
                int key = int.Parse(entry.Substring(0, slash)) * 100 + int.Parse(entry.Substring(slash + 1, space - slash - 1));
                map[key] = entry.Substring(space + 1);
            }
            return map;
        }

        // All of a day's occasions, holidays first; each is "!text" for a holiday or plain text.
        public static List<string> For(DateTime d)
        {
            var list = new List<string>();
            string s;
            int ly, lm, ld, ny, nm, nd;
            Persian.Lunar(d, out ly, out lm, out ld);
            Persian.Lunar(d.AddDays(1), out ny, out nm, out nd);
            if (lunar.TryGetValue(lm * 100 + ld, out s)) list.Add(s);
            if (nd == 1 && lunar.TryGetValue(lm * 100, out s)) list.Add(s);
            if (solar.TryGetValue(Persian.Month(d) * 100 + Persian.Day(d), out s)) list.Add(s);
            if (gregorian.TryGetValue(d.Month * 100 + d.Day, out s)) list.Add(s);

            int py, pm, pd;
            Persian.Lunar(d.AddDays(7), out py, out pm, out pd);
            if (lm == 9 && pm != 9 && d.DayOfWeek == DayOfWeek.Friday) list.Add("روز جهانی قدس");
            if (d.DayOfWeek == DayOfWeek.Wednesday && Persian.Year(d.AddDays(7)) != Persian.Year(d)) list.Add("آخرین چهارشنبه سال");

            var sorted = list.FindAll(x => x.StartsWith("!"));
            sorted.AddRange(list.FindAll(x => !x.StartsWith("!")));
            return sorted;
        }

        public static bool IsHoliday(DateTime d)
        {
            foreach (string s in For(d)) if (s.StartsWith("!")) return true;
            return false;
        }

        // The day's occasions as one line, or null; holiday tells whether any is an official holiday.
        public static string Text(DateTime d, out bool holiday)
        {
            var list = For(d);
            holiday = list.Count > 0 && list[0].StartsWith("!");
            if (list.Count == 0) return null;
            for (int i = 0; i < list.Count; i++) list[i] = list[i].TrimStart('!');
            return string.Join("، ", list.ToArray());
        }
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
        public Color Bg, Text, Muted, Line, Hover, Holiday, HolidayBg, TodayBg, TodayText;

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
                    Hover = Color.FromArgb(48, 48, 48), Holiday = Color.FromArgb(255, 138, 128), HolidayBg = Color.FromArgb(74, 36, 34),
                    TodayBg = Color.FromArgb(76, 194, 255), TodayText = Color.FromArgb(10, 10, 10) };
            return new Theme {
                Bg = Color.FromArgb(249, 249, 249), Text = Color.FromArgb(26, 26, 26),
                Muted = Color.FromArgb(110, 110, 110), Line = Color.FromArgb(224, 224, 224),
                Hover = Color.FromArgb(234, 234, 234), Holiday = Color.FromArgb(196, 43, 28), HolidayBg = Color.FromArgb(253, 231, 228),
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
        public static Bitmap DayIcon(int size, int day) { return DayIcon(size, day, false); }

        // The tray icon: day of the month on an orange plate, or a red one on Fridays and holidays.
        public static Bitmap DayIcon(int size, int day, bool off)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                var plate = new RectangleF(0, 0, size - 1, size - 1);
                Color top = off ? Color.FromArgb(232, 72, 58) : Color.FromArgb(255, 150, 50);
                Color bottom = off ? Color.FromArgb(170, 30, 28) : Color.FromArgb(222, 56, 46);
                using (var brush = new LinearGradientBrush(RectangleF.Inflate(plate, 0, 1), top, bottom, 90f))
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
                // Draw with the same tight format it was measured with; the padded default format
                // trimmed two-digit days down to their first digit.
                using (f)
                using (var sf = (StringFormat)StringFormat.GenericTypographic.Clone())
                using (var white = new SolidBrush(Color.White))
                {
                    // The font's line is taller than the icon; LineLimit would skip it entirely.
                    sf.FormatFlags = (sf.FormatFlags & ~StringFormatFlags.LineLimit) | StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(txt, f, white, new RectangleF(0, size * 0.04f, size, size), sf);
                }
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

            using (var bmp = Ui.DayIcon(SystemInformation.SmallIconSize.Width, Persian.Day(today), Persian.IsOff(today)))
            {
                IntPtr old = iconHandle;
                iconHandle = bmp.GetHicon();
                tray.Icon = Icon.FromHandle(iconHandle);
                if (old != IntPtr.Zero) Native.DestroyIcon(old);
            }
            bool holiday;
            string occasion = Occasions.Text(today, out holiday);
            string tip = Persian.LongDate(today) + "\n" + (occasion ?? today.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));
            tray.Text = tip.Length > 63 ? tip.Substring(0, 62) + "…" : tip;

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
                var occasions = Occasions.For(date);
                bool red = c == 6 || (occasions.Count > 0 && occasions[0].StartsWith("!"));
                var box = RectangleF.Inflate(cell, -3, -2);

                if (isToday) Ui.FillRound(g, th.TodayBg, box, 8);
                else if (hoverDay == d - 1) Ui.FillRound(g, th.Hover, box, 8);
                else if (red) Ui.FillRound(g, th.HolidayBg, box, 8);

                Color main = isToday ? th.TodayText : red ? th.Holiday : th.Text;
                Color small = isToday ? th.TodayText : red ? th.Holiday : th.Muted;
                // A dot marks days with occasions to read in the footer.
                if (occasions.Count > 0)
                    using (var b = new SolidBrush(small)) g.FillEllipse(b, box.Right - 7.5f, box.Y + 4, 4, 4);
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
                bool holiday;
                string occasion = Occasions.Text(fd, out holiday);
                var textRect = new RectangleF(100, footTop + 6, W - Pad - 100, 20);
                Ui.Text(g, Persian.LongDate(fd), fFoot, th.Text, textRect, Align.Right, true);
                textRect.Y += 20;
                if (occasion != null) Ui.Text(g, occasion, fFoot, holiday ? th.Holiday : th.Muted, textRect, Align.Right, true);
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
            bool holiday;
            string occasion = Occasions.Text(today, out holiday);
            bool red = Persian.Col(today) == 6 || holiday;

            Ui.FillRound(g, red ? th.Holiday : th.TodayBg, new RectangleF(W - 12, 20, 4, H - 40), 2);
            float right = W - 22;
            Ui.Text(g, Persian.WeekDays[Persian.Col(today)], fWeek, red ? th.Holiday : th.Muted,
                new RectangleF(14, 12, right - 14, 24), Align.Right, true);
            Ui.Text(g, Persian.ShortDate(today), fBig, th.Text, new RectangleF(14, 36, right - 14, 42), Align.Right, true);
            Ui.Text(g, today.ToString("ddd, d MMM yyyy", CultureInfo.InvariantCulture), fSmall, th.Muted,
                new RectangleF(14, 84, 112, 20), Align.Left, false);
            Ui.Text(g, occasion ?? Persian.HijriDate(today), fSmall, holiday ? th.Holiday : th.Muted,
                new RectangleF(122, 84, right - 122, 20), Align.Right, true);
        }
    }
}
