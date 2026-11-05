// Copy to an isolated project's Assets/Editor; execute ProjectDM.Editor.PixelFxRegression.Run.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class PixelFxRegression
    {
        private static int checks;
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        public static void Run()
        {
            try
            {
                var texture=Resources.Load<Texture2D>("ProjectDM/Skills/PixelFX/skill-effects");
                Check(texture!=null&&texture.width==1200&&texture.height==1600,"Native atlas size must survive import");
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                Check(importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.textureCompression==TextureImporterCompression.Uncompressed,"Pixel edges must stay crisp");
                var rng=UnityEngine.Random.state;
                foreach(string id in new[]{"missile","beam","satellite","cone","firefield","meteor","chain","orb","thunder","frostshard","blizzard","shatter","fireimpact","hit","mote"})
                {
                    var clip=PixelFxLibrary.Find(id);Check(clip!=null&&clip.sprites.Length>=12&&clip.fps==24,"All skill and particle clips must exist: "+id);
                    foreach(var sprite in clip.sprites)Check(sprite.texture==texture&&sprite.textureRect.width>0&&sprite.textureRect.height>0,"All frames share one valid atlas");
                    Check(PixelFxLibrary.Frame(id,0)!=PixelFxLibrary.Frame(id,.125f),"Animated frames must advance: "+id);
                    if(clip.loop)Check(PixelFxLibrary.Frame(id,0)==PixelFxLibrary.Frame(id,(clip.sprites.Length + .25f) / clip.fps),"Loop must wrap at its authored duration: " + id);
                    else Check(PixelFxLibrary.Frame(id,2)==clip.sprites[clip.sprites.Length-1],"One-shots hold their empty terminal frame");
                }
                Check(PixelFxLibrary.Frame("missing",0)==null,"Missing clip keeps legacy fallback available");
                var host=new GameObject("PixelFX regression");var fx=host.AddComponent<PrototypeSkillVisuals>();
                var missileSprite=PixelFxLibrary.Frame("missile",0);fx.Configure(missileSprite,missileSprite,Resources.Load<SkillVisualAssets>("ProjectDM/Skills/SkillVisuals"));
                fx.FlameCone(Vector3.zero,Vector2.right,3,60);fx.SkillLine(Vector3.zero,Vector3.right*5,.3f,new Color(.8f,.4f,1),.3f);
                fx.Link(Vector3.zero,Vector3.right*4);fx.Thunder(Vector3.up*5,Vector3.zero);
                fx.SkillPulse(Vector3.zero,1,"skill.lightning.orb",Color.white,.3f);fx.HitSpark(Vector3.zero,CombatDamageElement.Flame);
                Check(fx.BatchedEffectCount==6,"Entry points emit one animated effect each");
                fx.Tick(.02f);
                var batch=(SpriteQuadBatch)typeof(PrototypeSkillVisuals).GetField("batch",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fx);
                Check(batch.Count==6,"Animated effects submit every visible instance");
                fx.Clear();
                for(int i=0;i<2000;i++)fx.PixelEffect(i%2==0?"hit":"cone",new Vector3(i%50,i/50),Vector2.one,0,.4f,Color.white);
                fx.Tick(.02f);Check(batch.Count==2000&&fx.BatchedEffectCount==2000,"Crowds retain all 2000 visible effects");
                var root=typeof(SpriteQuadBatch).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(batch) as GameObject;
                Check(root.GetComponentsInChildren<MeshRenderer>().Length==1,"All animated sprites batch into one texture page");
                fx.Tick(0);Check(fx.BatchedEffectCount==2000,"Pause freezes effects");
                fx.Tick(1);Check(fx.BatchedEffectCount==0&&batch.Count==0,"Expired effects leave no stale mesh");
                fx.BeginFrostOrbit(5,.3f);fx.MoveFrostShard(0,Vector3.right,45);fx.EndFrostOrbit();
                Check(host.GetComponentsInChildren<Collider2D>().Length==0,"Art must not add combat colliders");
                Check(UnityEngine.Random.state.Equals(rng),"VFX must not consume gameplay RNG");
                fx.Clear();UnityEngine.Object.DestroyImmediate(host);
                var teardown=new SpriteQuadBatch("teardown order test",6);teardown.Begin();teardown.Add(missileSprite,Vector3.zero,Vector2.one,0,Color.white);teardown.End();
                var teardownRoot=(GameObject)typeof(SpriteQuadBatch).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(teardown);
                UnityEngine.Object.DestroyImmediate(teardownRoot);teardown.Dispose();teardown.Dispose();
                Check(true,"Scene teardown can destroy renderers before the batch owner");
                Debug.Log("PIXELFX PASSED: "+checks+" assertions; 2000 effects, one atlas page");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
