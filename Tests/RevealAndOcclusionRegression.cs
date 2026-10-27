// Copy into a Unity project's Assets/Editor and run ProjectDM.RevealAndOcclusionCheck.Run.
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace ProjectDM {
 public static class RevealAndOcclusionCheck {
  static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
  static void Near(float a,float b,string why){Check(Mathf.Abs(a-b)<.001f,why+" "+a);}
  public static void Run(){try{
   var state=new GrowthNodeReveal();state.Begin("new",5);
   Near(state.Line("new",5),0,"Line starts invisible");Near(state.Opacity("new",5.2f),0,"Node waits for line");
   Check(state.Line("new",5.21f)>.1f&&state.Line("new",5.21f)<.9f,"Line grows");
   Check(state.Opacity("new",5.37f)>0&&state.Opacity("new",5.37f)<1,"Node fades in");
   Check(state.Scale("new",5.47f)>1,"Node pops larger");Near(state.Scale("new",6f),1,"Node settles");
   Check(!state.IsAnimating("old",5.1f),"Existing nodes must not animate");state.Clear();Near(state.Line("new",5.1f),1,"Navigation clears reveals");
   var settings=new GameSettings();var tables=GameTables.LoadResources(settings);var save=new SaveService(tables,new MemoryProgressStore(),true){progress=new PrototypeSave()};
   var run=new RunController(settings,save,tables,null,null,null,null,null,()=>{},()=>{},()=>false,new RunMana());
   using(var view=new GrowthGraphView(settings,save,tables,null,null,run,null,null)){
    view.BuildPrototypeGraph();var graph=tables.characters.CommonGraph;var root=graph.growthTable.nodes.First(n=>n.VisualParent=="ROOT");
    int gold=int.MaxValue;Check(PrototypeRules.TryPurchaseNode(graph.balance,graph.GrowthRow(root.growthId),root,save.progress,4,ref gold,graph.growthTable),"Root purchase");
    view.AnimateNewlyRevealedNodes(10);var f=typeof(GrowthGraphView).GetField("reveal",BindingFlags.NonPublic|BindingFlags.Instance);var anim=(GrowthNodeReveal)f.GetValue(view);
    var newly=graph.growthTable.nodes.Where(n=>view.metaTreeNodes[Array.IndexOf(graph.growthTable.nodes,n)+1].state==MetaTreeNodeState.Hidden&&PrototypeRules.IsNodeRevealed(graph.growthTable,n,save.progress,4)).ToArray();
    Check(newly.Length>0&&newly.All(n=>anim.IsAnimating(n.id,10)),"Only newly revealed nodes animate");
    Check(!anim.IsAnimating(root.id,10),"Purchased existing node must not animate");view.ResetNavigation();Check(newly.All(n=>!anim.IsAnimating(n.id,10)),"Reopen must not replay");
   }
   var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color32[4096];for(int y=20;y<44;y++)for(int x=20;x<44;x++)pixels[y*64+x]=new Color32(255,255,255,255);tex.SetPixels32(pixels);tex.Apply();
   var sprite=Sprite.Create(tex,new Rect(0,0,64,64),Vector2.one*.5f,64,0,SpriteMeshType.FullRect);var layout=ScriptableObject.CreateInstance<StageSceneryLayout>();layout.palette=new[]{new StageSceneryLayout.Prop{id="tree",sprite=sprite,height=4}};layout.placements.Add(new StageSceneryLayout.Placement{propId="tree"});
   var holder=new GameObject("occlusion-test");var scenery=holder.AddComponent<StageSceneryRenderer>();scenery.Configure(layout,new Bounds(Vector3.zero,new Vector3(100,100,1)));
   var actor=new GameObject("actor");var renderer=actor.AddComponent<SpriteRenderer>();renderer.sprite=sprite;actor.transform.position=new Vector3(1.5f,0,0);
   var entries=(System.Collections.IList)typeof(StageSceneryRenderer).GetField("occluders",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(scenery);
   var entry=entries[0];var level=entry.GetType().GetField("level",BindingFlags.NonPublic|BindingFlags.Instance);
   scenery.UpdateOcclusion(renderer,.25f);Check((int)level.GetValue(entry)==16,"Transparent margins must not trigger dither");
   actor.transform.position=Vector3.zero;scenery.UpdateOcclusion(renderer,.25f);Check((int)level.GetValue(entry)==4,"Opaque art must trigger dither");
   actor.transform.position=new Vector3(1.5f,0,0);scenery.UpdateOcclusion(renderer,.25f);Check((int)level.GetValue(entry)==16,"Leaving visible art restores opacity");
   UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(holder);UnityEngine.Object.DestroyImmediate(layout);UnityEngine.Object.DestroyImmediate(sprite);UnityEngine.Object.DestroyImmediate(tex);
   Debug.Log("REVEAL AND OCCLUSION CHECK PASSED");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}



