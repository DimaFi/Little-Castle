using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// <summary>Native mesh adapters for the existing small Oak Kit flowers; originals are untouched.</summary>
    public static class MeadowFlowerKitAssets
    {
        const string Root = "Assets/_Game/Art/Studies/Meadow_20261025/FlowerKit";
        const string Ready = "Assets/_Game/Models/Ready/Test_Vegetation";
        const string Materials = "Assets/_Game/Art/Imported/TerrainStarter_v001/Study/Materials";

        public static MeadowScatterLibrary.Prototype[] Build()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(MeadowStudyBuilder.Output);
            AssetDatabase.Refresh();
            var report = new StringBuilder("Oak Kit flower adapter preflight. Existing Ready A/B sources; static first.\n");
            var previous = Selection.activeObject;
            Application.LogCallback collect = (message, stack, type) =>
            { if (message.Contains("[Little Castle Foliage]")) report.AppendLine(type + ": " + message); };
            Application.logMessageReceived += collect;
            try
            {
                var result = new[] { Bake('A', .34f, .45f, report), Bake('B', .38f, .30f, report) };
                report.AppendLine("Classification: COMPATIBLE as static LC Stylized Lit palette geometry. Not accepted for UVY wind; no grass/leaf wind is enabled.");
                report.AppendLine("Three authored LODs preserved; fourth library slot shares the authored last mesh. Source normals/UV0 and available tangents are retained; no runtime colliders or per-flower GameObjects.");
                report.AppendLine("Source prefabs, FBXs, import settings and source palette materials unchanged. Actual Player close/far visual approval remains required.");
                AssetDatabase.SaveAssets();
                return result;
            }
            catch (Exception exception) { report.AppendLine(exception.ToString()); throw; }
            finally
            {
                Application.logMessageReceived -= collect;
                Selection.activeObject = previous;
                File.WriteAllText(Path.Combine(MeadowStudyBuilder.Output, "flowerkit_preflight.txt"), report.ToString());
            }
        }

        static MeadowScatterLibrary.Prototype Bake(char letter, float desiredHeight, float weight, StringBuilder report)
        {
            string id = "VEG_FlowerCluster_" + letter;
            string sourcePath = Ready + "/" + id + "/" + id + ".prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source) throw new FileNotFoundException("Missing Ready flower prefab", sourcePath);
            var materialSource = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/SM_FlowerCluster_" + letter + ".mat");
            if (!materialSource || !materialSource.GetTexture("_MainTex")) throw new InvalidDataException("Flower palette texture is missing: " + letter);
            var shader = Shader.Find("Little Castle/Surface/LC Stylized Lit");
            if (!shader) throw new InvalidDataException("Missing shared solid shader.");
            var material = new Material(materialSource) { shader = shader, enableInstancing = true };
            material.SetFloat("_LightResponse", 1);
            material = Save(material, Root + "/FlowerCluster_" + letter + ".mat");
            var instance = UnityEngine.Object.Instantiate(source);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.position = Vector3.zero;
            var temporary = new List<Mesh>();
            try
            {
                Selection.activeGameObject = instance;
                FoliageModelCompatibilityValidator.ValidateSelected();
                var group = instance.GetComponentInChildren<LODGroup>(true);
                if (!group || group.GetLODs().Length != 3) throw new InvalidDataException(id + " requires its three authored LODs.");
                var lods = group.GetLODs();
                var meshes = new Mesh[4];
                Bounds raw = default;
                for (int lod = 0; lod < 3; lod++)
                {
                    meshes[lod] = ReadAndCombine(lods[lod].renderers, id + "_LOD" + lod);
                    temporary.Add(meshes[lod]);
                    if (lod == 0) raw = meshes[lod].bounds; else raw.Encapsulate(meshes[lod].bounds);
                }
                // Old Ready imports retained Z-up geometry and very small units. Inspect actual vertices,
                // not the rounded Mesh.bounds string, before choosing the axis adapter.
                bool zUp = raw.size.z > raw.size.y * 1.6f && raw.center.z > Mathf.Abs(raw.center.y) * 1.5f && raw.center.z > raw.size.z * .1f;
                Quaternion axis = zUp ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
                Bounds oriented = default;
                for (int lod = 0; lod < 3; lod++)
                {
                    var vertices = meshes[lod].vertices;
                    var normals = meshes[lod].normals;
                    var tangents = meshes[lod].tangents;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        vertices[v] = axis * vertices[v];
                        normals[v] = axis * normals[v];
                        if (tangents.Length == vertices.Length)
                        {
                            Vector3 tangent = axis * (Vector3)tangents[v];
                            tangents[v] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[v].w);
                        }
                    }
                    meshes[lod].vertices = vertices; meshes[lod].normals = normals;
                    if (tangents.Length == vertices.Length) meshes[lod].tangents = tangents;
                    meshes[lod].RecalculateBounds();
                    if (lod == 0) oriented = meshes[lod].bounds; else oriented.Encapsulate(meshes[lod].bounds);
                }
                if (float.IsNaN(oriented.size.y) || float.IsInfinity(oriented.size.y) || oriented.size.y < 1e-9f)
                    throw new InvalidDataException(id + " has no measurable height.");
                float scale = desiredHeight / oriented.size.y;
                Vector3 pivot = new Vector3(oriented.center.x, oriented.min.y, oriented.center.z);
                Bounds normalized = default;
                for (int lod = 0; lod < 3; lod++)
                {
                    var vertices = meshes[lod].vertices;
                    for (int v = 0; v < vertices.Length; v++) vertices[v] = (vertices[v] - pivot) * scale;
                    meshes[lod].vertices = vertices; meshes[lod].RecalculateBounds();
                    if (lod == 0) normalized = meshes[lod].bounds; else normalized.Encapsulate(meshes[lod].bounds);
                    meshes[lod] = Save(meshes[lod], Root + "/FlowerCluster_" + letter + "_LOD" + lod + ".asset");
                }
                meshes[3] = meshes[2];
                var validation = new GameObject(id + " normalized static adapter") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    validation.AddComponent<MeshFilter>().sharedMesh = meshes[0];
                    validation.AddComponent<MeshRenderer>().sharedMaterial = material;
                    Selection.activeGameObject = validation;
                    FoliageModelCompatibilityValidator.ValidateSelected();
                }
                finally { UnityEngine.Object.DestroyImmediate(validation); }
                report.AppendLine(id + ": " + sourcePath);
                report.AppendLine("  Actual transformed source center=" + Vector(raw.center) + "; size=" + Vector(raw.size));
                report.AppendLine("  Axis=" + (zUp ? "Z-up to Y-up (-90 X)" : "authored Y-up retained") + "; unit normalization=" + scale.ToString("G9", CultureInfo.InvariantCulture));
                report.AppendLine("  Normalized center=" + Vector(normalized.center) + "; size=" + Vector(normalized.size) +
                    "; triangles=" + string.Join("/", meshes.Select(mesh => mesh.GetIndexCount(0) / 3)) +
                    "; palette=" + AssetDatabase.GetAssetPath(material.GetTexture("_MainTex")));
                return new MeadowScatterLibrary.Prototype { stableId = id, sourceAssetPath = sourcePath, lodMeshes = meshes,
                    materials = new[] { material }, localBounds = normalized, scaleRange = new Vector2(.8f, 1.15f), weight = weight, groundInset = .01f };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                foreach (var mesh in temporary) if (mesh && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        static Mesh ReadAndCombine(Renderer[] renderers, string name)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var tangents = new List<Vector4>(); var indices = new List<int>();
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) throw new InvalidDataException("Flower must use static mesh geometry.");
                var source = filter.sharedMesh;
                if (!source.HasVertexAttribute(VertexAttribute.Normal) || !source.HasVertexAttribute(VertexAttribute.TexCoord0))
                    throw new InvalidDataException("Flower mesh is missing normals or palette UV0.");
                Matrix4x4 matrix = renderer.localToWorldMatrix, normalMatrix = matrix.inverse.transpose;
                bool mirrored = matrix.determinant < 0, hasTangents = source.HasVertexAttribute(VertexAttribute.Tangent);
                int first = vertices.Count;
                using (var data = Mesh.AcquireReadOnlyMeshData(source))
                using (var positions = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp))
                using (var ns = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp))
                using (var tex = new NativeArray<Vector2>(source.vertexCount, Allocator.Temp))
                using (var ts = new NativeArray<Vector4>(source.vertexCount, Allocator.Temp))
                {
                    data[0].GetVertices(positions); data[0].GetNormals(ns); data[0].GetUVs(0, tex);
                    if (hasTangents) data[0].GetTangents(ts);
                    for (int v = 0; v < source.vertexCount; v++)
                    {
                        vertices.Add(matrix.MultiplyPoint3x4(positions[v]));
                        Vector3 normal = normalMatrix.MultiplyVector(ns[v]).normalized;
                        normals.Add(normal); uv.Add(tex[v]);
                        Vector3 tangent = hasTangents ? matrix.MultiplyVector(ts[v]) : Vector3.Cross(normal, Vector3.forward);
                        tangent -= normal * Vector3.Dot(normal, tangent);
                        if (tangent.sqrMagnitude < 1e-12f) tangent = Vector3.Cross(normal, Vector3.up);
                        tangent.Normalize();
                        tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, (hasTangents ? ts[v].w : 1) * (mirrored ? -1 : 1)));
                    }
                    for (int sub = 0; sub < source.subMeshCount; sub++)
                    {
                        var range = data[0].GetSubMesh(sub);
                        if (range.topology != MeshTopology.Triangles) throw new InvalidDataException("Flower source must be triangulated.");
                        int Index(int i) => first + range.baseVertex + (source.indexFormat == IndexFormat.UInt16
                            ? data[0].GetIndexData<ushort>()[range.indexStart + i]
                            : (int)data[0].GetIndexData<uint>()[range.indexStart + i]);
                        for (int i = 0; i < range.indexCount; i += 3)
                        { indices.Add(Index(i)); indices.Add(Index(i + (mirrored ? 2 : 1))); indices.Add(Index(i + (mirrored ? 1 : 2))); }
                    }
                }
            }
            if (vertices.Count == 0) throw new InvalidDataException("Empty flower LOD.");
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            return mesh;
        }

        static string Vector(Vector3 value) => value.x.ToString("G9", CultureInfo.InvariantCulture) + "," + value.y.ToString("G9", CultureInfo.InvariantCulture) + "," + value.z.ToString("G9", CultureInfo.InvariantCulture);

        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            value.name = Path.GetFileNameWithoutExtension(path);
            var old = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!old) { AssetDatabase.CreateAsset(value, path); return value; }
            if (old is Mesh target && value is Mesh source)
            {
                target.Clear(); target.indexFormat = source.indexFormat; target.vertices = source.vertices;
                target.normals = source.normals; target.tangents = source.tangents; target.uv = source.uv;
                target.subMeshCount = source.subMeshCount;
                for (int sub = 0; sub < source.subMeshCount; sub++) target.SetTriangles(source.GetTriangles(sub), sub, false);
                target.bounds = source.bounds;
            }
            else EditorUtility.CopySerialized(value, old);
            EditorUtility.SetDirty(old); AssetDatabase.SaveAssetIfDirty(old); UnityEngine.Object.DestroyImmediate(value);
            return old;
        }
    }
}
