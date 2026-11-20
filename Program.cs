// Basic Capture — capture kartı için düşük gecikmeli görüntüleyici.
// Windows'un DirectShow sistemini doğrudan kullanır, hiçbir ek paket gerektirmez.
//
// Gecikmeyi düşürmek için:
//  * Kartın kendi çıkışı (capture pin) doğrudan ekrana bağlanır, arada kopyalama yok.
//  * Grafiğin saati kapatılır: kareler zamanlamaya göre bekletilmez, geldiği an çizilir.
//  * Ses tamponu küçük tutulur.
//
// Tuşlar: F / çift tık = tam ekran, M = sessiz, Q = çıkış, sağ tık = ayarlar menüsü

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Basic Capture")]
[assembly: System.Reflection.AssemblyProduct("Basic Capture")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace CaptureIzle
{
    // ---------------------------------------------------------------- DirectShow tanımları
    static class G
    {
        public static Guid SystemDeviceEnum = new Guid("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
        public static Guid VideoInputCat = new Guid("860BB310-5D01-11d0-BD3B-00A0C911CE86");
        public static Guid AudioInputCat = new Guid("33D9A762-90C8-11d0-BD43-00A0C911CE86");
        public static Guid FilterGraph = new Guid("E436EBB3-524F-11CE-9F53-0020AF0BA770");
        public static Guid CaptureGraphBuilder2 = new Guid("BF87B6E1-8C27-11D0-B3F0-00AA003761C5");
        public static Guid SampleGrabber = new Guid("C1F400A0-3F08-11d3-9F0B-006008039E37");
        public static Guid NullRenderer = new Guid("C1F400A4-3F08-11d3-9F0B-006008039E37");
        public static Guid SubRGB32 = new Guid("e436eb7e-524f-11ce-9f53-0020af0ba770");
        public static Guid PinCapture = new Guid("fb6c4281-0353-11d1-905f-0000c0cc16ba");
        public static Guid MediaVideo = new Guid("73646976-0000-0010-8000-00AA00389B71");
        public static Guid MediaAudio = new Guid("73647561-0000-0010-8000-00AA00389B71");
        public static Guid FormatVideoInfo = new Guid("05589f80-c356-11ce-bf01-00aa0055595a");
        public static Guid FormatVideoInfo2 = new Guid("F72A76A0-EB0A-11d0-ACE4-0000C0CC16BA");
        public static Guid FormatWave = new Guid("05589f81-c356-11ce-bf01-00aa0055595a");
        public static Guid SubMJPG = new Guid("47504A4D-0000-0010-8000-00AA00389B71");
        public static Guid IID_IBaseFilter = new Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770");
        public static Guid IID_IAMStreamConfig = new Guid("C6E13340-30AC-11d0-A18C-00A0C9118956");
        public static Guid IID_IAMBufferNegotiation = new Guid("56ED71A0-AF5F-11D0-B3F0-00AA003761C5");
        public static Guid IID_IPropertyBag = new Guid("55272A00-42CB-11CE-8135-00AA004BB851");
    }

    [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ICreateDevEnum
    {
        [PreserveSig] int CreateClassEnumerator([In] ref Guid clsid, out IEnumMoniker e, int flags);
    }

    [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyBag
    {
        [PreserveSig] int Read([MarshalAs(UnmanagedType.LPWStr)] string name, out object val, IntPtr log);
        [PreserveSig] int Write([MarshalAs(UnmanagedType.LPWStr)] string name, ref object val);
    }

    [ComImport, Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IBaseFilter { }

    [ComImport, Guid("56A868A9-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IGraphBuilder
    {
        [PreserveSig] int AddFilter(IBaseFilter f, [MarshalAs(UnmanagedType.LPWStr)] string name);
    }

    [ComImport, Guid("56A86899-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMediaFilter
    {
        [PreserveSig] int GetClassID(out Guid g);
        [PreserveSig] int Stop();
        [PreserveSig] int Pause();
        [PreserveSig] int Run(long t);
        [PreserveSig] int GetState(int ms, out int st);
        [PreserveSig] int SetSyncSource(IntPtr clock);
    }

    [ComImport, Guid("93E5A4E0-2D50-11D2-ABFA-00A0C9C6E38D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ICaptureGraphBuilder2
    {
        [PreserveSig] int SetFiltergraph(IGraphBuilder g);
        [PreserveSig] int GetFiltergraph(out IGraphBuilder g);
        [PreserveSig] int SetOutputFileName(IntPtr a, IntPtr b, IntPtr c, IntPtr d);
        [PreserveSig] int FindInterface([In] ref Guid cat, [In] ref Guid type, IBaseFilter f, [In] ref Guid riid,
                                        [MarshalAs(UnmanagedType.IUnknown)] out object o);
        [PreserveSig] int RenderStream([In] ref Guid cat, [In] ref Guid type, [MarshalAs(UnmanagedType.IUnknown)] object src,
                                       IBaseFilter comp, IBaseFilter rend);
    }

    [ComImport, Guid("56A868B1-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    interface IMediaControl
    {
        [PreserveSig] int Run();
        [PreserveSig] int Pause();
        [PreserveSig] int Stop();
    }

    [ComImport, Guid("56A868B3-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    interface IBasicAudio
    {
        [PreserveSig] int put_Volume(int v);
        [PreserveSig] int get_Volume(out int v);
    }

    [ComImport, Guid("C6E13340-30AC-11d0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAMStreamConfig
    {
        [PreserveSig] int SetFormat(IntPtr pmt);
        [PreserveSig] int GetFormat(out IntPtr pmt);
        [PreserveSig] int GetNumberOfCapabilities(out int count, out int size);
        [PreserveSig] int GetStreamCaps(int i, out IntPtr pmt, IntPtr scc);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AllocProps { public int cBuffers, cbBuffer, cbAlign, cbPrefix; }

    [ComImport, Guid("56ED71A0-AF5F-11D0-B3F0-00AA003761C5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAMBufferNegotiation
    {
        [PreserveSig] int SuggestAllocatorProperties(ref AllocProps p);
        [PreserveSig] int GetAllocatorProperties(out AllocProps p);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AMMediaType
    {
        public Guid majortype, subtype;
        public int fixedSize, temporal, sampleSize;
        public Guid formattype;
        public IntPtr pUnk;
        public int cbFormat;
        public IntPtr pbFormat;
    }

    // ---------------------------------------------------------------- Dil (Türkçe / English)
    static class Dil
    {
        // Varsayılan: Windows Türkçe ise Türkçe, değilse İngilizce
        public static bool En = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "tr";
        public static string T(string tr, string en) { return En ? en : tr; }
    }

    // Ekran ölçeği (%100 = 1, %150 = 1.5): 4K ekranlarda her şey orantılı büyüsün
    static class Olcek
    {
        public static float K = 1f;
        public static int S(float v) { return (int)Math.Round(v * K); }
    }

    // ---------------------------------------------------------------- Yardımcılar
    class Format
    {
        public int Index, W, H, Fps;
        public string Kod;
        public long MinInterval;
        public string Ad { get { return W + "x" + H + " " + Kod + " " + Fps + " fps"; } }
    }

    static class DS
    {
        [DllImport("ole32.dll")] static extern int CreateBindCtx(int r, out IBindCtx ctx);

        public static void Check(int hr, string ne)
        {
            if (hr < 0) throw new Exception(ne + Dil.T(" başarısız", " failed") + " (0x" + hr.ToString("X8") + ")");
        }

        public static Dictionary<string, string> Yollar = new Dictionary<string, string>();

        // MS2109 çipli kartlar 1280x720'den büyük çözünürlükte "60 FPS" bildirir ama
        // gerçekte 30 kare gönderir (kareler ikişerli, düzensiz gelir ve görüntü takılır).
        // Ölçümle doğrulandı: 1080p/1600x1200/1360x768 vb. düzensiz, 1280x720 ve altı düzgün 60.
        // MJPG'de 24 FPS de desteklenmiyor: 1080p'de 25, 720p'de 20 veriyor.
        public static bool SahteAltmis(string cihaz)
        {
            string yol;
            return Yollar.TryGetValue(cihaz ?? "", out yol) && yol.Contains("vid_534d&pid_2109");
        }

        public static List<KeyValuePair<string, IMoniker>> Cihazlar(Guid kategori)
        {
            var list = new List<KeyValuePair<string, IMoniker>>();
            var de = (ICreateDevEnum)Activator.CreateInstance(Type.GetTypeFromCLSID(G.SystemDeviceEnum));
            IEnumMoniker em;
            if (de.CreateClassEnumerator(ref kategori, out em, 0) != 0 || em == null) return list;
            var arr = new IMoniker[1];
            while (em.Next(1, arr, IntPtr.Zero) == 0)
            {
                string ad = "?";
                try
                {
                    object bag;
                    Guid iid = G.IID_IPropertyBag;
                    arr[0].BindToStorage(null, null, ref iid, out bag);
                    object v;
                    if (((IPropertyBag)bag).Read("FriendlyName", out v, IntPtr.Zero) == 0) ad = (string)v;
                    object yol;
                    if (((IPropertyBag)bag).Read("DevicePath", out yol, IntPtr.Zero) == 0 && yol is string)
                        Yollar[ad] = ((string)yol).ToLowerInvariant();
                    Marshal.ReleaseComObject(bag);
                }
                catch { }
                list.Add(new KeyValuePair<string, IMoniker>(ad, arr[0]));
            }
            return list;
        }

        public static IBaseFilter Filtre(IMoniker m)
        {
            IBindCtx ctx;
            CreateBindCtx(0, out ctx);
            object o;
            Guid iid = G.IID_IBaseFilter;
            m.BindToObject(ctx, null, ref iid, out o);
            return (IBaseFilter)o;
        }

        public static void Sil(IntPtr p)
        {
            if (p == IntPtr.Zero) return;
            var mt = (AMMediaType)Marshal.PtrToStructure(p, typeof(AMMediaType));
            if (mt.cbFormat != 0 && mt.pbFormat != IntPtr.Zero) Marshal.FreeCoTaskMem(mt.pbFormat);
            if (mt.pUnk != IntPtr.Zero) Marshal.Release(mt.pUnk);
            Marshal.FreeCoTaskMem(p);
        }

        // VIDEOINFOHEADER / VIDEOINFOHEADER2 içinde bmiHeader'ın konumu
        public static int BmiOfs(Guid formattype)
        {
            if (formattype == G.FormatVideoInfo) return 48;
            if (formattype == G.FormatVideoInfo2) return 72;
            return -1;
        }

        static string FourCC(Guid sub)
        {
            if (sub == G.SubMJPG) return "MJPG";
            byte[] b = sub.ToByteArray();
            var sb = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                char c = (char)b[i];
                if (!char.IsLetterOrDigit(c)) return "RGB";
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static List<Format> Formatlar(IAMStreamConfig sc)
        {
            var list = new List<Format>();
            int n, size;
            if (sc.GetNumberOfCapabilities(out n, out size) < 0) return list;
            IntPtr scc = Marshal.AllocCoTaskMem(Math.Max(size, 256));
            try
            {
                for (int i = 0; i < n; i++)
                {
                    IntPtr p;
                    if (sc.GetStreamCaps(i, out p, scc) < 0) continue;
                    try
                    {
                        var mt = (AMMediaType)Marshal.PtrToStructure(p, typeof(AMMediaType));
                        int ofs = BmiOfs(mt.formattype);
                        if (ofs < 0 || mt.pbFormat == IntPtr.Zero) continue;
                        long avg = Marshal.ReadInt64(mt.pbFormat, 40);
                        long min = size >= 112 ? Marshal.ReadInt64(scc, 104) : 0;
                        long max = size >= 120 ? Marshal.ReadInt64(scc, 112) : 0;
                        long iv = min > 0 ? min : avg;
                        int w = Marshal.ReadInt32(mt.pbFormat, ofs + 4);
                        int h = Math.Abs(Marshal.ReadInt32(mt.pbFormat, ofs + 8));
                        string kod = FourCC(mt.subtype);
                        int enYuksek = iv > 0 ? (int)Math.Round(10000000.0 / iv) : 30;
                        // En yüksek FPS'in yanında, kartın/kameranın desteklediği aralıktaysa
                        // 30 FPS ve 24 FPS (sinema / kamera) seçeneklerini de sun
                        var hizlar = new List<int> { enYuksek };
                        foreach (int ek in new[] { 30, 24 })
                            if (enYuksek > ek && (max == 0 || max >= (10000000L + ek - 1) / ek)) hizlar.Add(ek);
                        foreach (int fps in hizlar)
                        {
                            var f = new Format();
                            f.Index = i; f.W = w; f.H = h; f.Kod = kod; f.Fps = fps;
                            f.MinInterval = fps == enYuksek ? iv : (10000000L + fps - 1) / fps;
                            if (!list.Exists(x => x.Ad == f.Ad)) list.Add(f);
                        }
                    }
                    finally { Sil(p); }
                }
            }
            finally { Marshal.FreeCoTaskMem(scc); }
            list.Sort((a, b) =>
            {
                int c = (b.W * b.H).CompareTo(a.W * a.H);              // büyük çözünürlük önce
                if (c == 0) c = (b.Kod == "MJPG").CompareTo(a.Kod == "MJPG"); // sonra MJPG
                if (c == 0) c = b.Fps.CompareTo(a.Fps);                  // sonra yüksek FPS
                return c;
            });
            return list;
        }

        public static void FormatAyarla(IAMStreamConfig sc, Format f)
        {
            IntPtr p;
            int size, n;
            sc.GetNumberOfCapabilities(out n, out size);
            IntPtr scc = Marshal.AllocCoTaskMem(Math.Max(size, 256));
            try
            {
                Check(sc.GetStreamCaps(f.Index, out p, scc), Dil.T("Format okuma", "Reading format"));
                try
                {
                    var mt = (AMMediaType)Marshal.PtrToStructure(p, typeof(AMMediaType));
                    if (f.MinInterval > 0) Marshal.WriteInt64(mt.pbFormat, 40, f.MinInterval);
                    Check(sc.SetFormat(p), Dil.T("Format ayarlama", "Setting format"));
                }
                finally { Sil(p); }
            }
            finally { Marshal.FreeCoTaskMem(scc); }
        }
    }

    // Tıklanınca odağı çalmayan küçük buton (tuş kısayolları çalışmaya devam etsin)
    class OdakSizButon : Button
    {
        public OdakSizButon()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 66);
            FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 80, 88);
            BackColor = Color.FromArgb(32, 32, 36);
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
        }
    }

    // Ses seviyesi çubuğu: tıkla/sürükle ya da tekerlekle ayarla
    class SesCubugu : Control
    {
        int deger = 100;
        bool sessiz;
        public event EventHandler Degisti;
        public int Deger { get { return deger; } set { deger = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Sessiz { get { return sessiz; } set { sessiz = value; Invalidate(); } }

        public SesCubugu()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            TabStop = false;
            BackColor = Color.FromArgb(32, 32, 36);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            Size = new Size(Olcek.S(190), Olcek.S(34));
            Cursor = Cursors.Hand;
        }

        static int Sol { get { return Olcek.S(34); } }   // simge alanı
        static int Sag { get { return Olcek.S(48); } }   // yüzde yazısı alanı
        Rectangle Iz { get { int h = Olcek.S(6); return new Rectangle(Sol, Height / 2 - h / 2, Width - Sol - Sag, h); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            string simge = sessiz || deger == 0 ? "🔇" : deger < 40 ? "🔈" : deger < 75 ? "🔉" : "🔊";
            using (var f = new Font("Segoe UI Emoji", 10))
                TextRenderer.DrawText(g, simge, f, new Rectangle(Olcek.S(4), 0, Sol - Olcek.S(4), Height), ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            var iz = Iz;
            using (var b = new SolidBrush(Color.FromArgb(70, 70, 78))) g.FillRectangle(b, iz);
            int dolu = (int)(iz.Width * deger / 100.0);
            Color renk = sessiz ? Color.FromArgb(120, 120, 128) : Color.FromArgb(70, 150, 255);
            using (var b = new SolidBrush(renk)) g.FillRectangle(b, iz.X, iz.Y, dolu, iz.Height);
            int t = Olcek.S(12);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, iz.X + dolu - t / 2, Height / 2 - t / 2, t, t);
            TextRenderer.DrawText(g, sessiz ? "—" : deger + "%", Font, new Rectangle(Width - Sag, 0, Sag - Olcek.S(4), Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        void Ayarla(int x)
        {
            var iz = Iz;
            Deger = (int)Math.Round((x - iz.X) * 100.0 / iz.Width);
            sessiz = false;
            if (Degisti != null) Degisti(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (e.X < Sol) { sessiz = !sessiz; Invalidate(); if (Degisti != null) Degisti(this, EventArgs.Empty); return; }
            Capture = true; Ayarla(e.X);
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture && e.Button == MouseButtons.Left) Ayarla(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Deger += e.Delta > 0 ? 5 : -5;
            sessiz = false;
            if (Degisti != null) Degisti(this, EventArgs.Empty);
        }
    }

    // Şeffaf arka planlı FPS göstergesi. Video penceresinin üstüne çizebilmek için
    // ayrı, tıklamaları geçiren, odak almayan küçük bir katman penceresi.
    class FpsKatmani : Form
    {
        static readonly Color Anahtar = Color.FromArgb(1, 1, 1); // bu renk şeffaf görünür
        string metin = "-- FPS";
        Color renk = Color.FromArgb(80, 230, 120);
        readonly FontFamily aile = new FontFamily("Segoe UI");

        public FpsKatmani()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Anahtar;
            TransparencyKey = Anahtar;
            Size = new Size(Olcek.S(120), Olcek.S(34));
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000; // LAYERED | TRANSPARENT (tıklama geçer) | TOOLWINDOW | NOACTIVATE
                return cp;
            }
        }

        public string Metin { get { return metin; } }

        public void Ayarla(string m, Color r)
        {
            if (m == metin && r == renk) return;
            metin = m; renk = r;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Anahtar);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var yol = new System.Drawing.Drawing2D.GraphicsPath())
            {
                yol.AddString(metin, aile, (int)FontStyle.Bold, 19f * Olcek.K, Point.Empty, StringFormat.GenericTypographic);
                RectangleF b = yol.GetBounds();
                using (var m = new System.Drawing.Drawing2D.Matrix())
                {
                    m.Translate(Width - b.Right - 4 * Olcek.K, (Height - b.Height) / 2 - b.Top); // sağa yasla
                    yol.Transform(m);
                }
                // Siyah kontur: her renkte görüntünün üstünde okunur kalsın
                using (var kalem = new Pen(Color.Black, 4f * Olcek.K))
                {
                    kalem.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                    g.DrawPath(kalem, yol);
                }
                using (var f = new SolidBrush(renk)) g.FillPath(f, yol);
            }
        }
    }

    // ---------------------------------------------------------------- Pencere
    class AnaForm : Form
    {
        IGraphBuilder graf;
        ICaptureGraphBuilder2 yapici;
        IMediaControl kontrol;
        IBasicAudio ses;
        IBaseFilter vFiltre, aFiltre, gFiltre, nFiltre;

        // Görüntü her zaman Sunucu ile GPU'da çizilir; akıcı mod yalnızca zamanlamayı değiştirir
        Sunucu sunucu;
        GoruntuPaneli panel;
        bool akici;
        int keskin; // 0 = kapalı, 1 = hafif, 2 = orta, 3 = güçlü
        static float KeskinGuc(int seviye) { return seviye <= 0 ? 0f : seviye == 1 ? 0.3f : seviye == 2 ? 0.6f : 1f; }

        // Arayüz
        OdakSizButon ayarBtn;
        FpsKatmani fpsK;
        Timer zamanlayici;
        DateTime sonHareket = DateTime.Now;
        bool imlecGizli, menuAcik, arayuzAcik;
        bool doldur = true;
        int fpsMod = 1;      // 0 = kapalı, 1 = tıklayınca (varsayılan), 2 = her zaman
        int seviye = 100;    // ses seviyesi %
        SesCubugu sesBar;
        Rectangle eskiSinir;
        int sonKare = -1;
        DateTime sonOlcum;

        List<KeyValuePair<string, IMoniker>> vCihazlar, aCihazlar;
        List<Format> formatlar = new List<Format>();
        string vSecili, aSecili, fSecili;
        int vidW = 16, vidH = 9;
        int hedefFps = 60;   // seçilen formatın FPS'i: sayaç rengi buna göre
        bool sessiz, tamEkran;
        Label mesaj;
        string ayarDosyasi;

        public string Durum = "";

        public AnaForm()
        {
            Text = "Basic Capture";
            BackColor = Color.Black;
            // Başlangıç boyutu: ölçeğe göre 1280x720, ama ekranın %80'ini geçmesin
            Rectangle alan = Screen.PrimaryScreen.WorkingArea;
            int bw = Math.Min(Olcek.S(1280), alan.Width * 8 / 10);
            ClientSize = new Size(bw, bw * 9 / 16);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { Icon = SystemIcons.Application; }

            mesaj = new Label();
            mesaj.Dock = DockStyle.Fill;
            mesaj.ForeColor = Color.Gainsboro;
            mesaj.TextAlign = ContentAlignment.MiddleCenter;
            mesaj.Font = new Font("Segoe UI", 12);
            mesaj.Visible = false;
            mesaj.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) MenuGoster(); else AyarButonuGoster(); };
            Controls.Add(mesaj);

            // Sol üstte tıklayınca beliren ayar butonu
            ayarBtn = new OdakSizButon();
            ayarBtn.Font = new Font("Segoe UI", 10);
            ayarBtn.Location = new Point(Olcek.S(12), Olcek.S(12));
            ayarBtn.Size = new Size(Olcek.S(112), Olcek.S(34));
            ayarBtn.Visible = false;
            ayarBtn.Click += (s, e) => MenuGoster(new Point(ayarBtn.Left, ayarBtn.Bottom + 4));
            Controls.Add(ayarBtn);

            // Ayar butonunun yanında ses çubuğu
            sesBar = new SesCubugu();
            sesBar.Location = new Point(ayarBtn.Right + Olcek.S(8), Olcek.S(12));
            sesBar.Visible = false;
            sesBar.Degisti += (s, e) =>
            {
                seviye = sesBar.Deger; sessiz = sesBar.Sessiz;
                sonHareket = DateTime.Now;
                SesUygula(); AyarYaz();
            };
            Controls.Add(sesBar);

            // Sağ üstte FPS göstergesi
            fpsK = new FpsKatmani();

            // Akıcı modda görüntünün çizildiği alan (butonların arkasında)
            panel = new GoruntuPaneli();
            panel.Dock = DockStyle.Fill;
            panel.Visible = false;
            Controls.Add(panel);
            panel.SendToBack();

            zamanlayici = new Timer();
            zamanlayici.Interval = 250;
            zamanlayici.Tick += (s, e) => Tik();
            zamanlayici.Start();

            string klasor = Path.GetDirectoryName(Application.ExecutablePath);
            ayarDosyasi = Path.Combine(klasor, "BasicCapture.ayar");
            // Eski adla kaydedilmiş ayarları taşı
            try
            {
                string eski = Path.Combine(klasor, "CaptureIzle.ayar");
                if (!File.Exists(ayarDosyasi) && File.Exists(eski)) File.Move(eski, ayarDosyasi);
            }
            catch { }
            AyarOku();
            MetinleriYenile();

            vCihazlar = DS.Cihazlar(G.VideoInputCat);
            aCihazlar = DS.Cihazlar(G.AudioInputCat);

            if (vSecili == null || !vCihazlar.Exists(x => x.Key == vSecili))
            {
                var tahmin = vCihazlar.Find(x => x.Key.IndexOf("USB Video", StringComparison.OrdinalIgnoreCase) >= 0
                                              || x.Key.IndexOf("capture", StringComparison.OrdinalIgnoreCase) >= 0);
                vSecili = tahmin.Key ?? (vCihazlar.Count > 0 ? vCihazlar[0].Key : null);
            }
            if (aSecili == null)
            {
                var tahmin = aCihazlar.Find(x => x.Key.IndexOf("USB Digital Audio", StringComparison.OrdinalIgnoreCase) >= 0
                                              || x.Key.IndexOf("MS2109", StringComparison.OrdinalIgnoreCase) >= 0
                                              || x.Key.IndexOf("capture", StringComparison.OrdinalIgnoreCase) >= 0);
                aSecili = tahmin.Key ?? "";
            }
        }

        // Koyu, modern pencere çerçevesi (Windows 11; eski sürümlerde sessizce yok sayılır)
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int acik = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref acik, 4) != 0)   // DWMWA_USE_IMMERSIVE_DARK_MODE
                    DwmSetWindowAttribute(Handle, 19, ref acik, 4);        // eski Windows 10 sürümleri
                int baslik = 0x00221E1C;   // başlık rengi (BGR) = #1C1E22, menülerle aynı ton
                int yazi = 0x00EBE7E6;     // başlık yazısı = #E6E7EB
                int kenar = 0x00342F2C;    // ince kenarlık = #2C2F34
                int kose = 2;              // yuvarlak köşeler
                DwmSetWindowAttribute(Handle, 35, ref baslik, 4);  // DWMWA_CAPTION_COLOR
                DwmSetWindowAttribute(Handle, 36, ref yazi, 4);    // DWMWA_TEXT_COLOR
                DwmSetWindowAttribute(Handle, 34, ref kenar, 4);   // DWMWA_BORDER_COLOR
                DwmSetWindowAttribute(Handle, 33, ref kose, 4);    // DWMWA_WINDOW_CORNER_PREFERENCE
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Baslat();
        }

        // ------------------------------------------------ grafiği kur / yık
        public void Baslat()
        {
            Durdur();
            mesaj.Visible = false;
            if (vSecili == null)
            {
                Hata(Dil.T("Capture kartı bulunamadı.\nKartı takıp programı yeniden aç.",
                           "No capture card found.\nPlug in the card and restart the program."));
                return;
            }
            try
            {
                graf = (IGraphBuilder)Activator.CreateInstance(Type.GetTypeFromCLSID(G.FilterGraph));
                yapici = (ICaptureGraphBuilder2)Activator.CreateInstance(Type.GetTypeFromCLSID(G.CaptureGraphBuilder2));
                DS.Check(yapici.SetFiltergraph(graf), Dil.T("Grafik", "Graph"));

                vFiltre = DS.Filtre(vCihazlar.Find(x => x.Key == vSecili).Value);
                DS.Check(graf.AddFilter(vFiltre, "Video"), Dil.T("Video cihazı ekleme", "Adding video device"));

                // Formatı seç
                object o;
                Guid cat = G.PinCapture, typ = G.MediaVideo, iid = G.IID_IAMStreamConfig;
                if (yapici.FindInterface(ref cat, ref typ, vFiltre, ref iid, out o) >= 0 && o != null)
                {
                    var sc = (IAMStreamConfig)o;
                    formatlar = DS.Formatlar(sc);
                    // 20 FPS'in altındaki seçenekler (ör. sıkıştırılmamış YUY2 1080p 5 FPS) pratikte işe yaramaz
                    if (formatlar.Exists(x => x.Fps >= 20)) formatlar.RemoveAll(x => x.Fps < 20);
                    if (DS.SahteAltmis(vSecili))
                    {
                        formatlar.RemoveAll(x => x.Kod == "MJPG" && x.W * x.H > 1280 * 720 && x.Fps > 30);
                        // Bu kart 24'ü desteklemiyor: 1080p'de 25, 720p'de 20 veriyor (ölçüldü)
                        formatlar.RemoveAll(x => x.Fps == 24 && x.Kod == "MJPG");
                    }
                    // Kayıtlı format artık yoksa (ör. eski "1080p 60"), aynı çözünürlüğün en iyi seçeneğine geç
                    Format f = formatlar.Find(x => x.Ad == fSecili);
                    if (f == null && fSecili != null)
                    {
                        string cozunurluk = fSecili.Split(' ')[0];
                        f = formatlar.Find(x => x.W + "x" + x.H == cozunurluk && x.Kod == "MJPG");
                    }
                    if (f == null) f = VarsayilanFormat();
                    if (f != null)
                    {
                        DS.FormatAyarla(sc, f);
                        fSecili = f.Ad;
                        vidW = f.W; vidH = f.H; hedefFps = f.Fps;
                    }
                }

                sonKare = -1;
                GoruntuBagla();

                // Ses
                string sesNot = "";
                if (!string.IsNullOrEmpty(aSecili) && aCihazlar.Exists(x => x.Key == aSecili))
                {
                    try
                    {
                        aFiltre = DS.Filtre(aCihazlar.Find(x => x.Key == aSecili).Value);
                        DS.Check(graf.AddFilter(aFiltre, "Ses"), Dil.T("Ses cihazı ekleme", "Adding audio device"));
                        SesTamponuKucult();
                        cat = G.PinCapture; typ = G.MediaAudio;
                        DS.Check(yapici.RenderStream(ref cat, ref typ, aFiltre, null, null), Dil.T("Sesi bağlama", "Connecting audio"));
                    }
                    catch (Exception ex) { sesNot = Dil.T("  (ses yok: ", "  (no audio: ") + ex.Message + ")"; }
                }

                // Saati kapat: kareler beklemeden, geldiği an Sunucu'ya ulaşsın (zamanlamayı Sunucu yapar)
                ((IMediaFilter)graf).SetSyncSource(IntPtr.Zero);

                sunucu.Baslat();
                Yerlestir();

                ses = graf as IBasicAudio;
                SesUygula();

                kontrol = (IMediaControl)graf;
                DS.Check(kontrol.Run(), Dil.T("Başlatma", "Starting") + " [" + fSecili + "]");

                Durum = vSecili + " | " + (fSecili ?? "?") + " | ses: " + (string.IsNullOrEmpty(aSecili) ? "kapalı" : aSecili) + sesNot;
                AyarYaz();
            }
            catch (Exception ex)
            {
                Durdur();
                Hata(Dil.T("Görüntü açılamadı: ", "Could not open video: ") + ex.Message +
                     Dil.T("\n\nKartı başka bir program (OBS, Kamera, tarayıcı) kullanıyor olabilir." +
                           "\nAyarlardan başka bir format ya da cihaz deneyebilirsin.",
                           "\n\nAnother program (OBS, Camera, a browser) may be using the card." +
                           "\nTry a different format or device from Settings."));
            }
        }

        // Görüntü grafiği: kart -> (MJPG çözücü) -> kare yakalayıcı (RGB32) -> boş çizici.
        // Kareleri Sunucu alır ve OpenGL ile çizer (akıcı modda eşit aralıklarla, değilse geldiği an).
        void GoruntuBagla()
        {
            gFiltre = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(G.SampleGrabber));
            var sg = (ISampleGrabber)gFiltre;
            var mt = new AMMediaType();
            mt.majortype = G.MediaVideo;
            mt.subtype = G.SubRGB32;
            DS.Check(sg.SetMediaType(ref mt), "SampleGrabber");
            DS.Check(graf.AddFilter(gFiltre, "Yakalayici"), Dil.T("Kare yakalayıcı ekleme", "Adding frame grabber"));
            nFiltre = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(G.NullRenderer));
            DS.Check(graf.AddFilter(nFiltre, "Bos"), "Null renderer");

            Guid cat = G.PinCapture, typ = G.MediaVideo;
            DS.Check(yapici.RenderStream(ref cat, ref typ, vFiltre, gFiltre, nFiltre), Dil.T("Görüntüyü bağlama", "Connecting video"));

            // Bağlanan gerçek boyut ve satır yönü
            int gw = vidW, gh = vidH;
            bool asagidanYukari = true;
            IntPtr p = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(AMMediaType)));
            Marshal.StructureToPtr(new AMMediaType(), p, false);
            try
            {
                if (sg.GetConnectedMediaType(p) >= 0)
                {
                    var cm = (AMMediaType)Marshal.PtrToStructure(p, typeof(AMMediaType));
                    int ofs = DS.BmiOfs(cm.formattype);
                    if (ofs >= 0 && cm.pbFormat != IntPtr.Zero)
                    {
                        gw = Marshal.ReadInt32(cm.pbFormat, ofs + 4);
                        int hh = Marshal.ReadInt32(cm.pbFormat, ofs + 8);
                        asagidanYukari = hh > 0;
                        gh = Math.Abs(hh);
                    }
                }
            }
            finally { DS.Sil(p); }

            panel.Visible = true;
            panel.SendToBack();
            sunucu = new Sunucu(panel.Handle, gw, gh, hedefFps > 0 ? hedefFps : 30, asagidanYukari, akici);
            sunucu.Doldur = tamEkran && doldur;
            sunucu.Keskinlik = KeskinGuc(keskin);
            sg.SetBufferSamples(0);
            sg.SetOneShot(0);
            DS.Check(sg.SetCallback(sunucu, 1), "SampleGrabber callback");
        }

        Format VarsayilanFormat()
        {
            // En az gecikme için: önce 720p 60 MJPG, sonra 1080p MJPG, sonra ne varsa
            Format f = formatlar.Find(x => x.W == 1280 && x.H == 720 && x.Fps >= 50 && x.Kod == "MJPG");
            if (f == null) f = formatlar.Find(x => x.W == 1920 && x.H == 1080 && x.Kod == "MJPG");
            if (f == null) f = formatlar.Find(x => x.Kod == "MJPG");
            if (f == null && formatlar.Count > 0) f = formatlar[0];
            return f;
        }

        void SesTamponuKucult()
        {
            object o;
            Guid cat = G.PinCapture, typ = G.MediaAudio, iid = G.IID_IAMStreamConfig;
            int saniyeBayt = 192000, blok = 4;
            if (yapici.FindInterface(ref cat, ref typ, aFiltre, ref iid, out o) >= 0 && o != null)
            {
                IntPtr p;
                if (((IAMStreamConfig)o).GetFormat(out p) >= 0)
                {
                    var mt = (AMMediaType)Marshal.PtrToStructure(p, typeof(AMMediaType));
                    if (mt.formattype == G.FormatWave && mt.pbFormat != IntPtr.Zero)
                    {
                        saniyeBayt = Marshal.ReadInt32(mt.pbFormat, 8);
                        blok = Math.Max(1, (int)Marshal.ReadInt16(mt.pbFormat, 12));
                    }
                    DS.Sil(p);
                }
            }
            cat = G.PinCapture; typ = G.MediaAudio; iid = G.IID_IAMBufferNegotiation;
            if (yapici.FindInterface(ref cat, ref typ, aFiltre, ref iid, out o) >= 0 && o != null)
            {
                var ap = new AllocProps();
                ap.cBuffers = 6;
                ap.cbBuffer = Math.Max(blok, saniyeBayt / 50 / blok * blok); // ~20 ms
                ap.cbAlign = -1;
                ap.cbPrefix = -1;
                ((IAMBufferNegotiation)o).SuggestAllocatorProperties(ref ap);
            }
        }

        void Durdur()
        {
            try { if (kontrol != null) kontrol.Stop(); } catch { }
            if (sunucu != null)
            {
                try { ((ISampleGrabber)gFiltre).SetCallback(null, 1); } catch { }
                sunucu.Durdur();
                sunucu = null;
            }
            if (panel != null) panel.Visible = false;
            foreach (object o in new object[] { aFiltre, vFiltre, gFiltre, nFiltre, yapici, graf })
                if (o != null) try { Marshal.ReleaseComObject(o); } catch { }
            graf = null; yapici = null; kontrol = null; ses = null;
            vFiltre = null; aFiltre = null; gFiltre = null; nFiltre = null;
        }

        void Hata(string s)
        {
            Durum = "HATA: " + s.Replace("\n", " ");
            mesaj.Text = s + Dil.T("\n\n(tıkla: ayarlar)", "\n\n(click: settings)");
            mesaj.BringToFront();
            ayarBtn.BringToFront();
            mesaj.Visible = true;
        }

        // ------------------------------------------------ yerleşim, tuşlar, menü
        void Yerlestir()
        {
            FpsKonumla();
            if (sunucu != null) sunucu.Doldur = tamEkran && doldur; // yerleşimi (oran/doldur) çizici yapar
        }

        // FPS katmanını pencerenin sağ üst köşesine sabitle
        void FpsKonumla()
        {
            if (fpsK == null || !fpsK.Visible || !IsHandleCreated) return;
            Point p = PointToScreen(new Point(ClientSize.Width - fpsK.Width - Olcek.S(6), Olcek.S(4)));
            if (fpsK.Location != p) fpsK.Location = p;
        }

        protected override void OnMove(EventArgs e) { base.OnMove(e); FpsKonumla(); }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Yerlestir(); }

        protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); TamEkranDegistir(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right) MenuGoster();
            else if (e.Button == MouseButtons.Left) AyarButonuGoster();
        }

        Point sonFare;
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Location == sonFare) return; // bazı çiziciler sahte hareket mesajı yollar
            sonFare = e.Location;
            sonHareket = DateTime.Now;
            ImlecGoster(true);
        }

        void AyarButonuGoster()
        {
            sonHareket = DateTime.Now;
            ImlecGoster(true);
            arayuzAcik = true;
            sesBar.Deger = seviye; sesBar.Sessiz = sessiz;
            ayarBtn.Visible = true;
            sesBar.Visible = true;
            ayarBtn.BringToFront();
            sesBar.BringToFront();
            FpsGorunurluk();
        }

        void FpsGorunurluk()
        {
            bool goster = sunucu != null && (fpsMod == 2 || (fpsMod == 1 && arayuzAcik));
            goster = goster && WindowState != FormWindowState.Minimized && Visible;
            if (goster && !fpsK.Visible)
            {
                if (fpsK.Owner == null) fpsK.Owner = this; // ana pencerenin hep üstünde kalsın
                fpsK.Show();
                FpsKonumla();
            }
            else if (!goster && fpsK.Visible) fpsK.Hide();
        }

        void KeskinlikAyarla(int seviye)
        {
            keskin = Math.Max(0, Math.Min(3, seviye));
            AyarYaz();
            if (sunucu != null) sunucu.Keskinlik = KeskinGuc(keskin); // yeniden başlatmadan anında uygula
            AyarButonuGoster();
        }

        void AkiciDegistir()
        {
            akici = !akici;
            AyarYaz();
            Baslat();
            AyarButonuGoster();
        }

        void SesDegistir(int fark)
        {
            seviye = Math.Max(0, Math.Min(100, seviye + fark));
            sessiz = false;
            SesUygula();
            AyarYaz();
            AyarButonuGoster();
        }

        bool FareArayuzde()
        {
            foreach (Control c in new Control[] { ayarBtn, sesBar })
                if (c.Visible && c.ClientRectangle.Contains(c.PointToClient(Cursor.Position))) return true;
            return false;
        }

        void ImlecGoster(bool goster)
        {
            if (goster && imlecGizli) { Cursor.Show(); imlecGizli = false; }
            else if (!goster && !imlecGizli) { Cursor.Hide(); imlecGizli = true; }
        }

        // 4 kez/sn: FPS ölç, boşta kalınca butonu ve imleci gizle
        void Tik()
        {
            if (FareArayuzde()) sonHareket = DateTime.Now;
            bool bosta = !menuAcik && (DateTime.Now - sonHareket).TotalSeconds > 3;
            if (bosta && arayuzAcik)
            {
                arayuzAcik = false;
                ayarBtn.Visible = false;
                sesBar.Visible = false;
            }
            if (tamEkran && bosta) ImlecGoster(false);

            // OpenGL başlatılamadıysa (eski/bozuk sürücü) siyah ekran yerine sebebini göster
            if (sunucu != null && sunucu.HataMetni != null) { string h = sunucu.HataMetni; Durdur(); Hata(Dil.T("Görüntü çizilemedi: ", "Could not draw video: ") + h); }

            FpsGorunurluk();
            if (sunucu == null || fpsMod == 0) return;
            int kare = sunucu.Cizilen;
            DateTime simdi = DateTime.Now;
            if (sonKare < 0 || kare < sonKare) { sonKare = kare; sonOlcum = simdi; return; }
            double sn = (simdi - sonOlcum).TotalSeconds;
            if (sn < 0.5) return;
            double fps = (kare - sonKare) / sn;
            sonKare = kare; sonOlcum = simdi;
            fpsK.Ayarla(Math.Round(fps).ToString("0") + " FPS",
                        // Yeşil: seçilen hıza ulaşıyor, sarı: biraz düşük, kırmızı: çok düşük
                        fps >= hedefFps * 0.9 ? Color.FromArgb(80, 230, 120)
                      : fps >= hedefFps * 0.6 ? Color.FromArgb(240, 200, 70)
                      : Color.FromArgb(240, 90, 80));
            FpsKonumla();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.F || (e.KeyCode == Keys.Enter && e.Alt)) TamEkranDegistir();
            else if (e.KeyCode == Keys.Escape && tamEkran) TamEkranDegistir();
            else if (e.KeyCode == Keys.M) { sessiz = !sessiz; SesUygula(); AyarButonuGoster(); }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add) SesDegistir(5);
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract) SesDegistir(-5);
            else if (e.KeyCode == Keys.I) { fpsMod = (fpsMod + 1) % 3; AyarYaz(); FpsGorunurluk(); }
            else if (e.KeyCode == Keys.A) AkiciDegistir();
            else if (e.KeyCode == Keys.K) KeskinlikAyarla((keskin + 1) % 4);
            else if (e.KeyCode == Keys.D) { doldur = !doldur; Yerlestir(); AyarYaz(); }
            else if (e.KeyCode == Keys.S) MenuGoster(new Point(ayarBtn.Left, ayarBtn.Bottom + 4));
            else if (e.KeyCode == Keys.Q) Close();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            SesDegistir(e.Delta > 0 ? 5 : -5);
        }

        void SesUygula()
        {
            // Kulağa doğal gelsin diye logaritmik: %50 ≈ -12 dB, %10 ≈ -40 dB
            int db100 = sessiz || seviye <= 0 ? -10000 : (int)Math.Max(-10000, 4000 * Math.Log10(seviye / 100.0));
            if (ses != null) try { ses.put_Volume(db100); } catch { }
            if (sesBar != null) { sesBar.Deger = seviye; sesBar.Sessiz = sessiz; }
        }

        void TamEkranDegistir()
        {
            tamEkran = !tamEkran;
            if (tamEkran)
            {
                // Görev çubuğu dahil bütün monitörü kapla
                eskiSinir = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                Rectangle ekran = Screen.FromControl(this).Bounds;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = ekran;
                Activate();
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                if (!eskiSinir.IsEmpty) Bounds = eskiSinir;
                ImlecGoster(true);
            }
            sonHareket = DateTime.Now;
            Yerlestir();
        }

        void MenuGoster() { MenuGoster(PointToClient(Cursor.Position)); }

        void MenuGoster(Point yer)
        {
            ImlecGoster(true);
            ayarBtn.Visible = true;
            menuAcik = true;
            var m = new ContextMenuStrip();
            m.Renderer = new ToolStripProfessionalRenderer(new KoyuRenk());
            m.ForeColor = Color.White;
            m.Font = new Font("Segoe UI", 10);

            var vm = new ToolStripMenuItem(Dil.T("Görüntü cihazı", "Video device"));
            foreach (var c in vCihazlar)
            {
                string ad = c.Key;
                var it = new ToolStripMenuItem(ad, null, (s, e) => { vSecili = ad; fSecili = null; Baslat(); });
                it.Checked = ad == vSecili;
                vm.DropDownItems.Add(it);
            }
            m.Items.Add(vm);

            var am = new ToolStripMenuItem(Dil.T("Ses cihazı", "Audio device"));
            var yok = new ToolStripMenuItem(Dil.T("Ses yok (Windows'tan dinle)", "No audio (listen via Windows)"), null, (s, e) => { aSecili = ""; Baslat(); });
            yok.Checked = string.IsNullOrEmpty(aSecili);
            am.DropDownItems.Add(yok);
            foreach (var c in aCihazlar)
            {
                string ad = c.Key;
                var it = new ToolStripMenuItem(ad, null, (s, e) => { aSecili = ad; Baslat(); });
                it.Checked = ad == aSecili;
                am.DropDownItems.Add(it);
            }
            m.Items.Add(am);

            var fm = new ToolStripMenuItem(Dil.T("Çözünürlük / FPS", "Resolution / FPS"));
            foreach (var f in formatlar)
            {
                string ad = f.Ad;
                var it = new ToolStripMenuItem(ad, null, (s, e) => { fSecili = ad; Baslat(); });
                it.Checked = ad == fSecili;
                fm.DropDownItems.Add(it);
            }
            fm.Enabled = formatlar.Count > 0;
            m.Items.Add(fm);

            m.Items.Add(new ToolStripSeparator());
            var tam = new ToolStripMenuItem(Dil.T("Tam ekran", "Full screen"), null, (s, e) => TamEkranDegistir());
            tam.ShortcutKeyDisplayString = "F";
            tam.Checked = tamEkran;
            m.Items.Add(tam);
            var dol = new ToolStripMenuItem(Dil.T("Tam ekranda ekranı doldur", "Fill screen in full screen"), null, (s, e) => { doldur = !doldur; Yerlestir(); AyarYaz(); });
            dol.ShortcutKeyDisplayString = "D";
            dol.Checked = doldur;
            m.Items.Add(dol);
            var akIt = new ToolStripMenuItem(Dil.T("Akıcı mod (+1 kare gecikme)", "Smooth mode (+1 frame delay)"), null, (s, e) => AkiciDegistir());
            akIt.ShortcutKeyDisplayString = "A";
            akIt.Checked = akici;
            m.Items.Add(akIt);
            var keIt = new ToolStripMenuItem(Dil.T("Keskinleştirme", "Sharpening"));
            keIt.ShortcutKeyDisplayString = "K";
            string[] keAd = { Dil.T("Kapalı", "Off"), Dil.T("Hafif", "Light"), Dil.T("Orta", "Medium"), Dil.T("Güçlü", "Strong") };
            for (int i = 0; i < 4; i++)
            {
                int sv = i;
                var it = new ToolStripMenuItem(keAd[i], null, (s, e) => KeskinlikAyarla(sv));
                it.Checked = keskin == i;
                keIt.DropDownItems.Add(it);
            }
            m.Items.Add(keIt);
            var fpsIt = new ToolStripMenuItem(Dil.T("FPS göstergesi", "FPS counter"));
            fpsIt.ShortcutKeyDisplayString = "I";
            string[] fpsAd = { Dil.T("Kapalı", "Off"), Dil.T("Ekrana tıklayınca", "When I click"), Dil.T("Her zaman", "Always") };
            for (int i = 0; i < 3; i++)
            {
                int mod = i;
                var it = new ToolStripMenuItem(fpsAd[i], null, (s, e) => { fpsMod = mod; AyarYaz(); FpsGorunurluk(); });
                it.Checked = fpsMod == i;
                fpsIt.DropDownItems.Add(it);
            }
            m.Items.Add(fpsIt);

            var svIt = new ToolStripMenuItem(Dil.T("Ses seviyesi", "Volume") + "  (" + (sessiz ? Dil.T("sessiz", "muted") : seviye + "%") + ")");
            svIt.ShortcutKeyDisplayString = "↑ ↓";
            foreach (int yuzde in new[] { 100, 75, 50, 25, 10 })
            {
                int y = yuzde;
                var it = new ToolStripMenuItem(y + "%", null, (s, e) => { seviye = y; sessiz = false; SesUygula(); AyarYaz(); });
                it.Checked = !sessiz && seviye == y;
                svIt.DropDownItems.Add(it);
            }
            svIt.DropDownItems.Add(new ToolStripSeparator());
            var sesIt = new ToolStripMenuItem(Dil.T("Sessiz", "Mute"), null, (s, e) => { sessiz = !sessiz; SesUygula(); AyarYaz(); });
            sesIt.ShortcutKeyDisplayString = "M";
            sesIt.Checked = sessiz;
            svIt.DropDownItems.Add(sesIt);
            m.Items.Add(svIt);

            var dm = new ToolStripMenuItem(Dil.T("Dil / Language", "Language / Dil"));
            var tr = new ToolStripMenuItem("Türkçe", null, (s, e) => DilDegistir(false));
            tr.Checked = !Dil.En;
            var en = new ToolStripMenuItem("English", null, (s, e) => DilDegistir(true));
            en.Checked = Dil.En;
            dm.DropDownItems.Add(tr);
            dm.DropDownItems.Add(en);
            m.Items.Add(dm);

            m.Items.Add(new ToolStripSeparator());
            var cik = new ToolStripMenuItem(Dil.T("Çıkış", "Quit"), null, (s, e) => Close());
            cik.ShortcutKeyDisplayString = "Q";
            m.Items.Add(cik);
            foreach (ToolStripItem it in m.Items) KoyuYap(it);
            m.Closed += (s, e) => { menuAcik = false; sonHareket = DateTime.Now; };
            m.Show(this, yer);
        }

        void MetinleriYenile()
        {
            ayarBtn.Text = Dil.T("⚙  Ayarlar", "⚙  Settings");
            Text = "Basic Capture";
        }

        void DilDegistir(bool ingilizce)
        {
            Dil.En = ingilizce;
            AyarYaz();
            if (mesaj.Visible) Baslat(); // hata mesajını yeni dilde yeniden üret
            else MetinleriYenile();
        }

        static void KoyuYap(ToolStripItem it)
        {
            it.ForeColor = Color.White;
            var mi = it as ToolStripMenuItem;
            if (mi == null) return;
            mi.DropDown.BackColor = Color.FromArgb(32, 32, 36);
            if (mi.DropDown is ToolStripDropDownMenu)
                ((ToolStripDropDownMenu)mi.DropDown).Renderer = new ToolStripProfessionalRenderer(new KoyuRenk());
            foreach (ToolStripItem alt in mi.DropDownItems) KoyuYap(alt);
        }

        // ------------------------------------------------ ayarları hatırla
        void AyarOku()
        {
            try
            {
                if (!File.Exists(ayarDosyasi)) return;
                foreach (string satir in File.ReadAllLines(ayarDosyasi, Encoding.UTF8))
                {
                    int i = satir.IndexOf('=');
                    if (i < 0) continue;
                    string k = satir.Substring(0, i), v = satir.Substring(i + 1);
                    if (k == "video") vSecili = v;
                    else if (k == "ses") aSecili = v;
                    else if (k == "format") fSecili = v;
                    else if (k == "fps") { int x; if (int.TryParse(v, out x) && x >= 0 && x <= 2) fpsMod = x; }
                    else if (k == "seviye") { int x; if (int.TryParse(v, out x)) seviye = Math.Max(0, Math.Min(100, x)); }
                    else if (k == "sessiz") sessiz = v == "1";
                    else if (k == "akici") akici = v == "1";
                    else if (k == "keskin") { int x; if (int.TryParse(v, out x)) keskin = Math.Max(0, Math.Min(3, x)); }
                    else if (k == "doldur") doldur = v != "0";
                    else if (k == "dil") Dil.En = v == "en";
                }
            }
            catch { }
        }

        void AyarYaz()
        {
            try
            {
                File.WriteAllLines(ayarDosyasi, new[] { "video=" + vSecili, "ses=" + aSecili, "format=" + fSecili,
                    "fps=" + fpsMod, "doldur=" + (doldur ? "1" : "0"), "dil=" + (Dil.En ? "en" : "tr"),
                    "seviye=" + seviye, "sessiz=" + (sessiz ? "1" : "0"), "akici=" + (akici ? "1" : "0"), "keskin=" + keskin }, Encoding.UTF8);
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            zamanlayici.Stop();
            Durdur();
            ImlecGoster(true);
            base.OnFormClosing(e);
        }

        // ------------------------------------------------ test modu (--test)
        public string FpsMetni { get { return fpsK.Metin; } }

        public static string CihazRaporu()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Görüntü cihazları:");
            foreach (var c in DS.Cihazlar(G.VideoInputCat)) sb.AppendLine("  " + c.Key);
            sb.AppendLine("Ses cihazları:");
            foreach (var c in DS.Cihazlar(G.AudioInputCat)) sb.AppendLine("  " + c.Key);
            return sb.ToString();
        }
    }

    // Koyu menü renkleri
    class KoyuRenk : ProfessionalColorTable
    {
        static readonly Color Zemin = Color.FromArgb(32, 32, 36);
        static readonly Color Secili = Color.FromArgb(58, 58, 66);
        public override Color ToolStripDropDownBackground { get { return Zemin; } }
        public override Color ImageMarginGradientBegin { get { return Zemin; } }
        public override Color ImageMarginGradientMiddle { get { return Zemin; } }
        public override Color ImageMarginGradientEnd { get { return Zemin; } }
        public override Color MenuBorder { get { return Color.FromArgb(70, 70, 78); } }
        public override Color MenuItemBorder { get { return Secili; } }
        public override Color MenuItemSelected { get { return Secili; } }
        public override Color MenuItemSelectedGradientBegin { get { return Secili; } }
        public override Color MenuItemSelectedGradientEnd { get { return Secili; } }
        public override Color MenuItemPressedGradientBegin { get { return Secili; } }
        public override Color MenuItemPressedGradientEnd { get { return Secili; } }
        public override Color SeparatorDark { get { return Color.FromArgb(70, 70, 78); } }
        public override Color SeparatorLight { get { return Zemin; } }
        public override Color CheckBackground { get { return Color.FromArgb(40, 90, 160); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(50, 110, 190); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(50, 110, 190); } }
    }

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) Olcek.K = Math.Max(1f, g.DpiX / 96f); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0 && args[0] == "--test")
            {
                // Pencereyi kısa süre açar, ne olduğunu test-sonuc.txt dosyasına yazar
                string yol = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "test-sonuc.txt");
                string rapor;
                try { rapor = AnaForm.CihazRaporu(); } catch (Exception ex) { rapor = "Cihaz listesi alınamadı: " + ex.Message + "\r\n"; }
                var f = new AnaForm();
                var t = new Timer();
                t.Interval = 4000;
                t.Tick += (s, e) =>
                {
                    t.Stop();
                    File.WriteAllText(yol, rapor + "Durum: " + f.Durum + " | sayaç: " + f.FpsMetni + Environment.NewLine, Encoding.UTF8);
                    f.Close();
                };
                t.Start();
                Application.Run(f);
                return;
            }

            Application.Run(new AnaForm());
        }
    }
}
