using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Scene boundary only: snapshot the same lattice triangles used by MapTerrainPresentation.</summary>
    public static class MapWaterfallSupportCapture
    {
        public static MapWaterfallSupport Capture(MapEditSession session, Rect area, bool includeSceneRocks = true, long plannedUpper = 0, long plannedLower = 0)
        {
            var support = new MapWaterfallSupport();
            float spacing = session.HeightSpacing; Vector2 origin = session.HeightOrigin;
            int x0 = Mathf.FloorToInt((area.xMin - origin.x) / spacing), x1 = Mathf.CeilToInt((area.xMax - origin.x) / spacing);
            int z0 = Mathf.FloorToInt((area.yMin - origin.y) / spacing), z1 = Mathf.CeilToInt((area.yMax - origin.y) / spacing);
            if ((long)(x1 - x0 + 1) * (z1 - z0 + 1) > 262144) throw new InvalidDataException("Слишком большая область контактной выборки.");
            Vector3 Point(int x, int z)
            {
                float px = origin.x + x * spacing, pz = origin.y + z * spacing;
                return new Vector3(px, session.SampleWaterfallSupportHeight(px, pz, plannedUpper, plannedLower), pz);
            }
            for (int z = z0; z < z1; z++) for (int x = x0; x < x1; x++)
            {
                Vector3 a = Point(x, z), b = Point(x + 1, z), c = Point(x, z + 1), d = Point(x + 1, z + 1);
                string key = "terrain:" + session.MapId + ":" + x + ":" + z;
                support.Add(key, 0, a, c, b); support.Add(key, 1, b, c, d);
            }
            if (!includeSceneRocks) return support;
            var meshes = new List<MeshFilter>();
            foreach (var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !filter.sharedMesh) continue;
                var lod = filter.GetComponentInParent<LODGroup>();
                if (lod)
                {
                    var levels = lod.GetLODs();
                    if (levels.Length == 0 || Array.IndexOf(levels[0].renderers, renderer) < 0) continue;
                }
                else if (!renderer.enabled) continue;
                bool foliage = false;
                foreach (var material in renderer.sharedMaterials)
                    if (material && material.shader && (material.shader.name.IndexOf("Foliage", StringComparison.OrdinalIgnoreCase) >= 0
                        || material.shader.name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0)) foliage = true;
                if (foliage) continue;
                string path = Key(filter.transform);
                if (path.IndexOf("rock", StringComparison.OrdinalIgnoreCase) < 0 && path.IndexOf("cliff", StringComparison.OrdinalIgnoreCase) < 0
                    && path.IndexOf("скал", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var bounds = renderer.bounds;
                if (!area.Overlaps(Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z))) continue;
                meshes.Add(filter);
            }
            meshes.Sort((a, b) => string.CompareOrdinal(Key(a.transform), Key(b.transform)));
            foreach (var filter in meshes)
            {
                var mesh = filter.sharedMesh;
                if (!mesh.isReadable) throw new InvalidDataException("Нет читаемой контактной геометрии скалы «" + filter.name + "».");
                var vertices = mesh.vertices; var indices = mesh.triangles; var transform = filter.transform.localToWorldMatrix;
                string key = Key(filter.transform) + "/" + mesh.name;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = transform.MultiplyPoint3x4(vertices[indices[i]]), b = transform.MultiplyPoint3x4(vertices[indices[i + 1]]),
                        c = transform.MultiplyPoint3x4(vertices[indices[i + 2]]);
                    Vector3 lo = Vector3.Min(a, Vector3.Min(b, c)), hi = Vector3.Max(a, Vector3.Max(b, c));
                    if (!area.Overlaps(Rect.MinMaxRect(lo.x - .001f, lo.z - .001f, hi.x + .001f, hi.z + .001f))) continue;
                    if (transform.determinant < 0) support.Add(key, i / 3, a, c, b); else support.Add(key, i / 3, a, b, c);
                }
            }
            return support;
        }
        static string Key(Transform t)
        {
            var parts = new List<string>();
            while (t) { parts.Add(t.name); t = t.parent; }
            parts.Reverse(); return string.Join("/", parts);
        }
    }
}
