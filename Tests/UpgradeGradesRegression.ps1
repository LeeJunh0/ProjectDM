$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$rules = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Systems/LevelUp/UpgradeGradeRules.cs') -Raw
$harness = @'
namespace ProjectDM
{
    public static class UpgradeGradesRegression
    {
        static int checks;
        static void Check(bool value, string why)
        { checks++; if (!value) throw new System.Exception(why); }

        public static int Run()
        {
            double previousGold=0, previousSilver=0, previousBronze=1;
            for (int luck=0;luck<=1000;luck++)
            {
                var p=UpgradeGradeRules.ForLuck(luck);
                Check(p.gold>=previousGold && p.silver>=previousSilver && p.bronze<=previousBronze, "Luck reverses grade improvement");
                Check(p.gold>=0 && p.silver>=0 && p.bronze>=0 && System.Math.Abs(p.gold+p.silver+p.bronze-1)<1e-12, "Invalid probability distribution");
                previousGold=p.gold; previousSilver=p.silver; previousBronze=p.bronze;
            }
            double[,] examples={{0,.05,.35,.60},{10,.07,.37,.56},{30,.11,.41,.48},{150,.35,.65,0},{25,.10,.40,.50},{50,.15,.45,.40},{75,.20,.50,.30}};
            for(int e=0;e<examples.GetLength(0);e++)
            {
                var p=UpgradeGradeRules.ForLuck(examples[e,0]);
                Check(System.Math.Abs(p.gold-examples[e,1])<1e-12 && System.Math.Abs(p.silver-examples[e,2])<1e-12 && System.Math.Abs(p.bronze-examples[e,3])<1e-12,"Grade tuning example mismatch");
                int gold=0,silver=0,bronze=0;
                for(int i=0;i<100000;i++)
                    switch(UpgradeGradeRules.Roll(examples[e,0],(i+.5)/100000))
                    {case "gold": gold++;break;case "silver":silver++;break;case "bronze":bronze++;break;default:throw new System.Exception("Unknown grade");}
                Check(gold==System.Math.Round(p.gold*100000) && silver==System.Math.Round(p.silver*100000) && bronze==System.Math.Round(p.bronze*100000),"Grade sampler disagrees with configured probabilities");
            }
            Check(UpgradeGradeRules.Roll(0,0)=="gold" && UpgradeGradeRules.Roll(0,.05)=="silver" && UpgradeGradeRules.Roll(0,.4)=="bronze" && UpgradeGradeRules.Roll(0,1)=="bronze","Cumulative interval boundaries");
            Check(UpgradeGradeRules.Roll(150,1)=="silver", "Zero bronze must never be rolled at the inclusive endpoint");
            Check(UpgradeGradeRules.ForLuck(-1).gold==.05 && UpgradeGradeRules.ForLuck(double.NaN).gold==.05,"Invalid luck must not corrupt the distribution");
            Check(UpgradeGradeRules.ForLuck(double.PositiveInfinity).bronze==0,"Unbounded luck must clamp");
            Check(UpgradeGradeRules.RankGain("bronze")==1 && UpgradeGradeRules.RankGain("silver")==2 && UpgradeGradeRules.RankGain("gold")==3,"Quality rank gains");
            return checks;
        }
    }
}
'@
Add-Type -TypeDefinition ($rules + [Environment]::NewLine + $harness)
$checks = [ProjectDM.UpgradeGradesRegression]::Run()
$growth = Get-Content (Join-Path $projectRoot 'Assets/Resources/ProjectDM/Data/PrototypeGrowth.json') -Raw | ConvertFrom-Json
$luck = $growth.rows | Where-Object id -eq 'luck'
if ($luck.step -ne 25 -or $luck.maxRank -ne 3) { throw 'Common luck must add 5 percentage points to each higher grade per rank, for three ranks.' }
Write-Output "Upgrade grades: $checks assertions and common growth tuning PASS (5/35/60 base chances, +5pp per higher grade per growth rank, 700000 stratified draws, endpoints, caps and quality gains)."
