$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Add-Type -Path @((Join-Path $projectRoot 'Assets/Scripts/Systems/Data/PrototypeProgression.cs'), (Join-Path $projectRoot 'Assets/Scripts/Systems/Pickups/LootGradeRules.cs'))
$dataRoot = Join-Path $projectRoot 'Assets/Resources/ProjectDM/Data'
$balance = [ProjectDM.PrototypeBalance](Get-Content (Join-Path $dataRoot 'PrototypeBalance.json') -Raw | ConvertFrom-Json)
$growth = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $dataRoot 'PrototypeGrowth.json') -Raw | ConvertFrom-Json)
$skills = [ProjectDM.PrototypeSkillTable](Get-Content (Join-Path $dataRoot 'PrototypeSkills.json') -Raw | ConvertFrom-Json)
$checks = 0
function Assert-True([bool] $condition, [string] $message) { if (-not $condition) { throw $message }; $script:checks++ }
Assert-True ($null -eq [ProjectDM.PrototypeRules]::Validate($balance,$skills,$growth)) 'Grade table must validate'
foreach ($stage in 0..4) {
    Assert-True ([ProjectDM.LootGradeRules]::HighestUnlocked($balance,$stage) -eq $stage + 1) 'Wrong grade ceiling'
    foreach ($kind in 0..$stage) {
        $monster = $balance.monsters[$kind]
        foreach ($chance in @(0,.02,.18,1)) {
            foreach ($sample in @(0,.019,.02,.179,.18,.5,.999999)) {
                foreach ($currency in @($false,$true)) {
                    $baseGrade = if ($currency) { $monster.currencyGrade } else { $monster.experienceGrade }
                    $actual = [ProjectDM.LootGradeRules]::ResolveGrade($balance,$baseGrade,$stage,$chance,$sample)
                    $expected = $baseGrade + $(if ($baseGrade -lt $stage + 1 -and $sample -lt $chance) {1} else {0})
                    Assert-True ($actual -eq $expected -and $actual -le $baseGrade + 1 -and $actual -le $stage + 1) 'Grade skips a tier or exceeds unlock ceiling'
                    $amount = [ProjectDM.LootGradeRules]::BaseAmount($balance,$monster,$actual,$currency)
                    $expectedAmount = if ($currency) { $balance.lootGrades[$actual-1].currencyAmount } else { $balance.lootGrades[$actual-1].experienceAmount }
                    Assert-True ($amount -eq $expectedAmount) 'Grade does not determine base payout'
                    Assert-True ([ProjectDM.LootGradeRules]::PrefabIndex($balance,$actual) -eq $actual - 1) 'Grade sprite index is wrong'
                }
            }
        }
        Assert-True ([ProjectDM.LootGradeRules]::BaseAmount($balance,$monster,$monster.currencyGrade,$true) -eq $monster.coins) 'Baseline currency changed without a grade upgrade'
        Assert-True ([ProjectDM.LootGradeRules]::BaseAmount($balance,$monster,$monster.experienceGrade,$false) -eq $monster.xp) 'Baseline XP changed without a grade upgrade'
    }
}
Assert-True ([ProjectDM.LootGradeRules]::Reward($balance,$balance.monsters[0],2,$true,.04,$true,0) -eq 4) 'Promote, bonus and quantity rounding order'
Assert-True ([Math]::Abs([ProjectDM.LootGradeRules]::Reward($balance,$balance.monsters[0],2,$false,.06,$false,0)-2.12) -lt .000001) 'Fractional XP lost'
foreach ($id in @('silverDropChance','goldDropChance','emeraldDropChance','amethystDropChance','experienceGradeChance')) {
    $row = $growth.rows | Where-Object id -eq $id
    $nodes = @($growth.nodes | Where-Object growthId -eq $id)
    Assert-True ($row.maxRank -eq 9 -and $nodes.Count -eq 3 -and @($nodes | Where-Object ranks -ne 3).Count -eq 0) 'Grade chance needs three nodes with three purchases each'
    $expectedStage = if ($id -eq 'experienceGradeChance') {1} else {[ProjectDM.LootGradeRules]::CurrencyGrowthGrade($id)-1}
    Assert-True ($row.minStage -eq $expectedStage -and $row.step -gt 0) 'Individual currency probability tuning mismatch'
    $save = [ProjectDM.PrototypeSave]::new(); $save.SetRank($row.requires,1)
    Assert-True ($null -ne [ProjectDM.PrototypeRules]::PurchaseBlock($row,$save,0,100000,$balance.latePriceScale)) 'Grade growth offered before any higher grade unlock'
    $parent = $growth.nodes | Where-Object id -eq $nodes[0].parent
    $save.SetRank($parent.growthId,$parent.startRank+$nodes[0].parentRanks)
    Assert-True ($null -eq [ProjectDM.PrototypeRules]::NodePurchaseBlock($balance,$row,$nodes[0],$save,$expectedStage,100000,$growth)) 'First currency node unavailable after parent purchase and stage unlock'
}
foreach ($issue in @('missing','non_increasing','bad_sprite','duplicate_sprite','future_monster','bad_unlock')) {
    $broken = [ProjectDM.PrototypeBalance](Get-Content (Join-Path $dataRoot 'PrototypeBalance.json') -Raw | ConvertFrom-Json)
    switch ($issue) {
        'missing' { $broken.lootGrades = @() }
        'non_increasing' { $broken.lootGrades[1].currencyAmount = 1 }
        'bad_sprite' { $broken.lootGrades[0].prefabIndex = 5 }
        'duplicate_sprite' { $broken.lootGrades[1].prefabIndex = 0 }
        'future_monster' { $broken.monsters[0].currencyGrade = 5 }
        'bad_unlock' { $broken.lootGrades[0].unlockStage = 1 }
    }
    Assert-True ($null -ne [ProjectDM.PrototypeRules]::Validate($broken,$skills,$growth)) "Invalid grade config accepted: $issue"
}
$balance.lootGrades = $null
Assert-True ($null -eq [ProjectDM.PrototypeRules]::Validate($balance,$skills,$growth)) 'Legacy table without grades no longer loads'
Assert-True ([ProjectDM.LootGradeRules]::Reward($balance,$balance.monsters[4],1,$true,0,$false,.5) -eq 9) 'Legacy currency payout lost'
Assert-True ([ProjectDM.LootGradeRules]::ResolveGrade($balance,1,4,1,0) -eq 1) 'Legacy grade fallback invalid'
Write-Output "Loot grades: $checks assertions PASS (five grades, amounts/sprites, unlock ceilings, one-step promotions, independent quantity, fractional XP, new graph gates, legacy/invalid tables)."
