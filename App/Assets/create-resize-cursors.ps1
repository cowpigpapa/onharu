$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-ResizeCursor([string]$Path, [string]$Direction) {
    $size = 32
    $bitmap = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $a = switch ($Direction) {
        'nesw' { New-Object Drawing.PointF 25, 7 }
        'horizontal' { New-Object Drawing.PointF 5, 16 }
        'vertical' { New-Object Drawing.PointF 16, 5 }
        default { New-Object Drawing.PointF 7, 7 }
    }
    $b = switch ($Direction) {
        'nesw' { New-Object Drawing.PointF 7, 25 }
        'horizontal' { New-Object Drawing.PointF 27, 16 }
        'vertical' { New-Object Drawing.PointF 16, 27 }
        default { New-Object Drawing.PointF 25, 25 }
    }
    foreach ($spec in @(@([Drawing.Color]::FromArgb(235,255,255,255), 6.0), @([Drawing.Color]::FromArgb(255,79,70,229), 2.6))) {
        $pen = New-Object Drawing.Pen $spec[0], $spec[1]
        $pen.StartCap = [Drawing.Drawing2D.LineCap]::ArrowAnchor
        $pen.EndCap = [Drawing.Drawing2D.LineCap]::ArrowAnchor
        $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
        $graphics.DrawLine($pen, $a, $b)
        $pen.Dispose()
    }
    $graphics.Dispose()
    $rect = New-Object Drawing.Rectangle 0, 0, $size, $size
    $data = $bitmap.LockBits($rect, [Drawing.Imaging.ImageLockMode]::ReadOnly, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stream = [IO.File]::Create($Path); $writer = New-Object IO.BinaryWriter $stream
    try {
        $imageBytes = 40 + $size * $size * 4 + $size * 4
        $writer.Write([uint16]0); $writer.Write([uint16]2); $writer.Write([uint16]1)
        $writer.Write([byte]$size); $writer.Write([byte]$size); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]16); $writer.Write([uint16]16); $writer.Write([uint32]$imageBytes); $writer.Write([uint32]22)
        $writer.Write([uint32]40); $writer.Write([int32]$size); $writer.Write([int32]($size * 2)); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]0); $writer.Write([uint32]($size * $size * 4)); $writer.Write([int32]0); $writer.Write([int32]0); $writer.Write([uint32]0); $writer.Write([uint32]0)
        $row = New-Object byte[] ($size * 4)
        for ($y = $size - 1; $y -ge 0; $y--) {
            [Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $row, 0, $row.Length)
            $writer.Write($row)
        }
        $writer.Write((New-Object byte[] ($size * 4)))
    }
    finally { $writer.Dispose(); $bitmap.UnlockBits($data); $bitmap.Dispose() }
}

New-ResizeCursor (Join-Path $PSScriptRoot 'resize-nwse.cur') 'nwse'
New-ResizeCursor (Join-Path $PSScriptRoot 'resize-nesw.cur') 'nesw'
New-ResizeCursor (Join-Path $PSScriptRoot 'resize-horizontal.cur') 'horizontal'
New-ResizeCursor (Join-Path $PSScriptRoot 'resize-vertical.cur') 'vertical'
