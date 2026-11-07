using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>
    /// Optional distant cloud wisps crossing the actual alpine mesh silhouette.
    /// A single procedural mesh (multiple billboards), one draw call, no cloud
    /// physics or camera-following geometry. Owned by LandscapeBorderPresentation.
    /// </summary>
    public sealed class MountainPeakCloudPresentation : IDisposable
    {
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material material;
        readonly MeshRenderer renderer;
        readonly float minimumDistance;
        public int CardCount { get; private set; }

        public static Vector3[] PickRidgeAnchors(Mesh source, int bands = 11)
        {
            if (!source || source.vertexCount == 0) return Array.Empty<Vector3>();
            bands = Mathf.Clamp(bands, 3, 16);
            Bounds bounds = source.bounds;
            if (bounds.size.y < 14f || bounds.size.x < 8f) return Array.Empty<Vector3>();
            var maxima = new float[bands];
            var peak = new Vector3[bands];
            var found = new bool[bands];
            for (int i = 0; i < bands; i++) maxima[i] = float.NegativeInfinity;
            float inverseWidth = 1f / Mathf.Max(.01f, bounds.size.x);
            foreach (Vector3 p in source.vertices)
            {
                int i = Mathf.Clamp(Mathf.FloorToInt((p.x - bounds.min.x) *
                    inverseWidth * bands), 0, bands - 1);
                if (p.y <= maxima[i]) continue;
                peak[i] = p;
                maxima[i] = p.y;
                found[i] = true;
            }
            var anchors = new List<Vector3>(bands);
            for (int i = 0; i < bands; i++)
            {
                // Don't place clouds on low, nearly flat foothills.
                if (!found[i] ||
                    maxima[i] < bounds.min.y + bounds.size.y * .49f) continue;
                anchors.Add(peak[i]);
            }
            return anchors.ToArray();
        }

        public MountainPeakCloudPresentation(Transform parent, MountainBorderProfile profile)
        {
            if (!parent || !profile || !profile.enablePeakStormClouds ||
                profile.lodMeshes == null || profile.lodMeshes.Length == 0)
                return;

            Shader shader = Resources.Load<Shader>("MapAuthoring/LC_MountainPeakClouds");
            Mesh source = profile.lodMeshes[0];
            if (!shader || !shader.isSupported || !source) return;
            Vector3[] peaks = PickRidgeAnchors(source, profile.peakCloudCount);
            if (peaks.Length == 0) return;

            root = new GameObject("Distant mountain storm wisps (derived)")
            {
                hideFlags = HideFlags.DontSave
            };
            root.transform.SetParent(parent, false);
            mesh = new Mesh
            {
                name = "Distant storm cloud billboards",
                hideFlags = HideFlags.HideAndDontSave
            };

            int rows = Mathf.Clamp(profile.peakCloudBillowRows, 1, 3);
            int count = peaks.Length * rows;
            CardCount = count;
            var vertices = new Vector3[count * 4];
            var uv = new Vector2[count * 4];
            var seeds = new Vector2[count * 4];
            var triangles = new int[count * 6];
            float cloudHeight = Mathf.Clamp(source.bounds.size.y * .24f, 14, 48);
            for (int i = 0; i < count; i++)
            {
                int anchorIndex = i / rows;
                int rowIndex = i % rows;
                float jitter = Mathf.Sin((i + profile.seed) * 1.917f);
                Vector3 center = peaks[anchorIndex];
                float rowOffset = rowIndex - (rows - 1) * .5f;
                float span = Mathf.Clamp(source.bounds.size.x /
                    Mathf.Max(3, profile.peakCloudCount) * 2.1f, 20, 96) *
                    profile.peakCloudWidthMultiplier;
                center.x += rowOffset * span * .40f;
                center.y -= cloudHeight * (.10f + (jitter + 1) * .09f
                    + Mathf.Abs(rowOffset) * .19f);
                center.z += Mathf.Sin(i * 1.43f) * cloudHeight * .12f;
                for (int j = 0; j < 4; j++)
                {
                    vertices[i * 4 + j] = center;
                    seeds[i * 4 + j] = new Vector2((i * .618033989f) % 1f,
                        (i * .414213562f) % 1f);
                }
                uv[i * 4] = Vector2.zero;
                uv[i * 4 + 1] = Vector2.right;
                uv[i * 4 + 2] = Vector2.one;
                uv[i * 4 + 3] = Vector2.up;
                int at = i * 6, v = i * 4;
                triangles[at] = v; triangles[at + 1] = v + 2;
                triangles[at + 2] = v + 1; triangles[at + 3] = v;
                triangles[at + 4] = v + 3; triangles[at + 5] = v + 2;
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.uv2 = seeds;
            mesh.triangles = triangles;
            // GPU expands billboards using camera-facing axes and can shift them
            // toward the viewer: never cull against a zero-size point cloud.
            mesh.bounds = new Bounds(source.bounds.center,
                source.bounds.size + Vector3.one * 200f);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = root.AddComponent<MeshRenderer>();
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat("_CloudWidth", Mathf.Clamp(
                source.bounds.size.x / Mathf.Max(3, profile.peakCloudCount) * 2.1f, 20, 96) *
                profile.peakCloudWidthMultiplier);
            material.SetFloat("_CloudHeight", cloudHeight);
            material.SetFloat("_Opacity", profile.peakCloudOpacity);
            material.SetFloat("_MinimumDistance", profile.peakCloudMinimumDistance);
            material.SetFloat("_FrontOffset", profile.peakCloudFrontOffset);
            minimumDistance = profile.peakCloudMinimumDistance;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
        }

        public void Tick(Camera camera)
        {
            if (!renderer) return;
            // No close-up cloud sheets obstruct the construction/play camera.
            // Shader additionally fades these out before reaching this threshold.
            float distance = camera ? Vector3.Distance(camera.transform.position,
                root.transform.TransformPoint(mesh.bounds.center)) : 0f;
            Material sky = RenderSettings.skybox;
            bool hasCloudVolume = sky && sky.HasProperty("_RainCloudMass") &&
                sky.HasProperty("_StormCloudMass") &&
                Mathf.Max(sky.GetFloat("_RainCloudMass"), sky.GetFloat("_StormCloudMass")) > .15f;
            renderer.enabled = camera && camera.cameraType != CameraType.Reflection &&
                distance > minimumDistance * .64f && !hasCloudVolume &&
                Shader.GetGlobalFloat("_LC_MountainStormCloud") > .035f;
        }

        public void Dispose()
        {
            Destroy(root);
            Destroy(mesh);
            Destroy(material);
        }

        static void Destroy(Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
