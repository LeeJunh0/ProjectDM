using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Presentation only: the wager is already saved; controls wait for the kettle to settle.</summary>
    internal sealed class RelicForgeMotion
    {
        internal const float Duration = 1.4f, RevealAt = .72f;
        [Serializable] private sealed class Manifest { public int width, height; public PixelFxLibrary.Clip[] clips; }
        private readonly Dictionary<string, PixelFxLibrary.Clip> clips = new();
        private Texture2D texture;
        private bool loaded, success, deposit;
        private float started = float.NegativeInfinity;
        private int previousFragments;
        private Texture2D coinTexture;
        internal int DepositCoins { get; private set; }
        internal static int CoinCount(int amount) => amount <= 0 ? 0 : Mathf.Min(amount, Mathf.Clamp(Mathf.CeilToInt(8 * Mathf.Log(1f + amount)), 1, 128));
        internal static float CoinPhase(float age, int index, int count)
        {
            if (count <= 0) return 2;
            float life = age - (count <= 1 ? 0 : .22f * Mathf.Pow((float)index / (count - 1), .65f) + .025f * PickupFlightPresentation.Variation(index + 1, 97));
            return life < 0 ? -1 : life < .18f ? life / .18f : 1 + (life - .18f) / .20f;
        }
        internal bool Busy(float now) => now - started < Duration;
        internal bool Revealed(float now) => now - started >= RevealAt;
        internal int VisibleFragments(int actual, float now) => Revealed(now) ? actual : previousFragments;
        internal string PhaseText(float now) => Revealed(now) ? null : deposit && now - started < .28f
            ? "원정 금화를 녹여 마력을 추출하고 있어요." : "가마 속 마력을 더 단단하게 응축하고 있어요.";
        internal void Begin(bool won, bool payGold, int fragmentsBefore, float now, int amount = 0)
        { success = won; deposit = payGold; previousFragments = fragmentsBefore; started = now; DepositCoins = payGold ? CoinCount(amount) : 0; }
        internal void Clear() { started = float.NegativeInfinity; success = false; DepositCoins = 0; }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            texture = Resources.Load<Texture2D>("ProjectDM/UI/RelicForge/forge-effects");
            coinTexture = Resources.Load<Texture2D>("ProjectDM/UI/RelicForge/forge-coin-v1");
            var data = Resources.Load<TextAsset>("ProjectDM/UI/RelicForge/forge-effects");
            if (texture == null || data == null || coinTexture == null) throw new InvalidOperationException("연금 가마 리소스가 없습니다.");
            var manifest = JsonUtility.FromJson<Manifest>(data.text);
            if (manifest == null || manifest.width != texture.width || manifest.height != texture.height || manifest.clips == null)
                throw new InvalidOperationException("연금 가마 시트 크기 오류");
            foreach (var clip in manifest.clips)
            {
                if (clip.fps < 1 || clip.frames == null || clip.frames.Length == 0 || clips.ContainsKey(clip.id))
                    throw new InvalidOperationException("연금 가마 클립 오류");
                foreach (var frame in clip.frames)
                    if (frame.x < 0 || frame.y < 0 || frame.width < 1 || frame.height < 1 || frame.x + frame.width > texture.width || frame.y + frame.height > texture.height)
                        throw new InvalidOperationException("연금 가마 프레임 영역 오류");
                clips.Add(clip.id, clip);
            }
        }

        internal void Draw(Rect area, float now, bool pending)
        {
            if (Event.current.type != EventType.Repaint) return;
            Load();
            float age = Mathf.Max(0, now - started);
            if (Busy(now) && age > .55f && age < RevealAt) area.x += Mathf.Round(Mathf.Sin(age * 80) * 1.5f);
            var idle = clips["idle"];
            DrawFrame(area, idle.frames[Mathf.FloorToInt(now * idle.fps) % idle.frames.Length]);
            if (Busy(now))
            {
                var clip = clips[(deposit ? "" : "condense-") + (success ? "success" : "failure")];
                DrawFrame(area, clip.frames[Mathf.Min(clip.frames.Length - 1, Mathf.FloorToInt(age * clip.fps))]);
                if (deposit) DrawCoins(area, age);
            }
            else if (success && pending)
            {
                var frame = clips["success"].frames[26];
                DrawFrame(new Rect(area.x + area.width * 65 / 160, area.y + area.height * 46 / 144, area.width * 30 / 160, area.height * 34 / 144),
                    new PixelFxLibrary.FrameRect { x = frame.x + 65, y = frame.y + 46, width = 30, height = 34 });
            }
        }
        private void DrawCoins(Rect area, float age)
        {
            for (int i = 0; i < DepositCoins; i++)
            {
                float q = CoinPhase(age, i, DepositCoins);
                if (q < 0 || q >= 2) continue;
                float V(uint salt) => PickupFlightPresentation.Variation(i + 1, salt);
                Vector2 start = new Vector2(-22 + 10 * V(11), -6 - 5 * V(23));
                Vector2 surface = new Vector2(-8 + 16 * V(37), 7 + 2 * V(53));
                float melt = Mathf.Max(0, q - 1);
                Vector2 point = q < 1 ? new Vector2(Mathf.Lerp(start.x, surface.x, q), Mathf.Lerp(start.y, surface.y, q * q))
                    : surface + new Vector2(0, melt * 2 - Mathf.Sin(melt * Mathf.PI) * 1.2f);
                float width = q < 1 ? 3 * (.45f + .55f * Mathf.Abs(Mathf.Cos(q * 12 + i))) : 3 * (1 - melt * .85f);
                float height = q < 1 ? 3 : 3 * (1 - melt * .97f);
                Color previous = GUI.color;
                GUI.color = previous * new Color(1, 1 - melt * .15f, 1 - melt * .55f, 1 - melt);
                GUI.DrawTextureWithTexCoords(CoinRect(area, point, width, height), coinTexture, new Rect(0, 0, 1, 1));
                GUI.color = previous;
                if (melt <= 0) continue;
                Color tint = new Color(1, .75f, .23f, (1 - melt) * .65f);
                float radius = 1 + 5 * melt;
                UiGraphics.DrawSolidRect(CoinRect(area, surface + Vector2.left * radius, 2, 1), tint);
                UiGraphics.DrawSolidRect(CoinRect(area, surface + Vector2.right * radius, 2, 1), tint);
                UiGraphics.DrawSolidRect(CoinRect(area, surface + new Vector2(0, -1), radius * 1.2f, 1), tint);
                UiGraphics.DrawSolidRect(CoinRect(area, surface + new Vector2((V(71) - .5f) * melt * 8, -melt * 5), 1, 1), tint);
            }
        }
        private static Rect CoinRect(Rect area, Vector2 point, float width, float height) => new Rect(
            area.x + (80 + point.x - width * .5f) * area.width / 160,
            area.y + (76 + point.y - height * .5f) * area.height / 144,
            width * area.width / 160, height * area.height / 144);
        internal void DrawFragmentIcon(Rect area)
        {
            if (Event.current.type != EventType.Repaint) return;
            Load();
            var frame = clips["success"].frames[26];
            DrawFrame(area, new PixelFxLibrary.FrameRect { x = frame.x + 65, y = frame.y + 46, width = 30, height = 34 });
        }
        private void DrawFrame(Rect rect, PixelFxLibrary.FrameRect frame) => GUI.DrawTextureWithTexCoords(rect, texture,
            new Rect((float)frame.x / texture.width, 1f - (float)(frame.y + frame.height) / texture.height, (float)frame.width / texture.width, (float)frame.height / texture.height));
    }
}
