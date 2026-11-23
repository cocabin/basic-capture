<p align="center"><img src="logo.png" width="96" alt="Basic Capture logo"></p>

<h1 align="center">Basic Capture</h1>

<p align="center">USB capture kartları ve kameralar için küçük, düşük gecikmeli bir görüntüleyici.<br>
<i>A tiny, low-latency viewer for USB capture cards and webcams.</i></p>

---

OBS gerektirmeden capture kartındaki görüntüyü ekranda gösterir. Tek bir exe dosyası, kurulum yok, ek paket yok (yalnızca Windows'un içindeki .NET Framework 4.x).

## Özellikler

- **Düşük gecikme:** Kareler geldiği an ekran kartıyla (OpenGL) çizilir, bekletilmez.
- **Akıcı mod:** Kareleri küçük bir tamponda tutup ekran kartıyla tam eşit aralıklarla çizer. Düzensiz kare gönderen kartlarda takılmayı yok eder (+1 kare gecikme).
- **Ara kare üretimi (2x / 3x / 4x):** GPU'da hareket tahmini ile gerçek karelerin arasına 1-3 kare üretir; örneğin 30 FPS'i 60, 90 ya da 120 FPS gösterir. Kalite: Normal (16x16 blok) ya da Yüksek (8x8 blok, daha ince hareket). Gecikme ekler ve hızlı/karmaşık hareketlerde hatalı kare görülebilir; oyun yerine izleme/yayın için.
- **Keskinleştirme (AMD FSR 1):** GPU'da kenar uyumlu büyütme (EASU) + keskinleştirme (RCAS). 720p/1080p görüntüyü büyük ekranda daha net gösterir (Kapalı / Hafif / Orta / Güçlü).
- **FPS göstergesi:** Ekrana çizilen gerçek kare sayısı. Kapalı, tıklayınca ya da her zaman görünür.
- Ses seviyesi, tam ekran (doldur / oranı koru), cihaz ve çözünürlük/FPS seçimi.
- Türkçe / English arayüz, koyu tema, 4K ve yüksek DPI ekran desteği.
- Ayarlar hatırlanır (`BasicCapture.ayar`, exe ile aynı klasörde).

## Tuşlar

| Tuş | İşlev |
|---|---|
| Tıklama | Ayarlar butonunu ve ses çubuğunu göster |
| **F** / çift tık | Tam ekran (**Esc** ile çık) |
| **S** / sağ tık | Ayarlar menüsü |
| **↑ ↓** / **+ −** / tekerlek | Ses seviyesi |
| **M** | Sessiz |
| **A** | Akıcı mod |
| **K** | Keskinleştirme seviyesi |
| **G** | Ara kare üretimi: Kapalı → 2x → 3x → 4x |
| **I** | FPS göstergesi modu |
| **D** | Tam ekranda doldur / oranı koru |
| **Q** | Çıkış |

## İndirme

Derlenmiş `BasicCapture.exe` dosyası **Releases** sayfasında. İndirip çift tıklaman yeterli.

## Derleme

Visual Studio ya da .NET SDK gerekmez, Windows'un kendi C# derleyicisi kullanılır:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Logoyu yeniden çizmek için: `powershell -ExecutionPolicy Bypass -File logo.ps1`

## MS2109 çipli kartlar hakkında

Ucuz "4K" USB capture kartlarının çoğunda bulunan MS2109 çipi:

- 4K girişi kabul eder ama bilgisayara en fazla **1080p** gönderir (USB 2.0).
- 1280x720'den büyük çözünürlüklerde "60 FPS" bildirir ama gerçekte **30 kare**, düzensiz aralıklarla gönderir. Basic Capture bu kartı tanır ve sahte 60 FPS seçeneklerini gizler.
- Gerçek 60 FPS yalnızca **720p ve altında** mümkündür.
- 24 FPS desteklenmez (1080p'de 25, 720p'de 20 verir), bu kartta 24 FPS seçeneği gizlenir.

Öneri: Oyun için **720p 60** (+ Orta keskinleştirme), izleme/yayın için **1080p 30 + akıcı mod**.

## Komut satırı (geliştirme)

```
BasicCapture.exe --test            # kartı kısa süre açar, sonucu test-sonuc.txt'ye yazar
```

---

### English

Basic Capture shows the video from a USB capture card or webcam without OBS. Single exe, no installer, no dependencies beyond the .NET Framework 4.x built into Windows. Features: low-latency GPU (OpenGL) rendering, an optional *smooth mode* (paced GPU presentation that removes stutter from cards that deliver frames unevenly, +1 frame latency), GPU upscaling and sharpening with AMD FidelityFX Super Resolution 1 (EASU + RCAS), FPS counter, volume control, full screen, Turkish/English UI. Build with `build.ps1`; prebuilt exe is on the Releases page.

## Üçüncü taraf / Third-party

Keskinleştirme, AMD FidelityFX Super Resolution 1'in (EASU + RCAS) GLSL'e çevrilmiş halini kullanır.

> Copyright (c) 2021 Advanced Micro Devices, Inc. All rights reserved.
>
> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
