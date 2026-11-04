// Isolated Unity Play Mode bot. Copy to Assets/, launcher to Assets/Editor. Never writes real saves.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace ProjectDM {
public sealed class CampaignPlaytest : MonoBehaviour {
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 T Get<T>(string name)=>(T)typeof(GameManager).GetField(name,Flags).GetValue(manager);
 GameManager manager; RunController run; SaveService save; GameTables tables; PlayerWorld world;
 EnemySystem enemies; CombatSystem combat; SkillSystem skills; PickupSystem pickups; LevelUpController selection; CombatStats stats; PlayerStats playerStats; PlayerCombatSystem contact;
 [Serializable] public class Record { public int run,kills,coins,level,peak,target,cap,batch,purchases,gold;public float seconds,damage,interval,mana,completion,personalCompletion,wallSeconds,xp,absorbed,spent,maxDamage,maxHit;public string bought; }
 [Serializable] public class Report { public string model="actual Unity systems; fixed 30Hz bot; accelerated; no human review";public string character="projectile";public bool sharedMax,collectionFirst,fullBuild;public int seed=73;public List<Record> runs=new();public List<PrototypeRank> ranks;public float completionSeconds=-1; }
 Report report=new();float wall;int index;List<GameTables> graphs;List<PrototypeGrowth> rows; float started;
 IEnumerator Start(){
  var args=Environment.GetCommandLineArgs();int suite=Array.IndexOf(args,"-qaSuite");
  string[] characters=suite>=0?args[suite+1].Split(','):new[]{"projectile"};
  int seedsAt=Array.IndexOf(args,"-qaSeeds");int[] seeds=seedsAt>=0?args[seedsAt+1].Split(',').Select(int.Parse).ToArray():new[]{73};
  foreach(string character in characters)foreach(int seed in seeds){
   report=new Report{character=character,seed=seed};
   var campaign=PlayCampaign();
   while(true){
   bool more;
   try {more=campaign.MoveNext();}
   catch(Exception error){Debug.LogException(error);
    #if UNITY_EDITOR
    UnityEditor.EditorApplication.Exit(1);
    #endif
    yield break;
   }
   if(!more)break;
   yield return campaign.Current;
   }
  }
  #if UNITY_EDITOR
  UnityEditor.EditorApplication.Exit(0);
  #endif
 }
 IEnumerator PlayCampaign(){
  started=Time.realtimeSinceStartup;wall=0;
  Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None);
  var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++){if(args[i]=="-qaCharacter")report.character=args[i+1];if(args[i]=="-qaSeed")report.seed=int.Parse(args[i+1]);}
  report.sharedMax=Array.IndexOf(args,"-qaSharedMax")>=0 || (Array.IndexOf(args,"-qaMixed")>=0 && report.character!="projectile");
  report.collectionFirst=Array.IndexOf(args,"-qaEconomy")>=0;report.fullBuild=Array.IndexOf(args,"-qaFullBuild")>=0;
  manager=FindFirstObjectByType<GameManager>();
  while(!(bool)typeof(GameManager).GetField("isInitialized",Flags).GetValue(manager)){if(Time.realtimeSinceStartup-started>90)throw new Exception("Bootstrap timeout");yield return null;}
  manager.enabled=false;run=Get<RunController>("run");save=Get<SaveService>("save");tables=Get<GameTables>("tables");world=Get<PlayerWorld>("playerWorld");
  if(!save.sandboxThisSession)throw new Exception("Sandbox required");
  save.progress.ranks.Clear();Array.Clear(save.progress.kills,0,5);save.metaCurrency=0;save.progress.completedRuns=0;
  if(!run.characters.Select(report.character))throw new Exception("Unsupported QA character");run.transition=null;
  enemies=Get<EnemySystem>("enemiesSystem");combat=Get<CombatSystem>("combat");skills=Get<SkillSystem>("skills");pickups=Get<PickupSystem>("pickupsSystem");selection=Get<LevelUpController>("selection");stats=Get<CombatStats>("stats");playerStats=Get<PlayerStats>("playerStats");contact=Get<PlayerCombatSystem>("playerCombat");
  graphs=new(){tables.characters.CommonGraph,tables.characters.Graphs[report.character]};rows=graphs.SelectMany(g=>g.growthTable.rows).GroupBy(r=>r.id).Select(g=>g.First()).ToList();
  if(report.fullBuild){foreach(var row in rows)save.progress.SetRank(row.id,row.maxRank);}
  if(report.sharedMax||report.fullBuild){foreach(var row in graphs[0].growthTable.rows)save.progress.SetRank(row.id,row.maxRank);for(int i=0;i<4;i++)save.progress.kills[i]=tables.balance.monsters[i].nextUnlockKills;}
  UnityEngine.Random.InitState(report.seed);
  for(index=1;index<=400;index++){
   run.BeginRun();int peak=0,choices=0;float xp=0;int lastLevel=1;float lastXp=0;
   var record=new Record{run=index,damage=stats.MainDamage,interval=stats.AttackInterval,mana=run.mana.Maximum,target=enemies.FieldTarget,cap=enemies.FieldCap,batch=enemies.RefillBatch};
   while(!run.runEnded){
    for(int step=0;step<180&&!run.runEnded;step++){
     if(selection.choosingUpgrade){choices++;var choice=selection.runUpgradeChoices.OrderByDescending(c=>c.upgrade.EndsWith("damage")?3:c.upgrade.EndsWith("speed")?2:1).First();selection.ChooseRunUpgrade(choice.upgrade);continue;}
     const float dt=1f/30;run.TickClock(dt);
     Vector2 pos=world.player.position,force=Vector2.zero;Vector2 goal=Vector2.zero;float nearest=float.MaxValue;
     foreach(var p in pickups.pickups){if(p.removed)continue;float d=((Vector2)p.Position-pos).sqrMagnitude;if(d<nearest){nearest=d;goal=(Vector2)p.Position-pos;}}
     foreach(var e in enemies.enemies){if(!EnemySystem.IsLive(e))continue;Vector2 delta=pos-(Vector2)e.transform.position;float d=delta.magnitude;if(d<2.3f)force+=delta.normalized*(2.3f-d)*2;}
     Vector2 direction=(goal.normalized+force).normalized;world.player.position+=(Vector3)(direction*playerStats.MoveSpeed*dt);
     var settings=Get<GameSettings>("settings");
     Vector2 center=settings.playerMovementArea!=null?(Vector2)settings.playerMovementArea.transform.position:Vector2.zero;
     Vector2 extents=settings.playerMovementArea!=null?settings.playerMovementArea.HalfExtents:settings.playerMovementBounds;
     var position=world.player.position;position.x=Mathf.Clamp(position.x,center.x-extents.x,center.x+extents.x);position.y=Mathf.Clamp(position.y,center.y-extents.y,center.y+extents.y);world.player.position=position;
     var area=settings.monsterSpawnArea;if(area!=null)area.transform.position=world.player.position;
     record.maxDamage=Mathf.Max(record.maxDamage,stats.MainDamage);
     enemies.SpawnEnemies(run.elapsed);combat.FireAtNearestEnemy(run.elapsed);
     if(run.mana.IsDepleted){run.CompleteIfDue(()=>Get<BossMilestone>("boss").ArriveAtPrototypeBoss());break;}
     enemies.UpdateEnemies(dt);enemies.spatial.Rebuild(enemies.enemies);combat.UpdateProjectiles(dt);Get<DamagePresentation>("presentation").Tick(dt);skills.UpdatePrototypeEffects(dt,world.player);contact.UpdatePlayerContactDamage(dt);pickups.UpdatePickups(dt);
     peak=Math.Max(peak,enemies.enemies.Count);
     if(selection.level!=lastLevel){xp-=lastXp;while(lastLevel<selection.level)xp+=PrototypeRules.XpThreshold(tables.balance,lastLevel++);lastXp=0;}
     xp+=Mathf.Max(0,selection.experience-lastXp);lastXp=selection.experience;
     run.CompleteIfDue(()=>Get<BossMilestone>("boss").ArriveAtPrototypeBoss());
    }
    yield return null;
   }
   record.seconds=run.elapsed;record.kills=run.monsterKillCounts.Sum();record.coins=run.runCurrency;record.level=selection.level;record.peak=peak;record.xp=xp;record.absorbed=run.mana.AbsorbedThisRun;record.spent=run.mana.SpentThisRun;record.maxHit=enemies.MaxDamageThisRun;
   if(index>1 && (run.previousResult==null || run.previousResult.coins!=report.runs[index-2].coins || run.previousResult.kills!=report.runs[index-2].kills))throw new Exception("Previous-run summary changed or was reset");
   wall+=run.elapsed+20+choices*2.5f;
   var bought=new List<string>();for(int buy=0;buy<300;buy++){
    var candidates=new List<(GameTables graph,PrototypeGrowthNode node,PrototypeGrowth row,double score)>();
    foreach(var graph in graphs)foreach(var n in graph.growthTable.nodes){var row=graph.GrowthRow(n.growthId);if(PrototypeRules.NodePurchaseBlock(tables.balance,row,n,save.progress,PrototypeRules.Stage(tables.balance,save.progress),save.metaCurrency,graph.growthTable)!=null)continue;
     double priority=report.collectionFirst&&row.id=="magnet"?4:row.id.Contains("Unlock")?5:row.id=="time"||row.id=="health"?2.5:row.id=="xp"||row.id=="yield"||row.id=="population"||row.id=="spawnBatch"||row.id.EndsWith(".hunt")?2:1;
     candidates.Add((graph,n,row,priority/PrototypeRules.Cost(row,save.progress.Rank(row.id),tables.balance.latePriceScale)));}
    if(candidates.Count==0)break;var c=candidates.OrderByDescending(c=>c.score).First();if(!PrototypeRules.TryPurchaseNode(tables.balance,c.row,c.node,save.progress,PrototypeRules.Stage(tables.balance,save.progress),ref save.metaCurrency,c.graph.growthTable))throw new Exception("Shop mismatch");bought.Add(c.row.id);
   }
   record.purchases=bought.Count;record.bought=string.Join(",",bought);wall+=bought.Count*1.5f;record.wallSeconds=wall;record.gold=save.metaCurrency;report.ranks=save.progress.ranks;
   record.completion=(float)rows.Sum(r=>Math.Min(save.progress.Rank(r.id),r.maxRank))/(float)rows.Sum(r=>r.maxRank);
   var personal=graphs[1].growthTable.rows;record.personalCompletion=(float)personal.Sum(r=>Math.Min(save.progress.Rank(r.id),r.maxRank))/personal.Sum(r=>r.maxRank);
   report.runs.Add(record);File.WriteAllText(Application.dataPath+"/../campaign-playtest.json",JsonUtility.ToJson(report,true));
   Debug.Log("CAMPAIGN_RUN "+JsonUtility.ToJson(record));
   if((report.sharedMax?record.personalCompletion:record.completion)>=.9f&&report.completionSeconds<0)report.completionSeconds=wall;
   if(record.completion>=1f||wall>5*3600)break;
  }
  File.WriteAllText(Application.dataPath+"/../campaign-playtest.json",JsonUtility.ToJson(report,true));
  File.WriteAllText(Application.dataPath+$"/../campaign-{report.character}-{report.seed}-{(report.sharedMax?"shared":"fresh")}{(report.collectionFirst?"-collection":"")}{(report.fullBuild?"-full":"")}.json",JsonUtility.ToJson(report,true));
  Debug.Log("CAMPAIGN_DONE "+report.runs.Count+" runs "+wall/3600+" hours; real elapsed="+(Time.realtimeSinceStartup-started));
 }
}
}
