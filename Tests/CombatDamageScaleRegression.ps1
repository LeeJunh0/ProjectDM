$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Systems/UI/Damage/CombatDamageScaleAnimation.cs') -Raw
$compiledType = Add-Type -TypeDefinition $source -PassThru
$method = $compiledType.GetMethod('Evaluate', [Reflection.BindingFlags]'NonPublic,Static')
$checks = 0
function Assert-True([bool] $condition, [string] $message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
function Get-Scale([float] $age, [float] $lifetime = .7, [float] $speed = 1,
    [float] $grow = .1, [float] $hold = .04, [float] $shrink = .3) {
    return $method.Invoke($null, [object[]]@($age, $lifetime, [float]0.55, [float]1.3, [float]0.55, $grow, $hold, $shrink, $speed))
}
function Assert-Near([float] $actual, [float] $expected, [string] $message) {
    Assert-True ([Math]::Abs($actual - $expected) -lt .00001) $message
}
Assert-Near (Get-Scale 0) .55 'Number must start small.'
Assert-Near (Get-Scale -1) .55 'Negative age must remain at start size.'
Assert-Near (Get-Scale .1) 1.3 'Grow phase must reach peak size.'
Assert-Near (Get-Scale .12) 1.3 'Peak must remain during hold phase.'
Assert-Near (Get-Scale .44) .55 'Shrink phase must end at final size.'
Assert-Near (Get-Scale .7) .55 'Final size must remain until expiration.'
Assert-True ((Get-Scale .05) -gt .55 -and (Get-Scale .05) -lt 1.3) 'Grow phase must interpolate.'
Assert-True ((Get-Scale .29) -gt .55 -and (Get-Scale .29) -lt 1.3) 'Shrink phase must interpolate.'
$previous = Get-Scale 0
foreach ($tick in 1..100) {
    $current = Get-Scale ($tick / 1000.0)
    Assert-True ($current -ge $previous -and $current -le 1.30001) 'Grow must be monotonic with no overshoot.'
    $previous = $current
}
$previous = Get-Scale .14
foreach ($tick in 1..100) {
    $current = Get-Scale (.14 + $tick * .003)
    Assert-True ($current -le $previous + .00001 -and $current -ge .54999) 'Shrink must be monotonic with no undershoot.'
    $previous = $current
}
foreach ($age in @(0, .025, .05, .1, .12, .2, .3, .44, .7)) {
    Assert-Near (Get-Scale ($age / 2) .7 2) (Get-Scale $age) 'Speed 2 must halve animation time.'
    Assert-Near (Get-Scale ($age * 2) 1.5 .5) (Get-Scale $age) 'Speed 0.5 must double animation time.'
}
Assert-Near (Get-Scale .05 .22) 1.3 'Short lifetime must proportionally fit grow phase.'
Assert-Near (Get-Scale .06 .22) 1.3 'Short lifetime must preserve peak hold.'
Assert-Near (Get-Scale .22 .22) .55 'Short lifetime must finish shrink rather than cut it off.'
Assert-Near (Get-Scale .02 .7 1 0 .04 .3) 1.3 'Zero grow must jump directly to peak.'
Assert-Near (Get-Scale .15 .7 1 .1 .04 0) .55 'Zero shrink must jump directly to final size.'
Assert-Near (Get-Scale .001 .7 1 0 0 0) .55 'All-zero phase durations must be finite and immediate.'
foreach ($lifetime in @(.001, .05, .1, .7, 2)) {
    foreach ($speed in @(0, .01, .5, 1, 2, 10)) {
        foreach ($tick in 0..20) {
            $value = Get-Scale ($tick * .05) $lifetime $speed
            Assert-True (-not [float]::IsNaN($value) -and -not [float]::IsInfinity($value) -and $value -ge .54999 -and $value -le 1.30001) 'Timing edge case produced invalid scale.'
        }
    }
}
$manager = Get-Content (Join-Path $projectRoot 'Assets/Scripts/GameManager.cs') -Raw
$inspector = Get-Content (Join-Path $projectRoot 'Assets/Editor/GameManagerInspector.cs') -Raw
foreach ($property in @('combatDamageStartScale','combatDamagePeakScale','combatDamageEndScale',
    'combatDamageGrowDuration','combatDamagePeakHoldDuration','combatDamageShrinkDuration','combatDamageScaleSpeed')) {
    Assert-True ($manager -match "SerializeField[^\r\n]* $property =") "Missing serialized setting: $property"
    Assert-True ($inspector.Contains('DrawProperty("' + $property + '"')) "Setting not exposed in Korean inspector: $property"
}
$display = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Systems/UI/Damage/CombatDamageDisplay.cs') -Raw
Assert-True ($display -match 'CombatDamageScaleAnimation.Evaluate\(popup.age, duration') 'Renderer must use tested timing with popup age.'
Assert-True ($display -match 'Mathf.Max\(1, Mathf.RoundToInt\(size\)\)') 'A large minimum font clamp must not hide small-size animation.'
Write-Output "Combat damage scale: $checks assertions PASS (small/peak/shrink, timing, speed, short lifetime, zero phases, inspector integration)."
