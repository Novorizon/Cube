param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [Parameter(Mandatory = $true)]
    [string]$Output,

    [int]$Size = 1024,
    [int]$BlurSize = 96,
    [double]$Strength = 1.4
)

Add-Type -AssemblyName System.Drawing

function New-Bitmap($width, $height) {
    return [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function Clamp-Byte([double]$value) {
    if ($value -lt 0) { return 0 }
    if ($value -gt 255) { return 255 }
    return [int][Math]::Round($value)
}

function Luma($color) {
    return 0.2126 * $color.R + 0.7152 * $color.G + 0.0722 * $color.B
}

$sourceImage = [System.Drawing.Image]::FromFile($Source)
$sourceBitmap = New-Bitmap $Size $Size
$graphics = [System.Drawing.Graphics]::FromImage($sourceBitmap)
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$graphics.DrawImage($sourceImage, 0, 0, $Size, $Size)
$graphics.Dispose()
$sourceImage.Dispose()

$small = New-Bitmap $BlurSize $BlurSize
$graphics = [System.Drawing.Graphics]::FromImage($small)
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$graphics.DrawImage($sourceBitmap, 0, 0, $BlurSize, $BlurSize)
$graphics.Dispose()

$blurred = New-Bitmap $Size $Size
$graphics = [System.Drawing.Graphics]::FromImage($blurred)
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$graphics.DrawImage($small, 0, 0, $Size, $Size)
$graphics.Dispose()

$detail = New-Bitmap $Size $Size
for ($y = 0; $y -lt $Size; $y++) {
    for ($x = 0; $x -lt $Size; $x++) {
        $baseLuma = Luma $sourceBitmap.GetPixel($x, $y)
        $blurLuma = Luma $blurred.GetPixel($x, $y)
        $value = 128 + (($baseLuma - $blurLuma) * $Strength)
        $v = Clamp-Byte $value
        $detail.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $v, $v, $v))
    }
}

$outputDir = Split-Path -Parent $Output
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$detail.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)

$sourceBitmap.Dispose()
$small.Dispose()
$blurred.Dispose()
$detail.Dispose()

Write-Host "Wrote detail overlay:"
Write-Host $Output
