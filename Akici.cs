// Akıcı mod: kareleri küçük bir tamponda tutup tam eşit aralıklarla, ekran kartıyla (OpenGL) çizer.
// Kart kareleri düzensiz gönderdiğinde (ör. MS2109'da 1080p 30) oluşan takılmaları yok eder.
// Bedeli: yaklaşık 1 kare ek gecikme.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CaptureIzle
{
    // ---------------------------------------------------------------- DirectShow kare yakalayıcı (qedit.dll)
    [ComImport, Guid("6B652FFF-11FE-4fce-92AD-0266B5D7C78F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISampleGrabber
    {
        [PreserveSig] int SetOneShot(int oneShot);
        [PreserveSig] int SetMediaType(ref AMMediaType pmt);
        [PreserveSig] int GetConnectedMediaType(IntPtr pmt);
        [PreserveSig] int SetBufferSamples(int buffer);
        [PreserveSig] int GetCurrentBuffer(ref int size, IntPtr buf);
        [PreserveSig] int GetCurrentSample(out IntPtr sample);
        [PreserveSig] int SetCallback(ISampleGrabberCB cb, int which);
    }

    [ComImport, Guid("0579154A-2B53-4994-B0D0-E773148EFF85"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISampleGrabberCB
    {
        [PreserveSig] int SampleCB(double t, IntPtr sample);
        [PreserveSig] int BufferCB(double t, IntPtr buf, int len);
    }

    // ---------------------------------------------------------------- Görüntünün çizildiği alan
    // Fare olaylarını ana pencereye geçirir (tıklama, çift tıklama, sağ tık çalışmaya devam eder).
    class GoruntuPaneli : Control
    {
        public GoruntuPaneli()
        {
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            BackColor = Color.Black;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x04000000 | 0x02000000; // CLIPSIBLINGS | CLIPCHILDREN (OpenGL için gerekli)
                cp.ClassStyle |= 0x20;               // CS_OWNDC
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; } // WM_NCHITTEST -> HTTRANSPARENT
            base.WndProc(ref m);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
    }

    // ---------------------------------------------------------------- Zamanlayıcılı çizici
    class Sunucu : ISampleGrabberCB
    {
        // --- Windows / OpenGL
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] static extern void Kopyala(IntPtr d, IntPtr s, IntPtr n);
        [DllImport("winmm.dll")] static extern int timeBeginPeriod(int p);
        [DllImport("winmm.dll")] static extern int timeEndPeriod(int p);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("gdi32.dll")] static extern int ChoosePixelFormat(IntPtr dc, ref PFD p);
        [DllImport("gdi32.dll")] static extern bool SetPixelFormat(IntPtr dc, int f, ref PFD p);
        [DllImport("gdi32.dll")] static extern bool SwapBuffers(IntPtr dc);
        [DllImport("opengl32.dll")] static extern IntPtr wglCreateContext(IntPtr dc);
        [DllImport("opengl32.dll")] static extern bool wglMakeCurrent(IntPtr dc, IntPtr rc);
        [DllImport("opengl32.dll")] static extern bool wglDeleteContext(IntPtr rc);
        [DllImport("opengl32.dll")] static extern IntPtr wglGetProcAddress(string ad);
        [DllImport("opengl32.dll")] static extern void glGenTextures(int n, out uint t);
        [DllImport("opengl32.dll")] static extern void glDeleteTextures(int n, ref uint t);
        [DllImport("opengl32.dll")] static extern void glBindTexture(uint hedef, uint t);
        [DllImport("opengl32.dll")] static extern void glTexParameteri(uint hedef, uint ad, int deger);
        [DllImport("opengl32.dll")] static extern void glPixelStorei(uint ad, int deger);
        [DllImport("opengl32.dll")] static extern void glTexImage2D(uint hedef, int sev, int ic, int w, int h, int kenar, uint fmt, uint tip, IntPtr veri);
        [DllImport("opengl32.dll")] static extern void glTexSubImage2D(uint hedef, int sev, int x, int y, int w, int h, uint fmt, uint tip, IntPtr veri);
        [DllImport("opengl32.dll")] static extern void glViewport(int x, int y, int w, int h);
        [DllImport("opengl32.dll")] static extern void glClearColor(float r, float g, float b, float a);
        [DllImport("opengl32.dll")] static extern void glClear(uint maske);
        [DllImport("opengl32.dll")] static extern void glEnable(uint ozellik);
        [DllImport("opengl32.dll")] static extern void glBegin(uint mod);
        [DllImport("opengl32.dll")] static extern void glEnd();
        [DllImport("opengl32.dll")] static extern void glTexCoord2f(float s, float t);
        [DllImport("opengl32.dll")] static extern void glVertex2f(float x, float y);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SwapAraligi(int n);

        // OpenGL 2.0 shader fonksiyonları (sürücüden çalışma anında alınır)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint glCreateShaderD(uint tip);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glShaderSourceD(uint s, int n, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] kod, int[] uzunluk);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glCompileShaderD(uint s);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glGetShaderivD(uint s, uint ad, out int deger);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glGetInfoLogD(uint s, int boyut, out int uzunluk, System.Text.StringBuilder log);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint glCreateProgramD();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glAttachShaderD(uint p, uint s);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glLinkProgramD(uint p);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glUseProgramD(uint p);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int glGetUniformLocationD(uint p, string ad);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glUniform1iD(int yer, int v);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glUniform1fD(int yer, float v);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glUniform2fD(int yer, float a, float b);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glDeleteProgramD(uint p);

        static T Fonk<T>(string ad) where T : class
        {
            IntPtr p = wglGetProcAddress(ad);
            if (p == IntPtr.Zero || p == (IntPtr)1 || p == (IntPtr)2 || p == (IntPtr)3 || p == (IntPtr)(-1))
                throw new Exception(ad + " bulunamadı");
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        const string KoseShader =
            "void main(){ gl_TexCoord[0] = gl_MultiTexCoord0; gl_Position = ftransform(); }";

        // Catmull-Rom büyütme (bilinear örneklerle 9 dokunuş) + AMD FidelityFX CAS keskinleştirme.
        // CAS: komşulara göre yerel kontrastı ölçer, zaten keskin kenarları ve düz alanları daha az işler.
        const string PikselShader = @"
uniform sampler2D tex;
uniform vec2 texel;
uniform float guc;
vec3 catmullRom(vec2 uv) {
    vec2 p = uv / texel;
    vec2 t1 = floor(p - 0.5) + 0.5;
    vec2 f = p - t1;
    vec2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    vec2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    vec2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    vec2 w3 = f * f * (-0.5 + 0.5 * f);
    vec2 w12 = w1 + w2;
    vec2 a = (t1 - 1.0) * texel;
    vec2 b = (t1 + w2 / w12) * texel;
    vec2 c = (t1 + 2.0) * texel;
    vec3 r = texture2D(tex, vec2(a.x, a.y)).rgb * (w0.x * w0.y)
           + texture2D(tex, vec2(b.x, a.y)).rgb * (w12.x * w0.y)
           + texture2D(tex, vec2(c.x, a.y)).rgb * (w3.x * w0.y)
           + texture2D(tex, vec2(a.x, b.y)).rgb * (w0.x * w12.y)
           + texture2D(tex, vec2(b.x, b.y)).rgb * (w12.x * w12.y)
           + texture2D(tex, vec2(c.x, b.y)).rgb * (w3.x * w12.y)
           + texture2D(tex, vec2(a.x, c.y)).rgb * (w0.x * w3.y)
           + texture2D(tex, vec2(b.x, c.y)).rgb * (w12.x * w3.y)
           + texture2D(tex, vec2(c.x, c.y)).rgb * (w3.x * w3.y);
    return clamp(r, 0.0, 1.0);
}
void main() {
    vec2 uv = gl_TexCoord[0].st;
    vec3 e = catmullRom(uv);
    vec3 b = texture2D(tex, uv + vec2(0.0, -texel.y)).rgb;
    vec3 d = texture2D(tex, uv + vec2(-texel.x, 0.0)).rgb;
    vec3 f = texture2D(tex, uv + vec2( texel.x, 0.0)).rgb;
    vec3 h = texture2D(tex, uv + vec2(0.0,  texel.y)).rgb;
    vec3 mn = min(min(min(d, e), min(f, b)), h);
    vec3 mx = max(max(max(d, e), max(f, b)), h);
    vec3 amp = sqrt(clamp(min(mn, 2.0 - mx) / max(mx, vec3(0.0001)), 0.0, 1.0));
    vec3 w = amp * (-1.0 / mix(8.0, 5.0, guc));
    vec3 sonuc = (b * w + d * w + f * w + h * w + e) / (1.0 + 4.0 * w);
    gl_FragColor = vec4(clamp(sonuc, 0.0, 1.0), 1.0);
}";

        uint program;
        int yerTex = -1, yerTexel = -1, yerGuc = -1;
        glUseProgramD glUseProgram;
        glUniform1iD glUniform1i;
        glUniform1fD glUniform1f;
        glUniform2fD glUniform2f;
        glDeleteProgramD glDeleteProgram;

        void ShaderHazirla()
        {
            var glCreateShader = Fonk<glCreateShaderD>("glCreateShader");
            var glShaderSource = Fonk<glShaderSourceD>("glShaderSource");
            var glCompileShader = Fonk<glCompileShaderD>("glCompileShader");
            var glGetShaderiv = Fonk<glGetShaderivD>("glGetShaderiv");
            var glGetShaderInfoLog = Fonk<glGetInfoLogD>("glGetShaderInfoLog");
            var glCreateProgram = Fonk<glCreateProgramD>("glCreateProgram");
            var glAttachShader = Fonk<glAttachShaderD>("glAttachShader");
            var glLinkProgram = Fonk<glLinkProgramD>("glLinkProgram");
            var glGetProgramiv = Fonk<glGetShaderivD>("glGetProgramiv");
            var glGetUniformLocation = Fonk<glGetUniformLocationD>("glGetUniformLocation");
            glUseProgram = Fonk<glUseProgramD>("glUseProgram");
            glUniform1i = Fonk<glUniform1iD>("glUniform1i");
            glUniform1f = Fonk<glUniform1fD>("glUniform1f");
            glUniform2f = Fonk<glUniform2fD>("glUniform2f");
            glDeleteProgram = Fonk<glDeleteProgramD>("glDeleteProgram");

            uint[] sh = new uint[2];
            string[] kodlar = { KoseShader, PikselShader };
            uint[] tipler = { 0x8B31, 0x8B30 }; // VERTEX_SHADER, FRAGMENT_SHADER
            for (int i = 0; i < 2; i++)
            {
                sh[i] = glCreateShader(tipler[i]);
                glShaderSource(sh[i], 1, new[] { kodlar[i] }, null);
                glCompileShader(sh[i]);
                int ok;
                glGetShaderiv(sh[i], 0x8B81, out ok); // COMPILE_STATUS
                if (ok == 0)
                {
                    var log = new System.Text.StringBuilder(2048); int n;
                    glGetShaderInfoLog(sh[i], log.Capacity, out n, log);
                    throw new Exception("shader: " + log);
                }
            }
            program = glCreateProgram();
            glAttachShader(program, sh[0]);
            glAttachShader(program, sh[1]);
            glLinkProgram(program);
            int bagli;
            glGetProgramiv(program, 0x8B82, out bagli); // LINK_STATUS
            if (bagli == 0) throw new Exception("shader bağlanamadı");
            yerTex = glGetUniformLocation(program, "tex");
            yerTexel = glGetUniformLocation(program, "texel");
            yerGuc = glGetUniformLocation(program, "guc");
        }

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)]
        struct PFD
        {
            public short nSize, nVersion; public int dwFlags;
            public byte iPixelType, cColorBits, cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift,
                        cAlphaBits, cAlphaShift, cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits,
                        cDepthBits, cStencilBits, cAuxBuffers;
            public sbyte iLayerType; public byte bReserved;
            public int dwLayerMask, dwVisibleMask, dwDamageMask;
        }

        const uint TEX2D = 0x0DE1, BGRA = 0x80E1, UBYTE = 0x1401, RGBA8 = 0x8058, RENK = 0x4000, DORTGEN = 7;

        // --- Durum
        readonly IntPtr hwnd;
        readonly int w, h, boyut;
        readonly double aralik;        // iki kare arası hedef süre (sn)
        readonly bool asagidanYukari;  // DirectShow RGB32 genelde ters (alt satır önce)
        readonly Queue<IntPtr> kuyruk = new Queue<IntPtr>();
        readonly Stack<IntPtr> havuz = new Stack<IntPtr>();
        readonly List<IntPtr> hepsi = new List<IntPtr>();
        readonly object kilit = new object();
        readonly AutoResetEvent geldi = new AutoResetEvent(false);
        Thread is_;
        volatile bool calis = true;

        public int Cizilen;   // ekrana çizilen yeni kare sayısı (FPS sayacı bunu okur)
        public int Gelen;     // karttan gelen kare sayısı
        public int Tekrar;    // kare zamanında gelmediği için eskisi tekrar gösterildi
        public int Atilan;    // tampon taştığı için atılan kare
        public volatile bool Doldur;
        public volatile float Keskinlik;   // 0 = kapalı (düz bilinear), 0..1 = keskinleştirme gücü
        public string HataMetni;
        public string ShaderNotu = "";
        readonly bool tamponlu;            // true = akıcı (eşit aralıklı), false = kare gelir gelmez çiz

        public Sunucu(IntPtr hwnd, int w, int h, double fps, bool asagidanYukari, bool tamponlu)
        {
            this.hwnd = hwnd; this.w = w; this.h = h;
            this.asagidanYukari = asagidanYukari;
            this.tamponlu = tamponlu;
            boyut = w * h * 4;
            aralik = 1.0 / Math.Max(1, fps);
        }

        public void Baslat()
        {
            is_ = new Thread(Dongu);
            is_.IsBackground = true;
            is_.Priority = ThreadPriority.AboveNormal;
            is_.Start();
        }

        public void Durdur()
        {
            calis = false;
            geldi.Set();
            if (is_ != null) is_.Join(2000);
            lock (kilit)
            {
                foreach (IntPtr p in hepsi) Marshal.FreeHGlobal(p);
                hepsi.Clear(); kuyruk.Clear(); havuz.Clear();
            }
        }

        // Kartın iş parçacığından her kare için çağrılır: kareyi kopyalayıp kuyruğa koy
        public int SampleCB(double t, IntPtr sample) { return 0; }

        public int BufferCB(double t, IntPtr buf, int len)
        {
            if (!calis || buf == IntPtr.Zero) return 0;
            IntPtr hedef = IntPtr.Zero;
            lock (kilit)
            {
                if (havuz.Count > 0) hedef = havuz.Pop();
                else if (kuyruk.Count >= 4) { hedef = kuyruk.Dequeue(); Atilan++; } // çizici takıldıysa en eskiyi feda et
                else { hedef = Marshal.AllocHGlobal(boyut); hepsi.Add(hedef); }
            }
            Kopyala(hedef, buf, (IntPtr)Math.Min(len, boyut));
            lock (kilit) { kuyruk.Enqueue(hedef); Gelen++; }
            geldi.Set();
            return 0;
        }

        void Dongu()
        {
            IntPtr dc = GetDC(hwnd), rc = IntPtr.Zero;
            uint doku = 0;
            timeBeginPeriod(1);
            try
            {
                var pfd = new PFD();
                pfd.nSize = 40; pfd.nVersion = 1;
                pfd.dwFlags = 0x04 | 0x20 | 0x01; // DRAW_TO_WINDOW | SUPPORT_OPENGL | DOUBLEBUFFER
                pfd.cColorBits = 32;
                int pf = ChoosePixelFormat(dc, ref pfd);
                if (pf == 0 || !SetPixelFormat(dc, pf, ref pfd)) throw new Exception("OpenGL piksel formatı ayarlanamadı");
                rc = wglCreateContext(dc);
                if (rc == IntPtr.Zero || !wglMakeCurrent(dc, rc)) throw new Exception("OpenGL başlatılamadı");
                IntPtr sw = wglGetProcAddress("wglSwapIntervalEXT");
                if (sw != IntPtr.Zero) ((SwapAraligi)Marshal.GetDelegateForFunctionPointer(sw, typeof(SwapAraligi)))(1); // yırtılma olmasın

                glGenTextures(1, out doku);
                glBindTexture(TEX2D, doku);
                glTexParameteri(TEX2D, 0x2801, 0x2601); // MIN_FILTER = LINEAR
                glTexParameteri(TEX2D, 0x2800, 0x2601); // MAG_FILTER = LINEAR
                glTexParameteri(TEX2D, 0x2802, 0x812F); // WRAP_S = CLAMP_TO_EDGE
                glTexParameteri(TEX2D, 0x2803, 0x812F); // WRAP_T = CLAMP_TO_EDGE
                glPixelStorei(0x0CF5, 4);               // UNPACK_ALIGNMENT
                glTexImage2D(TEX2D, 0, (int)RGBA8, w, h, 0, BGRA, UBYTE, IntPtr.Zero);
                glEnable(TEX2D);

                // Keskinleştirme shader'ı; derlenemezse düz çizime devam
                try { ShaderHazirla(); ShaderNotu = "shader hazır"; }
                catch (Exception ex) { program = 0; ShaderNotu = "shader yok: " + ex.Message; }

                if (!tamponlu)
                {
                    AnindaDongu(dc);
                    return;
                }

                // Başlarken 2 kare biriksin: 1 karelik pay, geç gelen kareleri saklar
                var saat = Stopwatch.StartNew();
                while (calis)
                {
                    lock (kilit) if (kuyruk.Count >= 2) break;
                    Ciz(dc, false);
                    geldi.WaitOne(50);
                }

                double sonraki = saat.Elapsed.TotalSeconds, ortKuyruk = 1.5;
                IntPtr son = IntPtr.Zero;
                while (calis)
                {
                    // Bir sonraki kare zamanına kadar bekle (son milisaniyede hassas bekleme)
                    double simdi;
                    while (calis && (simdi = saat.Elapsed.TotalSeconds) < sonraki)
                    {
                        if (sonraki - simdi > 0.0025) Thread.Sleep(1); else Thread.SpinWait(200);
                    }
                    if (!calis) break;

                    IntPtr yeni = IntPtr.Zero;
                    int q;
                    lock (kilit)
                    {
                        q = kuyruk.Count;
                        if (q > 0)
                        {
                            yeni = kuyruk.Dequeue();
                            while (kuyruk.Count > 2) { havuz.Push(kuyruk.Dequeue()); Atilan++; } // gecikme birikmesin
                        }
                    }
                    ortKuyruk = ortKuyruk * 0.97 + q * 0.03;

                    if (yeni != IntPtr.Zero)
                    {
                        glTexSubImage2D(TEX2D, 0, 0, 0, w, h, BGRA, UBYTE, yeni);
                        lock (kilit) if (son != IntPtr.Zero) havuz.Push(son);
                        son = yeni;
                        Interlocked.Increment(ref Cizilen);
                    }
                    else if (son != IntPtr.Zero) Interlocked.Increment(ref Tekrar);

                    Ciz(dc, son != IntPtr.Zero);

                    // Kartın saati bizimkinden biraz hızlı/yavaşsa kuyruğa bakıp hızı hafifçe ayarla (en fazla %2)
                    double duzelt = Math.Max(-0.02, Math.Min(0.02, (ortKuyruk - 1.5) * 0.01));
                    sonraki += aralik * (1 - duzelt);
                    if (saat.Elapsed.TotalSeconds - sonraki > aralik) sonraki = saat.Elapsed.TotalSeconds; // uzun duraklamadan sonra yeniden hizala
                }
            }
            catch (Exception ex) { HataMetni = ex.Message; }
            finally
            {
                try
                {
                    if (program != 0 && glDeleteProgram != null) { glUseProgram(0); glDeleteProgram(program); }
                    if (doku != 0) glDeleteTextures(1, ref doku);
                    wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                    if (rc != IntPtr.Zero) wglDeleteContext(rc);
                }
                catch { }
                ReleaseDC(hwnd, dc);
                timeEndPeriod(1);
            }
        }

        // Düşük gecikme: tampon yok, her kare geldiği an çizilir (yalnızca keskinleştirme için GPU yolu)
        void AnindaDongu(IntPtr dc)
        {
            IntPtr son = IntPtr.Zero;
            Ciz(dc, false);
            while (calis)
            {
                if (!geldi.WaitOne(100)) { if (son != IntPtr.Zero) Ciz(dc, true); continue; } // pencere boyutu değişirse yine çiz
                IntPtr yeni = IntPtr.Zero;
                lock (kilit)
                {
                    while (kuyruk.Count > 1) { havuz.Push(kuyruk.Dequeue()); Atilan++; } // hep en yeni kare
                    if (kuyruk.Count > 0) yeni = kuyruk.Dequeue();
                }
                if (yeni == IntPtr.Zero) continue;
                glTexSubImage2D(TEX2D, 0, 0, 0, w, h, BGRA, UBYTE, yeni);
                lock (kilit) if (son != IntPtr.Zero) havuz.Push(son);
                son = yeni;
                Interlocked.Increment(ref Cizilen);
                Ciz(dc, true);
            }
        }

        void Ciz(IntPtr dc, bool goruntuVar)
        {
            RECT r;
            GetClientRect(hwnd, out r);
            int cw = r.R, ch = r.B;
            if (cw <= 0 || ch <= 0) return;
            glViewport(0, 0, cw, ch);
            glClearColor(0, 0, 0, 1);
            glClear(RENK);
            if (goruntuVar)
            {
                int vw = cw, vh = ch;
                if (!Doldur)
                {
                    vh = (int)((long)cw * h / w);
                    if (vh > ch) { vh = ch; vw = (int)((long)ch * w / h); }
                }
                glViewport((cw - vw) / 2, (ch - vh) / 2, vw, vh);
                float guc = Keskinlik;
                if (program != 0 && guc > 0)
                {
                    glUseProgram(program);
                    glUniform1i(yerTex, 0);
                    glUniform2f(yerTexel, 1f / w, 1f / h);
                    glUniform1f(yerGuc, Math.Min(1f, guc));
                }
                else if (program != 0) glUseProgram(0);
                float alt = asagidanYukari ? 0f : 1f, ust = asagidanYukari ? 1f : 0f;
                glBegin(DORTGEN);
                glTexCoord2f(0, alt); glVertex2f(-1, -1);
                glTexCoord2f(1, alt); glVertex2f(1, -1);
                glTexCoord2f(1, ust); glVertex2f(1, 1);
                glTexCoord2f(0, ust); glVertex2f(-1, 1);
                glEnd();
            }
            SwapBuffers(dc);
        }
    }
}
