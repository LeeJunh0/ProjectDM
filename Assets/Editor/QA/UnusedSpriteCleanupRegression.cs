using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Verifies art cleanup without deleting files or modifying scenes/player progress.</summary>
    public static class UnusedSpriteCleanupRegression
    {
        private static int checks;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        [MenuItem("Project DM/검증/미사용 스프라이트 정리 검증")]
        public static void RunMenu() { Verify(); Debug.Log($"Sprite cleanup: {checks} assertions PASS."); }

        public static void PrepareBatch()
        {
            try
            {
                if (!Application.isBatchMode) throw new InvalidOperationException("격리 배치 프로젝트에서만 실행하세요.");
                ProjectDMArtPipeline.ImportProjectileAnimation();
                StageArtPipeline.ImportContent();
                RunMenu(); StageContentRegression.RunMenu(); PrototypeRegression.RunMenu(); GrowthGraphQa.RunMenu();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/SpriteCleanup-result.txt", $"PASS: {checks} cleanup assertions; stage, combat and growth regression suites passed.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void Verify()
        {
            checks = 0;
            string[] removedArt = { "ProjectDM_Sprites", "ProjectDM_FloorTiles_v1", "ProjectDM_FloorTiles_Calm_v2",
                "ProjectDM_FloorBorderTiles_v1", "ProjectDM_FloorBorderTiles_Calm_v2", "ProjectDM_Actors_Side_v3" };
            foreach (string name in removedArt)
            {
                string path = $"Assets/GameContent/Art/{name}.png";
                Check(!File.Exists(path) && !File.Exists(path + ".meta"), "Obsolete image and meta removed together: " + name);
            }
            foreach (string name in new[] { "ProjectDM_Player.controller", "ProjectDM_PlayerIdle.anim", "ProjectDM_PlayerWalk.anim" })
                Check(!File.Exists("Assets/GameContent/Animation/" + name), "Unreferenced legacy animation removed: " + name);

            Sprite[] gameplay = AssetDatabase.LoadAllAssetsAtPath("Assets/GameContent/Art/ProjectDM_Sprites_TopDown_v2.png").OfType<Sprite>().ToArray();
            Check(gameplay.Length == 6, "Only fallback coin/gem and four projectile sprites remain");
            string[] required = { "Experience_Gem", "Gold_Coin", "Projectile_Bolt_0", "Projectile_Bolt_1", "Projectile_Bolt_2", "Projectile_Bolt_3" };
            long[] ids = { -5252421055456862097, -287246070730913912, -1132183327, 1982213110, -1418005776, 1341735273 };
            for (int i = 0; i < required.Length; i++)
            {
                Sprite sprite = gameplay.SingleOrDefault(s => s.name == required[i]);
                Check(sprite != null, "Required gameplay sprite survives: " + required[i]);
                Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id) && guid == "502dc223c83ea6b4ebad6eefe15e9d07" && id == ids[i], "Existing gameplay sprite identity preserved: " + required[i]);
            }
            Sprite[] dungeon = AssetDatabase.LoadAllAssetsAtPath("Assets/GameContent/Art/ProjectDM_DungeonMonsters_2Frame_v1.png").OfType<Sprite>().ToArray();
            Check(dungeon.Length == 2 && dungeon.All(s => s.name.StartsWith("Dungeon_5_")), "Unused dungeon creature slices removed; both boss frames survive");
            StageDefinition stage2 = Resources.Load<StageDefinition>("ProjectDM/Stages/Stage2_Dungeon");
            Check(stage2.bossFrames.All(s => s != null && AssetDatabase.GetAssetPath(s) == ProjectDMMonsterArtSetup.SheetPath("Dungeon")),
                "Active boss preview uses the replacement atlas; legacy slices remain available");
            AnimationClip bolt = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/GameContent/Animation/ProjectDM_BoltLoop.anim");
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = AnimationUtility.GetObjectReferenceCurve(bolt, binding);
            Check(keys != null && keys.Length == 4 && keys.All(k => k.value is Sprite s && gameplay.Contains(s)), "Actual projectile clip keeps four valid sprite frames");
            ProjectDMAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<ProjectDMAssetCatalog>("Assets/GameContent/Configuration/ProjectDMAssetCatalog.asset");
            Check(AssetDatabase.GUIDToAssetPath(catalog.floorSheet.AssetGUID) == "Assets/GameContent/Art/ProjectDM_FloorTiles_Calm_v3.png", "Bootstrap uses the current floor sheet, not a deleted version");

            string[] removedGuids = { "0d735ca4c1354a34681f9edf2d262b46", "ad983b563b2842d4784de62d1316bd0b",
                "72a123b8ee09fa24d8653ae7dbef0644", "b997f6497f34b58419131a3a7fd40e29", "fe478d1f56fd515479fe33cc76e70b7f",
                "570ea5ec1abd0754993db27522aca4c1", "a750f9bb76be9974093cf669ef88cbb9", "9b5b89c24c700ee47be826d1cd8b1d99", "c51fdfed6d8d4354fbd3496e42dde50f" };
            foreach (string path in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories)
                .Where(p => new[] { ".unity", ".prefab", ".asset", ".anim", ".controller" }.Contains(Path.GetExtension(p))))
            {
                string text = File.ReadAllText(path);
                Check(!removedGuids.Any(text.Contains), "No dangling deleted-art/animation reference: " + path);
            }
        }
    }
}
