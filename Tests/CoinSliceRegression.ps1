$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Drawing

function Convert-SliceFraction([string] $expression) {
    $parts = ($expression.Trim() -replace 'f', '') -split '/'
    if ($parts.Count -gt 2 -or $parts.Where({ $_ -notmatch '^\s*[0-9.]+\s*$' }).Count -gt 0) {
        throw "Unexpected slice expression: $expression"
    }
    $value = [double]::Parse($parts[0], [Globalization.CultureInfo]::InvariantCulture)
    if ($parts.Count -eq 2) { $value /= [double]::Parse($parts[1], [Globalization.CultureInfo]::InvariantCulture) }
    return $value
}

function Assert-CoinBounds([string] $label, [double[]] $bounds) {
    $left, $bottom, $width, $height = $bounds
    # Solid artwork measured from the original 1774 x 887 atlas; second coin begins at x=1052.
    if ($left -gt 965 -or ($left + $width) -lt 1036 -or $bottom -gt 63 -or ($bottom + $height) -lt 140) {
        throw "$label clips the first coin: $($bounds -join ', ')"
    }
    if (($left + $width) -gt 1052 -or $left -lt 950 -or $bottom -lt 50 -or ($bottom + $height) -gt 155) {
        throw "$label includes neighboring artwork or excessive padding: $($bounds -join ', ')"
    }
    Write-Output "$label contains one complete coin and excludes adjacent frames: PASS"
}

$bitmap = [Drawing.Bitmap]::new((Join-Path $projectRoot 'Assets/GameContent/Art/ProjectDM_Sprites_TopDown_v2.png'))
try {
    if ($bitmap.Width -ne 1774 -or $bitmap.Height -ne 887) { throw 'Atlas changed; re-measure the coin bounds.' }
    if ($bitmap.GetPixel(965, 780).A -lt 128 -or $bitmap.GetPixel(1053, 780).A -lt 128) {
        throw 'Coin artwork changed; re-measure the regression landmarks.'
    }
} finally { $bitmap.Dispose() }

$runtime = Get-Content (Join-Path $projectRoot 'Assets/Scripts/GameManager.cs') -Raw
$pipeline = Get-Content (Join-Path $projectRoot 'Assets/Editor/ProjectDMArtPipeline.cs') -Raw
$runtimeMatch = [regex]::Match($runtime, 'coinSprite\s*=\s*Slice\(assets\.GameplaySheet,\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*([^,]+),')
$pipelineMatch = [regex]::Match($pipeline, 'Slice\("Gold_Coin", width, height,\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*([^\)]+)\)')
if (-not $runtimeMatch.Success -or -not $pipelineMatch.Success) { throw 'Coin slice definition not found.' }
$runtimeBounds = 1..4 | ForEach-Object { (Convert-SliceFraction $runtimeMatch.Groups[$_].Value) * (@(1774, 887, 1774, 887)[$_ - 1]) }
$pipelineBounds = 1..4 | ForEach-Object { (Convert-SliceFraction $pipelineMatch.Groups[$_].Value) * (@(1774, 887, 1774, 887)[$_ - 1]) }
Assert-CoinBounds 'Runtime slice' $runtimeBounds
Assert-CoinBounds 'Art pipeline slice' $pipelineBounds
for ($i = 0; $i -lt 4; $i++) {
    if ([Math]::Abs($runtimeBounds[$i] - $pipelineBounds[$i]) -gt .01) { throw 'Runtime and pipeline disagree.' }
}

$metadata = Get-Content (Join-Path $projectRoot 'Assets/GameContent/Art/ProjectDM_Sprites_TopDown_v2.png.meta') -Raw
$metaMatch = [regex]::Match($metadata, 'name: Gold_Coin\s+rect:\s+serializedVersion: \d+\s+x: ([\d.]+)\s+y: ([\d.]+)\s+width: ([\d.]+)\s+height: ([\d.]+)')
if (-not $metaMatch.Success) { throw 'Imported Gold_Coin sprite not found.' }
$metaBounds = 1..4 | ForEach-Object { [double]::Parse($metaMatch.Groups[$_].Value, [Globalization.CultureInfo]::InvariantCulture) }
Assert-CoinBounds 'Imported sprite' $metaBounds
for ($i = 0; $i -lt 4; $i++) {
    if ([Math]::Abs($runtimeBounds[$i] - $metaBounds[$i]) -gt .01) { throw 'Runtime and imported sprite disagree.' }
}
Write-Output 'Coin slice regression checks: PASS'
