using System;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Presentation only: one small render target, no gameplay RNG, MP mutations or GUI interactions.</summary>
    internal sealed class ManaBarRenderer : IDisposable
    {
        private readonly RunMana mana;
        private readonly ManaBarSettings settings;
        private Material material;
        private RenderTexture target;
        private bool hasOverride;
        private ManaVisualState stateOverride;
        private Color currentColor, colorFrom, colorTo;
        private float colorAge, animationTime, reaction, lastMana, lastAbsorbed, lastSpent;
        private bool initialized;

        internal ManaBarRenderer(RunMana mana, ManaBarSettings settings)
        { this.mana = mana; this.settings = settings; Reset(); }

        internal Color CurrentColor => currentColor;
        internal float AnimationTime => animationTime;
        internal float Reaction => reaction;
        internal ManaVisualState State => hasOverride ? stateOverride : settings != null ? settings.previewState : ManaVisualState.Normal;

        internal void SetState(ManaVisualState state) { hasOverride = true; stateOverride = state; }

        internal void Reset()
        {
            hasOverride = false; initialized = false;
            animationTime = reaction = 0; lastMana = mana.Current;
            lastAbsorbed = mana.AbsorbedThisRun; lastSpent = mana.SpentThisRun;
            currentColor = colorFrom = colorTo = settings != null ? settings.StateColor(State) : new Color(.1f, .48f, 1);
            colorAge = 0;
        }

        internal void Tick(float deltaTime)
        {
            if (settings == null || deltaTime <= 0) return;
            Color wanted = settings.StateColor(State);
            if (wanted != colorTo) { colorFrom = currentColor; colorTo = wanted; colorAge = 0; }
            colorAge += deltaTime;
            float blend = settings.colorTransitionDuration <= 0 ? 1 : Mathf.Clamp01(colorAge / settings.colorTransitionDuration);
            currentColor = Color.Lerp(colorFrom, colorTo, blend * blend * (3 - 2 * blend));
            float activity = Mathf.Abs(mana.Current - lastMana) + Mathf.Max(0, mana.AbsorbedThisRun - lastAbsorbed)
                + Mathf.Max(0, mana.SpentThisRun - lastSpent);
            if (initialized && activity > 0)
                reaction = Mathf.Clamp01(reaction + activity / Mathf.Max(1, mana.Maximum) * 8);
            initialized = true; lastMana = mana.Current;
            lastAbsorbed = mana.AbsorbedThisRun; lastSpent = mana.SpentThisRun;
            reaction = Mathf.MoveTowards(reaction, 0, deltaTime / Mathf.Max(.01f, settings.reactionDuration));
            animationTime += deltaTime;
        }

        internal Texture Render(int width, int height)
        {
            if (settings == null || settings.liquidShader == null || !settings.liquidShader.isSupported) return null;
            material ??= new Material(settings.liquidShader) { hideFlags = HideFlags.HideAndDontSave };
            width = Mathf.Clamp(width, 24, 1024); height = Mathf.Clamp(height, 8, 128);
            if (target == null || target.width != width || target.height != height)
            {
                if (target != null) { target.Release(); Release(target); }
                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                { name = "액체 MP 바", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                target.Create();
            }
            material.SetFloat("_Fill", mana.Fraction);
            material.SetColor("_LiquidColor", currentColor);
            material.SetFloat("_Clock", animationTime);
            material.SetFloat("_WaveSpeed", settings.waveSpeed);
            material.SetFloat("_WaveAmplitude", settings.waveAmplitude);
            material.SetFloat("_WaveFrequency", settings.waveFrequency);
            material.SetFloat("_FlowSpeed", settings.flowSpeed);
            material.SetFloat("_Glow", settings.glowStrength);
            material.SetFloat("_Reaction", reaction * settings.reactionStrength);
            material.SetFloat("_LowPulse", mana.Fraction <= settings.lowManaThreshold ? settings.lowManaPulse : 0);
            material.SetVector("_Resolution", new Vector4(width, height, 0, 0));
            RenderTexture previous = RenderTexture.active;
            try { Graphics.Blit(Texture2D.whiteTexture, target, material); }
            finally { RenderTexture.active = previous; }
            return target;
        }

        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        public void Dispose()
        {
            if (target != null) target.Release();
            Release(target); Release(material); target = null; material = null;
        }
    }
}
