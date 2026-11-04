using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    internal static class PixelFxLibrary
    {
        [Serializable] internal sealed class FrameRect { public int x, y, width, height; }
        [Serializable] internal sealed class Clip { public string id; public int fps; public bool loop; public FrameRect[] frames; [NonSerialized] internal Sprite[] sprites; }
        [Serializable] private sealed class Manifest { public int width, height; public Clip[] clips; }
        private static readonly Dictionary<string, Clip> clips = new();
        private static bool loaded;
        internal static Clip Find(string id)
        {
            if (!loaded)
            {
                loaded = true;
                var data = Resources.Load<TextAsset>("ProjectDM/Skills/PixelFX/skill-effects");
                var texture = Resources.Load<Texture2D>("ProjectDM/Skills/PixelFX/skill-effects");
                if (data == null || texture == null) return null;
                var manifest = JsonUtility.FromJson<Manifest>(data.text);
                if (manifest?.clips == null || texture.width != manifest.width || texture.height != manifest.height)
                    throw new InvalidOperationException("PixelFX 텍스처 크기 또는 애니메이션 정의 오류");
                foreach (var clip in manifest.clips)
                {
                    if (string.IsNullOrEmpty(clip.id) || clip.fps < 1 || clip.frames == null || clip.frames.Length == 0 || clips.ContainsKey(clip.id))
                        throw new InvalidOperationException("PixelFX 클립 정의 오류");
                    clip.sprites = new Sprite[clip.frames.Length];
                    for (int i = 0; i < clip.frames.Length; i++)
                    {
                        var frame = clip.frames[i];
                        if (frame.x < 0 || frame.y < 0 || frame.width < 1 || frame.height < 1 || frame.x + frame.width > texture.width || frame.y + frame.height > texture.height)
                            throw new InvalidOperationException("PixelFX 프레임 범위 오류");
                        clip.sprites[i] = Sprite.Create(texture, new Rect(frame.x, texture.height - frame.y - frame.height, frame.width, frame.height),
                            clip.id == "missile" ? new Vector2(.625f, .5f) : Vector2.one * .5f, 32, 0, SpriteMeshType.FullRect);
                        clip.sprites[i].name = "PixelFX." + clip.id + "." + i;
                    }
                    clips.Add(clip.id, clip);
                }
            }
            return id != null && clips.TryGetValue(id, out var result) ? result : null;
        }
        internal static Sprite Frame(string id, float age)
        {
            var clip = Find(id); if (clip == null) return null;
            int index = Mathf.Max(0, Mathf.FloorToInt(age * clip.fps));
            return clip.sprites[clip.loop ? index % clip.sprites.Length : Mathf.Min(index, clip.sprites.Length - 1)];
        }

        internal static bool DrawMote(Vector2 center,float diameter,Color tint,float age)
        {
            var sprite=Frame("mote",age);if(sprite==null)return false;
            var pixels=sprite.textureRect;var texture=sprite.texture;
            Color previous=GUI.color;GUI.color=previous*tint;
            GUI.DrawTextureWithTexCoords(new Rect(center-Vector2.one*diameter*.5f,Vector2.one*diameter),texture,
                new Rect(pixels.x/texture.width,pixels.y/texture.height,pixels.width/texture.width,pixels.height/texture.height));
            GUI.color=previous;return true;
        }
    }
}
