using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LittleCastle.Prototypes
{
    /// <summary>
    /// Independent art/UX study. Does not interact with saves, networking, wall placement,
    /// world chunks, gameplay buildings, or the existing map editor.
    /// Parametric data is serialized; generated render geometry is ephemeral.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CozyCottagePrototype : MonoBehaviour
    {
        public enum WallFace { Front, Back, Left, Right }
        public enum ExtensionStyle { AutoByHeight, StoneWall, WoodenFence }

        [Serializable]
        public sealed class WindowSpec
        {
            public WallFace face = WallFace.Front;
            [Range(-1f, 1f)] public float position = 0.5f;
            [Min(0.45f)] public float width = 1.0f;
            [Min(0.5f)] public float height = 1.3f;
            [Min(0.5f)] public float sill = 1.1f;
            public bool flowerBox = true;
        }

        [Header("House footprint - drag the scene handles")]
        [SerializeField, Range(4f, 16f)] private float width = 7.0f;
        [SerializeField, Range(4f, 16f)] private float depth = 6.0f;
        [SerializeField, Range(2.7f, 7f)] private float wallHeight = 3.7f;

        [Header("Independent roof controls")]
        [SerializeField, Range(0.7f, 5f)] private float roofRise = 2.1f;
        [SerializeField, Range(0.15f, 1.25f)] private float roofOverhang = 0.45f;
        [SerializeField, Range(0.1f, 1f)] private float gableOverhang = 0.35f;

        [Header("Editable window positions and openings")]
        [SerializeField] private List<WindowSpec> windows = new List<WindowSpec>
        {
            new WindowSpec { face = WallFace.Front, position = -0.63f },
            new WindowSpec { face = WallFace.Front, position = 0.65f },
            new WindowSpec { face = WallFace.Left, position = 0f, flowerBox = false },
            new WindowSpec { face = WallFace.Back, position = -0.45f },
            new WindowSpec { face = WallFace.Back, position = 0.45f }
        };

        [Header("Wall extension - attached to the front-right corner")]
        [SerializeField] private bool showExtension = true;
        [SerializeField] private ExtensionStyle extensionStyle = ExtensionStyle.AutoByHeight;
        [SerializeField, Range(1f, 16f)] private float extensionLength = 5.5f;
        [SerializeField, Range(0.6f, 3.5f)] private float extensionHeight = 1.1f;

        [Header("Appearance")]
        [SerializeField] private int variationSeed = 24;
        [SerializeField] private Material plasterMaterial;
        [SerializeField] private Material stoneMaterial;
        [SerializeField] private Material woodMaterial;
        [SerializeField] private Material roofMaterial;
        [SerializeField] private Material glassMaterial;
        [SerializeField] private Material doorMaterial;
        [SerializeField] private Material flowerMaterial;
        [SerializeField] private Material foliageMaterial;

        private const float WallThickness = 0.26f;
        private GameObject generatedRoot;
        private static Material[] defaultPalette;

        public float Width { get => width; set => width = Mathf.Clamp(value, 4f, 16f); }
        public float Depth { get => depth; set => depth = Mathf.Clamp(value, 4f, 16f); }
        public float WallHeight { get => wallHeight; set => wallHeight = Mathf.Clamp(value, 2.7f, 7f); }
        public float RoofRise { get => roofRise; set => roofRise = Mathf.Clamp(value, .7f, 5f); }
        public float RoofOverhang { get => roofOverhang; set => roofOverhang = Mathf.Clamp(value, .15f, 1.25f); }
        public float ExtensionLength { get => extensionLength; set => extensionLength = Mathf.Clamp(value, 1f, 16f); }
        public float ExtensionHeight { get => extensionHeight; set => extensionHeight = Mathf.Clamp(value, .6f, 3.5f); }
        public bool ShowExtension => showExtension;
        public List<WindowSpec> Windows => windows;

        private struct Opening
        {
            public float left, right, bottom, top;
            public bool Contains(float x, float y) =>
                x >= left && x <= right && y >= bottom && y <= top;
        }

        private struct Facade
        {
            public Vector3 center;
            public Vector3 normal;
            public Vector3 tangent;
            public float length;
            public Quaternion rotation;

            public Vector3 Point(float x, float y, float outward = 0f) =>
                center + tangent * x + Vector3.up * y + normal * outward;
        }

        public void AddWindow()
        {
            if (windows == null) windows = new List<WindowSpec>();
            windows.Add(new WindowSpec { face = WallFace.Front,
                position = windows.Count % 2 == 0 ? -.4f : .4f });
            Rebuild();
        }

        public Vector3 GetWindowCenter(WindowSpec w)
        {
            Facade face = GetFacade(w.face);
            float usable = Mathf.Max(0f, face.length * .5f - w.width * .65f);
            return face.Point(Mathf.Clamp(w.position, -1f, 1f) * usable,
                Mathf.Clamp(w.sill + w.height * .5f, .5f, wallHeight - .2f), .3f);
        }

        public void SetWindowCenter(WindowSpec w, Vector3 world)
        {
            Facade face = GetFacade(w.face);
            Vector3 local = transform.InverseTransformPoint(world);
            float usable = Mathf.Max(.001f, face.length * .5f - w.width * .65f);
            w.position = Mathf.Clamp(Vector3.Dot(local - face.center, face.tangent) / usable,
                -1f, 1f);
            w.sill = Mathf.Clamp(local.y - w.height * .5f, .5f,
                Mathf.Max(.5f, wallHeight - w.height - .15f));
        }

        public Vector3 GetExtensionEnd()
        {
            return transform.TransformPoint(new Vector3(width * .5f + extensionLength,
                extensionHeight, -depth * .5f + .18f));
        }

        private void OnEnable()
        {
            if (gameObject.scene.IsValid()) Rebuild();
        }

        private void OnDisable() => ClearGenerated();

#if UNITY_EDITOR
        private void OnValidate()
        {
            width = Mathf.Clamp(width, 4f, 16f);
            depth = Mathf.Clamp(depth, 4f, 16f);
            wallHeight = Mathf.Clamp(wallHeight, 2.7f, 7f);
            roofRise = Mathf.Clamp(roofRise, .7f, 5f);
            roofOverhang = Mathf.Clamp(roofOverhang, .15f, 1.25f);
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Creating / destroying Unity objects directly inside OnValidate is unsafe.
            EditorApplication.delayCall += DeferredRebuild;
        }

        private void DeferredRebuild()
        {
            if (this != null && isActiveAndEnabled && gameObject.scene.IsValid())
                Rebuild();
        }
#endif

        public void Rebuild()
        {
            if (!isActiveAndEnabled || !gameObject.scene.IsValid()) return;
            ClearGenerated();

            var builder = new CozyMeshBuilder();
            for (int face = 0; face < 4; face++)
                DrawFacade(builder, (WallFace)face);

            DrawFoundation(builder);
            DrawCornerStones(builder);
            DrawGableEnds(builder);
            DrawRoof(builder);
            if (showExtension) DrawExtension(builder);

            Mesh mesh = builder.ToMesh();
            generatedRoot = new GameObject("__Generated_Cozy_Cottage_Preview");
            generatedRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            generatedRoot.transform.SetParent(transform, false);
            var filter = generatedRoot.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = generatedRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = GetMaterials();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private void ClearGenerated()
        {
            if (generatedRoot == null) return;
            MeshFilter filter = generatedRoot.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (Application.isPlaying)
            {
                if (mesh != null) Destroy(mesh);
                Destroy(generatedRoot);
            }
            else
            {
                if (mesh != null) DestroyImmediate(mesh);
                DestroyImmediate(generatedRoot);
            }
            generatedRoot = null;
        }

        private Facade GetFacade(WallFace face)
        {
            Vector3 normal;
            Vector3 center;
            float length;
            switch (face)
            {
                case WallFace.Back:
                    normal = Vector3.forward;
                    center = Vector3.forward * (depth * .5f);
                    length = width;
                    break;
                case WallFace.Left:
                    normal = Vector3.left;
                    center = Vector3.left * (width * .5f);
                    length = depth;
                    break;
                case WallFace.Right:
                    normal = Vector3.right;
                    center = Vector3.right * (width * .5f);
                    length = depth;
                    break;
                default:
                    normal = Vector3.back;
                    center = Vector3.back * (depth * .5f);
                    length = width;
                    break;
            }
            return new Facade
            {
                center = center, normal = normal,
                tangent = Vector3.Cross(Vector3.up, normal),
                rotation = Quaternion.LookRotation(normal, Vector3.up),
                length = length
            };
        }

        private void FacadeBox(CozyMeshBuilder b, int material, Facade f,
            float x, float y, float outward, float sx, float sy, float sz)
        {
            b.Box(material, f.Point(x, y, outward), new Vector3(sx, sy, sz), f.rotation);
        }

        private void DrawFacade(CozyMeshBuilder b, WallFace side)
        {
            Facade f = GetFacade(side);
            var holes = new List<Opening>();
            var activeWindows = new List<WindowSpec>();
            var xs = new List<float> { -f.length * .5f, f.length * .5f };
            var ys = new List<float> { 0f, wallHeight };

            // Real holes in the plaster mesh, not windows painted onto a solid box.
            if (side == WallFace.Front)
                AddOpening(new Opening { left = -.63f, right = .63f,
                    bottom = 0f, top = Mathf.Min(2.22f, wallHeight - .2f) }, holes, xs, ys);

            if (windows != null)
            foreach (WindowSpec w in windows)
            {
                if (w == null || w.face != side) continue;
                float windowWidth = Mathf.Clamp(w.width, .45f, f.length * .40f);
                float windowHeight = Mathf.Clamp(w.height, .5f, wallHeight - 1f);
                float centerX = Mathf.Clamp(w.position, -1f, 1f) *
                    Mathf.Max(0f, f.length * .5f - windowWidth * .65f);
                float bottom = Mathf.Clamp(w.sill, .5f,
                    Mathf.Max(.5f, wallHeight - windowHeight - .15f));
                var hole = new Opening {
                    left = centerX - windowWidth * .5f,
                    right = centerX + windowWidth * .5f,
                    bottom = bottom, top = bottom + windowHeight };
                if (hole.left < -f.length * .5f + .08f ||
                    hole.right > f.length * .5f - .08f) continue;
                if (side == WallFace.Front && hole.left < .73f && hole.right > -.73f &&
                    hole.bottom < 2.32f) continue;
                bool intersects = false;
                foreach (Opening existing in holes)
                    if (hole.left < existing.right + .12f &&
                        hole.right > existing.left - .12f &&
                        hole.bottom < existing.top + .12f &&
                        hole.top > existing.bottom - .12f)
                    { intersects = true; break; }
                if (intersects) continue;
                AddOpening(hole, holes, xs, ys);
                activeWindows.Add(w);
            }

            xs.Sort();
            ys.Sort();
            for (int i = 0; i < xs.Count - 1; i++)
            for (int j = 0; j < ys.Count - 1; j++)
            {
                float x0 = xs[i], x1 = xs[i + 1];
                float y0 = ys[j], y1 = ys[j + 1];
                if (x1 - x0 < .002f || y1 - y0 < .002f) continue;
                float midX = (x0 + x1) * .5f, midY = (y0 + y1) * .5f;
                bool cutout = false;
                foreach (Opening hole in holes)
                    if (hole.Contains(midX, midY)) { cutout = true; break; }
                if (!cutout)
                    FacadeBox(b, 0, f, midX, midY, 0f,
                        x1 - x0 + .003f, y1 - y0 + .003f, WallThickness);
            }

            if (side == WallFace.Front) DrawDoor(b, f);
            foreach (WindowSpec w in activeWindows) DrawWindow(b, f, w);
        }

        private static void AddOpening(Opening o, List<Opening> holes,
            List<float> xs, List<float> ys)
        {
            holes.Add(o);
            xs.Add(o.left);
            xs.Add(o.right);
            ys.Add(o.bottom);
            ys.Add(o.top);
        }

        private void DrawDoor(CozyMeshBuilder b, Facade f)
        {
            float h = Mathf.Min(2.22f, wallHeight - .2f);
            FacadeBox(b, 7, f, 0f, h * .5f, -.012f, 1.16f, h - .05f, .08f);
            FacadeBox(b, 2, f, -.66f, h * .5f, .20f, .16f, h + .12f, .24f);
            FacadeBox(b, 2, f, .66f, h * .5f, .20f, .16f, h + .12f, .24f);
            FacadeBox(b, 2, f, 0f, h + .04f, .20f, 1.46f, .16f, .25f);
            for (int i = 1; i <= 3; i++)
                FacadeBox(b, 2, f, -.58f + i * .29f, h * .5f, .055f,
                    .025f, h - .15f, .02f);
            FacadeBox(b, 1, f, 0f, .06f, .35f, 1.52f, .15f, .68f);
            b.Box(1, f.Point(.35f, 1.03f, .10f), new Vector3(.07f, .07f, .07f));
        }

        private void DrawWindow(CozyMeshBuilder b, Facade f, WindowSpec w)
        {
            float ww = Mathf.Clamp(w.width, .45f, f.length * .4f);
            float wh = Mathf.Clamp(w.height, .5f, wallHeight - 1f);
            float x = Mathf.Clamp(w.position, -1f, 1f) *
                Mathf.Max(0f, f.length * .5f - ww * .65f);
            float y = Mathf.Clamp(w.sill, .5f, wallHeight - wh - .15f) + wh * .5f;
            FacadeBox(b, 6, f, x, y, .008f, ww - .09f, wh - .09f, .08f);
            float z = .22f;
            FacadeBox(b, 2, f, x - ww * .5f, y, z, .13f, wh + .19f, .18f);
            FacadeBox(b, 2, f, x + ww * .5f, y, z, .13f, wh + .19f, .18f);
            FacadeBox(b, 2, f, x, y - wh * .5f, z, ww + .2f, .14f, .23f);
            FacadeBox(b, 2, f, x, y + wh * .5f, z, ww + .2f, .16f, .19f);
            FacadeBox(b, 2, f, x, y, z + .03f, .055f, wh - .06f, .075f);
            FacadeBox(b, 2, f, x, y, z + .03f, ww - .06f, .055f, .075f);
            // Shutters and a small, asymmetric flower box add the cozy silhouette.
            FacadeBox(b, 2, f, x - ww * .5f - .22f, y, .13f, .30f, wh * .90f, .08f);
            FacadeBox(b, 2, f, x + ww * .5f + .22f, y, .13f, .30f, wh * .90f, .08f);
            if (!w.flowerBox) return;
            float yBox = y - wh * .5f - .19f;
            FacadeBox(b, 2, f, x, yBox, .33f, ww + .43f, .23f, .40f);
            for (int i = 0; i < 5; i++)
            {
                float fx = x + (i - 2) * (ww / 5f);
                float fy = yBox + .16f + .05f * Hash(i + variationSeed, 0);
                b.Box(9, f.Point(fx, fy, .44f), new Vector3(.18f, .21f, .14f));
                b.Box(8, f.Point(fx + .035f, fy + .12f, .50f),
                    new Vector3(.10f, .12f, .10f));
            }
        }

        private void DrawFoundation(CozyMeshBuilder b)
        {
            b.Box(1, new Vector3(0f, .12f, 0f),
                new Vector3(width + .27f, .35f, depth + .27f));
            // A visibly broken-up stone sill rather than an uninterrupted strip.
            for (int side = 0; side < 4; side++)
            {
                Facade f = GetFacade((WallFace)side);
                int count = Mathf.CeilToInt(f.length / .68f);
                for (int i = 0; i < count; i++)
                {
                    float sx = f.length / count;
                    float x = -f.length * .5f + (i + .5f) * sx;
                    FacadeBox(b, 1, f, x, .38f, .12f,
                        sx - .025f, .25f + .05f * Hash(side, i), .14f);
                }
            }
        }

        private void DrawCornerStones(CozyMeshBuilder b)
        {
            int rows = Mathf.CeilToInt(wallHeight / .45f);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = (corner & 1) == 0 ? -width * .5f : width * .5f;
                float z = (corner & 2) == 0 ? -depth * .5f : depth * .5f;
                for (int row = 1; row < rows; row++)
                {
                    float cy = (row + .25f) * .45f;
                    if (cy + .12f > wallHeight) break;
                    float offset = Hash(corner, row) * .08f;
                    b.Box(1, new Vector3(x, cy, z),
                        new Vector3(.42f + offset, .28f, .44f + offset));
                }
            }
        }

        private void DrawGableEnds(CozyMeshBuilder b)
        {
            float eave = wallHeight;
            float top = wallHeight + roofRise;
            float z = depth * .5f;
            float x = width * .5f;
            b.Triangle(0, new Vector3(-x, eave, -z),
                new Vector3(-x, eave, z), new Vector3(-x, top, 0f));
            b.Triangle(0, new Vector3(x, eave, z),
                new Vector3(x, eave, -z), new Vector3(x, top, 0f));
        }

        private Vector3 RoofPoint(float x, float signedZ, float lift)
        {
            float slope = roofRise / Mathf.Max(.1f, depth * .5f);
            return new Vector3(x,
                wallHeight + roofRise - Mathf.Abs(signedZ) * slope + lift,
                signedZ);
        }

        private void DrawRoof(CozyMeshBuilder b)
        {
            float halfX = width * .5f + gableOverhang;
            float zMax = depth * .5f + roofOverhang;
            // The two whole roof panels (and their underside) are independent of
            // the footprint. Shingle width/count is rebuilt, never scale-stretched.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 a = RoofPoint(-halfX, 0f, 0f);
                Vector3 c = RoofPoint(halfX, sign * zMax, 0f);
                Vector3 d = RoofPoint(halfX, 0f, 0f);
                Vector3 e = RoofPoint(-halfX, sign * zMax, 0f);
                if (sign > 0) b.Quad(3, a, e, c, d);
                else b.Quad(3, d, c, e, a);

                float tileW = .54f, tileZ = .44f;
                int rows = Mathf.CeilToInt(zMax / tileZ);
                int columns = Mathf.CeilToInt(2f * halfX / tileW) + 2;
                for (int row = 0; row < rows; row++)
                {
                    float z0 = Mathf.Min(zMax, row * tileZ);
                    float z1 = Mathf.Min(zMax, (row + 1) * tileZ + .085f);
                    for (int col = 0; col < columns; col++)
                    {
                        float shift = (row % 2) * tileW * .5f;
                        float x0 = Mathf.Max(-halfX,
                            -halfX + col * tileW - shift);
                        float x1 = Mathf.Min(halfX,
                            -halfX + (col + 1) * tileW - shift + .025f);
                        if (x1 - x0 < .1f) continue;
                        float lift = .025f + .025f * Hash(col + row * 2, variationSeed);
                        Vector3 p0 = RoofPoint(x0, sign * z0, lift);
                        Vector3 p1 = RoofPoint(x0, sign * z1, lift);
                        Vector3 p2 = RoofPoint(x1, sign * z1, lift);
                        Vector3 p3 = RoofPoint(x1, sign * z0, lift);
                        int variant = 3 + Mathf.FloorToInt(
                            Hash(col * 31 + row * 67, variationSeed) * 3f);
                        if (sign > 0) b.Quad(variant, p0, p1, p2, p3);
                        else b.Quad(variant, p3, p2, p1, p0);
                        // The exposed lower tile edge catches a small highlight.
                        Vector3 lower = Vector3.down * .047f;
                        if (sign > 0) b.Quad(variant, p1, p1 + lower,
                            p2 + lower, p2);
                        else b.Quad(variant, p2, p2 + lower,
                            p1 + lower, p1);
                    }
                }
                b.Beam(2, RoofPoint(-halfX, sign * zMax, -.055f),
                    RoofPoint(halfX, sign * zMax, -.055f), .13f);
            }
            b.Beam(2, RoofPoint(-halfX, 0f, .08f),
                RoofPoint(halfX, 0f, .08f), .13f);
        }

        private void DrawExtension(CozyMeshBuilder b)
        {
            float z = -depth * .5f + .18f;
            float x0 = width * .5f;
            bool wooden = extensionStyle == ExtensionStyle.WoodenFence ||
                (extensionStyle == ExtensionStyle.AutoByHeight &&
                    extensionHeight < 1.55f);
            int sections = Mathf.Max(1, Mathf.CeilToInt(extensionLength / 1.6f));
            float step = extensionLength / sections;
            for (int i = 0; i <= sections; i++)
            {
                float x = x0 + i * step;
                b.Box(wooden ? 2 : 1, new Vector3(x, extensionHeight * .5f, z),
                    new Vector3(wooden ? .17f : .36f, extensionHeight,
                        wooden ? .17f : .38f));
                if (i == sections) continue;
                float mx = x + step * .5f;
                if (wooden)
                {
                    b.Box(2, new Vector3(mx, extensionHeight * .42f, z),
                        new Vector3(step, .13f, .12f));
                    b.Box(2, new Vector3(mx, extensionHeight * .78f, z),
                        new Vector3(step, .13f, .12f));
                    int slats = Mathf.Max(2, Mathf.RoundToInt(step / .27f));
                    for (int j = 0; j < slats; j++)
                    {
                        float px = x + (j + .5f) * step / slats;
                        b.Box(2, new Vector3(px, extensionHeight * .48f, z),
                            new Vector3(.11f, extensionHeight * .90f, .1f));
                    }
                }
                else
                {
                    b.Box(1, new Vector3(mx, extensionHeight * .5f, z),
                        new Vector3(step - .03f, extensionHeight - .09f, .29f));
                    b.Box(1, new Vector3(mx, extensionHeight + .01f, z),
                        new Vector3(step - .015f, .13f, .39f));
                }
            }
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177;
                return (h & 0xffff) / 65536f;
            }
        }

        private Material[] GetMaterials()
        {
            if (defaultPalette == null) CreateFallbackMaterials();
            return new[]
            {
                plasterMaterial != null ? plasterMaterial : defaultPalette[0],
                stoneMaterial != null ? stoneMaterial : defaultPalette[1],
                woodMaterial != null ? woodMaterial : defaultPalette[2],
                roofMaterial != null ? roofMaterial : defaultPalette[3],
                roofMaterial != null ? roofMaterial : defaultPalette[4],
                roofMaterial != null ? roofMaterial : defaultPalette[5],
                glassMaterial != null ? glassMaterial : defaultPalette[6],
                doorMaterial != null ? doorMaterial : defaultPalette[7],
                flowerMaterial != null ? flowerMaterial : defaultPalette[8],
                foliageMaterial != null ? foliageMaterial : defaultPalette[9]
            };
        }

        private static void CreateFallbackMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("Cozy cottage: neither URP/Lit nor Standard shader is available.");
                return;
            }
            Color[] colors =
            {
                new Color(.83f, .76f, .60f), // warm limewashed plaster
                new Color(.57f, .51f, .40f), // old honey limestone
                new Color(.37f, .24f, .16f), // aged wood
                new Color(.49f, .24f, .19f), // russet roof
                new Color(.37f, .18f, .15f), // dark tiles
                new Color(.62f, .35f, .26f), // sunlit tiles
                new Color(.28f, .38f, .40f), // subdued bluish glass
                new Color(.30f, .21f, .17f), // door
                new Color(.74f, .36f, .48f), // flowers
                new Color(.33f, .45f, .25f)  // leafy greens
            };
            defaultPalette = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                var material = new Material(shader) {
                    name = "Prototype cottage palette " + i,
                    hideFlags = HideFlags.DontSave,
                    enableInstancing = true
                };
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", colors[i]);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", colors[i]);
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", .16f);
                defaultPalette[i] = material;
            }
        }
    }
}
