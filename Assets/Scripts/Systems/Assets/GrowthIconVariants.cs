using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Growth-only redrawn artwork. Combat effects keep their own existing sprite atlas.</summary>
    internal static class GrowthIconVariants
    {
        private static readonly Dictionary<string, Sprite> icons = new(), modifiers = new(), dungeon = new();
        private static readonly Dictionary<string, Sprite> resolvedModifiers = new();
        private static readonly Dictionary<Sprite, Vector2> contentOffsets = new();
        private static bool loaded;
        private static readonly string[] Stats = {
            "damage", "speed", "time", "extension", "yield", "xp", "movement", "criticalChance",
            "criticalDamage", "invulnerability", "magnet", "luck", "population", "populationCap", "spawnBatch", "respawn"
        };
        private static readonly string[] Utility = {
            "experienceGradeChance", "silverDropChance", "goldDropChance", "emeraldDropChance",
            "amethystDropChance", "magnetDropChance", "multishot", "chain", "splash", "meteor", "range", "duration"
        };
        private static readonly string[] Skills = {
            "skill.projectile.missile", "skill.projectile.beam", "skill.projectile.satellite",
            "skill.flame.cone", "skill.flame.firefield", "skill.flame.meteor",
            "skill.lightning.chain", "skill.lightning.orb", "skill.lightning.thunder",
            "skill.frost.orbit", "skill.frost.blizzard", "skill.frost.shatter"
        };
        private static readonly string[] Modifiers = {
            "power", "speed", "count", "range", "width", "duration", "rotation", "homing",
            "pierce", "fork", "chain", "burn", "cold", "freeze", "unlock", "chance"
        };
        private static readonly string[] Dungeon = { "population", "populationCap", "spawnBatch", "respawn" };

        // Alpha-weighted centers of the visible pixels relative to each original sheet cell,
        // measured with an alpha threshold of 32. Small particles have less influence than the
        // main artwork; keep the asymmetric silhouettes and original sprite sizes.
        // Coordinates use top-down GUI space, and follow the ID order of each sheet above.
        private static readonly Vector2[] StatsOffsets = {
            new Vector2(.1081f, .1630f), new Vector2(.0980f, .1302f), new Vector2(-.0336f, .1652f), new Vector2(-.0197f, .1656f),
            new Vector2(.0655f, .1107f), new Vector2(.0416f, .0416f), new Vector2(.0285f, .1056f), new Vector2(-.0366f, .0500f),
            new Vector2(.0817f, .0204f), new Vector2(.0480f, .0119f), new Vector2(-.0179f, .0317f), new Vector2(-.0320f, .0242f),
            new Vector2(.0556f, -.0438f), new Vector2(.0272f, -.0537f), new Vector2(-.0223f, .0074f), new Vector2(-.0662f, .0336f),
        };
        private static readonly Vector2[] UtilityOffsets = {
            new Vector2(.0457f, .0690f), new Vector2(.0243f, .1030f), new Vector2(.0085f, .0951f), new Vector2(-.0126f, .0831f),
            new Vector2(.0402f, .0355f), new Vector2(.0463f, .0293f), new Vector2(.0429f, .0500f), new Vector2(-.0166f, .0284f),
            new Vector2(.0439f, .0070f), new Vector2(.0031f, -.0349f), new Vector2(-.0076f, -.0364f), new Vector2(-.0371f, -.0771f),
        };
        private static readonly Vector2[] SkillsOffsets = {
            new Vector2(.0286f, .1104f), new Vector2(-.0378f, .1109f), new Vector2(.0201f, .1225f),
            new Vector2(.0780f, .0880f), new Vector2(.0012f, .1183f), new Vector2(-.0238f, .1100f),
            new Vector2(.0578f, .0003f), new Vector2(.0046f, .0404f), new Vector2(.0021f, .1130f),
            new Vector2(.0203f, -.0424f), new Vector2(-.0122f, -.0644f), new Vector2(.0370f, -.0582f),
        };
        private static readonly Vector2[] DungeonOffsets = {
            new Vector2(.0605f, .1314f), new Vector2(-.0121f, .1154f),
            new Vector2(.0428f, .0130f), new Vector2(.0079f, .0262f),
        };

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            // Generative sheets have slightly uneven gutters. Boundaries are measured on the
            // source PNGs so one icon never inherits a clipped piece of its neighbour.
            Load("MatchedStats_v2", new[] { 0, 322, 645, 968, 1290 }, new[] { 0, 344, 619, 914, 1219 }, Stats, icons, offsets: StatsOffsets);
            Load("MatchedUtility_v2", new[] { 0, 373, 724, 1086, 1448 }, new[] { 0, 362, 708, 1086 }, Utility, icons, offsets: UtilityOffsets);
            Load("OrganicSkills_v3", new[] { 0, 410, 840, 1230 }, new[] { 0, 342, 680, 988, 1278 }, Skills, icons,
                new[] { 330, 658, 965, 1278 }, SkillsOffsets);
            // This orb's current combat effect is violet; keep its redrawn growth icon consistent.
            LoadSingle("OrganicLightningOrb_v3", "skill.lightning.orb", icons);
            if (icons.TryGetValue("skill.lightning.orb", out Sprite orb) && orb.name == "OrganicLightningOrb_v3.skill.lightning.orb")
                contentOffsets[orb] = new Vector2(.0145f, .0532f);
            Load("MatchedModifiers_v2", new[] { 0, 330, 627, 940, 1254 }, new[] { 0, 321, 627, 919, 1254 }, Modifiers, modifiers);
            Load("MatchedDungeonPopulation_v2", new[] { 0, 645, 1263 }, new[] { 0, 623, 1246 }, Dungeon, dungeon, offsets: DungeonOffsets);
        }

        internal static Vector2 ContentOffset(Sprite sprite)
            => sprite != null && contentOffsets.TryGetValue(sprite, out Vector2 offset) ? offset : Vector2.zero;

        internal static Sprite Find(string rowId, string skillId, int stageId)
        {
            EnsureLoaded();
            rowId = rowId switch { "health" => "time", "chainChance" => "chain", "splashChance" => "splash", "meteorChance" => "meteor", _ => rowId };
            if (stageId == 2 && rowId != null && dungeon.TryGetValue(rowId, out Sprite bat)) return bat;
            if (rowId != null && icons.TryGetValue(rowId, out Sprite common)) return common;
            return skillId != null && icons.TryGetValue(skillId, out Sprite skill) ? skill : null;
        }

        internal static Sprite Modifier(string rowId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(rowId)) return null;
            if (resolvedModifiers.TryGetValue(rowId, out Sprite cached)) return cached;
            string key = ModifierKey(rowId);
            Sprite icon = key != null && modifiers.TryGetValue(key, out Sprite found) ? found : null;
            resolvedModifiers[rowId] = icon;
            return icon;
        }

        internal static string ModifierKey(string rowId)
        {
            if (string.IsNullOrEmpty(rowId) || !rowId.StartsWith("magic.", StringComparison.Ordinal)) return null;
            string property = rowId.Substring(rowId.LastIndexOf('.') + 1);
            if (property.EndsWith("Unlock", StringComparison.Ordinal)) return "unlock";
            if (property.EndsWith("Power", StringComparison.Ordinal) || property == "damage") return "power";
            if (property == "orbitSpeed" || property.EndsWith("Sweep", StringComparison.Ordinal) || property == "echo") return "rotation";
            if (property == "speed" || property.EndsWith("Rate", StringComparison.Ordinal)) return "speed";
            if (property == "fork" || property == "split") return "fork";
            if (property == "jumps" || property.EndsWith("Link", StringComparison.Ordinal) || property.EndsWith("Chain", StringComparison.Ordinal) || property.EndsWith("Cascade", StringComparison.Ordinal)) return "chain";
            if (property == "count" || property == "rings" || property.EndsWith("Count", StringComparison.Ordinal) || property.EndsWith("Shards", StringComparison.Ordinal) || property.EndsWith("Volley", StringComparison.Ordinal)) return "count";
            if (property == "range" || property.EndsWith("Range", StringComparison.Ordinal)) return "range";
            if (property == "width" || property == "size" || property.EndsWith("Width", StringComparison.Ordinal)) return "width";
            if (property.EndsWith("Duration", StringComparison.Ordinal)) return "duration";
            if (property == "homing") return "homing";
            if (property == "pierce") return "pierce";
            if (property == "burn" || property == "spread" || property.EndsWith("Ignite", StringComparison.Ordinal)) return "burn";
            if (property == "chill" || property.EndsWith("Cold", StringComparison.Ordinal)) return "cold";
            return null;
        }

        private static void Load(string name, int[] xs, int[] ys, string[] ids, Dictionary<string, Sprite> target, int[] rowEnds = null, Vector2[] offsets = null)
        {
            Texture2D texture = Resources.Load<Texture2D>("ProjectDM/UI/GrowthIcons/" + name);
            if (texture == null) return;
            int columns = xs.Length - 1;
            float width = xs[columns], height = ys[ys.Length - 1];
            for (int i = 0; i < ids.Length; i++)
            {
                int column = i % columns, row = i / columns;
                int left = Mathf.RoundToInt(xs[column] * texture.width / width), right = Mathf.RoundToInt(xs[column + 1] * texture.width / width);
                int top = texture.height - Mathf.RoundToInt(ys[row] * texture.height / height);
                int bottom = texture.height - Mathf.RoundToInt((rowEnds == null ? ys[row + 1] : rowEnds[row]) * texture.height / height);
                var sprite = Sprite.Create(texture, new Rect(left, bottom, right - left, top - bottom), Vector2.one * .5f,
                    Mathf.Max(right - left, top - bottom), 0, SpriteMeshType.FullRect);
                sprite.name = name + "." + ids[i]; sprite.hideFlags = HideFlags.HideAndDontSave;
                target[ids[i]] = sprite;
                if (offsets != null) contentOffsets[sprite] = offsets[i];
            }
        }

        private static void LoadSingle(string name, string id, Dictionary<string, Sprite> target)
        {
            Texture2D texture = Resources.Load<Texture2D>("ProjectDM/UI/GrowthIcons/" + name);
            if (texture == null) return;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f,
                Mathf.Max(texture.width, texture.height), 0, SpriteMeshType.FullRect);
            sprite.name = name + "." + id; sprite.hideFlags = HideFlags.HideAndDontSave;
            target[id] = sprite;
        }
    }
}
