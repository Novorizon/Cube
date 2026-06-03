param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [int]$Size = 1024,
    [int]$SeamMargin = 128,
    [double]$NormalStrength = 2.4,
    [string]$Prefix = "Grass_Top"
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

function Lerp-Color($a, $b, [double]$t) {
    $r = (1.0 - $t) * $a.R + $t * $b.R
    $g = (1.0 - $t) * $a.G + $t * $b.G
    $bb = (1.0 - $t) * $a.B + $t * $b.B
    return [System.Drawing.Color]::FromArgb(255, (Clamp-Byte $r), (Clamp-Byte $g), (Clamp-Byte $bb))
}

function SmoothStep([double]$x) {
    if ($x -lt 0) { $x = 0 }
    if ($x -gt 1) { $x = 1 }
    return $x * $x * (3.0 - 2.0 * $x)
}

function Save-Png($bitmap, [string]$path) {
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$sourceImage = [System.Drawing.Image]::FromFile($Source)
$resized = New-Bitmap $Size $Size
$graphics = [System.Drawing.Graphics]::FromImage($resized)
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$graphics.DrawImage($sourceImage, 0, 0, $Size, $Size)
$graphics.Dispose()
$sourceImage.Dispose()

$albedoPath = Join-Path $OutputDir "$($Prefix)_Albedo_AI_1024.png"
Save-Png $resized $albedoPath

$seamless = New-Bitmap $Size $Size
for ($y = 0; $y -lt $Size; $y++) {
    for ($x = 0; $x -lt $Size; $x++) {
        $color = $resized.GetPixel($x, $y)

        if ($x -lt $SeamMargin) {
            $t = SmoothStep (($SeamMargin - $x) / [double]$SeamMargin)
            $other = $resized.GetPixel($x + $Size - $SeamMargin, $y)
            $color = Lerp-Color $color $other ($t * 0.55)
        } elseif ($x -ge $Size - $SeamMargin) {
            $t = SmoothStep (($x - ($Size - $SeamMargin)) / [double]$SeamMargin)
            $other = $resized.GetPixel($x - ($Size - $SeamMargin), $y)
            $color = Lerp-Color $color $other ($t * 0.55)
        }

        if ($y -lt $SeamMargin) {
            $t = SmoothStep (($SeamMargin - $y) / [double]$SeamMargin)
            $other = $resized.GetPixel($x, $y + $Size - $SeamMargin)
            $color = Lerp-Color $color $other ($t * 0.55)
        } elseif ($y -ge $Size - $SeamMargin) {
            $t = SmoothStep (($y - ($Size - $SeamMargin)) / [double]$SeamMargin)
            $other = $resized.GetPixel($x, $y - ($Size - $SeamMargin))
            $color = Lerp-Color $color $other ($t * 0.55)
        }

        $seamless.SetPixel($x, $y, $color)
    }
}

$seamlessPath = Join-Path $OutputDir "$($Prefix)_Albedo_Tileable_1024.png"
Save-Png $seamless $seamlessPath

$height = New-Bitmap $Size $Size
for ($y = 0; $y -lt $Size; $y++) {
    for ($x = 0; $x -lt $Size; $x++) {
        $c = $seamless.GetPixel($x, $y)
        $luma = 0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B
        $centered = 128 + ($luma - 128) * 0.35
        $v = Clamp-Byte $centered
        $height.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $v, $v, $v))
    }
}

$heightPath = Join-Path $OutputDir "$($Prefix)_Height_1024.png"
Save-Png $height $heightPath

$normal = New-Bitmap $Size $Size
for ($y = 0; $y -lt $Size; $y++) {
    $ym = ($y - 1 + $Size) % $Size
    $yp = ($y + 1) % $Size
    for ($x = 0; $x -lt $Size; $x++) {
        $xm = ($x - 1 + $Size) % $Size
        $xp = ($x + 1) % $Size

        $hl = $height.GetPixel($xm, $y).R / 255.0
        $hr = $height.GetPixel($xp, $y).R / 255.0
        $hd = $height.GetPixel($x, $ym).R / 255.0
        $hu = $height.GetPixel($x, $yp).R / 255.0

        $dx = ($hl - $hr) * $NormalStrength
        $dy = ($hd - $hu) * $NormalStrength
        $dz = 1.0
        $len = [Math]::Sqrt($dx * $dx + $dy * $dy + $dz * $dz)
        $nx = $dx / $len
        $ny = $dy / $len
        $nz = $dz / $len

        $r = Clamp-Byte (($nx * 0.5 + 0.5) * 255)
        $g = Clamp-Byte (($ny * 0.5 + 0.5) * 255)
        $b = Clamp-Byte (($nz * 0.5 + 0.5) * 255)
        $normal.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $r, $g, $b))
    }
}

$normalPath = Join-Path $OutputDir "$($Prefix)_Normal_1024.png"
Save-Png $normal $normalPath

$resized.Dispose()
$seamless.Dispose()
$height.Dispose()
$normal.Dispose()

Write-Host "Wrote:"
Write-Host $albedoPath
Write-Host $seamlessPath
Write-Host $heightPath
Write-Host $normalPath
