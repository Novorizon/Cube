param(
    [string]$OutputDir = "Assets/Arts/Map/Tiles/Textures/Generated",
    [int]$Size = 1024
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

function New-Color([double]$r, [double]$g, [double]$b) {
    return [System.Drawing.Color]::FromArgb(255, (Clamp-Byte $r), (Clamp-Byte $g), (Clamp-Byte $b))
}

function SmoothStep([double]$x) {
    if ($x -lt 0) { $x = 0 }
    if ($x -gt 1) { $x = 1 }
    return $x * $x * (3.0 - 2.0 * $x)
}

function Draw-SoftSideTexture(
    [string]$path,
    [int]$size,
    [double[]]$base,
    [double[]]$topTint,
    [double[]]$bottomTint,
    [double]$seamStrength,
    [double]$panelVariance,
    [double]$noiseStrength
) {
    $bitmap = New-Bitmap $size $size
    $panelCount = 7
    $panelWidth = $size / [double]$panelCount

    for ($y = 0; $y -lt $size; $y++) {
        $v = $y / [double]($size - 1)
        $verticalShade = (1.0 - $v) * 0.10 - $v * 0.08
        $topSoft = 1.0 - (SmoothStep ($v / 0.18))
        $bottomSoft = SmoothStep (($v - 0.74) / 0.26)

        for ($x = 0; $x -lt $size; $x++) {
            $u = $x / [double]($size - 1)
            $panel = [Math]::Floor($x / $panelWidth)
            $panelLocal = (($x / $panelWidth) - $panel)
            $panelTone = [Math]::Sin(($panel + 1) * 2.13) * $panelVariance

            $seam = 0.0
            $edgeDistance = [Math]::Min($panelLocal, 1.0 - $panelLocal)
            if ($edgeDistance -lt 0.055) {
                $seam = (1.0 - (SmoothStep ($edgeDistance / 0.055))) * $seamStrength
            }

            $largeNoise = [Math]::Sin(($u * 15.0) + ($v * 7.0)) * 0.5 + [Math]::Sin(($u * 5.0) - ($v * 11.0)) * 0.5
            $smallNoise = [Math]::Sin(($u * 83.0) + ($v * 37.0)) * 0.5
            $noise = ($largeNoise * 0.65 + $smallNoise * 0.35) * $noiseStrength

            $r = $base[0] + $panelTone + $verticalShade * 255.0 + $noise - $seam * 255.0
            $g = $base[1] + $panelTone * 0.72 + $verticalShade * 210.0 + $noise * 0.75 - $seam * 185.0
            $b = $base[2] + $panelTone * 0.48 + $verticalShade * 160.0 + $noise * 0.45 - $seam * 130.0

            $r = $r * (1.0 - $topSoft * 0.14) + $topTint[0] * $topSoft * 0.14
            $g = $g * (1.0 - $topSoft * 0.14) + $topTint[1] * $topSoft * 0.14
            $b = $b * (1.0 - $topSoft * 0.14) + $topTint[2] * $topSoft * 0.14

            $r = $r * (1.0 - $bottomSoft * 0.12) + $bottomTint[0] * $bottomSoft * 0.12
            $g = $g * (1.0 - $bottomSoft * 0.12) + $bottomTint[1] * $bottomSoft * 0.12
            $b = $b * (1.0 - $bottomSoft * 0.12) + $bottomTint[2] * $bottomSoft * 0.12

            $bitmap.SetPixel($x, $y, (New-Color $r $g $b))
        }
    }

    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

function Draw-NeutralDetail([string]$path, [int]$size, [double]$strength) {
    $bitmap = New-Bitmap $size $size
    $panelCount = 7
    $panelWidth = $size / [double]$panelCount

    for ($y = 0; $y -lt $size; $y++) {
        $v = $y / [double]($size - 1)
        for ($x = 0; $x -lt $size; $x++) {
            $u = $x / [double]($size - 1)
            $panelLocal = (($x / $panelWidth) - [Math]::Floor($x / $panelWidth))
            $edgeDistance = [Math]::Min($panelLocal, 1.0 - $panelLocal)
            $seam = 0.0
            if ($edgeDistance -lt 0.055) {
                $seam = 1.0 - (SmoothStep ($edgeDistance / 0.055))
            }

            $noise = [Math]::Sin(($u * 19.0) + ($v * 11.0)) * 0.5 + [Math]::Sin(($u * 71.0) - ($v * 29.0)) * 0.25
            $value = 128 + $noise * 18.0 * $strength - $seam * 20.0 * $strength
            $c = Clamp-Byte $value
            $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c, $c, $c))
        }
    }

    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$soilPath = Join-Path $OutputDir "Soil_Side_V1_Albedo_Tileable_1024.png"
$soilDetailPath = Join-Path $OutputDir "Soil_Side_V1_DetailOverlay_1024.png"
$rockPath = Join-Path $OutputDir "Rock_Side_V1_Albedo_Tileable_1024.png"
$rockDetailPath = Join-Path $OutputDir "Rock_Side_V1_DetailOverlay_1024.png"

Draw-SoftSideTexture $soilPath $Size @(150, 86, 42) @(178, 111, 58) @(108, 61, 33) 0.065 10.0 7.0
Draw-NeutralDetail $soilDetailPath $Size 0.60

Draw-SoftSideTexture $rockPath $Size @(72, 82, 80) @(94, 105, 102) @(49, 57, 56) 0.055 6.0 4.0
Draw-NeutralDetail $rockDetailPath $Size 0.42

Write-Host "Wrote soft side textures:"
Write-Host $soilPath
Write-Host $soilDetailPath
Write-Host $rockPath
Write-Host $rockDetailPath
