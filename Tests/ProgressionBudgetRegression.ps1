$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @((Join-Path $root 'Assets/Scripts/Systems/Data/PrototypeProgression.cs'), (Join-Path $root 'Assets/Scripts/Systems/Pickups/LootGradeRules.cs'))
$data = Join-Path $root 'Assets/Resources/ProjectDM/Data'
$balance = [ProjectDM.PrototypeBalance](Get-Content "$data/PrototypeBalance.json" -Raw | ConvertFrom-Json)
$growth = [ProjectDM.PrototypeGrowthTable](Get-Content "$data/PrototypeGrowth.json" -Raw | ConvertFrom-Json)
$characters = (Get-Content "$data/PrototypeCharacters.json" -Raw | ConvertFrom-Json).characters
$checks = 0
function Check([bool]$ok, [string]$message) { if (-not $ok) { throw $message }; $script:checks++ }
function Amount([string]$id) { $r = $growth.rows | Where-Object id -eq $id; [ProjectDM.PrototypeRules]::Amount($balance,$r,$r.maxRank) }
$archived = @('multishot','chain','splash','meteor','chainChance','splashChance','meteorChance')
$allRows = @($growth.rows | Where-Object id -notin $archived)
foreach ($character in $characters) {
    $expanded = @($character.upgrades | Where-Object permanentMaxRank -gt 3)
    Check ($expanded.Count -eq 4) "$($character.id): every skill needs mastery plus a personal hunting path"
    $hunt = $character.upgrades | Where-Object id -eq hunt
    Check ($hunt.permanentOnly -and $hunt.permanentMaxRank -eq 12 -and $hunt.permanentStep -eq 3) 'Personal hunting must be permanent-only, +36 population and +6 refill at full growth'
    foreach ($upgrade in $character.upgrades) {
        $row = [ProjectDM.PrototypeGrowth]::new()
        $row.id = "magic.$($character.id).$($upgrade.id)"; $row.baseCost = $upgrade.cost; $row.costGrowth = 1.8
        $row.rankCostStep = $upgrade.permanentCostStep
        $row.maxRank = if ($upgrade.permanentMaxRank) { $upgrade.permanentMaxRank } else { [Math]::Min(3,$upgrade.maxRank) }
        $allRows += $row
    }
    # Large input envelope; the actual damage ceiling is checked separately for extreme inputs.
    $damage = $character.upgrades | Where-Object id -eq damage
    $personal = $damage.permanentMaxRank * $damage.permanentStep + $damage.maxRank * $damage.step
    $secondary = 1
    foreach ($skill in $character.skills) {
        $power = $character.upgrades | Where-Object id -eq ($skill.id+'Power')
        if ($power) { $secondary = [Math]::Max($secondary,$skill.damage*(1+$power.permanentMaxRank*$power.permanentStep+$power.maxRank*$power.step)) }
    }
    $ceiling = $balance.baseDamage*$character.damage*(1+(Amount 'damage'))*(1+$personal)*$secondary*($balance.criticalMultiplier+(Amount 'criticalDamage'))*5
    Check ([ProjectDM.PrototypeRules]::CombatDamage($ceiling,$character.finalCombatScale) -lt 1000000) "$($character.id): damage enters M even in the conservative envelope"
}
foreach ($row in $allRows) {
    $previous = 0
    for ($rank=0;$rank -lt $row.maxRank;$rank++) {
        $price = [ProjectDM.PrototypeRules]::Cost($row,$rank,$balance.latePriceScale)
        Check ($price -ge $previous -and $price -gt 0 -and $price -lt 1000000) "$($row.id): invalid or M-sized purchase price"
        $previous = $price
    }
    if ($row.rankCostStep -gt 0) {
        $expectedStep = if ($row.id.EndsWith('.hunt')) { 3000 } elseif ($row.id.StartsWith('magic.projectile.')) { 1000 } else { 4000 }
        Check (([ProjectDM.PrototypeRules]::Cost($row,4,$balance.latePriceScale)-[ProjectDM.PrototypeRules]::Cost($row,3,$balance.latePriceScale)) -eq $expectedStep) 'Starter/advanced mastery must grow at a steady cost instead of an exponential tail'
    }
}
$mana = $balance.ResolvedInitialMana+(Amount 'time')+(Amount 'health')
Check ($mana -eq 180) 'Full mana growth must reach 180 MP'
foreach ($monster in $balance.monsters) { Check ($monster.hp*$balance.miniBossHealthMultiplier*($characters | Measure-Object finalCombatScale -Maximum).Maximum -lt 1000000) 'Monster/miniboss health enters M' }
$maxSpawn = 0
foreach ($stage in $balance.stages) {
    $target = $stage.target+(Amount 'population')+36; $cap = $stage.cap+(Amount 'populationCap')+36
    $initial = [Math]::Min($cap,[Math]::Ceiling($stage.initial*$target/$stage.target))
    $batch = $stage.batch+(Amount 'spawnBatch')+6; $interval = $stage.refillSeconds/(1+(Amount 'respawn'))
    # Assume every spawn dies immediately so refills always succeed; include three minibosses.
    $maxSpawn = [Math]::Max($maxSpawn,$initial+[Math]::Ceiling($balance.bossElapsedSeconds/$interval)*$batch+3)
    Check ($target -lt 1000000 -and $cap -lt 1000000) 'Field count enters M'
}
$xpPerKill = ($balance.lootGrades | Measure-Object experienceAmount -Maximum).Maximum*(1+(Amount 'xp'))*$balance.miniBossRewardMultiplier
$coinsPerKill = (($balance.lootGrades | Measure-Object currencyAmount -Maximum).Maximum+($balance.monsters | Measure-Object bonusCoins -Maximum).Maximum)*(1+(Amount 'yield'))*$balance.miniBossRewardMultiplier
Check ($maxSpawn*$xpPerKill -lt 1000000) 'Even a perfect all-miniboss-reward run must remain under M XP'
Check ($maxSpawn*$coinsPerKill*1.1 -lt 1000000) 'Even a perfect all-miniboss-reward run plus chests must remain under M coins'
$invalid = [ProjectDM.PrototypeGrowthTable](Get-Content "$data/PrototypeGrowth.json" -Raw | ConvertFrom-Json)
$invalid.rows[0].rankCostGrowth = .9
$skills = [ProjectDM.PrototypeSkillTable](Get-Content "$data/PrototypeSkills.json" -Raw | ConvertFrom-Json)
Check ($null -ne [ProjectDM.PrototypeRules]::Validate($balance,$skills,$invalid)) 'Invalid price curve accepted'
$invalid.rows[0].rankCostGrowth = 1.18; $invalid.rows[0].rankCostStep = -1
Check ($null -ne [ProjectDM.PrototypeRules]::Validate($balance,$skills,$invalid)) 'Negative mastery price step accepted'


foreach ($character in $characters) {
 $ratio = switch ($character.id) { projectile {1.3} lightning {2.5} flame {3} frost {3} }
 $raw = $balance.baseDamage*$character.damage*(1+(Amount "damage"))*(1+24*.15+10*.15)*$ratio*6.4*($balance.criticalMultiplier+(Amount "criticalDamage"))
 $actual = [ProjectDM.PrototypeRules]::CombatDamage($raw,$character.finalCombatScale)
 Check ($actual -ge 974900 -and $actual -le 975100) "Practical final hit must reach 975k"
 Check ([ProjectDM.PrototypeRules]::CombatScale($character.finalCombatScale,0) -eq 1) "Starting damage/HP must remain unchanged"
 foreach ($hit in @(1,10,100000,1000000,1000000000)) { Check ([ProjectDM.PrototypeRules]::CombatDamage($hit,$character.finalCombatScale) -le 999999) "Damage exceeded ceiling" }
}
Write-Output "Progression budget: $checks assertions PASS (prices, HP, rewards, practical 975k hits, actual 999999 ceiling)."
