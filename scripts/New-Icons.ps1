$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$workspace = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $workspace 'src\ProxyChoiceApp\Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null

function Write-ProxyIcon([string]$Name, [string]$Hex) {
    $images = @()
    foreach ($size in @(16,32,48,64,128,256)) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.ScaleTransform($size / 64.0, $size / 64.0)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($Hex))
        $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $path.AddBezier(8,14,8,8,14,5,20,5)
        $path.AddLine(20,5,44,5)
        $path.AddBezier(44,5,51,5,56,10,56,16)
        $path.AddLine(56,16,56,36)
        $path.AddBezier(56,36,56,46,45,55,32,60)
        $path.AddBezier(32,60,19,55,8,46,8,36)
        $path.CloseFigure()
        $graphics.FillPath($brush,$path)
        $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White,4.5)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $graphics.DrawLine($pen,20,40,43,20)
        $graphics.DrawLine($pen,29,20,43,20)
        $graphics.DrawLine($pen,43,20,43,34)
        $memory = [System.IO.MemoryStream]::new()
        $bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png)
        $images += ,@{Size=$size;Bytes=$memory.ToArray()}
        $memory.Dispose(); $pen.Dispose(); $path.Dispose(); $brush.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    }
    $file = [System.IO.File]::Create((Join-Path $assets "$Name.ico"))
    $writer = [System.IO.BinaryWriter]::new($file)
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $image.Bytes.Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image.Bytes) }
    $writer.Dispose(); $file.Dispose()
}

Write-ProxyIcon 'disconnected' '#E05151'
Write-ProxyIcon 'connected' '#16A56B'
Copy-Item -LiteralPath (Join-Path $assets 'disconnected.ico') -Destination (Join-Path $assets 'app.ico') -Force

$flagFolder = Join-Path $assets 'Flags'
New-Item -ItemType Directory -Path $flagFolder -Force | Out-Null
function Draw-Stripe($graphics, [string]$Color, [single]$X, [single]$Y, [single]$Width, [single]$Height) {
    $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($Color))
    try { $graphics.FillRectangle($brush, $X, $Y, $Width, $Height) } finally { $brush.Dispose() }
}
function Draw-Star($graphics, [single]$X, [single]$Y, [single]$Radius, [single]$Rotation) {
    $points = [Drawing.PointF[]]::new(10)
    for ($index = 0; $index -lt 10; $index++) {
        $angle = $Rotation + $index * [Math]::PI / 5
        $distance = if ($index % 2 -eq 0) { $Radius } else { $Radius * 0.382 }
        $points[$index] = [Drawing.PointF]::new($X + $distance * [Math]::Cos($angle), $Y + $distance * [Math]::Sin($angle))
    }
    $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#FFDE00'))
    try { $graphics.FillPolygon($brush, $points) } finally { $brush.Dispose() }
}
foreach ($code in @('en','fr','es','zh','de','ru')) {
    $bitmap = [Drawing.Bitmap]::new(60, 40)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::White)
    switch ($code) {
        'en' {
            $graphics.Clear([Drawing.ColorTranslator]::FromHtml('#012169'))
            foreach ($color in @('#FFFFFF','#C8102E')) {
                $width = if ($color -eq '#FFFFFF') { 8 } else { 3 }
                $pen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($color), $width)
                $graphics.DrawLine($pen, 0, 0, 60, 40); $graphics.DrawLine($pen, 0, 40, 60, 0)
                $pen.Dispose()
            }
            Draw-Stripe $graphics '#FFFFFF' 0 14 60 12; Draw-Stripe $graphics '#FFFFFF' 24 0 12 40
            Draw-Stripe $graphics '#C8102E' 0 17 60 6; Draw-Stripe $graphics '#C8102E' 27 0 6 40
        }
        'fr' { Draw-Stripe $graphics '#002395' 0 0 20 40; Draw-Stripe $graphics '#ED2939' 40 0 20 40 }
        'es' { $graphics.Clear([Drawing.ColorTranslator]::FromHtml('#AA151B')); Draw-Stripe $graphics '#F1BF00' 0 10 60 20 }
        'zh' {
            $graphics.Clear([Drawing.ColorTranslator]::FromHtml('#DE2910'))
            Draw-Star $graphics 10 10 6 (-[Math]::PI / 2)
            foreach ($point in @(@(20,4),@(24,8),@(24,14),@(20,18))) {
                Draw-Star $graphics $point[0] $point[1] 2 ([Math]::Atan2(10-$point[1],10-$point[0]))
            }
        }
        'de' { Draw-Stripe $graphics '#000000' 0 0 60 14; Draw-Stripe $graphics '#DD0000' 0 14 60 13; Draw-Stripe $graphics '#FFCE00' 0 27 60 13 }
        'ru' { Draw-Stripe $graphics '#0039A6' 0 14 60 13; Draw-Stripe $graphics '#D52B1E' 0 27 60 13 }
    }
    $bitmap.Save((Join-Path $flagFolder "$code.png"), [Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
}
