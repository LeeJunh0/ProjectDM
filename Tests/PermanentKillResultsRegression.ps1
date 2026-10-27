$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$types = Add-Type -Path (Join-Path $projectRoot 'Assets/Scripts/Systems/UI/Results/RunResultPresentation.cs') -PassThru
$presentation = $types | Where-Object FullName -eq 'ProjectDM.RunResultPresentation'
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$instanceFlags = [Reflection.BindingFlags]'NonPublic,Instance'
$build = $presentation.GetMethod('PermanentKills',$flags)
$animate = $presentation.GetMethod('AnimatedPermanentKills',$flags)
$fraction = $presentation.GetMethod('PermanentKillFraction',$flags)
$checks=0
function Assert-True([bool]$condition,[string]$message) {if(-not $condition){throw $message};$script:checks++}
function Progress([int]$before,[int]$total,[int]$runKills,[int]$goal,[bool]$available=$true) {
    return $build.Invoke($null,[object[]]@($before,$total,$runKills,$goal,$available))
}
function Value($row,[string]$field) {return $row.GetType().GetField($field,$instanceFlags).GetValue($row)}
function Animate($row,[double]$time,[double]$duration=1.1,[double]$delay=0) {return $animate.Invoke($null,[object[]]@($row,$time,$duration,$delay))}
function Fraction($row,[int]$count) {return $fraction.Invoke($null,[object[]]@($row,$count))}
foreach ($case in @(@(0,0,0,120),@(0,12,12,120),@(100,123,23,120),@(1000,1010,10,120),@(299,300,1,300),@(([int]::MaxValue-2),[int]::MaxValue,3,120))) {
    $row=Progress $case[0] $case[1] $case[2] $case[3]
    foreach ($delay in @(0,.07,.42)) {
        Assert-True ((Animate $row 0 1.1 $delay) -eq $case[0]) 'Permanent count must start at the previous run total, not zero'
        $previous=$case[0]
        foreach ($tick in 0..200) {
            $actual=Animate $row ($tick/100.0) 1.1 $delay
            Assert-True ($actual -ge $previous -and $actual -le $case[1]) 'Permanent count overflow/decreased/overshot'
            $fill=Fraction $row $actual
            Assert-True ($fill -ge 0 -and $fill -le 1) 'Progress bar escaped 0..1'
            $previous=$actual
        }
        Assert-True ((Animate $row 2 1.1 $delay) -eq $case[1]) 'Final permanent total mismatch'
    }
    Assert-True ((Animate $row 0 0) -eq $case[1]) 'Disabled count-up must show final permanent total immediately'
    $isNew=$row.GetType().GetProperty('NewlyUnlocked',$instanceFlags).GetValue($row)
    Assert-True ($isNew -eq ($case[0] -lt $case[3] -and $case[1] -ge $case[3])) 'New unlock detection incorrect'
}
$locked=Progress 0 12 12 120 $false
Assert-True ((Fraction $locked 12) -eq 0 -and -not $locked.GetType().GetProperty('NewlyUnlocked',$instanceFlags).GetValue($locked)) 'Locked monster should not display active progress/unlock'
$last=Progress 11 15 4 0
Assert-True ((Fraction $last 15) -eq 0 -and -not $last.GetType().GetProperty('HasTarget',$instanceFlags).GetValue($last)) 'Final monster must not invent a next unlock goal'
$clamped=Progress 100 50 -4 -1
Assert-True ((Value $clamped 'before') -eq 50 -and (Value $clamped 'runKills') -eq 0 -and (Value $clamped 'target') -eq 0) 'Defensive display normalization failed'
$run=Get-Content (Join-Path $projectRoot 'Assets/Scripts/Systems/Run/RunController.cs') -Raw
$view=Get-Content (Join-Path $projectRoot 'Assets/Scripts/Systems/UI/Results/RunResultView.cs') -Raw
Assert-True ($run.Contains('Array.Copy(save.progress.kills, permanentKillsAtRunStart') -and $run.Contains('Array.Copy(save.progress.kills, resultPermanentKillCounts')) 'Run start/end need separate permanent kill snapshots'
Assert-True ($view.Contains('AnimatedPermanentKillCount(progress, i + 2, presentationElapsed)')) 'Permanent count and bar must share the existing unscaled presentation'
Assert-True ($view.Contains('이번 런: ') -and $view.Contains('미해금') -and $view.Contains('최종 몬스터 · 다음 해금 없음')) 'Result states not distinguished'
Assert-True ($view -notmatch 'save\.progress\.kills|SetRank\(|MarkPrototypeProgressDirty\(|SaveMetaProgress\(') 'Viewing statistics must not modify progression'
Assert-True ($view.Contains('PixelUiArt.Mana') -and $view.Contains('ProjectDM_TimerFill_v1')) 'Use the approved pixel progress frame and existing fill artwork'
Write-Output "Permanent kill results: $checks assertions PASS (previous-to-final count-up, unclipped lifetime totals, capped bars, unlock/locked/final states, overflow, snapshots, read-only and existing artwork)."
