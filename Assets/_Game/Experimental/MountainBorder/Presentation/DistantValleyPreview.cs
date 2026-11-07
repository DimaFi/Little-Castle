using LittleCastle.Research;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>Optional visual surround for comparing atmosphere in a larger valley.</summary>
    [DisallowMultipleComponent]
    public sealed class DistantValleyPreview : MonoBehaviour
    {
        const float OuterExtent = 720f;
        const int GroundSegments = 24;

        GameObject surround;
        Mesh groundMesh;
        Material groundMaterial;
        Material ridgeMaterial;
        MeadowSurfacePainter painter;
        LandscapeBorderPresentation mountainBorder;
        bool connected;

        public void Connect(MeadowSurfacePainter map, LandscapeBorderPresentation border)
        {
            painter = map;
            mountainBorder = border;
            connected = true;
        }

        void Start()
        {
            if (!connected)
            {
                painter = FindAnyObjectByType<MeadowSurfacePainter>();
                mountainBorder = FindAnyObjectByType<LandscapeBorderPresentation>();
            }
            if (!painter || !painter.source) return;
            var profile = mountainBorder ? mountainBorder.profile : null;
            var baseGround = profile && profile.groundMaterial ? profile.groundMaterial : painter.terrainMaterial;
            if (!baseGround) return;

            surround = new GameObject("Distant valley comparison") { hideFlags = HideFlags.DontSave };
            surround.transform.SetParent(transform, false);

            Vector4 bounds = painter.Session?.Bounds ?? painter.source.bounds;
            Vector2 center = new Vector2(bounds.x + bounds.z * .5f, bounds.y + bounds.w * .5f);
            var inner = new Rect(bounds.x, bounds.y, bounds.z, bounds.w);
            groundMesh = BuildGround(inner, center);
            groundMaterial = new Material(baseGround)
                { name = "Distant valley floor", hideFlags = HideFlags.HideAndDontSave };
            if (groundMaterial.HasProperty("_UseSurfacePaint"))
            {
                groundMaterial.SetFloat("_UseSurfacePaint", 0f);
                groundMaterial.DisableKeyword("_LC_SURFACE_PAINT");
            }
            // This surround has no authored roads or altitude-based dirt band.
            groundMaterial.SetFloat("_UseVertexMasks", 0f);
            groundMaterial.SetFloat("_HeightBlendStrength", 0f);
            groundMaterial.SetFloat("_RiverSandStrength", 0f);
            groundMaterial.SetFloat("_ContactShadeStrength", 0f);
            Color meadow = baseGround.HasProperty("_GrassColor") ? baseGround.GetColor("_GrassColor") : new Color(.31f,.4f,.21f);
            groundMaterial.SetColor("_DirtColor", meadow);
            groundMaterial.SetColor("_RockColor", meadow * .87f);
            AddMesh("Continuous valley floor", groundMesh, groundMaterial,
                Vector3.zero, 0, Vector3.one);

            // A scene without a mountain profile can still extend its own ground.
            if (!profile || profile.lodMeshes == null || profile.lodMeshes.Length == 0 || !profile.alpineMaterial)
                return;
            var mountain = profile.lodMeshes[profile.lodMeshes.Length - 1];
            if (!mountain) return;
            ridgeMaterial = new Material(profile.alpineMaterial)
                { name = "Distant ridge palette", hideFlags = HideFlags.HideAndDontSave };
            ridgeMaterial.SetFloat("_PathMaskStrength", 0f);
            ridgeMaterial.SetFloat("_HeightBlendStrength", 0f);
            ridgeMaterial.SetColor("_DirtColor", meadow);

            // Reuse the coarsest existing mesh. Offsets, rotations and scale
            // vary the skyline while leaving the authored north sector intact.
            AddRidge(mountain, center, -290, 340, -30, new Vector3(.95f, .83f, .85f));
            AddRidge(mountain, center, 285, 360, 30, new Vector3(.92f, .94f, .85f));
            AddRidge(mountain, center, -490, -60, -88, new Vector3(1.03f, .75f, .84f));
            AddRidge(mountain, center, 475, -65, 91, new Vector3(1.13f, .82f, .84f));
            AddRidge(mountain, center, 20, -450, 174, new Vector3(1.4f, .89f, .84f));
        }

        void AddRidge(Mesh mesh, Vector2 center,
            float x, float z, float yaw, Vector3 scale)
        {
            AddMesh("Distant ridge", mesh, ridgeMaterial,
                new Vector3(center.x + x, -11f, center.y + z), yaw, scale);
        }

        void AddMesh(string name, Mesh mesh, Material material, Vector3 position, float yaw, Vector3 scale)
        {
            var child = new GameObject(name);
            child.transform.SetParent(surround.transform, false);
            child.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            child.transform.localScale = scale;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static Mesh BuildGround(Rect inner, Vector2 center)
        {
            float[] xs = {center.x - OuterExtent, inner.xMin, inner.xMax, center.x + OuterExtent};
            float[] zs = {center.y - OuterExtent, inner.yMin, inner.yMax, center.y + OuterExtent};
            int segments = GroundSegments * 3;
            int side = segments + 1;
            var vertices = new Vector3[side * side];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[8 * GroundSegments * GroundSegments * 6];
            int index = 0;

            // Use shared vertices across all strips so lighting stays continuous.
            for (int j = 0; j < side; j++)
            for (int i = 0; i < side; i++)
            {
                float x = GroundAxis(xs, i);
                float z = GroundAxis(zs, j);
                float dx = Mathf.Max(Mathf.Max(inner.xMin - x, x - inner.xMax), 0f);
                float dz = Mathf.Max(Mathf.Max(inner.yMin - z, z - inner.yMax), 0f);
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Max(dx, dz) / 90f);
                float hills = (Mathf.PerlinNoise(x * .012f + 7f, z * .012f + 13f) - .32f) * 11f;
                int v = j * side + i;
                vertices[v] = new Vector3(x, -.30f + hills * fade, z);
                uv[v] = new Vector2(x, z) * .1f;

                bool insideMap = i >= GroundSegments && i < GroundSegments * 2 &&
                                 j >= GroundSegments && j < GroundSegments * 2;
                if (i == segments || j == segments || insideMap) continue;
                triangles[index++] = v;
                triangles[index++] = v + side;
                triangles[index++] = v + 1;
                triangles[index++] = v + 1;
                triangles[index++] = v + side;
                triangles[index++] = v + side + 1;
            }

            var mesh = new Mesh { name = "Distant valley floor", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        static float GroundAxis(float[] edges, int index)
        {
            int strip = Mathf.Min(index / GroundSegments, 2);
            float t = (index - strip * GroundSegments) / (float)GroundSegments;
            return Mathf.Lerp(edges[strip], edges[strip + 1], t);
        }

        void OnDestroy()
        {
            if (groundMesh) Destroy(groundMesh);
            if (groundMaterial) Destroy(groundMaterial);
            if (ridgeMaterial) Destroy(ridgeMaterial);
            if (surround) Destroy(surround);
        }
    }
}
