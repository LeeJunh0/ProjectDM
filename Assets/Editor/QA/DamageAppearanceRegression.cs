using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    public static class DamageAppearanceRegression
    {
        [MenuItem("Project DM/검증/데미지 숫자 외형 확인")]
        public static void RunMenu()
        {
            var appearance = Resources.Load<CombatDamageAppearance>("ProjectDM/UI/CombatDamageAppearance");
            if (appearance == null || appearance.font == null) throw new InvalidOperationException("Pixel damage appearance/font not loadable");
            foreach (char digit in "0123456789.!<−")
                if (!appearance.font.HasCharacter(digit)) throw new InvalidOperationException("Missing damage glyph: " + digit);
            if (appearance.outlineWidth > 1 || appearance.outlineColor.a > .85f) throw new InvalidOperationException("Damage outline is too heavy");
            CheckElementColors(appearance);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) Render(appearance);
            Debug.Log("Damage appearance PASS: dedicated font, all damage glyphs, elemental normal/critical colors, received override, pool reset, subdued outline and native preview.");
        }

        private static void CheckElementColors(CombatDamageAppearance appearance)
        {
            GameSettings settings = new();
            Color Resolve(CombatDamageElement element, CombatDamageKind kind) => appearance.ResolveColor(element, kind,
                settings.combatAttackColor, settings.combatReceivedColor, settings.combatCriticalColor);
            if (Resolve(CombatDamageElement.None, CombatDamageKind.Attack) != settings.combatAttackColor ||
                Resolve(CombatDamageElement.None, CombatDamageKind.Critical) != settings.combatCriticalColor)
                throw new InvalidOperationException("Missile default colors changed");
            foreach (CombatDamageElement element in new[] { CombatDamageElement.Flame, CombatDamageElement.Lightning, CombatDamageElement.Frost })
            {
                Color normal = Resolve(element, CombatDamageKind.Attack), critical = Resolve(element, CombatDamageKind.Critical);
                if (normal == critical || critical.r + critical.g + critical.b <= normal.r + normal.g + normal.b)
                    throw new InvalidOperationException("Element critical must retain a distinct, brighter palette: " + element);
                if (Resolve(element, CombatDamageKind.Received) != settings.combatReceivedColor)
                    throw new InvalidOperationException("Received damage inherited an attack element");
            }
            var display = new CombatDamageDisplay();
            display.ShowElemental(Vector3.zero, 12, CombatDamageKind.Critical, 4, CombatDamageElement.Flame);
            var popups = (System.Collections.IList)RuntimeTestAccess.Get(display, "active");
            if ((CombatDamageElement)RuntimeTestAccess.Get(popups[0], "element") != CombatDamageElement.Flame)
                throw new InvalidOperationException("Popup did not capture its hit element");
            display.Tick(1, .7f); display.Show(Vector3.zero, 3, CombatDamageKind.Attack, 4);
            if ((CombatDamageElement)RuntimeTestAccess.Get(popups[0], "element") != CombatDamageElement.None)
                throw new InvalidOperationException("Reused missile popup retained an old element");
            display.Clear();
        }

        private static void Render(CombatDamageAppearance appearance)
        {
            GameObject root = new("Isolated damage type study");
            var target = new RenderTexture(1200, 640, 24); var pixels = new Texture2D(1200, 640, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = new GameObject("Type study camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 3.2f; camera.aspect = 1.875f;
                camera.transform.position = Vector3.back * 10; camera.backgroundColor = new Color(.11f,.14f,.13f);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.cullingMask = 1 << 31; camera.targetTexture = target;
                void Text(string value, Vector3 position, Color color, float size, int order)
                {
                    GameObject item = new("Typeface sample"); item.transform.SetParent(root.transform); item.transform.position = position; item.layer = 31;
                    var mesh = item.AddComponent<TextMesh>(); mesh.font = appearance.font; mesh.fontSize = 64;
                    mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.characterSize = size * .1f;
                    mesh.text = value; mesh.color = color;
                    var renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = appearance.font.material; renderer.sortingOrder = order;
                }
                void Outlined(string value, Vector3 position, Color color, float size)
                {
                    float edge = .01f * appearance.outlineWidth;
                    foreach (Vector3 delta in new[] { Vector3.left, Vector3.right, Vector3.up, Vector3.down })
                        Text(value, position + delta * edge, appearance.outlineColor, size, 0);
                    Text(value, position, color, size, 1);
                }
                GameSettings settings = new();
                Text("ELEMENT DAMAGE / NORMAL + CRITICAL", new Vector3(0,2.4f,0), new Color(.64f,.67f,.61f), .35f, 2);
                string[] labels = { "MISSILE", "FLAME", "LIGHTNING", "FROST" };
                CombatDamageElement[] elements = { CombatDamageElement.None, CombatDamageElement.Flame, CombatDamageElement.Lightning, CombatDamageElement.Frost };
                for (int column = 0; column < 4; column++)
                {
                    float x = (column - 1.5f) * 2.7f;
                    Text(labels[column], new Vector3(x,1.35f,0), new Color(.64f,.67f,.61f), .3f, 2);
                    Color normal = appearance.ResolveColor(elements[column], CombatDamageKind.Attack, settings.combatAttackColor, settings.combatReceivedColor, settings.combatCriticalColor);
                    Color critical = appearance.ResolveColor(elements[column], CombatDamageKind.Critical, settings.combatAttackColor, settings.combatReceivedColor, settings.combatCriticalColor);
                    Outlined("128", new Vector3(x,.35f,0), normal, .8f);
                    Outlined("256!", new Vector3(x,-.8f,0), critical, 1.12f);
                }
                Text("NORMAL ABOVE / CRITICAL BELOW", new Vector3(0,-2.2f,0), new Color(.53f,.57f,.52f), .3f, 2);
                appearance.font.RequestCharactersInTexture("0123456789.!<−NORMALCRITICALRECEIVEDPIXELDAMAGTYPSTUDYWF/", 64, FontStyle.Normal);
                appearance.font.material.mainTexture.filterMode = FilterMode.Point;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.bounds.min.x < -6 || renderer.bounds.max.x > 6 || renderer.bounds.min.y < -3.2f || renderer.bounds.max.y > 3.2f)
                        throw new InvalidOperationException("Typeface preview label escapes its camera framing");
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,1200,640),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/DamageAppearance-preview.png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(root); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
