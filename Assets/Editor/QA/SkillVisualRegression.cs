using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    /// <summary>Tests real imported art, anchor alignment, motion, pause, pooling and caps without saved scenes/data.</summary>
    public static class SkillVisualRegression
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++;
        }
        private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .000001f;

        [MenuItem("Project DM/검증/스킬 이펙트 검증")]
        public static void RunMenu() { Verify(); Debug.Log($"Skill visual regression: {checks} assertions PASS."); }

        public static void RunBatch()
        {
            try
            {
                Verify();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/SkillVisualRegression-result.txt", $"PASS: {checks} assertions; imported artwork, anchors, timing, pause, tint, pooling, cap, no combat RNG.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/SkillVisualRegression-result.txt", "FAIL: " + exception);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Verify()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드 종료 후 검증하세요.");
            checks = 0;
            SkillVisualAssets art = Resources.Load<SkillVisualAssets>("ProjectDM/Skills/SkillVisuals");
            Check(art != null, "Resource settings load in player-compatible path");
            foreach (Sprite sprite in new[] { art.chain, art.splash, art.meteor })
            {
                Check(sprite != null, "Assigned approved sprite");
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Check(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single, "Each source is one complete sprite, not a broken atlas slice");
                Check(importer.alphaIsTransparency && !importer.mipmapEnabled && !importer.isReadable, "Transparent, no mips/readback");
                Check(importer.filterMode == FilterMode.Point && sprite.texture.width <= 512 && sprite.texture.height <= 512, "Pixel filtering and bounded texture memory");
            }
            Check(art.impactMaterial != null && art.impactMaterial.shader != null, "Warm impact tint material assigned");
            Check(!ShaderUtil.ShaderHasError(art.impactMaterial.shader), "Impact shader imports without compilation errors");
            // Compile the actual render pass when a graphics device is available.
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                ShaderUtil.CompilePass(art.impactMaterial, 0);
                Check(!ShaderUtil.ShaderHasError(art.impactMaterial.shader), "Impact shader compiles for the current graphics device");
            }
            GameObject root = new GameObject("Isolated skill visual QA");
            SkillVisualAssets limited = Object.Instantiate(art);
            UnityEngine.Random.State random = UnityEngine.Random.state;
            try
            {
                PrototypeSkillVisuals effects = root.AddComponent<PrototypeSkillVisuals>();
                effects.Configure(art.chain, art.splash, art);
                IList active = (IList)RuntimeTestAccess.Get(effects, "active");
                foreach (Vector3 end in new[] { Vector3.right * 3, Vector3.left * 3, Vector3.up * 2, new Vector3(-2, 1) })
                {
                    Vector3 start = new Vector3(.25f, -.5f);
                    effects.Link(start, end);
                    Check(active.Count == 1, "One generated lightning sprite per link");
                    SpriteRenderer line = (SpriteRenderer)RuntimeTestAccess.Get(active[0], "renderer");
                    Check(line.sprite == art.chain, "Lightning uses approved sprite");
                    Check(Near(line.transform.TransformPoint(PrototypeSkillVisuals.Anchor(art.chain, art.chainStart)), start), "Lightning starts at exact enemy position");
                    Check(Near(line.transform.TransformPoint(PrototypeSkillVisuals.Anchor(art.chain, art.chainEnd)), end), "Lightning ends at exact chained target");
                    effects.Tick(art.chainDuration + .01f);
                    Check(active.Count == 0 && !line.gameObject.activeSelf, "Lightning expires and returns to pool");
                }
                effects.Burst(Vector3.one, 1.65f, Color.blue, .3f);
                SpriteRenderer burst = (SpriteRenderer)RuntimeTestAccess.Get(active[0], "renderer");
                Check(burst.sprite == art.splash && burst.color.r == 1 && burst.color.b == 1, "Splash retains approved aqua palette instead of multiplying blue over it");
                Vector3 initialScale = burst.transform.localScale;
                effects.Tick(0);
                Check(burst.transform.localScale == initialScale && (float)RuntimeTestAccess.Get(active[0], "age") == 0, "Paused effects do not animate");
                effects.Tick(art.splashDuration / 2);
                Check(burst.transform.localScale.x > initialScale.x && burst.color.a < art.splashOpacity, "Splash expands and fades");
                effects.Clear();

                Vector3 center = new Vector3(1.2f, -.7f);
                float delay = .65f;
                effects.Meteor(center, 2, delay);
                Check(active.Count == 2, "Meteor has ground warning and falling sprite");
                SpriteRenderer marker = (SpriteRenderer)RuntimeTestAccess.Get(active[0], "renderer");
                SpriteRenderer falling = (SpriteRenderer)RuntimeTestAccess.Get(active[1], "renderer");
                Check(marker.sharedMaterial == art.impactMaterial && marker.sortingOrder < falling.sortingOrder, "Warm warning is beneath the meteor");
                Check(falling.sprite == art.meteor && falling.color == Color.white, "Falling meteor preserves approved fire colors");
                Vector3 AnchorPosition() => falling.transform.TransformPoint(PrototypeSkillVisuals.Anchor(art.meteor, art.meteorAnchor));
                Check(Near(AnchorPosition(), center + Vector3.up * art.meteorFallHeight), "Meteor head starts above exact damage point");
                effects.Tick(delay / 2);
                Check(Near(AnchorPosition(), center + Vector3.up * art.meteorFallHeight * .75f), "Meteor accelerates downward toward impact");
                effects.Tick(delay / 2);
                Check(Near(AnchorPosition(), center) && active.Count == 0, "Meteor head reaches exact damage point at original deadline");
                effects.MeteorImpact(center, 2);
                SpriteRenderer impact = (SpriteRenderer)RuntimeTestAccess.Get(active[0], "renderer");
                Check(impact.sprite == art.splash && impact.sharedMaterial == art.impactMaterial, "Impact reuses approved radial silhouette with warm tint shader");
                Check(impact.color == new Color(art.impactColor.r, art.impactColor.g, art.impactColor.b, art.impactOpacity), "Warm orange impact color and opacity");
                effects.Clear();
                int pooledObjects = root.transform.childCount;
                for (int i = 0; i < 50; i++)
                {
                    effects.Link(Vector3.zero, Vector3.right);
                    SpriteRenderer line = (SpriteRenderer)RuntimeTestAccess.Get(active[0], "renderer");
                    Check(line.sharedMaterial != art.impactMaterial && line.color.r == 1, "Pooled sprites reset tint material and color");
                    effects.Clear();
                }
                Check(root.transform.childCount == pooledObjects, "Repeated effects reuse renderer pool");
                effects.Link(Vector3.zero, Vector3.zero);
                Check(active.Count == 0, "Zero-distance chain does not create invalid transform");
                limited.maxActiveEffects = 1;
                effects.Configure(art.chain, art.splash, limited);
                for (int i = 0; i < 30; i++) effects.Meteor(center, 2, delay);
                Check(active.Count == 1, "Visual cap suppresses extra sprites without controlling damage");
                effects.Clear();
                effects.Configure(art.chain, art.splash);
                effects.Link(Vector3.zero, Vector3.right);
                Check(active.Count == 3, "Legacy fixtures/tables retain default zigzag fallback");
                effects.Clear();
                Check(random.Equals(UnityEngine.Random.state), "All visual operations leave combat RNG untouched");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    RenderPreview(root, effects, art);
            }
            finally
            {
                UnityEngine.Random.state = random;
                Object.DestroyImmediate(limited);
                Object.DestroyImmediate(root);
            }
        }

        private static void RenderPreview(GameObject root, PrototypeSkillVisuals effects, SkillVisualAssets art)
        {
            effects.Configure(art.chain, art.splash, art);
            effects.Meteor(new Vector3(4.8f, -.7f), 2, 1);
            effects.Tick(.75f);
            effects.Burst(Vector3.zero, 1.65f, Color.white, .3f);
            effects.Tick(.12f);
            effects.Link(new Vector3(-5.8f, -1.2f), new Vector3(-3.3f, 1));
            effects.Tick(.025f);
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) renderer.gameObject.layer = 31;
            GameObject cameraObject = new GameObject("Isolated skill render camera");
            RenderTexture target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = Vector3.back * 10;
                camera.orthographic = true; camera.orthographicSize = 3.6f;
                camera.aspect = 16f / 9;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .045f, .07f, 1);
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                Color splash = pixels.GetPixel(480, 270);
                Check(splash.g > splash.r && splash.b > splash.r, "Approved splash actually renders in aqua rather than shader-error magenta");
                effects.Clear();
                effects.MeteorImpact(Vector3.zero, 1.65f);
                effects.Tick(.12f);
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) renderer.gameObject.layer = 31;
                camera.Render();
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                Color warm = pixels.GetPixel(480, 270);
                Check(warm.r > warm.g && warm.g > warm.b, "Tint shader actually renders orange, not aqua or magenta");
                // Restore the three-skill illustration after the separate tint test.
                effects.Clear();
                effects.Meteor(new Vector3(4.8f, -.7f), 2, 1); effects.Tick(.75f);
                effects.Burst(Vector3.zero, 1.65f, Color.white, .3f); effects.Tick(.12f);
                effects.Link(new Vector3(-5.8f, -1.2f), new Vector3(-3.3f, 1)); effects.Tick(.025f);
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) renderer.gameObject.layer = 31;
                camera.Render();
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/SkillVisuals-preview.png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(cameraObject);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
                effects.Clear();
            }
        }
    }
}
