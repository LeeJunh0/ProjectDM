using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ProjectDM.Editor
{
    public static class ManaPickupRegression
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++;
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;

        [MenuItem("Project DM/검증/마나 드랍 검증")]
        public static void RunMenu()
        {
            checks = 0;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Prefabs/ManaPickup.prefab");
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectDMFrostManaSetup.ManaPath);
            Check(prefab != null && sprite != null, "Dedicated mana art and prefab exist");
            Check(prefab.GetComponentInChildren<SpriteRenderer>().sprite == sprite && prefab.GetComponentInChildren<PickupFloatVisual>() != null,
                "Mana prefab has its own art and pooled floating visual");
            ProjectDMAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<ProjectDMAssetCatalog>("Assets/GameContent/Configuration/ProjectDMAssetCatalog.asset");
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            Check(catalog.manaPickupPrefab.AssetGUID == guid && AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid).address == "prefab/pickup-mana",
                "Mana prefab is included in the runtime Addressables catalog");

            Random.State random = Random.state;
            GameObject root = new("Isolated mana pickup QA");
            GameObject player = new("Pickup test player");
            PickupSystem pickups = null;
            try
            {
                GameSettings settings = new();
                GameTables tables = GameTables.LoadResources(settings);
                tables.balance.manaAbsorbChance = 1;
                tables.balance.manaAbsorbAmount = 1;
                tables.balance.chestChance = 0;
                SaveService save = new(tables, new MemoryProgressStore(), true); save.Load();
                ProgressionService growth = new(save, tables); growth.ResetSnapshot();
                PlayerStats stats = new(tables, growth);
                WorldArtAssets art = new() { runtimeAssets = new ProjectDMRuntimeAssets
                {
                    ManaPickupPrefab = prefab,
                    ExperiencePickupPrefabs = new GameObject[5], CurrencyPickupPrefabs = new GameObject[5]
                } };
                for (int i = 0; i < 5; i++)
                {
                    art.runtimeAssets.ExperiencePickupPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameContent/Prefabs/ExperiencePickup_{i + 1:00}.prefab");
                    art.runtimeAssets.CurrencyPickupPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameContent/Prefabs/CurrencyPickup_{i + 1:00}.prefab");
                }
                PlayerWorld world = new(settings, tables, art, stats) { player = player.transform };
                RunMana mana = new(); mana.Reset(40, 1); mana.Consume(5);
                RunController run = new(settings, save, tables, growth, world, () => { }, () => { }, () => { }, () => { }, () => { }, () => false, mana);
                GameObjectPool pool = root.AddComponent<GameObjectPool>();
                float gain = 0;
                pickups = new PickupSystem(settings, save, tables, growth, art, world, run, pool, _ => { }, stats, (_, value) => gain += value);
                pickups.SpawnPrototypeLoot(Vector3.zero, 0);
                Pickup drop = pickups.pickups.Find(p => p.kind == PickupKind.Mana);
                Check(drop != null && drop.isLaunching && Near(drop.amount, 1) && Near(mana.Current, 35), "Kill drops mana without instantly restoring it");
                int wallet = save.metaCurrency;
                pickups.UpdatePickups(0);
                Check(drop.launchElapsed == 0 && Near(mana.Current, 35), "Paused pickups do not launch or collect");
                pickups.UpdatePickups(1);
                Check(!drop.isLaunching && Near(mana.Current, 35), "Fountain completes before collection");
                // Keep other rewards far away so only mana can be collected in this frame.
                foreach (Pickup other in pickups.pickups) other.transform.position = Vector3.one * 20;
                drop.transform.position = Vector3.right * .5f;
                pickups.UpdatePickups(.01f);
                Check(drop.transform.position.x < .5f && Near(mana.Current, 35), "Mana obeys the shared magnet before collection");
                drop.transform.position = player.transform.position;
                Transform previous = drop.transform;
                pickups.UpdatePickups(.01f);
                Check(Near(mana.Current, 36) && Near(mana.AbsorbedThisRun, 1) && Near(gain, 1), "Collection restores MP and shows only actual recovery");
                Check(save.metaCurrency == wallet && run.runCurrency == 0 && !previous.gameObject.activeSelf, "Mana never grants currency and returns to pool");
                pickups.UpdatePickups(.01f);
                Check(Near(mana.Current, 36) && Near(gain, 1), "Collected drop cannot award again next frame");
                pickups.CreatePickup("Mana reuse", Vector3.zero, null, PickupKind.Mana, 100, .28f);
                drop = pickups.pickups.Find(p => p.kind == PickupKind.Mana);
                Check(drop.transform == previous && drop.isLaunching && drop.launchElapsed == 0 && drop.transform.GetComponentInChildren<SpriteRenderer>().sprite == sprite,
                    "Pool reuse resets launch state and retains mana art");
                drop.isLaunching = false; pickups.UpdatePickups(.01f);
                Check(Near(mana.Current, 40) && Near(gain, 5) && Near(mana.AbsorbedThisRun, 5), "Oversized drop is capped and statistics reflect actual gain");
                pickups.CreatePickup("Full mana", Vector3.zero, null, PickupKind.Mana, 1, .28f);
                drop = pickups.pickups.Find(p => p.kind == PickupKind.Mana); drop.isLaunching = false;
                pickups.UpdatePickups(.01f);
                Check(!pickups.pickups.Contains(drop) && Near(gain, 5), "Full MP consumes the pickup without fake gain text");
                pickups.CreatePickup("Late mana", Vector3.zero, null, PickupKind.Mana, 10, .28f);
                drop = pickups.pickups.Find(p => p.kind == PickupKind.Mana); drop.isLaunching = false;
                mana.Consume(40); pickups.UpdatePickups(.01f); pickups.Collect(drop);
                Check(mana.IsDepleted && Near(gain, 5), "Late mana cannot revive a depleted run");
                mana.Reset(40, 1); mana.Consume(5); run.runEnded = true;
                pickups.Collect(drop);
                Check(Near(mana.Current, 35), "Results screen cannot absorb a late drop");
                pickups.Reset();
                run.runEnded = false; tables.balance.manaAbsorbChance = 0;
                pickups.SpawnPrototypeLoot(Vector3.zero, 0);
                Check(pickups.pickups.TrueForAll(p => p.kind != PickupKind.Mana), "Zero drop probability produces no mana");
                pickups.Reset(); Check(pickups.pickups.Count == 0, "Restart returns all pending drops");
            }
            finally
            {
                pickups?.Reset();
                Object.DestroyImmediate(player); Object.DestroyImmediate(root);
                Random.state = random;
            }
            Debug.Log($"Mana pickup regression: {checks} assertions PASS.");
        }
    }
}
