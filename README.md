<p align="center"><img src="logo.png" width="96" alt="Basic Capture logo"></p>

<h1 align="center">Basic Capture</h1>

<p align="center">USB capture kartları ve kameralar için küçük, düşük gecikmeli bir görüntüleyici.<br>
<i>A tiny, low-latency viewer for USB capture cards and webcams.</i></p>

---

OBS gerektirmeden capture kartındaki görüntüyü ekranda gösterir. Tek bir exe dosyası, kurulum yok, ek paket yok (yalnızca Windows'un içindeki .NET Framework 4.x).

## Özellikler

- **Düşük gecikme:** Kareler geldiği an ekran kartıyla (OpenGL) çizilir, bekletilmez.
- **Akıcı mod:** Kareleri küçük bir tamponda tutup ekran kartıyla tam eşit aralıklarla çizer. Düzensiz kare gönderen kartlarda takılmayı yok eder (+1 kare gecikme).
- **Keskinleştirme:** GPU'da Catmull-Rom ölçekleme + AMD FidelityFX CAS. 720p görüntüyü büyük ekranda daha net gösterir (Kapalı / Hafif / Orta / Güçlü).
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

Basic Capture shows the video from a USB capture card or webcam without OBS. Single exe, no installer, no dependencies beyond the .NET Framework 4.x built into Windows. Features: low-latency GPU (OpenGL) rendering, an optional *smooth mode* (paced GPU presentation that removes stutter from cards that deliver frames unevenly, +1 frame latency), GPU sharpening (Catmull-Rom upscaling + AMD FidelityFX CAS), FPS counter, volume control, full screen, Turkish/English UI. Build with `build.ps1`; prebuilt exe is on the Releases page.
