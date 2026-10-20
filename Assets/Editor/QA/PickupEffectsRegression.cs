using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ProjectDM.Editor
{
    public static class PickupEffectsRegression
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++;
        }

        [MenuItem("Project DM/검증/드랍 상승 입자 검증")]
        public static void RunMenu()
        {
            checks = 0;
            var random = Random.state;
            GameObject root = new("Isolated pickup effects QA");
            List<GameObject> instances = new();
            var pool = root.AddComponent<GameObjectPool>();
            try
            {
                string[] names = new string[12];
                for (int i = 0; i < 5; i++) { names[i] = $"ExperiencePickup_{i + 1:00}"; names[i + 5] = $"CurrencyPickup_{i + 1:00}"; }
                names[10] = "ManaPickup"; names[11] = "ChestPickup";
                foreach (string name in names)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjectDMPrefabFactory.PrefabFolder}/{name}.prefab");
                    Check(prefab != null && prefab.GetComponentInChildren<PickupAmbientVfx>() != null, "Ambient effect installed: " + name);
                    GameObject instance = pool.Rent(prefab); instances.Add(instance);
                    PickupAmbientVfx effect = instance.GetComponentInChildren<PickupAmbientVfx>();
                    ParticleSystem particles = effect.GetComponent<ParticleSystem>();
                    Check(effect.opacity <= .3f && particles.main.maxParticles <= 6 && effect.emissionRate <= 2,
                        "Subtle opacity and bounded per-item particle budget: " + name);
                    Check(!particles.main.useUnscaledTime && particles.main.simulationSpace == ParticleSystemSimulationSpace.Local &&
                        particles.main.scalingMode == ParticleSystemScalingMode.Hierarchy, "Pause and movement follow the pickup: " + name);
                    float spriteBottom = instance.GetComponentInChildren<SpriteRenderer>().sprite.bounds.min.y;
                    Check(effect.transform.localPosition.y < spriteBottom && particles.velocityOverLifetime.y.constantMin > 0,
                        "Motes originate below and rise upward: " + name);
                    Check(effect.particleMaterial != null && !ShaderUtil.ShaderHasError(effect.particleMaterial.shader), "Real mote material/shader loads: " + name);
                    Color actualTint = particles.main.startColor.color;
                    Check(Mathf.Abs(actualTint.r - effect.tint.r) < .005f && Mathf.Abs(actualTint.g - effect.tint.g) < .005f &&
                        Mathf.Abs(actualTint.b - effect.tint.b) < .005f, "Native particles use this resource's configured color: " + name);
                    particles.Simulate(1.2f, true, true, false);
                    Check(particles.particleCount > 0 && particles.particleCount <= 6, "Native emission is visible and capped: " + name);
                    var values = new ParticleSystem.Particle[10];
                    int count = particles.GetParticles(values);
                    Dictionary<uint, Vector3> before = new();
                    for (int i = 0; i < count; i++) before[values[i].randomSeed] = values[i].position;
                    var emission = particles.emission; emission.enabled = false;
                    particles.Simulate(.1f, true, false, false);
                    count = particles.GetParticles(values);
                    int matched = 0;
                    for (int i = 0; i < count; i++) if (before.TryGetValue(values[i].randomSeed, out Vector3 origin))
                    { Check(values[i].position.y > origin.y, "Actual particle moves from bottom to top: " + name); matched++; }
                    Check(matched > 0, "Observed a surviving rising particle: " + name);
                    particles.Simulate(0, true, false, false);
                    Check(particles.particleCount == count, "Zero delta does not advance the effect: " + name);
                    pool.Return(instance);
                    Check(particles.particleCount == 0, "Pool return clears every mote: " + name);
                    GameObject reused = pool.Rent(prefab);
                    Check(reused == instance && particles.particleCount == 0 && instance.GetComponentsInChildren<PickupAmbientVfx>().Length == 1,
                        "Pool reuse starts clean with one effect component: " + name);
                    // QA deliberately disabled emission; a normal reused instance keeps its configured emission.
                    effect.Configure(); particles.Simulate(1.2f, true, true, false);
                    Check(particles.particleCount > 0, "Effect emits again after reset: " + name);
                    instance.transform.position = new Vector3((instances.Count - 1) % 6 * 2.2f - 5.5f,
                        (instances.Count - 1) / 6 * -2.5f + 1.2f, 0);
                    instance.transform.localScale = Vector3.one * .8f;
                }
                Check(random.Equals(Random.state), "VFX creation, simulation and reuse do not consume combat RNG");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) Render(instances);
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/PickupEffectsRegression-result.txt", $"PASS: {checks} assertions; all 12 pickup prefabs, matching palette, rising motion, quiet budget, scaled time, clear/reuse, shared shader and unchanged combat RNG.\n");
                Debug.Log($"Pickup effects: {checks} assertions PASS.");
            }
            finally
            {
                foreach (GameObject instance in instances) if (instance != null) Object.DestroyImmediate(instance);
                Object.DestroyImmediate(root); Random.state = random;
            }
        }

        private static void Render(List<GameObject> instances)
        {
            foreach (GameObject instance in instances)
                foreach (Transform child in instance.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
            GameObject cameraObject = new("Pickup effects preview camera");
            var target = new RenderTexture(1400, 650, 24);
            var pixels = new Texture2D(1400, 650, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.position = Vector3.back * 10; camera.orthographic = true;
                camera.orthographicSize = 3.1f; camera.aspect = 1400f / 650;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .115f, .13f);
                camera.cullingMask = 1 << 31; camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1400, 650), 0, 0); pixels.Apply();
                File.WriteAllBytes("Logs/PickupEffects-preview.png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(cameraObject);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
