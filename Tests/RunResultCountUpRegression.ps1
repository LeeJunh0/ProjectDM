$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$helperSource = Get-Content (Join-Path $projectRoot 'Assets/Scripts/RunResultPresentation.cs') -Raw
$compiledType = Add-Type -TypeDefinition $helperSource -PassThru
$countMethod = $compiledType.GetMethod('CountUp', [Reflection.BindingFlags]'NonPublic,Static')
function Get-Count([int] $target, [double] $elapsed, [double] $duration = 1.1, [double] $delay = 0) {
    return $countMethod.Invoke($null, [object[]]@($target, $elapsed, $duration, $delay))
}
foreach ($target in @(0, 1, 3, 72, 128, 1000000, [int]::MaxValue)) {
    foreach ($delay in @(0, .07, .42)) {
        if ((Get-Count $target 0 1.1 $delay) -ne 0) { throw 'Count-up must start at zero.' }
        if ((Get-Count $target $delay 1.1 $delay) -ne 0) { throw 'Count-up starts before its delay.' }
        $previous = 0
        for ($tick = 0; $tick -le 200; $tick++) {
            $actual = Get-Count $target ($tick / 100.0) 1.1 $delay
            if ($actual -lt $previous -or $actual -lt 0 -or $actual -gt $target) {
                throw "Non-monotonic or out-of-range count: $target -> $actual"
            }
            $previous = $actual
        }
        if ((Get-Count $target 2 1.1 $delay) -ne $target) { throw 'Final count must exactly match the statistic.' }
    }
}
if ((Get-Count 128 .1 0) -ne 128) { throw 'Zero duration must complete immediately.' }
if ((Get-Count 128 -1) -ne 0) { throw 'Negative elapsed time must stay at zero.' }
if ((Get-Count -1 2) -ne 0) { throw 'Negative targets must stay at zero.' }
if ((Get-Count 128 .2) -le 0 -or (Get-Count 128 .2) -ge 128) { throw 'Intermediate count is not animated.' }

$manager = Get-Content (Join-Path $projectRoot 'Assets/Scripts/GameManager.cs') -Raw
if ($manager -notmatch 'resultPresentationOpenedAt = Time\.unscaledTime' -or
    $manager -notmatch 'Time\.unscaledTime - resultPresentationOpenedAt') { throw 'Paused results must use unscaled time.' }
foreach ($pattern in @('AnimatedResultCount\(runCurrency, 0', 'AnimatedResultCount\(totalKills, 1', 'AnimatedResultCount\(monsterKillCounts\[i\], i \+ 2')) {
    if ($manager -notmatch $pattern) { throw "Unanimated statistic: $pattern" }
}
Write-Output 'Count-up: zero start, stagger, monotonic range, intermediate values, exact completion, zero duration, zero/large counts: PASS'
Write-Output 'Integration: all statistics animated using unscaled time: PASS'
