// Copy to an isolated project's Assets/; execute ProjectDM.PixelFxRuntimeRegression.Run.
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectDM
{
    public sealed class PixelFxRuntimeRegression : MonoBehaviour
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var serialized=new SerializedObject(Object.FindFirstObjectByType<GameManager>());
            serialized.FindProperty("prototypeSandbox").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("QA PixelFX world render",typeof(PixelFxRuntimeRegression));EditorApplication.isPlaying=true;
        }
        IEnumerator Start()
        {
            var manager=Object.FindFirstObjectByType<GameManager>();const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            float start=Time.realtimeSinceStartup;
            while(!(bool)typeof(GameManager).GetField("isInitialized",flags).GetValue(manager))
            {if(Time.realtimeSinceStartup-start>90){Debug.LogError("PIXELFX RUNTIME BOOT TIMEOUT");EditorApplication.Exit(1);yield break;}yield return null;}
            manager.enabled=false;
            try
            {
                var save=(SaveService)typeof(GameManager).GetField("save",flags).GetValue(manager);
                if(!save.sandboxThisSession)throw new System.Exception("Sandbox required");
                var effects=manager.GetComponent<PrototypeSkillVisuals>();
                var settings=(GameSettings)typeof(GameManager).GetField("settings",flags).GetValue(manager);
                var run=(RunController)typeof(GameManager).GetField("run",flags).GetValue(manager);
                var enemies=(EnemySystem)typeof(GameManager).GetField("enemiesSystem",flags).GetValue(manager);enemies.Reset();
                Camera camera=settings.gameplayCamera;camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=5;camera.aspect=16f/9;
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"pixelfx-world");Directory.CreateDirectory(output);
                foreach(int stage in new[]{1,2})
                {
                    run.stages.Select(stage);run.stages.ApplySelected();effects.Clear();
                    effects.SkillLine(new Vector3(-7,3),new Vector3(-1,3),.35f,new Color(.8f,.4f,1),.6f);
                    effects.FlameCone(new Vector3(1,2.4f),Vector2.right,3,75);
                    effects.Link(new Vector3(-7,.5f),new Vector3(-1,1));
                    effects.Thunder(new Vector3(4,3),new Vector3(4,-1));
                    effects.SkillPulse(new Vector3(-5,-2),.85f,"skill.flame.firefield",Color.white,.5f);
                    effects.SkillPulse(new Vector3(-1,-2),.65f,"skill.lightning.orb",Color.white,.5f);
                    effects.SkillPulse(new Vector3(2,-2.5f),.85f,"skill.frost.blizzard",Color.white,.5f);
                    effects.SkillPulse(new Vector3(5,-2.5f),.85f,"skill.frost.shatter",Color.white,.5f);
                    effects.Tick(.08f);
                    var target=new RenderTexture(1280,720,24);var previous=RenderTexture.active;var original=camera.targetTexture;
                    camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                    var capture=new Texture2D(1280,720,TextureFormat.RGBA32,false);capture.ReadPixels(new Rect(0,0,1280,720),0,0);capture.Apply();
                    File.WriteAllBytes(Path.Combine(output,"stage-"+stage+".png"),capture.EncodeToPNG());
                    camera.targetTexture=original;RenderTexture.active=previous;Object.Destroy(capture);target.Release();Object.Destroy(target);
                    if(effects.BatchedEffectCount<8)throw new System.Exception("Skill entry points lost effects");
                }
                run.characters.Select("projectile");run.characters.ApplySelected();
                var combat=(CombatSystem)typeof(GameManager).GetField("combat",flags).GetValue(manager);
                combat.Reset();enemies.SpawnPrototypeEnemy(true);run.mana.Reset(40,1);
                combat.FireAtNearestEnemy(run.elapsed+10);
                if(combat.projectiles.Count==0||!combat.projectiles[0].pixelFx)throw new System.Exception("Actual basic attack did not use PixelFX");
                var projectile=combat.projectiles[0];var frame=projectile.renderer.sprite;combat.UpdatePrototypeProjectiles(.05f);
                if(projectile.renderer.sprite==frame)throw new System.Exception("Actual moving projectile did not animate");
                combat.Reset();enemies.Reset();
                effects.Clear();
                Debug.Log("PIXELFX RUNTIME PASSED: production Main bootstrap, two actual stage renders, eight simultaneous skill paths, sandbox");EditorApplication.Exit(0);
            }
            catch(System.Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
#endif
