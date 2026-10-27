using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Node glyphs are independent of progression IDs, prices and the existing combat artwork.</summary>
    internal static class GrowthIconAtlas
    {
        private static readonly Dictionary<string, Sprite> icons = new();
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

        internal static Sprite Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!loaded)
            {
                loaded = true;
                Load("GrowthStats", 4, 4, Stats); Load("GrowthUtility", 4, 3, Utility); Load("GrowthSkills", 3, 4, Skills);
            }
            id = id switch { "health" => "time", "chainChance" => "chain", "splashChance" => "splash", "meteorChance" => "meteor", _ => id };
            return icons.TryGetValue(id, out var sprite) ? sprite : null;
        }

        private static void Load(string name, int columns, int rows, string[] ids)
        {
            var atlas = Resources.Load<Texture2D>("ProjectDM/UI/GrowthIcons/" + name);
            if (atlas == null) return;
            for (int i = 0; i < ids.Length; i++)
            {
                int column = i % columns, row = i / columns;
                int left = Mathf.RoundToInt(column * atlas.width / (float)columns), right = Mathf.RoundToInt((column + 1) * atlas.width / (float)columns);
                int bottom = Mathf.RoundToInt((rows - row - 1) * atlas.height / (float)rows), top = Mathf.RoundToInt((rows - row) * atlas.height / (float)rows);
                var sprite = Sprite.Create(atlas, new Rect(left, bottom, right - left, top - bottom), Vector2.one * .5f,
                    Mathf.Max(right - left, top - bottom), 0, SpriteMeshType.FullRect);
                sprite.name = "growth." + ids[i]; sprite.hideFlags = HideFlags.HideAndDontSave;
                icons[ids[i]] = sprite;
            }
        }
    }
}
