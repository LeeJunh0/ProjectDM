using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    internal sealed class ResourceAnimationSequence
    {
        internal string key, category, label, family, direction, note;
        internal Sprite[] frames;
        internal float[] durations;
        internal Object source;
        internal float Duration => durations.Sum();

        internal int FrameAt(double time)
        {
            double remaining = Math.Max(0, time) % Duration;
            for (int i = 0; i < durations.Length; i++)
            {
                if (remaining + .000001 < durations[i]) return i;
                remaining -= durations[i];
            }
            return 0;
        }

        internal double TimeAt(int frame) => durations.Take(Mathf.Clamp(frame, 0, frames.Length - 1)).Sum();
    }

    /// <summary>Read live asset bindings; no preview objects, imports or changes to scene data.</summary>
    internal static class ResourceAnimationPreviewLibrary
    {
        internal static List<ResourceAnimationSequence> Load()
        {
            var result = new List<ResourceAnimationSequence>();
            void Add(string key, string category, string label, Sprite[] frames, float seconds, Object source, string note = null)
            {
                if (frames == null || frames.Length == 0 || frames.Any(frame => frame == null)) return;
                result.Add(new ResourceAnimationSequence { key = key, category = category, label = label, frames = frames,
                    durations = frames.Select(_ => Mathf.Max(.001f, seconds)).ToArray(), source = source, note = note });
            }
            void Clip(AnimationClip clip, string category, string label, string family = null, string direction = null)
            {
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                    .Where(item => item.type == typeof(SpriteRenderer) && item.propertyName == "m_Sprite").ToArray();
                if (binding.Length == 0) return;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding[0]);
                if (keys.Length == 0 || keys.Any(key => !(key.value is Sprite))) return;
                Add(AssetDatabase.GetAssetPath(clip), category, label, keys.Select(key => (Sprite)key.value).ToArray(), 1f / clip.frameRate, clip);
                var sequence = result[result.Count - 1]; sequence.family = family; sequence.direction = direction;
                sequence.durations = keys.Select((key, index) => Mathf.Max(.001f,
                    (index + 1 < keys.Length ? keys[index + 1].time : clip.length) - key.time)).ToArray();
            }
            foreach (string name in GameplayArtPaths.CharacterNames)
            {
                foreach (string direction in new[] { "Idle_Down", "Walk_Down", "Walk_Left", "Walk_Right", "Walk_Up" })
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{GameplayArtPaths.CharacterAnimations}/{name}_{direction}.anim");
                    if (clip != null) Clip(clip, "캐릭터", name + " / " + direction, name,
                        direction.StartsWith("Walk_") ? direction.Substring(5) : null);
                }
            }
            foreach (StageDefinition stage in Resources.LoadAll<StageDefinition>("ProjectDM/Stages").OrderBy(stage => stage.stageId))
            {
                for (int slot = 0; slot < stage.monsters.Length; slot++)
                {
                    var monster = stage.monsters[slot];
                    Add($"stage/{stage.stageId}/{slot}", "몬스터", $"{stage.stageId}스테이지 / {monster.displayName}",
                        monster.frames, monster.frameSeconds, stage);
                }
                Add($"stage/{stage.stageId}/boss", "보스", $"{stage.stageId}스테이지 / {stage.bossName}", stage.bossFrames, .2f, stage,
                    "보스 리소스 프레임: 5fps 비교 재생. 현재 게임의 도달 미리보기는 첫 프레임을 표시합니다.");
            }
            var skills = Resources.Load<SkillVisualAssets>("ProjectDM/Skills/SkillVisuals");
            if (skills != null)
            {
                Add("skill/chain", "스킬", "연쇄 번개", new[] { skills.chain }, 1, skills);
                Add("skill/splash", "스킬", "스플래쉬", new[] { skills.splash }, 1, skills);
                Add("skill/meteor", "스킬", "운석", new[] { skills.meteor }, 1, skills);
                Add("skill/frost", "스킬", "빙결", new[] { skills.frostShard }, 1, skills);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/GameContent/Animation" }).OrderBy(id => AssetDatabase.GUIDToAssetPath(id)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(GameplayArtPaths.CharacterAnimations + "/")) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                string category = clip.name.Contains("Experience") || clip.name.Contains("Currency") || clip.name.Contains("Pickup")
                    ? "드랍템" : clip.name.Contains("Bolt") || clip.name.Contains("Projectile") ? "스킬" : "기타";
                Clip(clip, category, clip.name.Replace("ProjectDM_", ""));
            }
            // Include native groups without AnimationClips, including static mana/chest art.
            var catalog = Resources.Load<Pixel32Catalog>("ProjectDM/Pixel32Catalog");
            if (catalog != null)
                foreach (var group in catalog.entries.Where(entry => entry.sourceFileId != 0 && entry.key.StartsWith("CollectiblesAndProjectiles/"))
                    .GroupBy(entry => entry.key.Substring(0, entry.key.LastIndexOf('/'))).OrderBy(group => group.Key))
                {
                    Sprite[] frames = group.OrderBy(entry => entry.sprite.name).Select(entry => entry.sprite).ToArray();
                    if (result.Any(sequence => sequence.frames.SequenceEqual(frames))) continue;
                    string name = group.Key.Substring(group.Key.LastIndexOf('/') + 1);
                    Add(group.Key, name.StartsWith("Projectile") ? "스킬" : "드랍템", name, frames, .125f, frames[0],
                        "원본 스프라이트 프레임 비교용입니다. 게임의 부유·파티클·스킬 변형 연출은 이 창에서 재생하지 않습니다.");
                }
            foreach (var sequence in result.Where(sequence => sequence.category == "스킬" && sequence.frames.Length == 1))
                sequence.note = "단일 스프라이트입니다. 게임에서 적용하는 이동·회전·확대·파티클 연출은 포함하지 않습니다.";
            return result;
        }
    }
}
