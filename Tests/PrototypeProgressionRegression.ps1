$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Add-Type -Path @((Join-Path $projectRoot 'Assets/Scripts/Systems/Data/PrototypeProgression.cs'), (Join-Path $projectRoot 'Assets/Scripts/Systems/Pickups/LootGradeRules.cs'))
$dataRoot = Join-Path $projectRoot 'Assets/Resources/ProjectDM/Data'
$balance = [ProjectDM.PrototypeBalance](Get-Content (Join-Path $dataRoot 'PrototypeBalance.json') -Raw | ConvertFrom-Json)
$skills = [ProjectDM.PrototypeSkillTable](Get-Content (Join-Path $dataRoot 'PrototypeSkills.json') -Raw | ConvertFrom-Json)
$growth = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $dataRoot 'PrototypeGrowth.json') -Raw | ConvertFrom-Json)
$checks = 0
function Assert-True([bool] $condition, [string] $message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
function Get-Row([string] $id) { return $growth.rows | Where-Object id -eq $id }
function Get-Source([string] $id) { if ($id -in @('chainChance','splashChance','meteorChance')) { return $proposal.upgrades.proc }; return $proposal.upgrades.($id) }
Assert-True ($null -eq [ProjectDM.PrototypeRules]::Validate($balance, $skills, $growth)) 'Runtime tables must validate.'
Assert-True ($balance.ResolvedInitialMana -eq 40 -and $balance.basicAttackManaCost -eq 1 -and $balance.bossElapsedSeconds -eq 180) 'Initial mana / basic attack cost / independent boss cap mismatch.'
Assert-True ($growth.rows.Count -eq 30 -and $skills.rows.Count -eq 6) 'Missing growth or skill rows.'

# Compression keeps the approved maximum effects; intermediate purchases are now larger and interleaved.
$proposal = Get-Content (Join-Path $projectRoot 'Tools/Balance/progression_v2.json') -Raw | ConvertFrom-Json
foreach ($row in $growth.rows) {
    if ($row.id -eq 'population') { Assert-True ($row.maxRank -eq 30 -and $row.step -eq 5) 'Population growth must expand to +150 monsters.'; continue }
    $source = Get-Source $row.id
    if ($null -eq $source) { Assert-True ($row.id -in @('silverDropChance','goldDropChance','emeraldDropChance','amethystDropChance','experienceGradeChance','health','movement','criticalChance','criticalDamage','invulnerability','magnet','luck','populationCap','spawnBatch','magnetDropChance')) 'Unknown new growth'; continue }
    $separateChance = $row.id.EndsWith('Chance')
    Assert-True ($row.maxRank -le $source.max_rank -and (($separateChance -and $row.baseCost -eq 10) -or
        (-not $separateChance -and $row.baseCost -eq $source.base_cost -and $row.minStage -eq $source.min_stage))) "V2 growth cap/cost/stage mismatch: $($row.id)"
    Assert-True ([Math]::Abs($row.costGrowth - $source.cost_growth) -lt 0.00001) "Cost growth mismatch: $($row.id)"
    if ($null -ne $source.step) { Assert-True ([Math]::Abs($row.step * $row.maxRank - $source.step * $source.max_rank) -lt 0.00001) "Maximum effect changed: $($row.id)" }
}
Assert-True ([Math]::Abs($balance.ResolvedManaAbsorbChance - $proposal.time_reward.base_chance) -lt 0.000001) 'Original recovery probability must carry over to mana absorption.'

$save = [ProjectDM.PrototypeSave]::new()
Assert-True ([ProjectDM.PrototypeRules]::Stage($balance, $save) -eq 0) 'New save starts with slimes.'
$save.kills[0] = 119
Assert-True ([ProjectDM.PrototypeRules]::Stage($balance, $save) -eq 0) 'Unlock occurs too early.'
$save.kills[0] = 120
Assert-True ([ProjectDM.PrototypeRules]::Stage($balance, $save) -eq 1 -and $save.kills[0] -eq 120) 'Unlock must retain kills.'
$save.kills[2] = 600
Assert-True ([ProjectDM.PrototypeRules]::Stage($balance, $save) -eq 1) 'Later kills must not skip prerequisite species.'
$save.kills[1] = 300
Assert-True ([ProjectDM.PrototypeRules]::Stage($balance, $save) -eq 3) 'Retained later-species progress should activate when predecessors unlock.'
$save.kills[3] = 1000
Assert-True ([ProjectDM.PrototypeRules]::Stage($balance, $save) -eq 4) 'Final normal stage did not unlock.'
foreach ($stage in 0..4) {
    $weights = $balance.stages[$stage].weights
    Assert-True (($weights[0..$stage] | Where-Object { $_ -le 0 }).Count -eq 0) 'Old species must remain mixed in later stages.'
    foreach ($kind in ($stage + 1)..4) { if ($kind -gt $stage -and $kind -le 4) { Assert-True ($weights[$kind] -eq 0) 'Locked species has a spawn weight.' } }
    Assert-True ($balance.stages[$stage].initial -le $balance.stages[$stage].target -and $balance.stages[$stage].target -le $balance.stages[$stage].cap) 'Invalid population bounds.'
}

$save = [ProjectDM.PrototypeSave]::new()
$damage = Get-Row 'damage'
$gold = 13
Assert-True (-not [ProjectDM.PrototypeRules]::TryPurchase($balance, $damage, $save, 0, [ref]$gold)) 'Insufficient gold purchase accepted.'
Assert-True ($gold -eq 13 -and $save.Rank('damage') -eq 0) 'Rejected purchase changed state.'
$gold = 14
Assert-True ([ProjectDM.PrototypeRules]::TryPurchase($balance, $damage, $save, 0, [ref]$gold)) 'First purchase rejected.'
Assert-True ($gold -eq 0 -and $save.Rank('damage') -eq 1) 'Purchase must deduct exactly once.'
Assert-True ([Math]::Abs([ProjectDM.PrototypeRules]::Amount($balance, $damage, 1) - 0.66) -lt 0.000001) 'Compressed first damage upgrade must total +66%.'
Assert-True ([Math]::Abs([ProjectDM.PrototypeRules]::Amount($balance, $damage, 2) - 0.86) -lt 0.000001) 'First-purchase bonus applied twice.'
$proc = Get-Row 'chainChance'
Assert-True ($null -ne [ProjectDM.PrototypeRules]::PurchaseBlock($proc, $save, 0, 1000, 3.5)) 'Proc upgrade must require chain unlock.'
$save.SetRank('chain', 1)
Assert-True ($null -eq [ProjectDM.PrototypeRules]::PurchaseBlock($proc, $save, 0, 1000, 3.5)) 'Unlocked prerequisite not recognized.'
Assert-True ($null -ne [ProjectDM.PrototypeRules]::PurchaseBlock((Get-Row 'splash'), $save, 1, 1000, 3.5)) 'Stage-locked skill allowed too early.'
Assert-True ($null -eq [ProjectDM.PrototypeRules]::PurchaseBlock((Get-Row 'splash'), $save, 2, 1000, 3.5)) 'Stage unlock did not open skill.'
Assert-True ($null -ne [ProjectDM.PrototypeRules]::PurchaseBlock((Get-Row 'chain'), $save, 0, 1000, 3.5)) 'Max-rank skill repurchase allowed.'
foreach ($row in $growth.rows) {
    $last = 0
    foreach ($rank in 0..$row.maxRank) {
        $cost = [ProjectDM.PrototypeRules]::Cost($row, $rank, $balance.latePriceScale)
        Assert-True ($cost -ge $last -and $cost -gt 0) "Nonmonotonic cost: $($row.id)"
        $last = $cost
    }
}

$thresholds = @(7,11,16,24,36,54,80,120,180,270,404,606)
foreach ($index in 0..($thresholds.Count - 1)) {
    Assert-True ([ProjectDM.PrototypeRules]::XpThreshold($balance, $index + 1) -eq $thresholds[$index]) 'XP curve mismatch.'
}
Assert-True ([ProjectDM.PrototypeRules]::ExtendDeadline(40, 1, 180) -eq 41) 'Time reward must extend deadline.'
Assert-True (-not [ProjectDM.PrototypeRules]::BossDue(40, 180)) 'Remaining/deadline time cannot trigger boss.'
Assert-True ([ProjectDM.PrototypeRules]::ExtendDeadline(179.5, 1, 180) -eq 180) 'Time rewards must respect hard cap.'
Assert-True ([ProjectDM.PrototypeRules]::BossDue(180, 180)) 'Boss must trigger at actual elapsed cap.'
Assert-True ([ProjectDM.PrototypeRules]::Chance(.99, .04, .08) -eq 1) 'Probabilities must clamp to 100%.'
Assert-True ([ProjectDM.PrototypeRules]::Chance(-1, 0, 0) -eq 0) 'Probabilities must clamp to zero.'
Assert-True ([ProjectDM.PrototypeRules]::StochasticCoins(1.008, .001) -eq 2) 'Fractional coin bonus lost.'
Assert-True ([ProjectDM.PrototypeRules]::StochasticCoins(1.008, .5) -eq 1) 'Fractional coin rounding invalid.'
Assert-True ([ProjectDM.PrototypeRules]::StochasticCoins([int]::MaxValue * 2.0, 0) -eq [int]::MaxValue) 'Large coin count must saturate.'

$broken = [ProjectDM.PrototypeBalance](Get-Content (Join-Path $dataRoot 'PrototypeBalance.json') -Raw | ConvertFrom-Json)
$broken.stages[0].weights[4] = 1
Assert-True ($null -ne [ProjectDM.PrototypeRules]::Validate($broken, $skills, $growth)) 'Validation must reject locked spawn weights.'
$brokenGrowth = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $dataRoot 'PrototypeGrowth.json') -Raw | ConvertFrom-Json)
$brokenGrowth.rows[0].parent = 'damage'
Assert-True ($null -ne [ProjectDM.PrototypeRules]::Validate($balance, $skills, $brokenGrowth)) 'Validation must reject graph cycles.'
# Split presentation nodes keep the original saved rank and continuous cost curve.
Assert-True ($growth.nodes.Count -eq 106) 'Expected 106 growth nodes excluding root.'
foreach ($row in $growth.rows) {
    $segments = @($growth.nodes | Where-Object growthId -eq $row.id | Sort-Object startRank)
    $end = 0
    foreach ($node in $segments) {
        Assert-True ($node.startRank -eq $end) "Non-contiguous tier range: $($node.id)"
        $end += $node.ranks
    }
    Assert-True ($end -eq $row.maxRank) "Tier splitting changed max rank: $($row.id)"
}
$first = $growth.nodes | Where-Object id -eq 'damage_1'
$second = $growth.nodes | Where-Object id -eq 'damage_2'
$third = $growth.nodes | Where-Object id -eq 'damage_3'
$save = [ProjectDM.PrototypeSave]::new()
$save.SetRank('damage', 4)
$secondParent = $growth.nodes | Where-Object id -eq $second.parent
$save.SetRank($secondParent.growthId, $secondParent.startRank + $second.parentRanks)
Assert-True ([ProjectDM.PrototypeRules]::NodeRank($first, $save) -eq 3) 'Earlier tier must be completed for existing saves.'
Assert-True ([ProjectDM.PrototypeRules]::NodeRank($second, $save) -eq 1) 'Existing rank must carry into tier II.'
Assert-True ([ProjectDM.PrototypeRules]::NodeRank($third, $save) -eq 0) 'Future tier should be empty.'
$gold = 1000000
Assert-True (-not [ProjectDM.PrototypeRules]::TryPurchaseNode($balance, $damage, $first, $save, 4, [ref]$gold, $growth)) 'Completed tier must reject purchases.'
Assert-True (-not [ProjectDM.PrototypeRules]::TryPurchaseNode($balance, $damage, $third, $save, 4, [ref]$gold, $growth)) 'Future tier must reject purchases.'
Assert-True ($gold -eq 1000000 -and $save.Rank('damage') -eq 4) 'Rejected tier purchase changed save or currency.'
$expectedCost = [ProjectDM.PrototypeRules]::Cost($damage, 4, $balance.latePriceScale)
Assert-True ([ProjectDM.PrototypeRules]::TryPurchaseNode($balance, $damage, $second, $save, 4, [ref]$gold, $growth)) 'Active tier II purchase rejected.'
Assert-True ($gold -eq 1000000 - $expectedCost -and $save.Rank('damage') -eq 5) 'Tier purchase reset cost curve or saved rank.'
$save.SetRank('damage', 2)
Assert-True ($null -ne [ProjectDM.PrototypeRules]::NodePurchaseBlock($balance, $damage, $second, $save, 4, $gold, $growth)) 'Tier II opened before tier I completion.'
Assert-True ([ProjectDM.PrototypeRules]::TryPurchaseNode($balance, $damage, $first, $save, 4, [ref]$gold, $growth)) 'Last tier I purchase rejected.'
Assert-True ($null -eq [ProjectDM.PrototypeRules]::NodePurchaseBlock($balance, $damage, $second, $save, 4, $gold, $growth)) 'Next tier must open when own tier and other-growth prerequisite are complete.'
foreach ($issue in @('overlap', 'gap', 'missing_parent', 'cycle', 'missing_growth')) {
    $brokenGrowth = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $dataRoot 'PrototypeGrowth.json') -Raw | ConvertFrom-Json)
    switch ($issue) {
        'overlap' { ($brokenGrowth.nodes | Where-Object id -eq 'damage_2').startRank = 2 }
        'gap' { ($brokenGrowth.nodes | Where-Object id -eq 'damage_2').startRank = 4 }
        'missing_parent' { $brokenGrowth.nodes[0].parent = 'NOT_A_NODE' }
        'cycle' { $brokenGrowth.nodes[0].parent = $brokenGrowth.nodes[1].id; $brokenGrowth.nodes[1].parent = $brokenGrowth.nodes[0].id }
        'missing_growth' { $brokenGrowth.nodes[0].growthId = 'NOT_A_GROWTH' }
    }
    Assert-True ($null -ne [ProjectDM.PrototypeRules]::Validate($balance, $skills, $brokenGrowth)) "Invalid split accepted: $issue"
}
Assert-True ([Math]::Abs($balance.criticalChance - .05) -lt .000001 -and $balance.criticalMultiplier -eq 2) 'Temporary combat defaults mismatch.'
Assert-True ($balance.playerInvulnerability -gt 0 -and $balance.playerContactRadius -gt 0) 'Contact damage requires positive radius and immunity.'
foreach ($issue in @('criticalChance', 'criticalMultiplier', 'contactDamage', 'pickupMagnetRadius')) {
    $broken = [ProjectDM.PrototypeBalance](Get-Content (Join-Path $dataRoot 'PrototypeBalance.json') -Raw | ConvertFrom-Json)
    switch ($issue) {
        'criticalChance' { $broken.criticalChance = 1.1 }
        'criticalMultiplier' { $broken.criticalMultiplier = .9 }
        'contactDamage' { $broken.monsters[0].contactDamage = -1 }
        'pickupMagnetRadius' { $broken.pickupMagnetRadius = -1 }
    }
    Assert-True ($null -ne [ProjectDM.PrototypeRules]::Validate($broken, $skills, $growth)) "Invalid combat config accepted: $issue"
}
# Every repeatable branch now mixes different growth types and has a real purchase gate.
$playerGrowthIds = @('health','movement','criticalChance','criticalDamage','invulnerability','magnet','luck')
foreach ($id in $playerGrowthIds) {
    $row = Get-Row $id
    Assert-True ($row.maxRank -eq 3 -and @($growth.nodes | Where-Object growthId -eq $id).Count -eq 1) "Player growth must have exactly one three-purchase node: $id"
}
$legacyGrowth = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $dataRoot 'PrototypeGrowth.json') -Raw | ConvertFrom-Json)
$optionalGrowthIds = $playerGrowthIds + @('populationCap','spawnBatch','magnetDropChance')
$legacyGrowth.rows = @($legacyGrowth.rows | Where-Object id -notin $optionalGrowthIds)
$legacyGrowth.nodes = @($legacyGrowth.nodes | Where-Object growthId -notin $optionalGrowthIds)
Assert-True ($null -eq [ProjectDM.PrototypeRules]::Validate($balance, $skills, $legacyGrowth)) 'Previously authored growth tables without player attributes must still validate.'
Assert-True ((Get-Row 'criticalDamage').name -eq '치명타 피해량') 'Critical damage uses the requested Korean display name.'
foreach ($id in @('playerContactRadius','projectileSpeed','projectileLifetime')) {
    Assert-True (@($growth.rows | Where-Object id -eq $id).Count -eq 0) "Excluded attribute became a growth: $id"
}
$totalPurchases = ($growth.rows | Measure-Object maxRank -Sum).Sum
Assert-True ($totalPurchases -eq 298) 'Unexpected radial total purchase count.'
foreach ($node in $growth.nodes) {
    Assert-True ($node.ranks -le 3) "Node still requires excessive repeated purchases: $($node.id)"
    if ($node.parent -ne 'ROOT') {
        $parent = $growth.nodes | Where-Object id -eq $node.parent
        Assert-True ($null -ne $parent) "Growth tier has no purchase parent: $($node.id)"
        Assert-True ($node.parentRanks -ge 1 -and $node.parentRanks -le $parent.ranks) "Missing actual parent gate: $($node.id)"
    }
}
$save = [ProjectDM.PrototypeSave]::new()
$gold = 1000000
$speedFirst = $growth.nodes | Where-Object id -eq 'chainChance_1'
Assert-True ($null -ne [ProjectDM.PrototypeRules]::NodeUnlockBlock($growth, $speedFirst, $save)) 'Speed I must start locked behind damage I.'
Assert-True ($null -ne [ProjectDM.PrototypeRules]::NodeUnlockBlock($null, $speedFirst, $save)) 'Missing prerequisite table must fail closed, not bypass gates.'
Assert-True (-not [ProjectDM.PrototypeRules]::TryPurchaseNode($balance, (Get-Row 'chainChance'), $speedFirst, $save, 4, [ref]$gold, $growth)) 'UI purchase gate allowed bypassing another growth.'
Assert-True ($gold -eq 1000000 -and $save.Rank('chainChance') -eq 0) 'Rejected cross-growth purchase changed state.'
Assert-True ([ProjectDM.PrototypeRules]::TryPurchaseNode($balance, $damage, $first, $save, 4, [ref]$gold, $growth)) 'Root purchase rejected.'
$save.SetRank('chain', 1)
Assert-True ($null -eq [ProjectDM.PrototypeRules]::NodeUnlockBlock($growth, $speedFirst, $save)) 'Early branch must open after just one parent purchase.'
$save.SetRank('damage', 3)
Assert-True ($null -ne [ProjectDM.PrototypeRules]::NodeUnlockBlock($growth, $second, $save)) 'Damage II must require an intervening different growth.'
$save.SetRank($secondParent.growthId, $secondParent.startRank + $second.parentRanks)
Assert-True ($null -eq [ProjectDM.PrototypeRules]::NodeUnlockBlock($growth, $second, $save)) 'Intervening growth should unlock damage II.'
$walk = [ProjectDM.PrototypeSave]::new()
$walkGold = [int]::MaxValue
$purchases = 0
do {
    $advanced = $false
    foreach ($node in $growth.nodes) {
        if ([ProjectDM.PrototypeRules]::TryPurchaseNode($balance, (Get-Row $node.growthId), $node, $walk, 4, [ref]$walkGold, $growth)) {
            $advanced = $true
            $purchases++
        }
    }
} while ($advanced -and $purchases -le $totalPurchases)
Assert-True ($purchases -eq $totalPurchases) 'Purchase graph contains unreachable nodes or a deadlock.'
foreach ($row in $growth.rows) { Assert-True ($walk.Rank($row.id) -eq $row.maxRank) "Growth cannot reach its cap: $($row.id)" }
# Discover only open nodes and their direct next nodes, not descendants of locked previews.
$fresh = [ProjectDM.PrototypeSave]::new()
$preview = $growth.nodes | Where-Object id -eq 'chainChance_1'
$distant = $growth.nodes | Where-Object id -eq 'damage_4'
Assert-True (-not [ProjectDM.PrototypeRules]::IsNodeOpen($growth, $preview, $fresh, 0)) 'Locked preview must not count as open.'
Assert-True ([ProjectDM.PrototypeRules]::IsNodeRevealed($growth, $preview, $fresh, 0)) 'Open skill unlock must reveal its immediate chance node.'
Assert-True (-not [ProjectDM.PrototypeRules]::IsNodeRevealed($growth, $distant, $fresh, 0)) 'Preview-only nodes must not recursively reveal distant nodes.'
$fresh.SetRank('chain', 1)
Assert-True ([ProjectDM.PrototypeRules]::IsNodeOpen($growth, $preview, $fresh, 0)) 'Buying unlock must open its chance node immediately.'
$fractional = [ProjectDM.PrototypeSave]::new(); $fractional.SetRank('damage', $distant.startRank, .2)
Assert-True ([ProjectDM.PrototypeRules]::IsNodeRevealed($growth, $distant, $fractional, 0)) 'Migrated fractional investment must never be concealed.'
foreach ($stage in 0..4) {
    foreach ($node in $growth.nodes) {
        $row = Get-Row $node.growthId
        $eligible = $null -eq [ProjectDM.PrototypeRules]::NodePurchaseBlock($balance, $row, $node, $fresh, $stage, [int]::MaxValue, $growth)
        if ($eligible) { Assert-True ([ProjectDM.PrototypeRules]::IsNodeRevealed($growth, $node, $fresh, $stage)) 'An eligible purchase must always be discoverable.' }
        $parent = $growth.nodes | Where-Object id -eq $node.parent
        $expected = $node.parent -eq 'ROOT' -or [ProjectDM.PrototypeRules]::IsNodeOpen($growth, $node, $fresh, $stage) -or
            ($null -ne $parent -and [ProjectDM.PrototypeRules]::IsNodeOpen($growth, $parent, $fresh, $stage))
        Assert-True ([ProjectDM.PrototypeRules]::IsNodeRevealed($growth, $node, $fresh, $stage) -eq $expected) "Visibility exceeds one link: $($node.id)"
        Assert-True ([ProjectDM.PrototypeRules]::IsNodeRevealed($growth, $node, $walk, $stage)) 'Completed saves must retain all discovered nodes.'
    }
}
# Keep old saves' EXACT effects, including a fraction smaller than one new purchase.
foreach ($row in $growth.rows) {
    $old = Get-Source $row.id
    if ($null -eq $old) { continue } # Grade chance is new, not a compressed legacy purchase.
    for ($oldRank = 0; $oldRank -le $old.max_rank; $oldRank++) {
        $migrated = [ProjectDM.PrototypeSave]::new()
        $migrated.version = 1
        $migrated.completedRuns = 7
        $migrated.kills[0] = 99
        $migrated.SetRank($(if ($row.id.EndsWith('Chance')) { 'proc' } else { $row.id }), $oldRank)
        [ProjectDM.PrototypeRules]::MigrateGrowthRanks($migrated, $growth)
        $expected = $oldRank * $old.step
        if ($row.id -eq 'damage' -and $oldRank -gt 0) { $expected += $balance.firstDamageBonus }
        $actual = [ProjectDM.PrototypeRules]::Amount($balance, $row, $migrated.Rank($row.id), $migrated.PartialRank($row.id))
        Assert-True ([Math]::Abs($actual - $expected) -lt .00001) "Save migration changed growth effect: $($row.id) rank $oldRank"
        $before = $migrated.Rank($row.id) + $migrated.PartialRank($row.id)
        [ProjectDM.PrototypeRules]::MigrateGrowthRanks($migrated, $growth)
        Assert-True ($migrated.version -eq 3 -and $migrated.completedRuns -eq 7 -and $migrated.kills[0] -eq 99 -and
            [Math]::Abs($before - $migrated.Rank($row.id) - $migrated.PartialRank($row.id)) -lt .00000001) 'Migration ran twice or changed unrelated progress.'
    }
    $legacyRow = [ProjectDM.PrototypeGrowth]::new()
    $legacyRow.baseCost = $old.base_cost
    $legacyRow.costGrowth = $row.costGrowth
    $legacyTotal = 0L
    for ($rank = 0; $rank -lt $old.max_rank; $rank++) { $legacyTotal += [ProjectDM.PrototypeRules]::Cost($legacyRow, $rank, $balance.latePriceScale) }
    $newTotal = 0L
    $compressedRanks = if ($row.legacyCompressedMaxRank -gt 0) { $row.legacyCompressedMaxRank } else { $row.maxRank }
    for ($rank = 0; $rank -lt $compressedRanks; $rank++) { $newTotal += [ProjectDM.PrototypeRules]::Cost($row, $rank, $balance.latePriceScale) }
    if ($row.id.EndsWith('Chance')) {
        Assert-True ($newTotal -ge .25 * $legacyTotal -and $newTotal -le .45 * $legacyTotal) "Individual skill chance costs must share the old common budget: $($row.id)"
    } else {
        Assert-True ($newTotal -ge .9 * $legacyTotal -and $newTotal -le 1.001 * $legacyTotal) "Compression destroyed total economic cost: $($row.id)"
    }
    Assert-True ([ProjectDM.PrototypeRules]::Cost($row, 0, $balance.latePriceScale) -eq $row.baseCost) 'First purchase must remain affordable.'
}
$migrated = [ProjectDM.PrototypeSave]::new()
$migrated.version = 1
$migrated.SetRank('damage', 1)
[ProjectDM.PrototypeRules]::MigrateGrowthRanks($migrated, $growth)
Assert-True ($migrated.Rank('damage') -eq 0 -and [Math]::Abs($migrated.PartialRank('damage') - .2) -lt .000001) 'A small old purchase must not disappear after compression.'
$gold = 1000
Assert-True ([ProjectDM.PrototypeRules]::TryPurchaseNode($balance, $damage, $first, $migrated, 4, [ref]$gold, $growth)) 'Migrated partial-rank purchase rejected.'
Assert-True ([Math]::Abs([ProjectDM.PrototypeRules]::Amount($balance, $damage, $migrated.Rank('damage'), $migrated.PartialRank('damage')) - .7) -lt .000001) 'New purchase consumed the migrated residual effect.'
foreach ($issue in @('parent_overflow', 'implicit_cycle', 'cost_scale')) {
    $brokenGrowth = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $dataRoot 'PrototypeGrowth.json') -Raw | ConvertFrom-Json)
    switch ($issue) {
        'parent_overflow' { $brokenGrowth.nodes[4].parentRanks = 100 }
        'implicit_cycle' {
            $brokenGrowth.nodes[0].parent = 'damage_2'; $brokenGrowth.nodes[0].parentRanks = 1
            $next = $brokenGrowth.nodes | Where-Object id -eq 'damage_2'; $next.parent = 'ROOT'; $next.parentRanks = 0
        }
        'cost_scale' { $brokenGrowth.rows[0].legacyMaxRank = 1 }
    }
    Assert-True ($null -ne [ProjectDM.PrototypeRules]::Validate($balance, $skills, $brokenGrowth)) "Invalid cross-growth config accepted: $issue"
}
$runtimeUI = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Systems/UI/Growth/GrowthGraphView.cs') -Raw
$runtimeGraph = $runtimeUI
Assert-True ($runtimeGraph -match '(?s)internal Rect GetMetaTreeNodeHitRect\([^}]+return GetMetaTreeNodeRect\(node, canvasSize\);\s*}') 'Node pointer hit area must be card-only, excluding titles at every zoom.'
Assert-True ($runtimeUI.Contains('new MetaTreeNodePresentation("ROOT", personalGraph') -and $runtimeUI.Contains(': "기본 공격 해금"') -and -not $runtimeUI.Contains('"기원의 문"')) 'Central node must represent basic attack unlock, or the selected personal-magic character.'
Assert-True ($runtimeUI.Contains('PrototypeRules.IsNodeRevealed(tables.growthTable, node, save.progress, stage)') -and
    $runtimeUI.Contains('revealed ? node.name : ""') -and $runtimeUI.Contains('revealed ? row.description : ""')) 'Hidden presentations must redact their title and description.'
Assert-True ($runtimeGraph -match '(?s)if \(node.state == MetaTreeNodeState.Hidden\)\s*\{(?:(?!\}).)*return;\s*}') 'Hidden cards must return before drawing icon, text, or click controls.'
Assert-True ($runtimeGraph.Contains('metaTreeNodes[i].state != MetaTreeNodeState.Hidden') -and
    $runtimeUI.Contains('if (node.state == MetaTreeNodeState.Hidden) return;')) 'Hidden nodes must be excluded from pointer and tooltip handling.'
Assert-True ($runtimeUI -match 'NodeUnlockBlock\(tables.growthTable, node, save.progress\)') 'Graph lock appearance must use purchase prerequisites.'
Assert-True ($runtimeUI -match 'TryPurchaseNode\([^\r\n]+ref save.metaCurrency, tables.growthTable\)') 'Runtime purchase must enforce the same graph prerequisites.'
# Retain the retired radial-layout regression as a compatibility fixture; current maps use authored grids.
$currentGrowth = $growth
$growth = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $PSScriptRoot 'Fixtures/LegacyGrowthRadial.json') -Raw | ConvertFrom-Json)
Assert-True (@($growth.nodes | Where-Object parent -eq 'ROOT').Count -eq 8) 'Radial graph must start in eight directions.'
$ringCounts = @(8, 12, 16, 16, 13)
for ($ring = 0; $ring -lt 5; $ring++) {
    $radius = 230 + $ring * 190
    $members = @($growth.nodes | Where-Object { [Math]::Abs([Math]::Sqrt($_.x * $_.x + $_.y * $_.y) - $radius) -lt .1 })
    Assert-True ($members.Count -eq $ringCounts[$ring]) "Wrong radial ring size: $ring"
    Assert-True (@($members.growthId | Select-Object -Unique).Count -ge 6) 'Each ring must mix multiple growth types.'
}
foreach ($node in $growth.nodes | Where-Object parent -ne 'ROOT') {
    $parent = $growth.nodes | Where-Object id -eq $node.parent
    Assert-True ([Math]::Sqrt($parent.x * $parent.x + $parent.y * $parent.y) -le [Math]::Sqrt($node.x * $node.x + $node.y * $node.y) + .1) 'Visible connections must grow outward.'
    $distance = [Math]::Sqrt([Math]::Pow($node.x - $parent.x, 2) + [Math]::Pow($node.y - $parent.y, 2))
    Assert-True ($distance -le 380) "Connection spans distant sectors: $($node.id) -> $($parent.id) ($distance)"
}
foreach ($skillId in @('chain', 'splash', 'meteor')) {
    $chanceNode = $growth.nodes | Where-Object id -eq ($skillId + 'Chance_1')
    Assert-True ($chanceNode.parent -eq ($skillId + '_1')) "First skill chance must connect directly to its nearby unlock: $skillId"
}
# At the supported zooms, a 48-unit clearance covers the scaled card half-diagonal.
foreach ($node in $growth.nodes) {
    $parent = if ($node.parent -eq 'ROOT') { [pscustomobject]@{x=0; y=0; id='ROOT'} } else { $growth.nodes | Where-Object id -eq $node.parent }
    $dx = $node.x - $parent.x; $dy = $node.y - $parent.y
    foreach ($other in $growth.nodes | Where-Object { $_.id -ne $node.id -and $_.id -ne $parent.id }) {
        $along = (($other.x - $parent.x) * $dx + ($other.y - $parent.y) * $dy) / ($dx * $dx + $dy * $dy)
        if ($along -le 0 -or $along -ge 1) { continue }
        $clearance = [Math]::Sqrt([Math]::Pow($other.x - $parent.x - $along * $dx, 2) + [Math]::Pow($other.y - $parent.y - $along * $dy, 2))
        Assert-True ($clearance -ge 48) "Connection crosses another card: $($node.id) -> $($parent.id) / $($other.id)"
    }
}
foreach ($resolution in @(@(1920,1080), @(1280,720), @(960,540))) {
    $width = $resolution[0]; $height = $resolution[1]
    $zoom = [ProjectDM.PrototypeRules]::GraphFitZoom($growth.nodes, $width, $height)
    $size = 66 * [Math]::Max(.2, [Math]::Min(1.2, $zoom * 1.45))
    $rootSize = 82 * $size / 66
    Assert-True ($zoom -ge .12 -and $zoom -le 1.05) 'Fit zoom outside input limits.'
    for ($i = 0; $i -lt $growth.nodes.Count; $i++) {
        $node = $growth.nodes[$i]
        Assert-True ([Math]::Abs($node.x) * $zoom + $size / 2 -lt $width / 2 - 12 -and
            [Math]::Abs($node.y) * $zoom + $size / 2 + $(if ($zoom -ge .4) {37} else {0}) -lt $height / 2 - 12) 'Whole-view node or title clipped.'
        Assert-True ([Math]::Abs($node.x) * $zoom -ge ($size + $rootSize) / 2 -or [Math]::Abs($node.y) * $zoom -ge ($size + $rootSize) / 2) 'Root overlaps an inner-ring node.'
        for ($j = $i + 1; $j -lt $growth.nodes.Count; $j++) {
            $other = $growth.nodes[$j]
            Assert-True ([Math]::Abs($node.x - $other.x) * $zoom -ge $size -or
                [Math]::Abs($node.y - $other.y) * $zoom -ge $size) "Radial node boxes overlap: $($node.id)/$($other.id)"
        }
    }
}
$growth = $currentGrowth
Assert-True ($null -eq (Get-Row 'proc') -and @(Get-Row 'chainChance').Count -eq 1 -and
    @(Get-Row 'splashChance').Count -eq 1 -and @(Get-Row 'meteorChance').Count -eq 1) 'Permanent common proc must be replaced by three independent growths.'
foreach ($version in @(1, 2)) {
    $limit = if ($version -eq 1) {75} else {15}
    foreach ($oldRank in 0..$limit) {
        $save = [ProjectDM.PrototypeSave]::new()
        $save.version = $version
        $save.SetRank('proc', $oldRank)
        [ProjectDM.PrototypeRules]::MigrateGrowthRanks($save, $growth)
        $expected = $oldRank * $(if ($version -eq 1) {.0008} else {.004})
        foreach ($id in @('chainChance','splashChance','meteorChance')) {
            Assert-True ([Math]::Abs([ProjectDM.PrototypeRules]::Amount($balance, (Get-Row $id), $save.Rank($id), $save.PartialRank($id)) - $expected) -lt .000001) 'Common proc migration must retain its effect on every skill.'
            if ($oldRank -eq $limit) { Assert-True ($save.Rank($id) -eq 9) 'A fully completed old proc must not leave a nearly-complete fractional tier locked.' }
        }
        Assert-True ($save.Rank('proc') -eq $oldRank) 'Archived common proc rank must remain recoverable.'
    }
}
$partial = [ProjectDM.PrototypeSave]::new(); $partial.version = 2; $partial.SetRank('proc', 7, .4)
[ProjectDM.PrototypeRules]::MigrateGrowthRanks($partial, $growth)
Assert-True ([Math]::Abs([ProjectDM.PrototypeRules]::Amount($balance, (Get-Row 'meteorChance'), $partial.Rank('meteorChance'), $partial.PartialRank('meteorChance')) - .0296) -lt .000001) 'Version 2 partial common proc must survive splitting.'
$runtimeCombat = (Get-Content (Join-Path $projectRoot 'Assets/Scripts/Systems/Combat/SkillSystem.cs') -Raw) +
    (Get-Content (Join-Path $projectRoot 'Assets/Scripts/Systems/LevelUp/LevelUpController.cs') -Raw)
Assert-True ($runtimeCombat.Contains('PermanentAmount(row.id + "Chance")') -and -not $runtimeCombat.Contains('PermanentAmount("proc")')) 'Real combat and card preview must use individual permanent skill chances.'
$badNodes = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $dataRoot 'PrototypeGrowth.json') -Raw | ConvertFrom-Json)
$badNodes.nodes[0].ranks = 4
Assert-True ($null -ne [ProjectDM.PrototypeRules]::Validate($balance, $skills, $badNodes)) 'Graph nodes must reject more than three purchases.'
foreach ($zoom in @(.12, .2, .46, 1, 1.45)) {
    $size = 66 * [Math]::Max(.2, [Math]::Min(1.2, $zoom * 1.45))
    foreach ($slots in 1..3) {
        $padding = [Math]::Min(6, $size * .15); $gap = [Math]::Min(3, $size * .06)
        Assert-True (($size - $padding * 2 - $gap * ($slots - 1)) / $slots -gt 0) 'Progress segments became negative at small overview zoom.'
    }
}
$fallback = [ProjectDM.PrototypeGrowthTable]::new(); $fallback.rows = $growth.rows
$fallback.nodes = [ProjectDM.PrototypeRules]::CreateFallbackNodes($growth.rows)
Assert-True ($null -eq [ProjectDM.PrototypeRules]::Validate($balance, $skills, $fallback)) 'Row-only fallback graph must validate.'
Assert-True (@($fallback.nodes | Where-Object ranks -gt 3).Count -eq 0) 'Row-only tables must also respect the three-purchase limit.'
foreach ($row in $growth.rows) {
    $sum = ($fallback.nodes | Where-Object growthId -eq $row.id | Measure-Object ranks -Sum).Sum
    Assert-True ($sum -eq $row.maxRank) 'Fallback split must preserve every purchase.'
}
Write-Output "Prototype progression: $checks assertions PASS (one-link reveal/concealment, radial fit/connections, <=3 purchases, independent chances, gates/no deadlocks, V1/V2 saves, costs, XP/time/rewards)."
