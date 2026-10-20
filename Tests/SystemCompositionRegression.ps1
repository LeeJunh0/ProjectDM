$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$manager = Get-Content (Join-Path $projectRoot 'Assets/Scripts/GameManager.cs') -Raw
$systemRoot = Join-Path $projectRoot 'Assets/Scripts/Systems'
$checks = 0
function Assert-True([bool] $condition, [string] $message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
Assert-True ($manager -match 'public sealed class GameManager : MonoBehaviour' -and $manager -notmatch '\bpartial\b') 'GameManager must be one non-partial composition root.'
Assert-True (($manager -split "`n").Count -lt 500) 'Coordinator regressed into a large implementation class.'
Assert-True ($manager -notmatch 'List<(Enemy|Projectile|Pickup)>|Dictionary<string, (int|PrototypeGrowth)>') 'Gameplay collections must be owned by injected systems, not GameManager.'
Assert-True ($manager -notmatch '\bGUI\.(Button|Label|DrawTexture|HorizontalSlider)\(') 'Coordinator must route views, not draw UI elements.'
foreach ($method in @('ApplyPrototypeBasicHit','SpawnPrototypeEnemy','Collect','BuildPrototypeGraph','DrawRunUpgradeCard')) {
    Assert-True ($manager -notmatch ('\b(?:void|bool) ' + $method + '\(')) "Gameplay implementation stayed in coordinator: $method"
}
$sources = @{}
foreach ($path in (Get-ChildItem -LiteralPath $systemRoot -Filter '*.cs' -Recurse)) {
    Assert-True (-not $sources.ContainsKey($path.BaseName)) "Duplicate source name: $($path.BaseName)"
    Assert-True (Test-Path -LiteralPath ($path.FullName + '.meta')) "Script meta missing: $($path.FullName)"
    $sources[$path.BaseName] = Get-Content -LiteralPath $path.FullName -Raw
}
foreach ($folder in @('Core','Data','Persistence','Run','Combat','Player','Pickups','World','Assets','Progression','LevelUp',
    'UI','UI/Common','UI/Growth','UI/Damage','UI/Results')) {
    Assert-True (Test-Path -LiteralPath (Join-Path $systemRoot $folder) -PathType Container) "Missing feature folder: $folder"
    Assert-True (Test-Path -LiteralPath (Join-Path $systemRoot ($folder + '.meta'))) "Folder meta missing: $folder"
}
Assert-True (@(Get-ChildItem -LiteralPath $systemRoot -Filter '*.cs').Count -eq 0) 'System sources must live inside feature folders.'
foreach ($name in @('RunController','RunMana','SaveService','ProgressionService','EnemySystem','CombatSystem','SkillSystem','PickupSystem',
    'GameplayCamera','PlayerWorld','PlayerStats','PlayerCombatSystem','GrowthGraphView','LevelUpController','LevelUpView','RunHudView','RunResultView')) {
    Assert-True ($sources.ContainsKey($name)) "Missing independent system: $name"
    $code = $sources[$name] -replace '(?m)//[^\r\n]*', ''
    Assert-True ($code -notmatch '\bGameManager\b|\bpartial\s+class\b|Find(?:Any|First)ObjectByType|GetComponent<GameManager>') "System must never receive/look up the coordinator: $name"
    Assert-True ($code -match ('internal ' + $name + '\(')) "Missing constructor injection: $name"
}
# Instance-constructor dependencies form a DAG; callbacks are explicit narrow boundaries.
$dependencies = @{}
foreach ($name in $sources.Keys) {
    $match = [regex]::Match($sources[$name], ('internal ' + $name + '\(([^)]*)\)'))
    if (-not $match.Success) { continue }
    $types = @([regex]::Matches($match.Groups[1].Value, '\b([A-Z]\w+)\s+\w+\b') | ForEach-Object { $_.Groups[1].Value })
    $dependencies[$name] = @($types | Where-Object { $sources.ContainsKey($_) })
}
function Test-NoCycle([string] $name, [string[]] $trail) {
    Assert-True ($name -notin $trail) "Constructor dependency cycle: $($trail -join ' -> ') -> $name"
    foreach ($dependency in $dependencies[$name]) { Test-NoCycle $dependency ($trail + $name) }
}
foreach ($name in $dependencies.Keys) { Test-NoCycle $name @() }

# Existing Unity serialization paths and Inspector bindings are unchanged.
$settings = $sources.GameSettings
$inspector = Get-Content (Join-Path $projectRoot 'Assets/Editor/GameManagerInspector.cs') -Raw
$properties = @([regex]::Matches($inspector, 'DrawProperty\("(\w+)"') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
Assert-True ($properties.Count -eq 57) 'Inspector property coverage changed unexpectedly.'
foreach ($property in $properties) {
    Assert-True ($manager -match ('\bprivate\s+[\w<>]+\s+' + $property + '\b')) "Serialized Inspector binding lost: $property"
    Assert-True ($settings -match ('\binternal\s+[\w<>]+\s+' + $property + '\b')) "Injected setting missing: $property"
    Assert-True ($manager.Contains("settings.$property = $property;")) "Live setting copy missing: $property"
}
Assert-True ($sources.SaveService.Contains('sandboxThisSession = sandbox;') -and $sources.SaveService.Contains('if (!sandboxThisSession)')) 'Sandbox policy must stay captured at session start.'
Assert-True ($sources.ProgressionService.Contains('internal void ResetSnapshot()') -and $manager.Contains('progression.ResetSnapshot();')) 'Purchased growth must still become active at next-run reset.'
Assert-True ($sources.SkillSystem.Contains('bool chain = RollAttackEffect("chain"), splash = RollAttackEffect("splash"), meteor = RollAttackEffect("meteor");')) 'Per-hit effect rolls must remain separate and occur before lethal damage.'
Assert-True ($sources.CombatSystem.Contains('skills.ApplyPrototypeBasicHit(hit, bolt.damage)')) 'Every actual projectile hit must pass through independent skill rolls.'
Assert-True ($sources.RunController.Contains('if (runEnded) return;') -and $sources.EnemySystem.Contains('enemy.dead = true;')) 'End/death ownership must remain idempotent.'
foreach ($key in @('PROJECT_DM_META_GOLD','PROJECT_DM_DAMAGE_LEVEL','PROJECT_DM_HASTE_LEVEL','PROJECT_DM_FORTUNE_LEVEL',
    'PROJECT_DM_PROTOTYPE_V2','PROJECT_DM_PROTOTYPE_V2_BEFORE_COMPRESSION','PROJECT_DM_PROTOTYPE_V2_BEFORE_RADIAL')) {
    Assert-True ($sources.SaveService.Contains('"' + $key + '"')) "Save key changed: $key"
}
Write-Output "System composition: $checks assertions PASS (independent classes, injection/no coordinator lookup, constructor DAG, Korean Inspector/live settings, save keys, next-run snapshot, per-hit rolls)."
