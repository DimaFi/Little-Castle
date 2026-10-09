using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    /// <summary>
    /// The EditMode asmdef references Runtime only. We deliberately invoke
    /// the editor-only audit by reflection, rather than changing shared
    /// assembly-definition dependencies outside Q17 ownership.
    /// </summary>
    public sealed class HouseBudgetTests
    {
        private const string AuditTypeName =
            "LittleCastle.Editor.HouseAssetBudgetAudit, LittleCastle.Editor";
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object obj in created)
                if (obj != null)
                    Object.DestroyImmediate(obj);
            created.Clear();
        }

        [Test]
        public void ThreeLods_CountsTrianglesVerticesAndMaterialsByLevel()
        {
            GameObject root = CreateRoot();
            MeshRenderer a = CreateRenderer(root.transform, "Near", 12);
            MeshRenderer b = CreateRenderer(root.transform, "Mid", 6);
            MeshRenderer c = CreateRenderer(root.transform, "Far", 2);

            Material material = MakeMaterial();
            a.sharedMaterial = material;
            b.sharedMaterial = material;
            c.sharedMaterial = material;
            c.shadowCastingMode = ShadowCastingMode.Off;
            c.receiveShadows = false;
            c.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;

            Configure(root.GetComponent<LODGroup>(),
                new[] { a }, new[] { b }, new[] { c });

            object report = Analyze(root);
            Assert.That(Number(report, "lodGroups"), Is.EqualTo(1));
            Assert.That(Number(report, "distinctMaterials"), Is.EqualTo(1));
            Assert.That(Levels(report).Count, Is.EqualTo(3));
            AssertLevel(Level(report, 0), 12, 36, 1, 1);
            AssertLevel(Level(report, 1), 6, 18, 1, 1);
            AssertLevel(Level(report, 2), 2, 6, 1, 1);
            Assert.That(Number(Level(report, 2), "shadowCasterCount"),
                Is.Zero);
            Assert.That(Number(Level(report, 2), "shadowReceiverCount"),
                Is.Zero);
            Assert.That(Number(Level(report, 2), "nonOffMotionVectorCount"),
                Is.Zero);
            Assert.That(
                Convert.ToSingle(Field(Level(report, 1),
                    "fractionOfLod0Triangles")),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                Convert.ToSingle(Field(Level(report, 0),
                    "transitionPixels1080")),
                Is.EqualTo(648f).Within(0.01f));
        }

        [Test]
        public void MultiMaterialRenderer_ReportsLowerBoundOnSubmissions()
        {
            GameObject root = CreateRoot();
            MeshRenderer near = CreateRenderer(root.transform, "Near", 3);
            MeshRenderer mid = CreateRenderer(root.transform, "Mid", 2);
            MeshRenderer far = CreateRenderer(root.transform, "Far", 1);
            Material a = MakeMaterial(), b = MakeMaterial();
            near.sharedMaterials = new[] { a, b };
            mid.sharedMaterial = a;
            far.sharedMaterial = a;
            Configure(root.GetComponent<LODGroup>(),
                new[] { near }, new[] { mid }, new[] { far });

            object report = Analyze(root);
            Assert.That(Number(Level(report, 0), "submeshCount"),
                Is.EqualTo(1));
            Assert.That(Number(Level(report, 0), "estimatedBasePassDraws"),
                Is.EqualTo(2));
            Assert.That(Number(Level(report, 0), "uniqueMaterials"),
                Is.EqualTo(2));
            Assert.That(Number(report, "distinctMaterials"), Is.EqualTo(2));
        }

        [Test]
        public void MissingRendererMeshAndEmptyFarTier_AreNotApproved()
        {
            GameObject root = CreateRoot();
            MeshRenderer near = CreateRenderer(root.transform, "Near", 3);
            MeshRenderer mid = CreateRenderer(root.transform, "Mid", 2);
            var broken = new GameObject("BrokenFar");
            broken.transform.SetParent(root.transform, false);
            MeshRenderer far = broken.AddComponent<MeshRenderer>();
            Configure(root.GetComponent<LODGroup>(),
                new[] { near }, new[] { mid }, new[] { far });

            object report = Analyze(root);
            Assert.That(Number(report, "missingMeshRenderers"), Is.EqualTo(1));
            Assert.That(Convert.ToBoolean(Field(report, "requiresArtReview")),
                Is.True);
            Assert.That(Number(Level(report, 2), "triangles"), Is.Zero);
            Assert.That(Number(report, "warningsCount"), Is.GreaterThan(0));
        }

        [Test]
        public void ReusedRendererAcrossLods_IsFlagged()
        {
            GameObject root = CreateRoot();
            MeshRenderer near = CreateRenderer(root.transform, "Same", 3);
            MeshRenderer far = CreateRenderer(root.transform, "Far", 1);
            Configure(root.GetComponent<LODGroup>(),
                new[] { near }, new[] { near }, new[] { far });

            object report = Analyze(root);
            Assert.That(Number(report, "multiplyAssignedRenderers"),
                Is.EqualTo(1));
            Assert.That(Convert.ToBoolean(Field(report, "requiresArtReview")),
                Is.True);
        }

        [Test]
        public void VisibleUnassignedRenderer_IsDetectedButDisabledOneIsNot()
        {
            GameObject root = CreateRoot();
            MeshRenderer near = CreateRenderer(root.transform, "Near", 3);
            MeshRenderer mid = CreateRenderer(root.transform, "Mid", 2);
            MeshRenderer far = CreateRenderer(root.transform, "Far", 1);
            CreateRenderer(root.transform, "AlwaysOn", 2);
            MeshRenderer disabled =
                CreateRenderer(root.transform, "Disabled", 2);
            disabled.enabled = false;
            Configure(root.GetComponent<LODGroup>(),
                new[] { near }, new[] { mid }, new[] { far });

            object report = Analyze(root);
            Assert.That(Number(report, "unassignedEnabledRenderers"),
                Is.EqualTo(1));
        }

        [Test]
        public void DifferentMeshesWithSharedTriangles_AreCountedPerRenderer()
        {
            GameObject root = CreateRoot();
            Mesh mesh = MakeMesh(6);
            MeshRenderer first = CreateRenderer(root.transform, "NearA", mesh);
            MeshRenderer second = CreateRenderer(root.transform, "NearB", mesh);
            MeshRenderer mid = CreateRenderer(root.transform, "Mid", 4);
            MeshRenderer far = CreateRenderer(root.transform, "Far", 1);
            Configure(root.GetComponent<LODGroup>(),
                new[] { first, second }, new[] { mid }, new[] { far });

            object report = Analyze(root);
            Assert.That(Number(Level(report, 0), "triangles"), Is.EqualTo(12));
            Assert.That(Number(Level(report, 0), "vertices"), Is.EqualTo(36));
            Assert.That(Number(report, "distinctMeshes"), Is.EqualTo(3),
                "The reported unique meshes differ from geometric workload.");
        }

        [Test]
        public void NoLodGroup_FailsArtIntakeWithoutModifyingScene()
        {
            GameObject root = new GameObject("NoLodHouse");
            created.Add(root);
            MeshRenderer lone = CreateRenderer(root.transform, "Mesh", 4);
            Assert.That(lone, Is.Not.Null);

            object report = Analyze(root);
            Assert.That(Number(report, "lodGroups"), Is.Zero);
            Assert.That(Number(report, "unassignedEnabledRenderers"),
                Is.EqualTo(1));
            Assert.That(Convert.ToBoolean(Field(report, "requiresArtReview")),
                Is.True);
        }

        [Test]
        public void HugeFarMesh_TriggersProvisionalReviewWithoutChangingMesh()
        {
            GameObject root = CreateRoot();
            MeshRenderer near = CreateRenderer(root.transform, "Near", 4);
            MeshRenderer mid = CreateRenderer(root.transform, "Mid", 3);
            MeshRenderer far = CreateRenderer(root.transform, "Far", 2);
            Configure(root.GetComponent<LODGroup>(),
                new[] { near }, new[] { mid }, new[] { far });
            Mesh mesh = far.GetComponent<MeshFilter>().sharedMesh;
            int before = mesh.vertexCount;
            object report = Analyze(root);

            Assert.That(Number(report, "warningsCount"), Is.GreaterThan(0),
                "A far tier with active realtime shadows must be reviewed.");
            Assert.That(Convert.ToBoolean(Field(report, "requiresArtReview")),
                Is.True);
            Assert.That(mesh.vertexCount, Is.EqualTo(before));
            Assert.That(far.shadowCastingMode,
                Is.Not.EqualTo(ShadowCastingMode.Off),
                "The audit must report, never silently fix, shadows.");
        }

        [Test]
        public void ReportSerializesSeparatePerLodMeasurements()
        {
            GameObject root = CreateRoot();
            MeshRenderer near = CreateRenderer(root.transform, "Near", 3);
            MeshRenderer mid = CreateRenderer(root.transform, "Mid", 2);
            MeshRenderer far = CreateRenderer(root.transform, "Far", 1);
            Configure(root.GetComponent<LODGroup>(),
                new[] { near }, new[] { mid }, new[] { far });

            string json = JsonUtility.ToJson(Analyze(root), true);
            Assert.That(json, Does.Contain("estimatedBasePassDraws"));
            Assert.That(json, Does.Contain("fractionOfLod0Triangles"));
            Assert.That(json, Does.Contain("transitionPixels1080"));
            Assert.That(json, Does.Contain("NOT measured"));
        }

        [Test]
        public void ApprovedPortableHouse_HasInspectableLodGroup_WhenImported()
        {
            const string path =
                "Assets/_Game/Art/Imported/ConceptWorldKit_v001/Prefabs/house.prefab";
            GameObject house = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (house == null)
                Assert.Ignore("Portable approved house dependency not imported; " +
                    "a skipped integration check is NOT a budget PASS.");

            object report = Analyze(house);
            Assert.That(Number(report, "lodGroups"), Is.GreaterThan(0));
            Assert.That(Levels(report).Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(Field(report, "sourcePath"), Is.EqualTo(path));
        }

        private GameObject CreateRoot()
        {
            var go = new GameObject("HouseTestRoot");
            created.Add(go);
            go.AddComponent<LODGroup>();
            return go;
        }

        private MeshRenderer CreateRenderer(
            Transform parent, string name, int triangleCount) =>
            CreateRenderer(parent, name, MakeMesh(triangleCount));

        private MeshRenderer CreateRenderer(
            Transform parent, string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            return go.AddComponent<MeshRenderer>();
        }

        private Mesh MakeMesh(int triangles)
        {
            var mesh = new Mesh();
            created.Add(mesh);
            var vertices = new Vector3[triangles * 3];
            var indices = new int[vertices.Length];
            for (int i = 0; i < triangles; i++)
            {
                int j = i * 3;
                vertices[j] = new Vector3(i, 0f, 0f);
                vertices[j + 1] = new Vector3(i, 1f, 0f);
                vertices[j + 2] = new Vector3(i, 0f, 1f);
                indices[j] = j;
                indices[j + 1] = j + 1;
                indices[j + 2] = j + 2;
            }
            mesh.vertices = vertices;
            mesh.triangles = indices;
            return mesh;
        }

        private Material MakeMaterial()
        {
            Shader shader = Shader.Find("Standard") ??
                Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = new Material(shader);
            created.Add(material);
            return material;
        }

        private static void Configure(
            LODGroup group,
            Renderer[] near, Renderer[] middle, Renderer[] far)
        {
            group.SetLODs(new[]
            {
                new LOD(0.6f, near),
                new LOD(0.2f, middle),
                new LOD(0.03f, far)
            });
            group.RecalculateBounds();
        }

        private static object Analyze(GameObject root)
        {
            Type type = Type.GetType(AuditTypeName);
            Assert.That(type, Is.Not.Null,
                "Expected editor-only audit assembly to be loaded.");
            MethodInfo method = type.GetMethod(
                "Analyze", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new object[] { root });
        }

        private static IList Levels(object report) =>
            (IList)Field(report, "levels");

        private static object Level(object report, int index) =>
            Levels(report)[index];

        private static object Field(object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().GetField(
                fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(obj);
        }

        private static long Number(object obj, string fieldName) =>
            Convert.ToInt64(Field(obj, fieldName));

        private static void AssertLevel(
            object level,
            long triangles,
            long vertices,
            long renderers,
            long estimatedDraws)
        {
            Assert.That(Number(level, "triangles"), Is.EqualTo(triangles));
            Assert.That(Number(level, "vertices"), Is.EqualTo(vertices));
            Assert.That(Number(level, "rendererCount"), Is.EqualTo(renderers));
            Assert.That(Number(level, "estimatedBasePassDraws"),
                Is.EqualTo(estimatedDraws));
        }
    }
}
