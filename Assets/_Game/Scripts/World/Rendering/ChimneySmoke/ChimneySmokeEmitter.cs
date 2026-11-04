using UnityEngine;

namespace LittleCastle.Rendering.ChimneySmoke
{
    /// <summary>Explicit attachment only: place on a smoke anchor at a real chimney mouth.</summary>
    [DisallowMultipleComponent]
    public sealed class ChimneySmokeEmitter : MonoBehaviour
    {
        [SerializeField] private Transform outlet;
        [SerializeField] private ChimneySmokeProfile profile;
        [SerializeField] private int seed = 1;
        [SerializeField] private bool emitting = true;
        [SerializeField, Range(0f, 1f)] private float strength = 1f;

        private GameObject visual;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Vector3[] vertices;
        private Vector2[] uvs;
        private Color[] colors;
        private int[] triangles;
        private MaterialPropertyBlock properties;
        private int activeSegments;
        private float startedAt;
        private bool warnedShader;

        private static readonly int TintId = Shader.PropertyToID("_SmokeTint");
        private static readonly int OpacityId = Shader.PropertyToID("_SmokeOpacity");

        public ChimneySmokeProfile Profile => profile;
        public Transform Outlet => outlet ? outlet : transform;
        public int Seed => seed;
        public bool Emitting { get => emitting; set => emitting = value; }
        public float Strength { get => strength; set => strength = Mathf.Clamp01(value); }

        public void Configure(ChimneySmokeProfile settings, Transform chimneyOutlet, int uniqueSeed)
        {
            profile = settings;
            outlet = chimneyOutlet;
            seed = uniqueSeed;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            startedAt = Time.time;
            ChimneySmokeManager.Register(this);
        }

        private void OnDisable()
        {
            if (Application.isPlaying) ChimneySmokeManager.Unregister(this);
            Release();
        }

        private void OnDestroy() => Release();

        internal void Render(Camera view, float now, Vector4 wind, int qualityLevel, Material sharedMaterial)
        {
            if (!profile || !emitting || strength <= 0f || !sharedMaterial || !view)
            {
                Hide();
                return;
            }

            Vector3 position = Outlet.position;
            float maxDistance = Mathf.Max(5f, profile.maxDistance);
            float distance = Vector3.Distance(position, view.transform.position);
            if (distance > maxDistance)
            {
                Hide();
                return;
            }

            float visibility = Mathf.Clamp01((maxDistance - distance) / Mathf.Max(1f, maxDistance * 0.22f));
            if (visibility <= 0f) { Hide(); return; }

            float bias = Mathf.Clamp01(profile.beautyBias);
            // Only a visual budget hint. Does not change the current project quality setting.
            float quality = qualityLevel <= 0 ? 0.48f : qualityLevel == 1 ? 0.72f : 1f;
            int count = Mathf.Clamp(Mathf.RoundToInt(profile.segments * Mathf.Lerp(0.5f, 1f, bias) * quality), 8, 40);
            if (distance > maxDistance * 0.6f) count = Mathf.Max(8, count / 2);
            EnsureMesh(count, sharedMaterial);
            if (!mesh || !meshRenderer) return;

            // Billboard the soft core toward the camera; a crossed thin strip gives a subtle halo.
            Vector3 towardCamera = view.transform.position - position;
            Vector3 side = Vector3.Cross(Vector3.up, towardCamera);
            if (side.sqrMagnitude < 0.0001f) side = view.transform.right;
            side.Normalize();
            Vector3 cross = Vector3.Cross(Vector3.up, side).normalized;
            float age = Mathf.Max(0f, now - startedAt);
            float entry = Mathf.Clamp01(age * Mathf.Max(0.05f, profile.riseSpeed) / Mathf.Max(0.2f, profile.height));
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                Vector3 center = ChimneySmokeMath.Evaluate(t, now, seed, profile, wind);
                float alpha = ChimneySmokeMath.ShapeAlpha(t) * Mathf.SmoothStep(1f, 0.76f, Mathf.Clamp01(t - entry + 1f));
                // Newly activated emitters reveal the plume from bottom upward.
                alpha *= Mathf.Clamp01((entry - t) * 12f);
                float width = ChimneySmokeMath.Width(t, profile);
                int o = i * 4;
                vertices[o] = center - side * width;
                vertices[o + 1] = center + side * width;
                vertices[o + 2] = center - cross * width * 1.15f;
                vertices[o + 3] = center + cross * width * 1.15f;
                colors[o] = colors[o + 1] = new Color(1, 1, 1, alpha);
                colors[o + 2] = colors[o + 3] = new Color(1, 1, 1, alpha * profile.haloOpacity);
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            // Explicit conservative bounds avoid per-frame RecalculateBounds calls.
            float radius = Mathf.Max(2f, profile.height * 3f * profile.windInfluence + profile.topWidth * 2f);
            mesh.bounds = new Bounds(new Vector3(0f, profile.height * 0.5f, 0f),
                new Vector3(radius * 2f, profile.height + 1f, radius * 2f));

            visual.transform.SetPositionAndRotation(position, Quaternion.identity);
            properties ??= new MaterialPropertyBlock();
            Color tint = profile.color;
            float daylight = Mathf.Clamp01(Shader.GetGlobalFloat("_LC_Daylight"));
            float twilight = Mathf.Clamp01(Shader.GetGlobalFloat("_LC_Twilight"));
            tint = Color.Lerp(tint, new Color(1f, 0.86f, 0.72f, tint.a), twilight * profile.duskWarmth);
            properties.SetColor(TintId, tint);
            properties.SetFloat(OpacityId, profile.opacity * strength * visibility * (1f - (1f - daylight) * profile.nightDim));
            meshRenderer.SetPropertyBlock(properties);
            meshRenderer.enabled = true;
        }

        private void EnsureMesh(int segments, Material material)
        {
            if (mesh && segments == activeSegments && meshRenderer && meshRenderer.sharedMaterial == material) return;
            Release();
            activeSegments = segments;
            int count = (segments + 1) * 4;
            vertices = new Vector3[count];
            colors = new Color[count];
            uvs = new Vector2[count];
            triangles = new int[segments * 12];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                int o = i * 4;
                uvs[o] = uvs[o + 2] = new Vector2(0, t);
                uvs[o + 1] = uvs[o + 3] = new Vector2(1, t);
            }
            for (int i = 0; i < segments; i++)
            {
                for (int strip = 0; strip < 2; strip++)
                {
                    int a = i * 4 + strip * 2;
                    int b = a + 4;
                    int o = i * 12 + strip * 6;
                    triangles[o] = a; triangles[o + 1] = b; triangles[o + 2] = a + 1;
                    triangles[o + 3] = a + 1; triangles[o + 4] = b; triangles[o + 5] = b + 1;
                }
            }
            mesh = new Mesh { name = "LC chimney smoke (runtime)", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.up * 2f, Vector3.one * 20f);
            visual = new GameObject("Chimney Smoke (visual only)") { hideFlags = HideFlags.DontSave };
            visual.transform.SetParent(transform, true);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        internal void Hide() { if (meshRenderer) meshRenderer.enabled = false; }

        private void Release()
        {
            if (visual)
            {
                if (Application.isPlaying) Destroy(visual);
                else DestroyImmediate(visual);
            }
            if (mesh)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            visual = null; mesh = null; meshRenderer = null;
            vertices = null; colors = null; uvs = null; triangles = null; activeSegments = 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.95f, 0.92f, 0.85f);
            Gizmos.DrawWireSphere(Outlet.position, 0.14f);
            if (profile) Gizmos.DrawLine(Outlet.position, Outlet.position + Vector3.up * profile.height);
        }
#endif
    }
}
