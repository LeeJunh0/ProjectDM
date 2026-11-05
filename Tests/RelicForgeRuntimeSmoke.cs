// Isolated project only: execute ProjectDM.RelicForgeRuntimeSmoke.Run.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectDM
{
    public sealed class RelicForgeRuntimeSmoke : MonoBehaviour
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private int phase;
        private float next, deadline;
        private RunController run;
        private SaveService save;
        private string output;
        private bool uiError;
        private float coinShotAt = -1, meltShotAt = -1;
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var serialized = new SerializedObject(FindFirstObjectByType<GameManager>());
            serialized.FindProperty("prototypeSandbox").boolValue = true; serialized.ApplyModifiedPropertiesWithoutUndo();
            var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            gameView.position = new Rect(0, 0, 1280, 720); gameView.Show();
            new GameObject("Relic forge runtime QA", typeof(RelicForgeRuntimeSmoke));
            EditorApplication.isPlaying = true;
        }
        private void Start() { deadline = Time.realtimeSinceStartup + 90; }
        private void Update()
        {
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("Relic forge boot timeout");
                if (coinShotAt > 0 && Time.realtimeSinceStartup >= coinShotAt)
                { ScreenCapture.CaptureScreenshot(Path.Combine(output, "coins.png")); coinShotAt = -1; }
                if (meltShotAt > 0 && Time.realtimeSinceStartup >= meltShotAt)
                { ScreenCapture.CaptureScreenshot(Path.Combine(output, "melting.png")); meltShotAt = -1; }
                if (Time.realtimeSinceStartup < next) return;
                if (phase == 0)
                {
                    var manager = FindFirstObjectByType<GameManager>();
                    if (!(bool)typeof(GameManager).GetField("isInitialized", Flags).GetValue(manager)) return;
                    save = (SaveService)typeof(GameManager).GetField("save", Flags).GetValue(manager);
                    if (!save.sandboxThisSession) throw new Exception("Sandbox required");
                    run = (RunController)typeof(GameManager).GetField("run", Flags).GetValue(manager);
                    int draw = 0;
                    typeof(RelicForge).GetField("roll", Flags).SetValue(run.relicForge, new Func<double>(() => draw++ == 0 ? .1 : .9));
                    save.progress.bestRunCurrency = 0; save.metaCurrency = 1000; run.runCurrency = 100;
                    Application.logMessageReceived += Observe;
                    run.EndRun(); run.resultPresentationOpenedAt = Time.unscaledTime - 10;
                    output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "relic-forge-ui"); Directory.CreateDirectory(output);
                }
                else if (phase == 1)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "result.png"));
                }
                else if (phase == 2)
                {
                    if (!run.relicForge.Open()) throw new Exception("Actual result cannot open forge");
                }
                else if (phase == 3)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "forge.png"));
                }
                else if (phase == 4)
                {
                    var manager = FindFirstObjectByType<GameManager>();
                    var results = (RunResultView)typeof(GameManager).GetField("results", Flags).GetValue(manager);
                    typeof(RunResultView).GetMethod("BeginForgeMotion", Flags).Invoke(results, new object[] { true });
                    if (run.relicForge.Rolls != 1 || save.metaCurrency != 900) throw new Exception("Animated entry did not spend exactly the latest income");
                    var motion = (RelicForgeMotion)typeof(RunResultView).GetField("forgeMotion", Flags).GetValue(results);
                    if (motion.DepositCoins != RelicForgeMotion.CoinCount(100)) throw new Exception("Actual stake did not control the deposit amount");
                    coinShotAt = Time.realtimeSinceStartup + .16f; meltShotAt = Time.realtimeSinceStartup + .45f;
                    run.ToggleMetaTree();
                    if (run.showMetaTree) throw new Exception("TAB cannot bypass the workshop decision");
                }
                else if (phase == 5)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "outcome.png"));
                }
                else if (phase == 6)
                {
                    var manager = FindFirstObjectByType<GameManager>();
                    var results = (RunResultView)typeof(GameManager).GetField("results", Flags).GetValue(manager);
                    typeof(RunResultView).GetMethod("BeginForgeMotion", Flags).Invoke(results, new object[] { false });
                    if (run.relicForge.Rolls != 2 || save.metaCurrency != 900 || run.relicForge.Pending != 0) throw new Exception("Continuation must fail without charging more gold");
                    if (((RelicForgeMotion)typeof(RunResultView).GetField("forgeMotion", Flags).GetValue(results)).DepositCoins != 0) throw new Exception("Continuation cannot deposit coins again");
                }
                else if (phase == 7)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "condense-failure.png"));
                }
                else if (phase == 8)
                {
                    run.relicForge.Close(); run.showMetaTree = true;
                    if (run.relicForge.Open()) throw new Exception("Growth map must not allow entry");
                }
                else
                {
                    if (uiError) throw new Exception("Result or forge UI raised an error");
                    Debug.Log("RELIC FORGE RUNTIME PASSED: actual Main, result UI, forge UI, stake debit and growth-map exclusion; sandbox");
                    Application.logMessageReceived -= Observe; EditorApplication.Exit(0); return;
                }
                phase++; next = Time.realtimeSinceStartup + (phase == 5 || phase == 7 ? 1.05f : .7f);
            }
            catch (Exception e) { Application.logMessageReceived -= Observe; Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private void Observe(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error) uiError = true; }
    }
}
#endif
