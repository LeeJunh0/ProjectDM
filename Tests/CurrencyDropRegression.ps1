$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Add-Type -Path @((Join-Path $projectRoot 'Assets/Scripts/Systems/Data/PrototypeProgression.cs'), (Join-Path $projectRoot 'Assets/Scripts/Systems/Pickups/LootGradeRules.cs'))
$dataRoot = Join-Path $projectRoot 'Assets/Resources/ProjectDM/Data'
$balance = [ProjectDM.PrototypeBalance](Get-Content (Join-Path $dataRoot 'PrototypeBalance.json') -Raw | ConvertFrom-Json)
$growth = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $dataRoot 'PrototypeGrowth.json') -Raw | ConvertFrom-Json)
$skills = [ProjectDM.PrototypeSkillTable](Get-Content (Join-Path $dataRoot 'PrototypeSkills.json') -Raw | ConvertFrom-Json)
$checks = 0
function Assert-True([bool] $condition, [string] $message) { if (-not $condition) { throw $message }; $script:checks++ }
Assert-True ($null -eq [ProjectDM.PrototypeRules]::Validate($balance,$skills,$growth)) 'Currency tables must validate'
Assert-True (@($growth.rows | Where-Object id -eq 'currencyGradeChance').Count -eq 0) 'Generic currency growth must not remain visible'
foreach ($stage in 0..4) {
    foreach ($kind in 0..$stage) {
        $monster = $balance.monsters[$kind]
        foreach ($bonuses in @([double[]]@(0,0,0,0,0), [double[]]@(0,.18,.09,.054,.027), [double[]]@(0,1,1,1,1))) {
            $p = [ProjectDM.LootGradeRules]::CurrencyProbabilities($balance,$monster,$stage,$bonuses)
            Assert-True ([Math]::Abs(($p | Measure-Object -Sum).Sum - 1) -lt .000001) 'Total probability is not 100%'
            foreach ($index in 0..4) {
                Assert-True ($p[$index] -ge 0 -and $p[$index] -le 1) 'Negative/overflow probability'
                if ($index -gt $stage) { Assert-True ($p[$index] -eq 0) 'Locked currency has nonzero probability' }
            }
            $counts = @(0,0,0,0,0)
            foreach ($sampleIndex in 0..999) {
                $grade = [ProjectDM.LootGradeRules]::ResolveCurrencyGrade($balance,$monster,$stage,$bonuses,($sampleIndex+.5)/1000)
                Assert-True ($grade -ge 1 -and $grade -le $stage+1 -and $p[$grade-1] -gt 0) 'Single draw selected an impossible currency'
                $counts[$grade-1]++
            }
            foreach ($index in 0..4) { Assert-True ([Math]::Abs($counts[$index]/1000-$p[$index]) -le .0011) 'Categorical intervals do not match table probabilities' }
            Assert-True (($counts | Measure-Object -Sum).Sum -eq 1000) 'One kill must select exactly one currency'
            $endGrade = [ProjectDM.LootGradeRules]::ResolveCurrencyGrade($balance,$monster,$stage,$bonuses,1)
            Assert-True ($p[$endGrade-1] -gt 0 -and $endGrade -le $stage+1) 'Inclusive Unity random endpoint chose an impossible currency'
        }
        $base = [ProjectDM.LootGradeRules]::CurrencyProbabilities($balance,$monster,$stage,[double[]]@(0,0,0,0,0))
        for ($grade=2; $grade -le $stage+1; $grade++) {
            $bonus = [double[]]@(0,0,0,0,0); $bonus[$grade-1]=.02
            $next = [ProjectDM.LootGradeRules]::CurrencyProbabilities($balance,$monster,$stage,$bonus)
            $pool = 0; for ($i=0; $i -lt $grade-1; $i++) {$pool += $base[$i]}
            Assert-True ([Math]::Abs($next[$grade-1]-$base[$grade-1]-[Math]::Min(.02,$pool)) -lt .000001) 'Named currency bonus has incorrect transfer amount'
            for ($i=$grade; $i -lt 5; $i++) {Assert-True ($base[$i] -eq $next[$i]) 'Named growth took probability from a higher currency'}
        }
    }
}
Assert-True ([ProjectDM.LootGradeRules]::ResolveCurrencyGrade($balance,$balance.monsters[0],4,[double[]]@(0,0,0,0,1),.5) -eq 5) 'Slime must be able to drop unlocked amethyst without a one-step restriction'
foreach ($version in 1..3) {
    foreach ($rank in 0..9) {
        $save=[ProjectDM.PrototypeSave]::new(); $save.version=$version
        $save.completedRuns=7; $save.playedSeconds=321; $save.kills[0]=120
        $save.SetRank('currencyGradeChance',$rank,.25)
        [ProjectDM.PrototypeRules]::MigrateGrowthRanks($save,$growth)
        $expected=[Math]::Min(9.0,$rank+.25)
        Assert-True ([Math]::Abs($save.Rank('silverDropChance')+$save.PartialRank('silverDropChance')-$expected) -lt .000001) 'Paid generic currency growth lost in migration'
        Assert-True ($save.Rank('currencyGradeChance') -eq $rank -and $save.currencyDropsVersion -eq 1) 'Migration archive/version missing'
        $save.SetRank('silverDropChance',0)
        [ProjectDM.PrototypeRules]::MigrateGrowthRanks($save,$growth)
        Assert-True ($save.Rank('silverDropChance') -eq 0) 'Migration ran again and recreated spent/reset ranks'
        Assert-True ($save.completedRuns -eq 7 -and $save.playedSeconds -eq 321 -and $save.kills[0] -eq 120) 'Currency migration changed run/kill statistics'
    }
}
foreach ($issue in @('length','negative','nan','sum','none_available')) {
    $broken=[ProjectDM.PrototypeBalance](Get-Content (Join-Path $dataRoot 'PrototypeBalance.json') -Raw | ConvertFrom-Json)
    switch ($issue) {
        'length' {$broken.monsters[0].currencyChances=@(.5,.5)}
        'negative' {$broken.monsters[0].currencyChances[0]=-.1}
        'nan' {$broken.monsters[0].currencyChances[0]=[float]::NaN}
        'sum' {$broken.monsters[0].currencyChances[0]=.1}
        'none_available' {$broken.monsters[0].currencyChances=@(0,0,0,0,1)}
    }
    Assert-True ($null -ne [ProjectDM.PrototypeRules]::Validate($broken,$skills,$growth)) "Invalid currency probabilities accepted: $issue"
}
Write-Output "Currency drops: $checks assertions PASS (per-monster categorical probabilities, exactly one currency, unlocked ceiling, independent named bonuses, legacy migration/once-only, invalid tables)."
