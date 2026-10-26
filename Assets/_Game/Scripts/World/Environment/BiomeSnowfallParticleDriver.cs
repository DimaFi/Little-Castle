using UnityEngine;

namespace LittleCastle.World.Environment
{
    /// <summary>
    /// Optional light snowfall presentation. Attach an authored ParticleSystem
    /// (with a URP-compatible snowflake material), rather than spawning physics
    /// particles for every snow-covered object. Moves near a camera/target.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class BiomeSnowfallParticleDriver : MonoBehaviour
    {
        [SerializeField] private BiomeSnowWorld world;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 9f, 0f);
        [Min(0f)] [SerializeField] private float maximumParticlesPerSecond = 100f;
        [Min(0.01f)] [SerializeField] private float rateResponse = 200f;

        private ParticleSystem particles;
        private float currentRate;

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
        }

        private void LateUpdate()
        {
            if (particles == null || world == null) return;
            if (followTarget != null)
                transform.position = followTarget.position + followOffset;

            float winter = world.Themes.Sample(world.WinterThemeId,
                transform.position.x, transform.position.z);
            float targetRate = world.SnowfallIntensity * winter *
                maximumParticlesPerSecond;
            currentRate = Mathf.MoveTowards(currentRate, targetRate,
                rateResponse * Time.deltaTime);

            var emission = particles.emission;
            emission.rateOverTime = currentRate;
            if (currentRate > 0.01f && !particles.isPlaying)
                particles.Play();
            // Don't clear already-fallen visual snowflakes abruptly.
            else if (currentRate <= 0.01f && particles.isPlaying)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnDisable()
        {
            if (particles != null)
            {
                var emission = particles.emission;
                emission.rateOverTime = 0f;
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            currentRate = 0f;
        }
    }
}
