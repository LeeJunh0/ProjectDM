using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    internal enum CombatDamageKind { Attack, Received, Critical }

    /// <summary>Screen-space labels at captured hit positions. Visual variation never consumes combat RNG.</summary>
    internal sealed class CombatDamageDisplay
    {
        private sealed class Popup
        {
            public Vector3 position;
            public string text;
            public CombatDamageKind kind;
            public float age, horizontal;
        }
        private readonly List<Popup> active = new();
        private readonly Queue<Popup> available = new();
        private GUIStyle textStyle, outlineStyle;
        private int sequence;
        internal int Count => active.Count;

        internal void Show(Vector3 position, float damage, CombatDamageKind kind, int maximum)
        {
            if (damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage) || maximum < 1) return;
            while (active.Count >= maximum) { available.Enqueue(active[0]); active.RemoveAt(0); }
            Popup popup = available.Count > 0 ? available.Dequeue() : new Popup();
            popup.position = position;
            string amount = damage < .01f ? "<0.01" : damage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            popup.text = kind == CombatDamageKind.Critical ? amount + "!" : kind == CombatDamageKind.Received ? "−" + amount : amount;
            popup.kind = kind;
            popup.age = 0;
            popup.horizontal = ((sequence++ % 7) - 3) / 3f;
            active.Add(popup);
        }

        internal void Tick(float dt, float duration)
        {
            if (dt <= 0) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Popup popup = active[i];
                popup.age += dt;
                if (popup.age < Mathf.Max(.05f, duration)) continue;
                available.Enqueue(popup);
                active.RemoveAt(i);
            }
        }

        internal void Draw(Camera camera, float duration, float height, float scatter, float rise,
            int fontSize, float criticalScale, Color attackColor, Color receivedColor, Color criticalColor,
            float startScale, float peakScale, float endScale, float growSeconds, float holdSeconds, float shrinkSeconds, float scaleSpeed)
        {
            if (camera == null || active.Count == 0 || Event.current.type != EventType.Repaint) return;
            textStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            outlineStyle ??= new GUIStyle(textStyle);
            float resolutionScale = Mathf.Clamp(Screen.height / 1080f, .65f, 1.5f);
            Color previousColor = GUI.color;
            foreach (Popup popup in active)
            {
                float progress = Mathf.Clamp01(popup.age / Mathf.Max(.05f, duration));
                float eased = 1 - Mathf.Pow(1 - progress, 3);
                Vector3 position = popup.position + new Vector3(popup.horizontal * scatter * (.4f + .6f * eased), height + rise * eased);
                Vector3 screen = camera.WorldToScreenPoint(position);
                if (screen.z <= 0 || !camera.pixelRect.Contains(screen)) continue;
                float pop = CombatDamageScaleAnimation.Evaluate(popup.age, duration, startScale, peakScale, endScale,
                    growSeconds, holdSeconds, shrinkSeconds, scaleSpeed);
                float size = fontSize * resolutionScale * pop * (popup.kind == CombatDamageKind.Critical ? criticalScale : 1);
                textStyle.fontSize = outlineStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(size));
                Color color = popup.kind == CombatDamageKind.Critical ? criticalColor : popup.kind == CombatDamageKind.Received ? receivedColor : attackColor;
                float alpha = progress <= .55f ? 1 : 1 - (progress - .55f) / .45f;
                textStyle.normal.textColor = new Color(color.r, color.g, color.b, color.a * alpha);
                outlineStyle.normal.textColor = new Color(.02f, .015f, .025f, color.a * alpha);
                Vector2 labelSize = textStyle.CalcSize(new GUIContent(popup.text));
                Rect rect = new Rect(screen.x - labelSize.x * .5f, Screen.height - screen.y - labelSize.y * .5f, labelSize.x + 4, labelSize.y + 4);
                GUI.color = Color.white;
                float outline = Mathf.Max(1, 1.5f * resolutionScale);
                GUI.Label(new Rect(rect.x - outline, rect.y, rect.width, rect.height), popup.text, outlineStyle);
                GUI.Label(new Rect(rect.x + outline, rect.y, rect.width, rect.height), popup.text, outlineStyle);
                GUI.Label(new Rect(rect.x, rect.y - outline, rect.width, rect.height), popup.text, outlineStyle);
                GUI.Label(new Rect(rect.x, rect.y + outline, rect.width, rect.height), popup.text, outlineStyle);
                GUI.Label(rect, popup.text, textStyle);
            }
            GUI.color = previousColor;
        }

        internal void Clear()
        {
            foreach (Popup popup in active) available.Enqueue(popup);
            active.Clear(); sequence = 0;
        }
    }
}
