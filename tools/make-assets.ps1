Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$src  = Join-Path $root 'logo.png'
$out  = Join-Path $root 'MT.PhotoViewer\Assets\Logo'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$img = [System.Drawing.Image]::FromFile($src)

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$path) {
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function New-Scaled([System.Drawing.Image]$image, [int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.SmoothingMode     = 'HighQuality'
    $g.PixelOffsetMode   = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($image, (New-Object System.Drawing.Rectangle 0, 0, $w, $h))
    $g.Dispose()
    return $bmp
}

# --- Banner (full logo) ---
$banner = New-Scaled $img 1200 400
Save-Png $banner (Join-Path $out 'MT_PhotoViewer_Logo_Banner.png')
$banner.Dispose()

# --- Square mark: crop the camera/shutter emblem ---
$cropRect = New-Object System.Drawing.Rectangle 161, 61, 595, 595
$mark = New-Object System.Drawing.Bitmap 595, 595
$g = [System.Drawing.Graphics]::FromImage($mark)
$g.InterpolationMode = 'HighQualityBicubic'
$g.DrawImage($img, (New-Object System.Drawing.Rectangle 0, 0, 595, 595), $cropRect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()

$logo256 = New-Scaled $mark 256 256
Save-Png $logo256 (Join-Path $out 'MT_PhotoViewer_Logo.png')

$logo64 = New-Scaled $mark 64 64
Save-Png $logo64 (Join-Path $out 'MT_PhotoViewer_Logo_Small.png')
$logo64.Dispose()

# --- Multi-size .ico (PNG-compressed entries, Vista+ format) ---
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $b = New-Scaled $mark $s $s
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,@($s, $ms.ToArray())
    $ms.Dispose()
    $b.Dispose()
}

$icoPath = Join-Path $out 'MT_PhotoViewer_Logo.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0)                 # reserved
$bw.Write([UInt16]1)                 # type = icon
$bw.Write([UInt16]$pngs.Count)       # image count

$offset = 6 + (16 * $pngs.Count)
foreach ($p in $pngs) {
    $size = $p[0]; $data = $p[1]
    $dim = 0
    if ($size -lt 256) { $dim = $size }
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)               # palette
    $bw.Write([Byte]0)               # reserved
    $bw.Write([UInt16]1)             # color planes
    $bw.Write([UInt16]32)            # bits per pixel
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($p in $pngs) { $bw.Write($p[1]) }
$bw.Flush(); $bw.Dispose(); $fs.Dispose()

$logo256.Dispose()
$mark.Dispose()
$img.Dispose()

Get-ChildItem $out | Select-Object Name, Length | Format-Table -AutoSize
