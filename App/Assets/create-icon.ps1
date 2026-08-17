$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Add-RoundedRectangle($path, [float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
}

$colors = @('#38BDF8', '#60A5FA', '#818CF8', '#34D399', '#22C55E', '#A3E635', '#FBBF24', '#FB923C', '#F472B6')
$images = @()

foreach ($size in @(16, 32, 48, 256)) {
    $scale = $size / 256.0
    $bitmap = New-Object Drawing.Bitmap($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)

    $card = New-Object Drawing.Drawing2D.GraphicsPath
    Add-RoundedRectangle $card (12 * $scale) (12 * $scale) (232 * $scale) (232 * $scale) (48 * $scale)
    $graphics.FillPath([Drawing.Brushes]::White, $card)
    $border = New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#BAE6FD'), [Math]::Max(1, 7 * $scale))
    $graphics.DrawPath($border, $card)

    $tileSize = 46 * $scale
    $gap = 10 * $scale
    for ($index = 0; $index -lt 9; $index++) {
        $row = [Math]::Floor($index / 3)
        $column = $index % 3
        $x = (49 * $scale) + $column * ($tileSize + $gap)
        $y = (49 * $scale) + $row * ($tileSize + $gap)
        $tile = New-Object Drawing.Drawing2D.GraphicsPath
        Add-RoundedRectangle $tile $x $y $tileSize $tileSize ([Math]::Max(1, 8 * $scale))
        $brush = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml($colors[$index]))
        $graphics.FillPath($brush, $tile)
        $brush.Dispose(); $tile.Dispose()
    }

    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $images += [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
    if ($size -eq 256) { $bitmap.Save((Join-Path $PSScriptRoot 'onharu-preview.png'), [Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $border.Dispose(); $card.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}

$output = Join-Path $PSScriptRoot 'onharu.ico'
$file = [IO.File]::Create($output)
$writer = New-Object IO.BinaryWriter($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $writer.Write([byte]($(if ($image.Size -eq 256) { 0 } else { $image.Size })))
    $writer.Write([byte]($(if ($image.Size -eq 256) { 0 } else { $image.Size })))
    $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Bytes.Length); $writer.Write([uint32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write($image.Bytes) }
$writer.Dispose(); $file.Dispose()
Write-Host "Created: $output"
