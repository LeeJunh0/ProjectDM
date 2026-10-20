using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>World-anchored ripples. Explicit scaled ticks share the existing pause/reset path.</summary>
    internal sealed class BossDeathRippleVisuals : MonoBehaviour
    {
        private sealed class Wave { public MeshRenderer renderer; public float age, duration; }
        private readonly List<Wave> active = new();
        private readonly Queue<MeshRenderer> pool = new();
        private MaterialPropertyBlock properties;
        private Mesh quad;
        private BossDeathRippleSettings settings;
        internal int ActiveCount => active.Count;

        internal void Emit(Vector3 position, bool boss)
        {
            settings ??= Resources.Load<BossDeathRippleSettings>("ProjectDM/Effects/BossDeathRipple");
            if (settings == null || settings.material == null || active.Count >= settings.maximumActive) return;
            MeshRenderer renderer;
            if (pool.Count > 0) renderer = pool.Dequeue();
            else
            {
                if (quad == null)
                {
                    quad = new Mesh { name = "공간 파동 사각형" };
                    quad.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(.5f,-.5f), new Vector3(.5f,.5f), new Vector3(-.5f,.5f) };
                    quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                    quad.triangles = new[] { 0, 1, 2, 0, 2, 3 }; quad.RecalculateBounds();
                }
                GameObject item = new("보스 처치 공간 파동"); item.transform.SetParent(transform, false);
                item.AddComponent<MeshFilter>().sharedMesh = quad;
                renderer = item.AddComponent<MeshRenderer>(); renderer.sortingOrder = 7;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            renderer.sharedMaterial = settings.material;
            renderer.transform.position = position; renderer.transform.rotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one * (2 * (boss ? settings.bossRadius : settings.miniBossRadius));
            renderer.gameObject.SetActive(true);
            Wave wave = new() { renderer = renderer, duration = Mathf.Max(.1f, settings.duration) };
            active.Add(wave); Present(wave);
        }

        private void Present(Wave wave)
        {
            properties ??= new MaterialPropertyBlock();
            properties.Clear(); properties.SetFloat("_Progress", Mathf.Clamp01(wave.age / wave.duration));
            properties.SetColor("_Tint", settings.tint); properties.SetFloat("_Distortion", settings.distortion);
            wave.renderer.SetPropertyBlock(properties);
        }

        internal void Tick(float dt)
        {
            if (dt <= 0) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Wave wave = active[i]; wave.age += dt;
                if (wave.age < wave.duration) { Present(wave); continue; }
                wave.renderer.gameObject.SetActive(false); pool.Enqueue(wave.renderer); active.RemoveAt(i);
            }
        }

        internal void Clear()
        {
            foreach (Wave wave in active) { wave.renderer.gameObject.SetActive(false); pool.Enqueue(wave.renderer); }
            active.Clear();
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (quad == null) return;
            if (Application.isPlaying) Destroy(quad); else DestroyImmediate(quad);
        }
    }
}
