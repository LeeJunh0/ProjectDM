using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    public static class ManaLiquidRegression
    {
        private static int checks;
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); checks++; }

        [MenuItem("Project DM/검증/액체 MP 바 검증")]
        public static void RunMenu() { Verify(); Debug.Log($"Liquid MP: {checks} assertions PASS."); }
        public static void RunBatch()
        {
            try
            {
                Verify(); Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ManaLiquidRegression-result.txt", $"PASS: {checks} assertions; shader render, liquid motion, palette transitions, pause, recovery reaction, resource reuse/cleanup, MP/RNG isolation.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ManaLiquidRegression-result.txt", "FAIL: " + exception);
                Debug.LogException(exception); EditorApplication.Exit(1);
            }
        }

        private static Texture2D Read(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                Texture2D image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                return image;
            }
            finally { RenderTexture.active = previous; }
        }

        private static void Verify()
        {
            checks = 0;
            ManaBarSettings original = Resources.Load<ManaBarSettings>("ProjectDM/UI/ManaBarSettings");
            Check(original != null && original.liquidShader != null, "Player-loadable appearance with directly referenced shader");
            ManaBarSettings settings = Object.Instantiate(original);
            RunMana mana = new RunMana(); mana.Reset(40, 1);
            ManaBarRenderer visual = new ManaBarRenderer(mana, settings);
            UnityEngine.Random.State rng = UnityEngine.Random.state;
            Texture2D board = null;
            try
            {
                visual.Tick(.01f);
                Check(visual.CurrentColor == settings.normalColor, "Default liquid palette is blue");
                visual.SetState(ManaVisualState.Awakened); visual.Tick(settings.colorTransitionDuration / 2);
                Check(visual.CurrentColor != settings.normalColor && visual.CurrentColor != settings.awakenedColor, "Palette blends instead of snapping");
                visual.Tick(settings.colorTransitionDuration);
                Check(visual.CurrentColor == settings.awakenedColor, "Awakened palette reaches purple");
                float clock = visual.AnimationTime;
                visual.Tick(0);
                Check(visual.AnimationTime == clock, "Pause freezes liquid time");
                mana.SpendForBasicAttack(); mana.Absorb(1); visual.Tick(.01f);
                Check(visual.Reaction > 0, "Spending and absorbing in one frame still creates liquid reaction despite unchanged total MP");
                Check(mana.Current == 40 && rng.Equals(UnityEngine.Random.state), "Presentation changes neither mana nor gameplay random sequence");
                visual.Reset();
                Check(visual.State == ManaVisualState.Normal && visual.Reaction == 0, "Restart restores preview state and clears reaction");
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
                using (MaterialScope material = new MaterialScope(settings.liquidShader))
                {
                    ShaderUtil.CompilePass(material.value, 0);
                    Check(!ShaderUtil.ShaderHasError(settings.liquidShader), "Liquid shader compiles on actual GPU");
                }
                const int width = 640, height = 56;
                board = new Texture2D(width, height * 4, TextureFormat.RGBA32, false);
                mana.Consume(16);
                int row = 0;
                foreach (ManaVisualState state in new[] { ManaVisualState.Normal, ManaVisualState.Awakened, ManaVisualState.Rage, ManaVisualState.Overheated })
                {
                    visual.SetState(state); visual.Tick(1);
                    RenderTexture texture = (RenderTexture)visual.Render(width, height);
                    Check(texture != null, "Actual liquid render exists");
                    Check(ReferenceEquals(texture, visual.Render(width, height)), "Render target reused without per-frame allocations");
                    Texture2D image = Read(texture);
                    try
                    {
                        Color c = image.GetPixel(100, 20), empty = image.GetPixel(600, 20);
                        Check(c.maxColorComponent > empty.maxColorComponent + .1f, "Liquid fill matches MP fraction, not the entire container");
                        if (state == ManaVisualState.Normal) Check(c.b > c.r, "Blue palette renders blue");
                        if (state == ManaVisualState.Awakened) Check(c.b > c.g && c.r > c.g, "Purple palette renders purple");
                        if (state == ManaVisualState.Rage) Check(c.r > c.g && c.r > c.b, "Red palette renders red");
                        if (state == ManaVisualState.Overheated) Check(c.r > c.g && c.g > c.b, "Orange palette renders orange");
                        board.SetPixels(0, height * (3 - row++), width, height, image.GetPixels());
                    }
                    finally { Object.DestroyImmediate(image); }
                }
                Texture2D before = Read((RenderTexture)visual.Render(width, height));
                visual.Tick(.25f);
                Texture2D after = Read((RenderTexture)visual.Render(width, height));
                try
                {
                    Color32[] a = before.GetPixels32(), b = after.GetPixels32(); int differences = 0;
                    for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) differences++;
                    Check(differences > width, "Rendered liquid actually flows and waves over time");
                }
                finally { Object.DestroyImmediate(before); Object.DestroyImmediate(after); }
                mana.Consume(mana.Current);
                Texture2D zero = Read((RenderTexture)visual.Render(width, height));
                try { Check(zero.GetPixel(100, 20).maxColorComponent < .3f, "Zero MP leaves only the empty container"); }
                finally { Object.DestroyImmediate(zero); }
                board.Apply(); Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/ManaLiquid-palettes.png", board.EncodeToPNG());
            }
            finally
            {
                visual.Dispose();
                Check(RuntimeTestAccess.Get(visual, "target") == null && RuntimeTestAccess.Get(visual, "material") == null, "Runtime render resources released");
                if (board != null) Object.DestroyImmediate(board);
                Object.DestroyImmediate(settings); UnityEngine.Random.state = rng;
            }
        }

        private sealed class MaterialScope : IDisposable
        {
            internal readonly Material value;
            internal MaterialScope(Shader shader) { value = new Material(shader); }
            public void Dispose() { Object.DestroyImmediate(value); }
        }
    }
}
