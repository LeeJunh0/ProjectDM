// Run only in an isolated project containing work/pixelfx, Design/Skills and Docs/QA backgrounds.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class PixelFxEditorRegression
    {
        private static PixelFxEditorWindow window;
        private static int phase;
        private static double deadline;
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Read(string field) => typeof(PixelFxEditorWindow).GetField(field, Flags).GetValue(window);
        private static void Call(string method, params object[] args) => typeof(PixelFxEditorWindow).GetMethod(method, Flags).Invoke(window, args);
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run()
        {
            window = ScriptableObject.CreateInstance<PixelFxEditorWindow>();
            window.Show();
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                string status = (string)Read("status");
                Check(!status.StartsWith("생성 실패") && !status.StartsWith("생성 오류"), status);
                Check(EditorApplication.timeSinceStartup < deadline, "Editor preview timeout: " + status);
                if (Read("process") != null || Read("preview") == null || (double)Read("renderAt") != double.PositiveInfinity) return;
                var settings = (PixelFxEditorSettings)Read("settings");
                var texture = (Texture2D)Read("sheet");
                Check(texture != null && texture.filterMode == FilterMode.Point, "Live preview texture must remain crisp");
                if (phase == 0)
                {
                    Check(settings.profiles.Length == 15, "All effects must be editable");
                    settings.profiles[0].speed = 2;
                    settings.profiles[0].palette[3] = "#ff0000";
                    Call("Changed"); phase = 1;
                }
                else if (phase == 1)
                {
                    var preview = Read("preview");
                    int count = ((Array)preview.GetType().GetField("frames").GetValue(preview)).Length;
                    Check(count == 6, "Changing speed must rebuild the live preview");
                    Call("Save");
                    string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    var saved = JsonUtility.FromJson<PixelFxEditorSettings>(File.ReadAllText(Path.Combine(root, "Design/Skills/PixelFX-v1/tuning.json")));
                    Check(saved.profiles[0].speed == 2 && string.Equals(saved.profiles[0].palette[3], "#ff0000", StringComparison.OrdinalIgnoreCase), "Editor settings must persist");
                    var defaults = JsonUtility.FromJson<PixelFxEditorSettings>(File.ReadAllText(Path.Combine(root, "Design/Skills/PixelFX-v1/tuning-defaults.json")));
                    settings.profiles = defaults.profiles;
                    Call("Save"); Call("StartRender", true); phase = 2;
                }
                else
                {
                    Check(Resources.Load<Texture2D>("ProjectDM/Skills/PixelFX/skill-effects").width == 1200, "Apply must export and import the game atlas");
                    Debug.Log("PIXELFX EDITOR PASSED: window GUI, 15 profiles, async live regeneration, speed/color edit, save and game apply");
                    Finish(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Finish(int code) { EditorApplication.update -= Tick; window.Close(); EditorApplication.Exit(code); }
    }
}
