using UnityEngine;

namespace ProjectDM
{
    /// <summary>Quiet local-space motes; native scaled-time simulation and clean pool reuse.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ParticleSystem))]
    public sealed class PickupAmbientVfx : MonoBehaviour
    {
        [InspectorName("입자 머티리얼")] public Material particleMaterial;
        [InspectorName("리소스에 맞춘 입자 색상")] public Color tint = new(.35f, .6f, .8f, 1);
        [InspectorName("입자 최대 불투명도"), Range(0, .6f)] public float opacity = .28f;
        [InspectorName("초당 입자 수"), Range(0, 5)] public float emissionRate = 2f;
        [InspectorName("상승 속도 (이미지 높이 배율)"), Range(.1f, 1.5f)] public float riseSpeed = .6f;
        [InspectorName("입자 수명 (초)"), Range(.5f, 3)] public float lifetime = 1.8f;
        [InspectorName("최대 동시 입자 수"), Range(1, 10)] public int maximumParticles = 6;

        private ParticleSystem motes;
        private bool configured;

        private void OnValidate() { configured = false; }

        private void OnEnable()
        {
            motes = GetComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!configured) Configure();
            if (Application.isPlaying && particleMaterial != null) motes.Play();
        }

        private void OnDisable()
        {
            if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>Also used by the prefab installer and isolated visual QA.</summary>
        public void Configure()
        {
            motes = GetComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SpriteRenderer body = GetComponentInParent<SpriteRenderer>();
            Bounds bounds = body != null && body.sprite != null ? body.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
            float height = Mathf.Max(.1f, bounds.size.y);
            transform.localPosition = new Vector3(bounds.center.x, bounds.min.y - height * .05f, 0);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            var main = motes.main;
            main.loop = true;
            main.duration = 2;
            main.prewarm = false;
            main.playOnAwake = false;
            main.useUnscaledTime = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Clamp(maximumParticles, 1, 10);
            main.startDelay = .18f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .8f, lifetime * 1.1f);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(height * .055f, height * .09f);
            main.startColor = new Color(tint.r, tint.g, tint.b, 1);
            main.gravityModifier = 0;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            main.stopAction = ParticleSystemStopAction.None;
            // Independent native particle RNG; never consumes the gameplay Random stream.
            motes.useAutoRandomSeed = false;
            motes.randomSeed = Application.isPlaying
                ? unchecked((uint)GetInstanceID() * 747796405u + 2891336453u) | 1u
                : 0xD15C0DEu;

            var emission = motes.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Clamp(emissionRate, 0, 5);
            emission.rateOverDistance = 0;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
            var shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(.08f, bounds.size.x * .7f), .015f, .01f);
            var velocity = motes.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-height * .035f, height * .035f);
            velocity.y = new ParticleSystem.MinMaxCurve(height * riseSpeed * .85f, height * riseSpeed * 1.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(0, 0);

            var colors = motes.colorOverLifetime;
            colors.enabled = true;
            Gradient fade = new();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(opacity, .2f),
                    new GradientAlphaKey(opacity * .65f, .65f), new GradientAlphaKey(0, 1) });
            colors.color = fade;
            var sizes = motes.sizeOverLifetime;
            sizes.enabled = true;
            sizes.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                new Keyframe(0, .55f), new Keyframe(.25f, 1), new Keyframe(1, .4f)));

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = particleMaterial;
            renderer.sortingLayerID = body != null ? body.sortingLayerID : 0;
            renderer.sortingOrder = 1;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            configured = true;
        }
    }
}
