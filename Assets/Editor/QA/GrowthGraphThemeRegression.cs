using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class GrowthGraphThemeRegression
    {
        private static int checks;
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }

        [MenuItem("Project DM/검증/캐릭터 성장 트리 색상 검증")]
        public static void RunMenu()
        {
            checks = 0;
            GrowthGraphTheme theme = Resources.Load<GrowthGraphTheme>("ProjectDM/UI/GrowthGraphTheme");
            Check(theme != null, "Growth color settings load in a player Resources path");
            GameSettings settings = new(); GameTables tables = GameTables.LoadResources(settings);
            SaveService save = new(tables, new MemoryProgressStore(), true); save.Load();
            ProgressionService progression = new(save, tables); progression.ResetSnapshot();
            WorldArtAssets art = new();
            PlayerWorld world = new(settings, tables, art, new PlayerStats(tables, progression));
            RunController run = new(settings, save, tables, progression, world, () => { }, () => { }, () => { }, () => { }, () => { }, () => false, new RunMana());
            GrowthGraphView view = new(settings, save, tables, progression, art, run, new GameNotifications(), new UiGraphics());
            var random = UnityEngine.Random.state;
            Color baseAccent = new(.96f, .66f, .19f);
            Color previousAccent = default;
            foreach (string id in new[] { "projectile", "flame", "lightning", "frost" })
            {
                run.characters.Select(id);
                string saveBefore = JsonUtility.ToJson(save.progress);
                RuntimeTestAccess.Set(view, "personalGraph", true); view.BuildPrototypeGraph();
                Check(view.ThemeCharacterId == id, "Theme uses the selected tree character immediately: " + id);
                Check(view.metaTreeNodes[1].accent == theme.Tint(baseAccent, id), "Actual personal node carries its theme: " + id);
                Check(view.metaTreeNodes[1].accent != previousAccent, "Personal palettes remain distinct: " + id);
                previousAccent = view.metaTreeNodes[1].accent;
                Check(run.characters.Current.id == "projectile", "Theme selection does not hot swap the active character");
                Color hidden = new(.035f, .023f, .017f, 1), reachable = new(.16f, .083f, .027f, .99f);
                Color.RGBToHSV(theme.Tint(hidden, id), out _, out _, out float hiddenValue);
                Color.RGBToHSV(theme.Tint(reachable, id), out _, out _, out float reachableValue);
                Check(hiddenValue < reachableValue && Mathf.Abs(theme.Tint(reachable, id).a - .99f) < .001f, "Theming preserves concealment and opacity");
                RuntimeTestAccess.Set(view, "personalGraph", false); view.BuildPrototypeGraph();
                Check(view.ThemeCharacterId == null && view.metaTreeNodes[1].accent == baseAccent, "Common tree keeps its existing gold for every selection");
                Check(JsonUtility.ToJson(save.progress) == saveBefore && save.metaCurrency == 0, "Drawing a themed tree never purchases or mutates saved growth");
            }
            Check(random.Equals(UnityEngine.Random.state), "Theme changes consume no gameplay RNG");
            Check(theme.Tint(baseAccent, "unknown") == baseAccent, "Unknown/legacy characters retain the existing palette");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/GrowthGraphThemeRegression-result.txt", $"PASS: {checks} assertions; four selected-character themes, common gold, hidden state, unchanged active character/save/RNG.\n");
            Debug.Log($"Growth graph colors: {checks} assertions PASS.");
        }
    }
}
