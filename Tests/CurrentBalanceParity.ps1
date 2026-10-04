param([string] $ResultsPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $ResultsPath) { $ResultsPath = Join-Path $projectRoot 'Temp/Balance-current.json' }
$result = Get-Content -LiteralPath $ResultsPath -Raw | ConvertFrom-Json
Add-Type -Path @((Join-Path $projectRoot 'Assets/Scripts/Systems/Data/PrototypeProgression.cs'), (Join-Path $projectRoot 'Assets/Scripts/Systems/Pickups/LootGradeRules.cs'))
$dataRoot = Join-Path $projectRoot 'Assets/Resources/ProjectDM/Data'
$balance = [ProjectDM.PrototypeBalance](Get-Content (Join-Path $dataRoot 'PrototypeBalance.json') -Raw | ConvertFrom-Json)
$growth = [ProjectDM.PrototypeGrowthTable](Get-Content (Join-Path $dataRoot 'PrototypeGrowth.json') -Raw | ConvertFrom-Json)
$checks = 0
function Assert-True([bool] $condition, [string] $message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
foreach ($file in Get-ChildItem $dataRoot -Filter 'Prototype*.json') {
    Assert-True ((Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower() -eq $result.table_sha256.($file.Name)) "Stale model table: $($file.Name)"
}
Assert-True ((Get-FileHash (Join-Path $projectRoot 'Assets/Scripts/Systems/Data/PrototypeProgression.cs')).Hash.ToLower() -eq $result.rules_sha256) 'Stale runtime rules hash'
Assert-True ((Get-FileHash (Join-Path $projectRoot 'Assets/Scripts/Systems/Pickups/LootGradeRules.cs')).Hash.ToLower() -eq $result.grade_rules_sha256) 'Stale grade rules hash'
foreach ($case in $result.loot_cases) {
    $monster = $balance.monsters[$case.kind]
    $baseGrade = if ($case.currency) { $monster.currencyGrade } else { $monster.experienceGrade }
    $grade = if ($case.currency) { [ProjectDM.LootGradeRules]::ResolveCurrencyGrade($balance,$monster,$case.stage,[double[]]$case.bonuses,$case.sample) } else { [ProjectDM.LootGradeRules]::ResolveGrade($balance,$baseGrade,$case.stage,$case.chance,$case.sample) }
    Assert-True ($grade -eq $case.grade) 'Model/runtime grade mismatch'
    $reward = [ProjectDM.LootGradeRules]::Reward($balance,$monster,$grade,$case.currency,$case.quantity,$case.bonus_coin,$case.rounding_sample)
    Assert-True ([Math]::Abs($reward-$case.reward) -lt .000001) 'Model/runtime graded payout mismatch'
}
foreach ($row in $growth.rows) {
    for ($rank = 0; $rank -lt $row.maxRank; $rank++) {
        Assert-True ([ProjectDM.PrototypeRules]::Cost($row, $rank, $balance.latePriceScale) -eq $result.costs.($row.id)[$rank]) "Model/runtime price mismatch: $($row.id) $rank"
    }
}
foreach ($policy in $result.policies.PSObject.Properties) {
    $save = [ProjectDM.PrototypeSave]::new()
    [int]$wallet = 0
    foreach ($run in $policy.Value.example_seed_0.records) {
        foreach ($row in $growth.rows) {
            [int]$startingRank = $run.starting_ranks.($row.id)
            Assert-True ($save.Rank($row.id) -eq $startingRank) "Model must snapshot growth only between runs: $($row.id)"
        }
        foreach ($kind in 0..4) { $save.kills[$kind] += $run.kills_by_kind[$kind] }
        $stage = [ProjectDM.PrototypeRules]::Stage($balance, $save)
        Assert-True ($stage -eq $run.stage) 'Model/runtime monster-stage mismatch'
        $wallet += $run.coins
        foreach ($purchase in $run.purchases) {
            $node = $growth.nodes | Where-Object id -eq $purchase.node
            $row = $growth.rows | Where-Object id -eq $node.growthId
            Assert-True ([ProjectDM.PrototypeRules]::Cost($row, $save.Rank($row.id), $balance.latePriceScale) -eq $purchase.cost) 'Purchase cost changed'
            Assert-True ([ProjectDM.PrototypeRules]::TryPurchaseNode($balance, $row, $node, $save, $stage, [ref]$wallet, $growth)) "Illegal model purchase: $($purchase.node)"
            Assert-True ($save.Rank($row.id) -eq $purchase.rank) 'Purchased rank mismatch'
        }
        Assert-True ($wallet -eq $run.wallet) 'Wallet lost or duplicated currency'
    }
}
$priceCount = ($growth.rows | Measure-Object maxRank -Sum).Sum
Write-Output "Current balance parity: $checks assertions PASS (table/rules hashes, all $priceCount prices, actual C# node purchase replay, stages, wallets, snapshots)."
