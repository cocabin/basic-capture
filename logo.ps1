# Basic Capture logosunu çizer: logo.ico (exe simgesi) ve logo.png (önizleme)
# Tasarım: koyu yuvarlak kare, beyaz vizör köşeleri, ortada kırmızı kayıt noktası
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-Logo([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $s / 256.0
    $kucuk = $s -lt 32

    # Zemin: koyu antrasit, üstten alta hafif geçiş
    $bg = New-RoundedPath (4*$k) (4*$k) (248*$k) (248*$k) (56*$k)
    $rect = New-Object System.Drawing.RectangleF 0, 0, $s, $s
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 48, 52, 63)), ([System.Drawing.Color]::FromArgb(255, 17, 19, 26)), 90.0
    $g.FillPath($grad, $bg)

    # Vizör köşeleri
    $kal = if ($kucuk) { 26*$k } else { 18*$k }
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([float]$kal)
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $a = if ($kucuk) { 46*$k } else { 54*$k }   # köşe uzaklığı
    $b = 256*$k - $a
    $l = if ($kucuk) { 52*$k } else { 46*$k }   # kol uzunluğu
    foreach ($c in @(@($a, $a, 1, 1), @($b, $a, -1, 1), @($a, $b, 1, -1), @($b, $b, -1, -1))) {
        $x = [float]$c[0]; $y = [float]$c[1]; $dx = $c[2]; $dy = $c[3]
        $pts = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF $x, ([float]($y + $dy*$l))),
            (New-Object System.Drawing.PointF $x, $y),
            (New-Object System.Drawing.PointF ([float]($x + $dx*$l)), $y)
        )
        $g.DrawLines($pen, $pts)
    }

    # Ortada kırmızı kayıt noktası, hafif parlak halka
    $r = if ($kucuk) { 40*$k } else { 38*$k }
    $c0 = 128*$k
    if (-not $kucuk) {
        $halo = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 239, 68, 68))
        $g.FillEllipse($halo, [float]($c0 - $r - 14*$k), [float]($c0 - $r - 14*$k), [float](($r + 14*$k)*2), [float](($r + 14*$k)*2))
    }
    $red = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 239, 68, 68))
    $g.FillEllipse($red, [float]($c0 - $r), [float]($c0 - $r), [float]($r*2), [float]($r*2))

    $g.Dispose()
    return $bmp
}

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $bm = New-Logo $s
    $ms = New-Object System.IO.MemoryStream
    $bm.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($s -eq 256) { $bm.Save((Join-Path $dir 'logo.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bm.Dispose()
    ,$ms.ToArray()
}

# ICO dosyası: başlık + her boyut için bir kayıt + PNG verileri
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$len); $w.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $dir 'logo.ico'), $out.ToArray())
"logo.ico / logo.png OK"
