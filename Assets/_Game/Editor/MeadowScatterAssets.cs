using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LittleCastle.Rendering;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>Isolated, collider-free mesh/material adapters; original starter prefabs stay intact.</summary>
    public static class MeadowScatterAssets
    {
        const string Root = "Assets/_Game/Art/Studies/Meadow_20261025";
        const string MeshRoot = Root + "/Scatter";
        const string Study = "Assets/_Game/Art/Studies/InnerverseStarter_20261023";
        const string Package = "Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment";
        public const string LibraryPath = Root + "/Meadow_ScatterLibrary.asset";

        public static MeadowScatterLibrary Build()
        {
            Directory.CreateDirectory(MeshRoot);
            AssetDatabase.Refresh();
            var materials = new Dictionary<string, Material>();
            var palette = CopyMaterial("UNS_Color_Palette", materials);
            if (!palette) throw new InvalidDataException("Missing adapted starter palette material.");
            var library = ScriptableObject.CreateInstance<MeadowScatterLibrary>();
            var report = new StringBuilder("Meadow scatter preflight: exact existing starter assets, static presentation.\n");
            var prototypes = new List<MeadowScatterLibrary.Prototype>();
            void Add(string name, string path, Vector2 scale, float weight, float inset)
            {
                var prototype = Bake(name, Package + path, materials, report);
                prototype.scaleRange = scale; prototype.weight = weight; prototype.groundInset = inset;
                prototype.category=name.Contains("Rock")?MeadowScatterCategory.Stone:name.Contains("Branch")||name.Contains("Mushroom")?MeadowScatterCategory.ForestDebris:MeadowScatterCategory.Flower;
                prototypes.Add(prototype);
            }
            Add("UNS_Tiny_Rock_01", "/Rocks/River/Prefabs/UNS_Tiny_Rock_01.prefab", new Vector2(.7f, 1.15f), .08f, .035f);
            Add("UNS_Tiny_Rock_02", "/Rocks/River/Prefabs/UNS_Tiny_Rock_02.prefab", new Vector2(.6f, .95f), .06f, .04f);
            Add("UNS_Branch", "/Props/Branches/Prefabs/UNS_Branch.prefab", new Vector2(.35f, .60f), .07f, .008f);
            Add("UNS_Mushroom_Patch", "/Vegetation/Mushrooms/Prefabs/UNS_Mushroom_Patch.prefab", new Vector2(.5f, .85f), .14f, .015f);
            Add("UNS_Flower", "/Vegetation/Flowers/Prefabs/UNS_Flower.prefab", new Vector2(.42f, .68f), .15f, .012f);
            prototypes.AddRange(MeadowFlowerKitAssets.Build());
            report.AppendLine("Oak Kit FlowerCluster A/B adapters included:3 authored LODs, fourth slot shares last; see flowerkit_preflight.txt for exact axes/units/palette.");
            library.prototypes = prototypes.ToArray();
            report.AppendLine("COMPATIBLE: static LC solid palette; alpha flower/leaf retain LC Foliage textures and UV0.");
            report.AppendLine("UNS_Flower UVY/height correlation=-0.166 in source audit: not accepted for LC Grass UVY bending. All flower wind amplitudes are zero in this first scatter pass.");
            report.AppendLine("Four authored mesh LODs retained per prototype. Source local transforms baked once, X/Z centered and Y base normalized; ground inset is presentation-only.");
            report.AppendLine("No source prefab/importer/material changed. No decorative collider, Light, Animator or per-prop GameObject is included in the runtime library.");
            report.AppendLine("Runtime: fixed world-cell candidates, paint/erase/road exclusion, max1500 instances,8m frustum patches,45m default cull,instanced type/material/LOD batches. Manual in-game visual validation remains required.");
            library.preflightNotes = report.ToString();
            library = Save(library, LibraryPath);
            string output = MeadowGrassPreflight.Output;
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "scatter_preflight.txt"), report.ToString());
            AssetDatabase.SaveAssets();
            return library;
        }

        static MeadowScatterLibrary.Prototype Bake(string name, string sourcePath, Dictionary<string, Material> materials, StringBuilder report)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source) throw new FileNotFoundException("Missing meadow scatter source", sourcePath);
            var instance = UnityEngine.Object.Instantiate(source);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.position = Vector3.zero;
            Selection.activeGameObject=instance;
            FoliageModelCompatibilityValidator.ValidateSelected();
            try
            {
                var group = instance.GetComponentInChildren<LODGroup>(true);
                if (!group || group.GetLODs().Length != 4) throw new InvalidDataException(name + " must retain four authored LODs.");
                var lods = group.GetLODs();
                var slots = new List<Material>();
                foreach (var lod in lods) foreach (var renderer in lod.renderers) if (renderer)
                    foreach (var material in renderer.sharedMaterials)
                    {
                        var adapted = CopyMaterial(material.name, materials);
                        if (!slots.Contains(adapted)) slots.Add(adapted);
                    }
                var meshes = new Mesh[4];
                Bounds union = default;
                for (int lod = 0; lod < 4; lod++)
                {
                    meshes[lod] = Combine(name + "_LOD" + lod, lods[lod].renderers, slots, materials);
                    if (lod == 0) union = meshes[lod].bounds; else union.Encapsulate(meshes[lod].bounds);
                }
                Vector3 pivot = new Vector3(union.center.x, union.min.y, union.center.z);
                for (int lod = 0; lod < meshes.Length; lod++)
                {
                    var vertices = meshes[lod].vertices;
                    for (int i = 0; i < vertices.Length; i++) vertices[i] -= pivot;
                    meshes[lod].vertices = vertices;
                    meshes[lod].RecalculateBounds();
                    meshes[lod] = Save(meshes[lod], MeshRoot + "/" + name + "_LOD" + lod + ".asset");
                }
                union.center -= pivot;
                int[] tris = meshes.Select(m => Enumerable.Range(0, m.subMeshCount).Sum(s => (int)m.GetIndexCount(s) / 3)).ToArray();
                report.AppendLine(name + ": source=" + sourcePath + "; LOD triangles=" + string.Join("/", tris) +
                    "; materials=" + string.Join(",", slots.Select(m => m.name)) + "; normalized bounds=" + union + "; original colliders omitted.");
                return new MeadowScatterLibrary.Prototype { stableId = name, sourceAssetPath = sourcePath,
                    lodMeshes = meshes, materials = slots.ToArray(), localBounds = union };
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static Mesh Combine(string name, Renderer[] renderers, List<Material> slots, Dictionary<string, Material> materials)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
            var tangents = new List<Vector4>();
            var indices = slots.Select(_ => new List<int>()).ToArray();
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) throw new InvalidDataException("Scatter source requires static MeshFilter: " + renderer.name);
                var source = filter.sharedMesh;
                if (!source.HasVertexAttribute(VertexAttribute.Normal) || !source.HasVertexAttribute(VertexAttribute.TexCoord0))
                    throw new InvalidDataException("Scatter source missing normals/UV0: " + source.name);
                var transform = renderer.localToWorldMatrix;
                var normalTransform = transform.inverse.transpose;
                int first = vertices.Count;
                using (var data = Mesh.AcquireReadOnlyMeshData(source))
                using (var positions = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp))
                using (var sourceNormals = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp))
                using (var sourceUV = new NativeArray<Vector2>(source.vertexCount, Allocator.Temp))
                using (var sourceTangents = new NativeArray<Vector4>(source.vertexCount, Allocator.Temp))
                {
                    data[0].GetVertices(positions); data[0].GetNormals(sourceNormals); data[0].GetUVs(0, sourceUV);
                    bool hasTangents = source.HasVertexAttribute(VertexAttribute.Tangent);
                    if (hasTangents) data[0].GetTangents(sourceTangents);
                    for (int i = 0; i < source.vertexCount; i++)
                    {
                        vertices.Add(transform.MultiplyPoint3x4(positions[i]));
                        Vector3 normal = normalTransform.MultiplyVector(sourceNormals[i]).normalized;
                        normals.Add(normal);
                        uv.Add(sourceUV[i]);
                        Vector3 tangent = hasTangents ? transform.MultiplyVector(sourceTangents[i]) : Vector3.Cross(normal, Vector3.forward);
                        tangent -= normal * Vector3.Dot(normal, tangent);
                        if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.Cross(normal, Vector3.up);
                        tangent.Normalize();
                        tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z,
                            (hasTangents ? sourceTangents[i].w : 1) * (transform.determinant < 0 ? -1 : 1)));
                    }
                    var sourceMaterials = renderer.sharedMaterials;
                    bool reverse = transform.determinant < 0;
                    for (int sub = 0; sub < source.subMeshCount; sub++)
                    {
                        if (sub >= sourceMaterials.Length || !sourceMaterials[sub]) throw new InvalidDataException("Missing scatter material slot.");
                        int slot = slots.IndexOf(CopyMaterial(sourceMaterials[sub].name, materials));
                        var range = data[0].GetSubMesh(sub);
                        if (range.topology != MeshTopology.Triangles) throw new InvalidDataException("Scatter source must be triangulated.");
                        int Index(int index) => first + range.baseVertex + (source.indexFormat == IndexFormat.UInt16
                            ? data[0].GetIndexData<ushort>()[range.indexStart + index]
                            : (int)data[0].GetIndexData<uint>()[range.indexStart + index]);
                        for (int index = 0; index < range.indexCount; index += 3)
                        {
                            indices[slot].Add(Index(index));
                            indices[slot].Add(Index(index + (reverse ? 2 : 1)));
                            indices[slot].Add(Index(index + (reverse ? 1 : 2)));
                        }
                    }
                }
            }
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0, uv); mesh.subMeshCount = slots.Count;
            for (int sub = 0; sub < slots.Count; sub++) mesh.SetTriangles(indices[sub], sub, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material CopyMaterial(string name, Dictionary<string, Material> materials)
        {
            name = name.Replace(" (Instance)", "");
            if (materials.TryGetValue(name, out var existing)) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Material>(Study + "/Study_" + name + ".mat");
            if (!source) throw new InvalidDataException("Missing pre-adapted starter material: " + name);
            var copy = new Material(source) { enableInstancing = true };
            // This library is static first: no unsuitable UV-based wind on mushrooms or branches.
            foreach (string property in new[] { "_WindAmplitude", "_CrownSway", "_VertexWave", "_LeafFlutter" })
                if (copy.HasProperty(property)) copy.SetFloat(property, 0);
            if (copy.HasProperty("_LightResponse")) copy.SetFloat("_LightResponse", 1);
            copy = Save(copy, MeshRoot + "/Scatter_" + name + ".mat");
            materials.Add(name, copy);
            return copy;
        }

        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            value.name = Path.GetFileNameWithoutExtension(path);
            var old = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!old) { AssetDatabase.CreateAsset(value, path); return value; }
            if (old is Mesh target && value is Mesh source)
            {
                target.Clear(); target.indexFormat = source.indexFormat; target.vertices = source.vertices;
                target.normals = source.normals; target.tangents = source.tangents; target.uv = source.uv; target.subMeshCount = source.subMeshCount;
                for (int sub = 0; sub < source.subMeshCount; sub++) target.SetTriangles(source.GetTriangles(sub), sub, false);
                target.bounds = source.bounds;
            }
            else EditorUtility.CopySerialized(value, old);
            EditorUtility.SetDirty(old); AssetDatabase.SaveAssetIfDirty(old); UnityEngine.Object.DestroyImmediate(value);
            return old;
        }
    }
}
