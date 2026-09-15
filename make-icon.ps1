$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $dir 'app.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()

function New-RoundedPath([single]$left, [single]$top, [single]$right, [single]$bottom, [single]$radius) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $radius * 2
  $path.AddArc($left, $top, $d, $d, 180, 90)
  $path.AddArc($right - $d, $top, $d, $d, 270, 90)
  $path.AddArc($right - $d, $bottom - $d, $d, $d, 0, 90)
  $path.AddArc($left, $bottom - $d, $d, $d, 90, 90)
  $path.CloseFigure()
  return $path
}

foreach ($size in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)

  $s = [single]$size
  $pad = [single]($s * 0.055)
  $radius = [single]($s * 0.20)
  $path = New-RoundedPath $pad $pad ($s - $pad) ($s - $pad) $radius
  $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 31, 38, 43))
  $g.FillPath($bg, $path)
  $edge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 240, 198, 116), [single][Math]::Max(1, $s * 0.035))
  $g.DrawPath($edge, $path)

  $face = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 244, 246, 248))
  $ring = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 240, 198, 116), [single][Math]::Max(1, $s * 0.045))
  $cx = [single]($s * 0.48)
  $cy = [single]($s * 0.47)
  $r = [single]($s * 0.28)
  $g.FillEllipse($face, $cx - $r, $cy - $r, $r * 2, $r * 2)
  $g.DrawEllipse($ring, $cx - $r, $cy - $r, $r * 2, $r * 2)

  $hand = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 31, 38, 43), [single][Math]::Max(1, $s * 0.055))
  $hand.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $hand.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawLine($hand, $cx, $cy, $cx, $cy - $r * 0.62)
  $g.DrawLine($hand, $cx, $cy, $cx + $r * 0.48, $cy + $r * 0.18)

  $coinR = [single]($s * 0.18)
  $coinX = [single]($s * 0.73)
  $coinY = [single]($s * 0.73)
  $coinBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 198, 116))
  $coinEdge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 31, 38, 43), [single][Math]::Max(1, $s * 0.025))
  $g.FillEllipse($coinBrush, $coinX - $coinR, $coinY - $coinR, $coinR * 2, $coinR * 2)
  $g.DrawEllipse($coinEdge, $coinX - $coinR, $coinY - $coinR, $coinR * 2, $coinR * 2)
  if ($size -ge 24) {
    $font = New-Object System.Drawing.Font('Microsoft YaHei UI', [single]($s * 0.20), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 31, 38, 43))
    $rect = New-Object System.Drawing.RectangleF(($coinX - $coinR), ($coinY - $coinR), ($coinR * 2), ($coinR * 2))
    $g.DrawString('¥', $font, $textBrush, $rect, $format)
    $textBrush.Dispose(); $font.Dispose()
  }

  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $images += ,$ms.ToArray()
  $ms.Dispose()
  $edge.Dispose(); $bg.Dispose(); $face.Dispose(); $ring.Dispose(); $hand.Dispose(); $coinBrush.Dispose(); $coinEdge.Dispose(); $path.Dispose(); $g.Dispose(); $bmp.Dispose()
}

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $size = $sizes[$i]
  $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
  $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
  $writer.Write([byte]0)
  $writer.Write([byte]0)
  $writer.Write([uint16]1)
  $writer.Write([uint16]32)
  $writer.Write([uint32]$images[$i].Length)
  $writer.Write([uint32]$offset)
  $offset += $images[$i].Length
}
foreach ($bytes in $images) { $writer.Write($bytes) }
$writer.Flush()
[System.IO.File]::WriteAllBytes($out, $stream.ToArray())
$writer.Dispose(); $stream.Dispose()
"icon: $out"
