// Copy to an isolated project's Assets/; execute ProjectDM.EnemyAttackRuntimeSmoke.Run.
#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectDM
{
    public sealed class EnemyAttackRuntimeSmoke : MonoBehaviour
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var settings = new SerializedObject(Object.FindFirstObjectByType<GameManager>());
            settings.FindProperty("prototypeSandbox").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("QA special attack production route", typeof(EnemyAttackRuntimeSmoke));
            EditorApplication.isPlaying = true;
        }
        IEnumerator Start()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            float started = Time.realtimeSinceStartup;
            while (!(bool)typeof(GameManager).GetField("isInitialized", flags).GetValue(manager))
            {
                if (Time.realtimeSinceStartup - started > 90) { Debug.LogError("ENEMY ATTACK RUNTIME TIMEOUT"); EditorApplication.Exit(1); yield break; }
                yield return null;
            }
            manager.enabled = false;
            var save = (SaveService)typeof(GameManager).GetField("save", flags).GetValue(manager);
            var field = (EnemySystem)typeof(GameManager).GetField("enemiesSystem", flags).GetValue(manager);
            var combat = (PlayerCombatSystem)typeof(GameManager).GetField("playerCombat", flags).GetValue(manager);
            var run = (RunController)typeof(GameManager).GetField("run", flags).GetValue(manager);
            if (!save.sandboxThisSession) { Debug.LogError("Sandbox required"); EditorApplication.Exit(1); yield break; }
            combat.ResetPlayerCombat(); run.mana.Reset(40, 1);
            field.specialAttacks.HitPlayer(2);
            if (Mathf.Abs(run.mana.Current - 38) > .001f) { Debug.LogError("Special attack failed to reach mana shield"); EditorApplication.Exit(1); yield break; }
            field.specialAttacks.HitPlayer(2);
            if (Mathf.Abs(run.mana.Current - 38) > .001f) { Debug.LogError("Special attack bypassed invulnerability"); EditorApplication.Exit(1); yield break; }
            field.Reset();
            Debug.Log("ENEMY ATTACK RUNTIME PASSED: actual Main bootstrap, sandbox, mana shield hit, shared invulnerability, reset");
            EditorApplication.Exit(0);
        }
    }
}
#endif
