$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Add-Type -Path @((Join-Path $projectRoot 'Assets/Scripts/Systems/Data/PrototypeProgression.cs'), (Join-Path $projectRoot 'Assets/Scripts/Systems/Pickups/LootGradeRules.cs'))
$dataRoot = Join-Path $projectRoot 'Assets/Resources/ProjectDM/Data'
$balance = [ProjectDM.PrototypeBalance](Get-Content -Raw (Join-Path $dataRoot 'PrototypeBalance.json') | ConvertFrom-Json)
$skills = [ProjectDM.PrototypeSkillTable](Get-Content -Raw (Join-Path $dataRoot 'PrototypeSkills.json') | ConvertFrom-Json)
$growth = [ProjectDM.PrototypeGrowthTable](Get-Content -Raw (Join-Path $dataRoot 'PrototypeGrowth.json') | ConvertFrom-Json)
$checks=0
function Assert-True([bool]$condition,[string]$message) { if(-not $condition){throw $message};$script:checks++ }
Assert-True ($null -eq [ProjectDM.PrototypeRules]::Validate($balance,$skills,$growth)) 'Goal line table must validate'
Assert-True ($growth.goalLine.Count -eq 4 -and $growth.rows.Count -eq 30 -and $growth.nodes.Count -eq 106) 'Two stage/boss pairs must be separate from 30 growth rows / 106 purchasing nodes'
$layout=[ProjectDM.PrototypeRules]::CreateGraphLayout($growth)
Assert-True ($layout.Count -eq $growth.nodes.Count + $growth.goalLine.Count) 'Layout must include both growth and goal lines'
$parent='ROOT';$y=0
for($i=0;$i -lt $growth.goalLine.Count;$i++) {
    $goal=$growth.goalLine[$i]
    Assert-True ($goal.x -eq 0 -and $goal.y -lt $y -and $goal.parent -eq $parent) 'Goals must form a single northbound root path'
    Assert-True ($goal.kind -eq $(if($i%2 -eq 0){'stage'}else{'boss'}) -and $goal.stage -eq [Math]::Floor($i/2)+1) 'Stage/boss pairs must be ordered independently of monster tiers'
    Assert-True (@($growth.rows | Where-Object id -eq $goal.id).Count -eq 0) 'Placeholder goal must not accidentally become a purchasable stat'
    if($goal.kind -eq 'boss'){Assert-True ($goal.limitSeconds -eq 180) 'Temporary boss limit should describe elapsed 180 seconds, not remaining time'}
    $parent=$goal.id;$y=$goal.y
}
Assert-True ($growth.goalLine[-1].final) 'Last placeholder must clearly mark the final goal'
# Goal connectors are straight; ordinary growth connectors may use routed paths.
foreach($n in $layout | Where-Object { $_.id -in $growth.goalLine.id }) {
    $p=if($n.parent -eq 'ROOT'){[pscustomobject]@{x=0;y=0;id='ROOT'}}else{$layout | Where-Object id -eq $n.parent}
    $dx=$n.x-$p.x;$dy=$n.y-$p.y
    foreach($o in $layout | Where-Object {$_.id -ne $n.id -and $_.id -ne $p.id}) {
        $t=(($o.x-$p.x)*$dx+($o.y-$p.y)*$dy)/($dx*$dx+$dy*$dy)
        if($t -le 0 -or $t -ge 1){continue}
        $clearance=[Math]::Sqrt([Math]::Pow($o.x-$p.x-$t*$dx,2)+[Math]::Pow($o.y-$p.y-$t*$dy,2))
        Assert-True ($clearance -ge 48) "Objective corridor causes a connector to cross a card: $($n.id)/$($o.id)"
    }
}
foreach($zoom in @(.04,.12,.2,.4,.75,1.45)) {
    $size=66*[Math]::Max(.06,[Math]::Min(1.2,$zoom*1.45))
    for($i=0;$i -lt $layout.Count;$i++) {
        $n=$layout[$i]
        $rootSize=$size*82/66
        if ($n.id -in $growth.goalLine.id) {
            Assert-True ([Math]::Abs($n.x)*$zoom -ge ($size+$rootSize)/2 -or [Math]::Abs($n.y)*$zoom -ge ($size+$rootSize)/2) 'Objective card overlaps the root'
        }
        for($j=$i+1;$j -lt $layout.Count;$j++){
            $other=$layout[$j]
            if ($n.id -notin $growth.goalLine.id -and $other.id -notin $growth.goalLine.id) { continue }
            Assert-True ([Math]::Abs($n.x-$other.x)*$zoom -ge $size -or [Math]::Abs($n.y-$other.y)*$zoom -ge $size) "Card overlap at zoom $zoom : $($n.id)/$($other.id)"
        }
    }
}
foreach($node in $growth.nodes) {
    $display=$layout | Where-Object id -eq $node.id
    Assert-True ($display.parent -eq $node.parent -and $display.startRank -eq $node.startRank -and $display.ranks -eq $node.ranks -and $display.y -eq $node.y) 'Display layout changed existing purchasing rules'
}
foreach($issue in @('duplicate','broken_parent','not_north','wrong_pair','limit','nan_limit','early_final','gap','incomplete')) {
    $broken=[ProjectDM.PrototypeGrowthTable](Get-Content -Raw (Join-Path $dataRoot 'PrototypeGrowth.json') | ConvertFrom-Json)
    switch($issue){
        'duplicate' {$broken.goalLine[0].id='damage_1'}
        'broken_parent' {$broken.goalLine[2].parent='ROOT'}
        'not_north' {$broken.goalLine[0].x=1}
        'wrong_pair' {$broken.goalLine[1].kind='stage'}
        'limit' {$broken.goalLine[1].limitSeconds=0}
        'nan_limit' {$broken.goalLine[1].limitSeconds=[float]::NaN}
        'early_final' {$broken.goalLine[1].final=$true}
        'gap' {$broken.goalLine[0].y=-1000}
        'incomplete' {$broken.goalLine=@($broken.goalLine[0])}
    }
    Assert-True ($null -ne [ProjectDM.PrototypeRules]::Validate($balance,$skills,$broken)) "Invalid objective configuration accepted: $issue"
}
$growth.goalLine=$null
Assert-True ($null -eq [ProjectDM.PrototypeRules]::Validate($balance,$skills,$growth)) 'Legacy/custom growth tables without goals still work'
$legacy=[ProjectDM.PrototypeRules]::CreateGraphLayout($growth)
for($i=0;$i -lt $growth.nodes.Count;$i++){Assert-True ($legacy[$i].x -eq $growth.nodes[$i].x -and $legacy[$i].y -eq $growth.nodes[$i].y) 'No-goal custom table must retain its original layout'}
Write-Output "Objective line: $checks assertions PASS (northbound stage/boss pairs, independent preview data, graph separation/connector clearance, preserved purchasing rules, validation and legacy tables)."
