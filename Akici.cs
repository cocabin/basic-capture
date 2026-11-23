// Görüntüyü ekran kartıyla (OpenGL) çizer. Normalde her kare geldiği an çizilir.
// Akıcı modda kareler küçük bir tamponda tutulup tam eşit aralıklarla çizilir: düzensiz kare
// gönderen kartlarda (ör. MS2109'da 1080p 30) takılmayı yok eder, bedeli ~1 kare ek gecikme.

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
        // Ara kare için: dokuya çizim (FBO), çoklu doku, mipmap (OpenGL 3.0)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glActiveTextureD(uint birim);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glGenFramebuffersD(int n, out uint f);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glBindFramebufferD(uint hedef, uint f);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glFramebufferTexture2DD(uint hedef, uint ek, uint dokuHedef, uint doku, int sev);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint glCheckFramebufferStatusD(uint hedef);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glGenerateMipmapD(uint hedef);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void glDeleteFramebuffersD(int n, ref uint f);

        static T Fonk<T>(string ad) where T : class
        {
            IntPtr p = wglGetProcAddress(ad);
            if (p == IntPtr.Zero || p == (IntPtr)1 || p == (IntPtr)2 || p == (IntPtr)3 || p == (IntPtr)(-1))
                throw new Exception(ad + " bulunamadı");
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        // ---- AMD FidelityFX Super Resolution 1 (FSR 1): EASU büyütme + RCAS keskinleştirme
        // GLSL'e çevrilmiştir. Orijinal: AMD FidelityFX FSR 1 (ffx_fsr1.h)
        // Copyright (c) 2021 Advanced Micro Devices, Inc. All rights reserved. MIT lisansı:
        // Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
        // documentation files (the "Software"), to deal in the Software without restriction, including without limitation
        // the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
        // permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright
        // notice and this permission notice shall be included in all copies or substantial portions of the Software.
        // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.

        // EASU: 12 komşu pikselden kenarın yönünü ve uzunluğunu bulur, o yöne uzatılmış bir filtreyle büyütür.
        const string EasuShader = @"#version 130
uniform sampler2D tex;
uniform vec2 girdi;
uniform vec2 cikti;
float L(vec3 c) { return c.b * 0.5 + (c.r * 0.5 + c.g); }
vec3 T(ivec2 p) { return texelFetch(tex, clamp(p, ivec2(0), ivec2(girdi) - 1), 0).rgb; }
void kur(inout vec2 dir, inout float len, float w, float lA, float lB, float lC, float lD, float lE) {
    float lenX = 1.0 / max(max(abs(lD - lC), abs(lC - lB)), 1e-5);
    float dirX = lD - lB;
    dir.x += dirX * w;
    lenX = clamp(abs(dirX) * lenX, 0.0, 1.0); len += lenX * lenX * w;
    float lenY = 1.0 / max(max(abs(lE - lC), abs(lC - lA)), 1e-5);
    float dirY = lE - lA;
    dir.y += dirY * w;
    lenY = clamp(abs(dirY) * lenY, 0.0, 1.0); len += lenY * lenY * w;
}
void tap(inout vec3 aC, inout float aW, vec2 off, vec2 dir, vec2 len2, float lob, float clp, vec3 c) {
    vec2 v = vec2(off.x * dir.x + off.y * dir.y, off.x * -dir.y + off.y * dir.x) * len2;
    float d2 = min(dot(v, v), clp);
    float wB = 0.4 * d2 - 1.0, wA = lob * d2 - 1.0;
    wB *= wB; wA *= wA;
    float w = (1.5625 * wB - 0.5625) * wA;
    aC += c * w; aW += w;
}
void main() {
    vec2 pp = gl_FragCoord.xy * (girdi / cikti) - 0.5;
    vec2 fp = floor(pp); pp -= fp;
    ivec2 i = ivec2(fp);
    vec3 b = T(i + ivec2(0, -1)), c = T(i + ivec2(1, -1));
    vec3 e = T(i + ivec2(-1, 0)), f = T(i), g = T(i + ivec2(1, 0)), h = T(i + ivec2(2, 0));
    vec3 q = T(i + ivec2(-1, 1)), j = T(i + ivec2(0, 1)), k = T(i + ivec2(1, 1)), l = T(i + ivec2(2, 1));
    vec3 n = T(i + ivec2(0, 2)), o = T(i + ivec2(1, 2));
    float bL = L(b), cL = L(c), eL = L(e), fL = L(f), gL = L(g), hL = L(h);
    float qL = L(q), jL = L(j), kL = L(k), lL = L(l), nL = L(n), oL = L(o);
    vec2 dir = vec2(0.0); float len = 0.0;
    kur(dir, len, (1.0 - pp.x) * (1.0 - pp.y), bL, eL, fL, gL, jL);
    kur(dir, len, pp.x * (1.0 - pp.y),         cL, fL, gL, hL, kL);
    kur(dir, len, (1.0 - pp.x) * pp.y,         fL, qL, jL, kL, nL);
    kur(dir, len, pp.x * pp.y,                 gL, jL, kL, lL, oL);
    float dirR = dot(dir, dir);
    bool sifir = dirR < 1.0 / 32768.0;
    dirR = sifir ? 1.0 : inversesqrt(dirR);
    dir.x = sifir ? 1.0 : dir.x;
    dir *= dirR;
    len = len * 0.5; len *= len;
    float stretch = dot(dir, dir) / max(abs(dir.x), abs(dir.y));
    vec2 len2 = vec2(1.0 + (stretch - 1.0) * len, 1.0 - 0.5 * len);
    float lob = 0.5 + ((1.0 / 4.0 - 0.04) - 0.5) * len;
    float clp = 1.0 / lob;
    vec3 aC = vec3(0.0); float aW = 0.0;
    tap(aC, aW, vec2( 0.0, -1.0) - pp, dir, len2, lob, clp, b);
    tap(aC, aW, vec2( 1.0, -1.0) - pp, dir, len2, lob, clp, c);
    tap(aC, aW, vec2(-1.0,  1.0) - pp, dir, len2, lob, clp, q);
    tap(aC, aW, vec2( 0.0,  1.0) - pp, dir, len2, lob, clp, j);
    tap(aC, aW, vec2( 0.0,  0.0) - pp, dir, len2, lob, clp, f);
    tap(aC, aW, vec2(-1.0,  0.0) - pp, dir, len2, lob, clp, e);
    tap(aC, aW, vec2( 1.0,  1.0) - pp, dir, len2, lob, clp, k);
    tap(aC, aW, vec2( 2.0,  1.0) - pp, dir, len2, lob, clp, l);
    tap(aC, aW, vec2( 2.0,  0.0) - pp, dir, len2, lob, clp, h);
    tap(aC, aW, vec2( 1.0,  0.0) - pp, dir, len2, lob, clp, g);
    tap(aC, aW, vec2( 1.0,  2.0) - pp, dir, len2, lob, clp, o);
    tap(aC, aW, vec2( 0.0,  2.0) - pp, dir, len2, lob, clp, n);
    vec3 mn4 = min(min(f, g), min(j, k)), mx4 = max(max(f, g), max(j, k));
    gl_FragColor = vec4(min(mx4, max(mn4, aC / aW)), 1.0); // halka (ringing) olmasın
}";

        // RCAS: ekran çözünürlüğünde, komşulara göre aşırıya kaçmayan (kenarda halka yapmayan) keskinleştirme.
        const string RcasShader = @"#version 130
uniform sampler2D tex;
uniform vec2 ofs;
uniform vec2 boyut;
uniform float ters;
uniform float keskin;
vec3 T(ivec2 p) { return texelFetch(tex, clamp(p, ivec2(0), ivec2(boyut) - 1), 0).rgb; }
void main() {
    vec2 ip = floor(gl_FragCoord.xy - ofs);
    if (ters > 0.5) ip.y = boyut.y - 1.0 - ip.y;
    ivec2 p = ivec2(ip);
    vec3 b = T(p + ivec2(0, -1)), d = T(p + ivec2(-1, 0)), e = T(p), f = T(p + ivec2(1, 0)), h = T(p + ivec2(0, 1));
    vec3 mn4 = min(min(b, d), min(f, h)), mx4 = max(max(b, d), max(f, h));
    vec3 hitMin = min(mn4, e) / max(4.0 * mx4, vec3(1e-5));
    vec3 hitMax = (1.0 - max(mx4, e)) / min(4.0 * mn4 - 4.0, vec3(-1e-5));
    vec3 lobeRGB = max(-hitMin, hitMax);
    float lobe = max(-0.1875, min(max(lobeRGB.r, max(lobeRGB.g, lobeRGB.b)), 0.0)) * keskin;
    gl_FragColor = vec4((lobe * (b + d + f + h) + e) / (4.0 * lobe + 1.0), 1.0);
}";

        // ---- Ara kare üretimi (30 -> 60): blok eşleme ile hareket tahmini + hareket yönünde harmanlama
        const string KoseShader130 =
            "#version 130\nvoid main(){ gl_TexCoord[0] = gl_MultiTexCoord0; gl_Position = ftransform(); }";

        // Her piksel = 16x16'lık bir blok. Önceki (A) ve yeni (B) karede bloğun nereye kaydığını arar:
        // önce bulanık (mip 3) görüntüde ±48 px kaba arama, sonra mip 1'de ±4 px, sonra tam çözünürlükte ±1 px.
        const string HareketShader = @"#version 130
uniform sampler2D A;
uniform sampler2D B;
uniform vec2 px;
uniform float blokPx;
float parlak(sampler2D s, vec2 uv, float lod) { return dot(textureLod(s, uv, lod).rgb, vec3(0.299, 0.587, 0.114)); }
// Blok içinde 8x8 noktada fark + hareketin uzunluğuna küçük ceza: seyrek dokuda (çim, kalabalık) noktaların
// çoğu düz alana denk gelir ve her hareket 'doğru' görünür; ceza belirsizlikte küçük hareketi seçtirir.
float fark(vec2 uv, vec2 d, float lod) {
    float s = 0.0;
    for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++) {
            vec2 o = (vec2(float(x), float(y)) - 3.5) * (blokPx / 8.0) * px;
            s += abs(parlak(A, uv + o, lod) - parlak(B, uv + o + d * px, lod));
        }
    return s / 64.0 + 0.0004 * length(d);
}
void main() {
    vec2 uv = gl_TexCoord[0].st;
    // Kaba-ince arama: her aşama bir öncekinin hatasını düzeltebilecek kadar geniş bakar.
    // (Mip 3'te ince doku, ör. çim, kaybolur; ara aşama olmadan hareket hiç bulunamıyordu.)
    vec2 en = vec2(0.0);
    float es = fark(uv, en, 3.0);
    for (int y = -6; y <= 6; y++)                 // mip 3: ±48 px, 8 px adım
        for (int x = -6; x <= 6; x++) {
            vec2 d = vec2(float(x), float(y)) * 8.0;
            float s = fark(uv, d, 3.0);
            if (s < es) { es = s; en = d; }
        }
    vec2 c = en; es = fark(uv, c, 2.0);
    for (int y = -2; y <= 2; y++)                 // mip 2: ±8 px, 4 px adım
        for (int x = -2; x <= 2; x++) {
            vec2 d = c + vec2(float(x), float(y)) * 4.0;
            float s = fark(uv, d, 2.0);
            if (s < es) { es = s; en = d; }
        }
    c = en; es = fark(uv, c, 1.0);
    for (int y = -2; y <= 2; y++)                 // mip 1: ±4 px, 2 px adım
        for (int x = -2; x <= 2; x++) {
            vec2 d = c + vec2(float(x), float(y)) * 2.0;
            float s = fark(uv, d, 1.0);
            if (s < es) { es = s; en = d; }
        }
    c = en; es = fark(uv, c, 0.0);
    for (int y = -2; y <= 2; y++)                 // tam çözünürlük: ±2 px, 1 px adım
        for (int x = -2; x <= 2; x++) {
            vec2 d = c + vec2(float(x), float(y));
            float s = fark(uv, d, 0.0);
            if (s < es) { es = s; en = d; }
        }
    float s0 = fark(uv, vec2(0.0), 0.0) * 0.9;    // hareketsizliğe hafif öncelik: düz alanlar titremesin
    if (s0 <= es) { es = s0; en = vec2(0.0); }
    gl_FragColor = vec4(en, es, 1.0);
}";

        // Ara kare: her pikseli hareket yönünde iki kareden alıp ortalar.
        // - Örnekler Catmull-Rom ile okunur: yarım piksel kaymada bilinear bulanıklaştırır ve ara kareler
        //   gerçeklerden yumuşak kalınca görüntü titrer.
        // - İki kare uyuşmuyorsa (yeni beliren nesne, yanlış vektör) kaydırmasız yumuşak karışıma geçer;
        //   yeni kareyi erken göstermek o bölgede takılma yaratır. Eşik, MJPG gürültüsünün üstünde.
        const string AraShader = @"#version 130
uniform sampler2D A;
uniform sampler2D B;
uniform sampler2D MV;
uniform vec2 px;
uniform vec2 blok;
uniform float t;
vec2 orta3(vec2 a, vec2 b, vec2 c) { return max(min(a, b), min(max(a, b), c)); }
// Blok vektörü, yumuşatmadan (tam değer): kenarda 32 ile 0 yan yana gelince bilinear okuma 16 gibi
// ikisi de yanlış bir değer üretip doğru adayı listeden düşürüyordu.
vec2 blokMV(ivec2 b) { return texelFetch(MV, clamp(b, ivec2(0), ivec2(round(1.0 / blok)) - 1), 0).xy; }
vec3 cr(sampler2D s, vec2 uv) {
    vec2 p = uv / px;
    vec2 t1 = floor(p - 0.5) + 0.5;
    vec2 f = p - t1;
    vec2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    vec2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    vec2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    vec2 w3 = f * f * (-0.5 + 0.5 * f);
    vec2 w12 = w1 + w2;
    vec2 a = (t1 - 1.0) * px, b = (t1 + w2 / w12) * px, c = (t1 + 2.0) * px;
    vec3 r = textureLod(s, vec2(a.x, a.y), 0.0).rgb * (w0.x * w0.y)
           + textureLod(s, vec2(b.x, a.y), 0.0).rgb * (w12.x * w0.y)
           + textureLod(s, vec2(c.x, a.y), 0.0).rgb * (w3.x * w0.y)
           + textureLod(s, vec2(a.x, b.y), 0.0).rgb * (w0.x * w12.y)
           + textureLod(s, vec2(b.x, b.y), 0.0).rgb * (w12.x * w12.y)
           + textureLod(s, vec2(c.x, b.y), 0.0).rgb * (w3.x * w12.y)
           + textureLod(s, vec2(a.x, c.y), 0.0).rgb * (w0.x * w3.y)
           + textureLod(s, vec2(b.x, c.y), 0.0).rgb * (w12.x * w3.y)
           + textureLod(s, vec2(c.x, c.y), 0.0).rgb * (w3.x * w3.y);
    return clamp(r, 0.0, 1.0);
}
float parlak(sampler2D s, vec2 uv) { return dot(textureLod(s, uv, 0.0).rgb, vec3(0.299, 0.587, 0.114)); }
// Aday hareketin bu pikselin çevresinde (5 noktalı artı) iki kareyi ne kadar iyi eşleştirdiği
// Aday hareketin bu pikselin çevresinde (3x3 nokta, 5x5 px) iki kareyi ne kadar iyi eşleştirdiği
float hata(vec2 uv, vec2 v) {
    vec2 a = uv - v * t, b = uv + v * (1.0 - t);
    float s = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++) {
            vec2 o = vec2(float(x), float(y)) * 2.0 * px;
            s += abs(parlak(A, a + o) - parlak(B, b + o));
        }
    return s / 9.0;
}
void main() {
    vec2 uv = gl_TexCoord[0].st;
    // Piksel başına hareket seçimi: blok kenarında (nesne sınırı) doğru komşunun hareketi, sabit yazı/HUD/skor için
    // sıfır hareket seçilir. Bloğun tamamına tek vektör uygulamak kare kare bozulma (pikselleşme) yapıyordu.
    ivec2 bi = ivec2(floor(uv / blok));
    vec2 aday[7];
    aday[1] = blokMV(bi);
    aday[2] = blokMV(bi + ivec2(1, 0));
    aday[3] = blokMV(bi - ivec2(1, 0));
    aday[4] = blokMV(bi + ivec2(0, 1));
    aday[5] = blokMV(bi - ivec2(0, 1));
    aday[6] = vec2(0.0);
    // Bloğun ve 4 komşusunun ortancası: tek başına yanlış çıkan blok vektörlerini eler
    aday[0] = orta3(aday[1], max(min(aday[2], aday[3]), min(aday[4], aday[5])), min(max(aday[2], aday[3]), max(aday[4], aday[5])));
    vec2 v = aday[0] * px;
    float en = hata(uv, v), ec = en;
    for (int i = 1; i < 7; i++) {
        vec2 c = aday[i] * px;
        float e2 = hata(uv, c);
        // ortancadan sapan aday ancak belirgin şekilde daha iyiyse seçilir (düz alanda dokuyu silmesin)
        float c2 = e2 + 0.01 + 0.0005 * length(aday[i] - aday[0]);
        if (c2 < ec) { ec = c2; en = e2; v = c; }
    }
    vec3 a = cr(A, uv - v * t);
    vec3 b = cr(B, uv + v * (1.0 - t));
    vec3 yedek = mix(textureLod(A, uv, 0.0).rgb, textureLod(B, uv, 0.0).rgb, t);
    gl_FragColor = vec4(mix(mix(a, b, t), yedek, smoothstep(0.10, 0.25, en)), 1.0);
}";

        uint easuProg, rcasProg, hareketProg, araProg;
        bool shaderHazir;      // FSR shader'ları derlendi (yoksa düz bilinear çizim)
        uint fsrDoku;          // EASU çıktısı, ekrandaki görüntü boyutunda
        int fsrW, fsrH;
        glUseProgramD glUseProgram;
        glUniform1iD glUniform1i;
        glUniform1fD glUniform1f;
        glUniform2fD glUniform2f;
        glDeleteProgramD glDeleteProgram;
        glGetUniformLocationD glGetUniformLocation;

        uint ProgramDerle(string koseKod, string pikselKod)
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

            uint p = glCreateProgram();
            string[] kodlar = { koseKod, pikselKod };
            uint[] tipler = { 0x8B31, 0x8B30 }; // VERTEX_SHADER, FRAGMENT_SHADER
            for (int i = 0; i < 2; i++)
            {
                uint sh = glCreateShader(tipler[i]);
                glShaderSource(sh, 1, new[] { kodlar[i] }, null);
                glCompileShader(sh);
                int ok;
                glGetShaderiv(sh, 0x8B81, out ok); // COMPILE_STATUS
                if (ok == 0)
                {
                    var log = new System.Text.StringBuilder(2048); int n;
                    glGetShaderInfoLog(sh, log.Capacity, out n, log);
                    throw new Exception("shader: " + log);
                }
                glAttachShader(p, sh);
            }
            glLinkProgram(p);
            int bagli;
            glGetProgramiv(p, 0x8B82, out bagli); // LINK_STATUS
            if (bagli == 0) throw new Exception("shader bağlanamadı");
            return p;
        }

        void ShaderHazirla()
        {
            glUseProgram = Fonk<glUseProgramD>("glUseProgram");
            glUniform1i = Fonk<glUniform1iD>("glUniform1i");
            glUniform1f = Fonk<glUniform1fD>("glUniform1f");
            glUniform2f = Fonk<glUniform2fD>("glUniform2f");
            glDeleteProgram = Fonk<glDeleteProgramD>("glDeleteProgram");
            glGetUniformLocation = Fonk<glGetUniformLocationD>("glGetUniformLocation");
            glActiveTexture = Fonk<glActiveTextureD>("glActiveTexture");
            var glGenFramebuffers = Fonk<glGenFramebuffersD>("glGenFramebuffers");
            glBindFramebuffer = Fonk<glBindFramebufferD>("glBindFramebuffer");
            glFramebufferTexture2D = Fonk<glFramebufferTexture2DD>("glFramebufferTexture2D");
            glCheckFramebufferStatus = Fonk<glCheckFramebufferStatusD>("glCheckFramebufferStatus");
            glGenerateMipmap = Fonk<glGenerateMipmapD>("glGenerateMipmap");
            glDeleteFramebuffers = Fonk<glDeleteFramebuffersD>("glDeleteFramebuffers");
            easuProg = ProgramDerle(KoseShader130, EasuShader);
            rcasProg = ProgramDerle(KoseShader130, RcasShader);
            glGenFramebuffers(1, out fbo);
            shaderHazir = true;
        }

        // --- Ara kare kaynakları
        const uint FB = 0x8D40, RENK0 = 0x8CE0, BIRIM0 = 0x84C0;
        glActiveTextureD glActiveTexture;
        glBindFramebufferD glBindFramebuffer;
        glFramebufferTexture2DD glFramebufferTexture2D;
        glGenerateMipmapD glGenerateMipmap;
        glDeleteFramebuffersD glDeleteFramebuffers;
        glCheckFramebufferStatusD glCheckFramebufferStatus;
        uint fbo, araDoku, hareketDoku;
        int bw, bh;   // hareket alanı boyutu (blok başına bir vektör)

        uint YeniDoku(int dw, int dh, int ic, uint tip)
        {
            uint d;
            glGenTextures(1, out d);
            glBindTexture(TEX2D, d);
            glTexParameteri(TEX2D, 0x2801, 0x2601); // MIN_FILTER = LINEAR
            glTexParameteri(TEX2D, 0x2800, 0x2601); // MAG_FILTER = LINEAR
            glTexParameteri(TEX2D, 0x2802, 0x812F); // WRAP_S = CLAMP_TO_EDGE
            glTexParameteri(TEX2D, 0x2803, 0x812F); // WRAP_T = CLAMP_TO_EDGE
            glTexImage2D(TEX2D, 0, ic, dw, dh, 0, BGRA, tip, IntPtr.Zero);
            return d;
        }

        void AraKareHazirla()
        {
            hareketProg = ProgramDerle(KoseShader130, HareketShader);
            araProg = ProgramDerle(KoseShader130, AraShader);
            bw = (w + blokPx - 1) / blokPx; bh = (h + blokPx - 1) / blokPx;
            araDoku = YeniDoku(w, h, (int)RGBA8, UBYTE);
            hareketDoku = YeniDoku(bw, bh, 0x881A, 0x1406); // RGBA16F, FLOAT: piksel cinsinden vektör
            foreach (uint d in new[] { araDoku, hareketDoku })
            {
                glBindFramebuffer(FB, fbo);
                glFramebufferTexture2D(FB, RENK0, TEX2D, d, 0);
                if (glCheckFramebufferStatus(FB) != 0x8CD5) throw new Exception("FBO desteklenmiyor"); // COMPLETE
            }
            glBindFramebuffer(FB, 0);
        }

        void Bagla(uint a, uint b)
        {
            glActiveTexture(BIRIM0 + 1); glBindTexture(TEX2D, b);
            glActiveTexture(BIRIM0 + 2); glBindTexture(TEX2D, hareketDoku);
            glActiveTexture(BIRIM0);     glBindTexture(TEX2D, a);
            glBindFramebuffer(FB, fbo);
        }

        void Coz()
        {
            glBindFramebuffer(FB, 0);
            glUseProgram(0);
            glActiveTexture(BIRIM0 + 2); glBindTexture(TEX2D, 0);
            glActiveTexture(BIRIM0 + 1); glBindTexture(TEX2D, 0);
            glActiveTexture(BIRIM0);
        }

        // A (önceki) -> B (yeni) hareketini hareketDoku'ya yazar (her gerçek kare için bir kez)
        void HareketHesapla(uint a, uint b)
        {
            Bagla(a, b);
            glFramebufferTexture2D(FB, RENK0, TEX2D, hareketDoku, 0);
            glViewport(0, 0, bw, bh);
            glUseProgram(hareketProg);
            glUniform1i(glGetUniformLocation(hareketProg, "A"), 0);
            glUniform1i(glGetUniformLocation(hareketProg, "B"), 1);
            glUniform2f(glGetUniformLocation(hareketProg, "px"), 1f / w, 1f / h);
            glUniform1f(glGetUniformLocation(hareketProg, "blokPx"), blokPx);
            Dortgen(0f, 1f);
            Coz();
        }

        // A ile B arasında t (0..1) anındaki ara kareyi araDoku'ya çizer
        void AraKareCiz(uint a, uint b, float t)
        {
            Bagla(a, b);
            glFramebufferTexture2D(FB, RENK0, TEX2D, araDoku, 0);
            glViewport(0, 0, w, h);
            glUseProgram(araProg);
            glUniform1i(glGetUniformLocation(araProg, "A"), 0);
            glUniform1i(glGetUniformLocation(araProg, "B"), 1);
            glUniform1i(glGetUniformLocation(araProg, "MV"), 2);
            glUniform2f(glGetUniformLocation(araProg, "px"), 1f / w, 1f / h);
            glUniform2f(glGetUniformLocation(araProg, "blok"), 1f / bw, 1f / bh);
            glUniform1f(glGetUniformLocation(araProg, "t"), t);
            Dortgen(0f, 1f);
            Coz();
        }

        // Tüm alanı kaplayan dörtgen; alt/ust = dokunun dikey yönü
        static void Dortgen(float alt, float ust)
        {
            glBegin(DORTGEN);
            glTexCoord2f(0, alt); glVertex2f(-1, -1);
            glTexCoord2f(1, alt); glVertex2f(1, -1);
            glTexCoord2f(1, ust); glVertex2f(1, 1);
            glTexCoord2f(0, ust); glVertex2f(-1, 1);
            glEnd();
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
        public volatile bool Doldur;
        public volatile float Keskinlik;   // 0 = kapalı (düz bilinear), 0..1 = keskinleştirme gücü
        public string HataMetni;
        readonly bool tamponlu;            // true = akıcı (eşit aralıklı), false = kare gelir gelmez çiz
        readonly bool araKareIstek;        // gerçek karelerin arasına üretilmiş kare ekle
        readonly int carpan;               // 2 = her gerçek kareye 1 ara kare, 3 = 2, 4 = 3
        readonly int blokPx;               // hareket bloğu: 16 (normal) ya da 8 (yüksek kalite, 4 kat iş)
        public volatile bool AraKareCalisiyor;
        public int Carpan { get { return AraKareCalisiyor ? carpan : 1; } } // ekrana çıkan FPS / kaynak FPS
        readonly uint[] kaynak = new uint[2]; // son iki gerçek kare (önceki / yeni)

        public Sunucu(IntPtr hwnd, int w, int h, double fps, bool asagidanYukari, bool tamponlu, int carpan, bool yuksekKalite)
        {
            this.hwnd = hwnd; this.w = w; this.h = h;
            this.asagidanYukari = asagidanYukari;
            araKareIstek = carpan >= 2;
            this.tamponlu = tamponlu || araKareIstek; // ara kare eşit aralıklı çizim gerektirir
            this.carpan = Math.Max(1, Math.Min(4, carpan));
            blokPx = yuksekKalite ? 8 : 16;
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
                else if (kuyruk.Count >= 4) hedef = kuyruk.Dequeue(); // çizici takıldıysa en eskiyi feda et
                else { hedef = Marshal.AllocHGlobal(boyut); hepsi.Add(hedef); }
            }
            Kopyala(hedef, buf, (IntPtr)Math.Min(len, boyut));
            lock (kilit) kuyruk.Enqueue(hedef);
            geldi.Set();
            return 0;
        }

        void Dongu()
        {
            IntPtr dc = GetDC(hwnd), rc = IntPtr.Zero;
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

                glPixelStorei(0x0CF5, 4); // UNPACK_ALIGNMENT
                kaynak[0] = YeniDoku(w, h, (int)RGBA8, UBYTE);
                kaynak[1] = YeniDoku(w, h, (int)RGBA8, UBYTE);
                glEnable(TEX2D);

                // FSR 1 (keskinleştirme) shader'ları; derlenemezse düz çizime devam
                try { ShaderHazirla(); }
                catch { shaderHazir = false; } // shader yoksa keskinleştirmesiz düz çizim

                // Ara kare: kurulamazsa (eski sürücü) normal akıcı moda düşer
                if (araKareIstek && shaderHazir)
                {
                    try
                    {
                        AraKareHazirla();
                        foreach (uint d in kaynak) { glBindTexture(TEX2D, d); glTexParameteri(TEX2D, 0x2801, 0x2703); } // MIN = LINEAR_MIPMAP_LINEAR
                        AraKareCalisiyor = true;
                    }
                    catch { AraKareCalisiyor = false; }
                }

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
                    Ciz(dc, 0);
                    geldi.WaitOne(50);
                }

                // Ara kare açıksa her gerçek kare N adıma bölünür: [A..B arası 1/N, 2/N ... ara kareler, gerçek B], [B..C ...]
                int n = Carpan;
                double adim = aralik / n;
                double sonraki = saat.Elapsed.TotalSeconds, ortKuyruk = 1.5;
                IntPtr son = IntPtr.Zero;
                int sonSlot = -1;          // en son yüklenen gerçek karenin dokusu
                uint onceki = 0, yeniDoku = 0;
                bool araGecerli = false;   // bu turda A..B arası ara kare üretilebilir mi
                int adimNo = 0;
                while (calis)
                {
                    // Bir sonraki adım zamanına kadar bekle (son milisaniyede hassas bekleme)
                    double simdi;
                    while (calis && (simdi = saat.Elapsed.TotalSeconds) < sonraki)
                    {
                        if (sonraki - simdi > 0.0025) Thread.Sleep(1); else Thread.SpinWait(200);
                    }
                    if (!calis) break;

                    uint goster = sonSlot >= 0 ? kaynak[sonSlot] : 0;
                    if (adimNo == 0)
                    {
                        IntPtr yeni = IntPtr.Zero;
                        int q;
                        lock (kilit)
                        {
                            q = kuyruk.Count;
                            if (q > 0)
                            {
                                yeni = kuyruk.Dequeue();
                                while (kuyruk.Count > 2) havuz.Push(kuyruk.Dequeue()); // gecikme birikmesin
                            }
                        }
                        ortKuyruk = ortKuyruk * 0.97 + q * 0.03;

                        araGecerli = false;
                        if (yeni != IntPtr.Zero)
                        {
                            int slot = sonSlot < 0 ? 0 : 1 - sonSlot;
                            glBindTexture(TEX2D, kaynak[slot]);
                            glTexSubImage2D(TEX2D, 0, 0, 0, w, h, BGRA, UBYTE, yeni);
                            lock (kilit) if (son != IntPtr.Zero) havuz.Push(son);
                            son = yeni;
                            if (n > 1)
                            {
                                glGenerateMipmap(TEX2D);
                                if (sonSlot >= 0) { onceki = kaynak[sonSlot]; HareketHesapla(onceki, kaynak[slot]); araGecerli = true; }
                            }
                            sonSlot = slot;
                            yeniDoku = kaynak[slot];
                            if (araGecerli) { AraKareCiz(onceki, yeniDoku, 1f / n); goster = araDoku; }
                            else goster = yeniDoku;
                            Interlocked.Increment(ref Cizilen);
                        }
                    }
                    else if (araGecerli)
                    {
                        if (adimNo < n - 1) { AraKareCiz(onceki, yeniDoku, (adimNo + 1f) / n); goster = araDoku; }
                        else goster = yeniDoku; // turun son adımı: gerçek kare
                        Interlocked.Increment(ref Cizilen);
                    }

                    Ciz(dc, goster);
                    adimNo = (adimNo + 1) % n;

                    // Kartın saati bizimkinden biraz hızlı/yavaşsa kuyruğa bakıp hızı hafifçe ayarla (en fazla %2)
                    double duzelt = Math.Max(-0.02, Math.Min(0.02, (ortKuyruk - 1.5) * 0.01));
                    sonraki += adim * (1 - duzelt);
                    if (saat.Elapsed.TotalSeconds - sonraki > aralik) sonraki = saat.Elapsed.TotalSeconds; // uzun duraklamadan sonra yeniden hizala
                }
            }
            catch (Exception ex) { HataMetni = ex.Message; }
            finally
            {
                try
                {
                    if (glDeleteProgram != null)
                    {
                        glUseProgram(0);
                        foreach (uint p in new[] { easuProg, rcasProg, hareketProg, araProg }) if (p != 0) glDeleteProgram(p);
                    }
                    if (fbo != 0) glDeleteFramebuffers(1, ref fbo);
                    foreach (uint d in new[] { kaynak[0], kaynak[1], araDoku, hareketDoku, fsrDoku }) { uint x = d; if (x != 0) glDeleteTextures(1, ref x); }
                    wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                    if (rc != IntPtr.Zero) wglDeleteContext(rc);
                }
                catch { }
                ReleaseDC(hwnd, dc);
                timeEndPeriod(1);
            }
        }

        // Düşük gecikme: tampon yok, her kare geldiği an çizilir
        void AnindaDongu(IntPtr dc)
        {
            IntPtr son = IntPtr.Zero;
            Ciz(dc, 0);
            while (calis)
            {
                if (!geldi.WaitOne(100)) { if (son != IntPtr.Zero) Ciz(dc, kaynak[0]); continue; } // pencere boyutu değişirse yine çiz
                IntPtr yeni = IntPtr.Zero;
                lock (kilit)
                {
                    while (kuyruk.Count > 1) havuz.Push(kuyruk.Dequeue()); // hep en yeni kare
                    if (kuyruk.Count > 0) yeni = kuyruk.Dequeue();
                }
                if (yeni == IntPtr.Zero) continue;
                glBindTexture(TEX2D, kaynak[0]);
                glTexSubImage2D(TEX2D, 0, 0, 0, w, h, BGRA, UBYTE, yeni);
                lock (kilit) if (son != IntPtr.Zero) havuz.Push(son);
                son = yeni;
                Interlocked.Increment(ref Cizilen);
                Ciz(dc, kaynak[0]);
            }
        }

        // doku = 0 ise sadece siyah ekran
        void Ciz(IntPtr dc, uint doku)
        {
            RECT r;
            GetClientRect(hwnd, out r);
            int cw = r.R, ch = r.B;
            if (cw <= 0 || ch <= 0) return;
            glViewport(0, 0, cw, ch);
            glClearColor(0, 0, 0, 1);
            glClear(RENK);
            if (doku != 0)
            {
                int vw = cw, vh = ch;
                if (!Doldur)
                {
                    vh = (int)((long)cw * h / w);
                    if (vh > ch) { vh = ch; vw = (int)((long)ch * w / h); }
                }
                int vx = (cw - vw) / 2, vy = (ch - vh) / 2;
                float guc = Keskinlik;
                glBindTexture(TEX2D, doku);
                if (shaderHazir && guc > 0)
                {
                    // 1) EASU: kaynağı ekrandaki boyuta büyüt (küçültmede düz bilinear yeter), dokunun kendi yönünde
                    if (fsrDoku == 0 || fsrW != vw || fsrH != vh)
                    {
                        if (fsrDoku != 0) glDeleteTextures(1, ref fsrDoku);
                        fsrDoku = YeniDoku(vw, vh, (int)RGBA8, UBYTE);
                        fsrW = vw; fsrH = vh;
                        glBindTexture(TEX2D, doku);
                    }
                    glBindFramebuffer(FB, fbo);
                    glFramebufferTexture2D(FB, RENK0, TEX2D, fsrDoku, 0);
                    glViewport(0, 0, vw, vh);
                    if (vw > w || vh > h)
                    {
                        glUseProgram(easuProg);
                        glUniform1i(glGetUniformLocation(easuProg, "tex"), 0);
                        glUniform2f(glGetUniformLocation(easuProg, "girdi"), w, h);
                        glUniform2f(glGetUniformLocation(easuProg, "cikti"), vw, vh);
                    }
                    else glUseProgram(0);
                    Dortgen(0f, 1f);
                    glBindFramebuffer(FB, 0);

                    // 2) RCAS: büyütülmüş görüntüyü ekrana keskinleştirerek çiz
                    glViewport(vx, vy, vw, vh);
                    glBindTexture(TEX2D, fsrDoku);
                    glUseProgram(rcasProg);
                    glUniform1i(glGetUniformLocation(rcasProg, "tex"), 0);
                    glUniform2f(glGetUniformLocation(rcasProg, "ofs"), vx, vy);
                    glUniform2f(glGetUniformLocation(rcasProg, "boyut"), vw, vh);
                    glUniform1f(glGetUniformLocation(rcasProg, "ters"), asagidanYukari ? 0f : 1f);
                    glUniform1f(glGetUniformLocation(rcasProg, "keskin"), (float)Math.Pow(2, -2 * (1 - Math.Min(1f, guc)))); // Güçlü = 1, Hafif ≈ 0,25
                    Dortgen(0f, 1f);
                    glUseProgram(0);
                }
                else
                {
                    glViewport(vx, vy, vw, vh);
                    if (shaderHazir) glUseProgram(0);
                    Dortgen(asagidanYukari ? 0f : 1f, asagidanYukari ? 1f : 0f);
                }
            }
            SwapBuffers(dc);
        }
    }
}
