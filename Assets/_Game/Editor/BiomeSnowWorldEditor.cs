using System;
using System.Collections.Generic;
using LittleCastle.World.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Biome authoring INSIDE the existing Unity Scene View and Inspector.
    /// No second map editor and no extra in-game tool.
    /// Mouse-wheel zoom is handled by SceneView; brush radius is world metres.
    /// </summary>
    [CustomEditor(typeof(BiomeSnowWorld))]
    public sealed class BiomeSnowWorldEditor : UnityEditor.Editor
    {
        private enum BrushOperation { Paint, EraseToSummer, Smooth }

        private bool enabledBrush;
        private bool fallbackToYZero;
        private bool showBiomeOverlay = true;
        private float radius = 18f;
        private float strength = 0.6f;
        private float hardness = 0.15f;
        private int selectedTheme = EnvironmentThemeIds.Winter;
        private BrushOperation operation;
        private bool strokeActive;
        private bool strokeChanged;
        private Vector3 previousStamp;
        private SceneView savedView;
        private Vector3 savedPivot;
        private Quaternion savedRotation;
        private float savedSize;
        private bool savedOrthographic;

        private BiomeSnowWorld World => (BiomeSnowWorld)target;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += ReloadAfterUndo;
        }

        private void OnDisable()
        {
            FinishStroke();
            RestoreView();
            Undo.undoRedoPerformed -= ReloadAfterUndo;
        }

        private void ReloadAfterUndo()
        {
            if (target == null) return;
            World.ReloadAuthoringState();
            SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Biomes - Map Painter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Paint on the current map in Scene View at ANY camera zoom. " +
                "This is an authoring overlay, not a second map editor. " +
                "Requires a ground collider for terrain-accurate hits.",
                MessageType.Info);

            bool nextActive = EditorGUILayout.ToggleLeft("Enable biome brush in Scene View",
                enabledBrush);
            if (nextActive != enabledBrush)
            {
                if (!nextActive) FinishStroke();
                enabledBrush = nextActive;
                SceneView.RepaintAll();
            }

            int[] ids;
            string[] names;
            Color[] colors;
            CollectPalette(out ids, out names, out colors);
            int index = Array.IndexOf(ids, selectedTheme);
            if (index < 0) index = 0;
            index = EditorGUILayout.Popup("Biome", index, names);
            selectedTheme = ids[index];

            operation = (BrushOperation)GUILayout.Toolbar(
                (int)operation, new[] { "Paint", "Erase", "Smooth" });
            radius = EditorGUILayout.Slider(
                new GUIContent("Brush radius (metres)", "Unchanged by camera zoom"),
                radius, 0.5f, 320f);
            strength = EditorGUILayout.Slider("Strength", strength, 0.01f, 1f);
            hardness = EditorGUILayout.Slider(
                new GUIContent("Hardness", "0 = soft boundary, 1 = fully hard"),
                hardness, 0f, 1f);
            showBiomeOverlay = EditorGUILayout.ToggleLeft(
                "Show sampled biome overlay in Scene View", showBiomeOverlay);
            fallbackToYZero = EditorGUILayout.ToggleLeft(
                "Allow flat Y=0 plane if terrain raycast misses", fallbackToYZero);

            EditorGUILayout.LabelField("Quick camera framing", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Frame entire map")) FrameMap();
            if (GUILayout.Button("Restore camera")) RestoreView();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "Alt/Right/Middle drag and wheel zoom remain Scene View navigation. " +
                "LMB paints. Hold Shift for temporary eraser. " +
                "Existing world objects/terrain heights are not deleted. " +
                "The current snow shader needs a separate URP integration.",
                MessageType.None);
        }

        private void CollectPalette(out int[] ids, out string[] names,
            out Color[] colors)
        {
            var resultIds = new List<int> { EnvironmentThemeIds.Summer,
                EnvironmentThemeIds.EnchantedForest, EnvironmentThemeIds.ElvenForest,
                EnvironmentThemeIds.Winter };
            var resultNames = new List<string>
                { "Summer / Meadow", "Enchanted Woodland", "Elven Forest", "Winter" };
            var resultColors = new List<Color>
                { new Color(0.35f, 0.75f, 0.35f, 0.3f),
                  new Color(0.28f, 0.55f, 0.37f, 0.3f),
                  new Color(0.7f, 0.38f, 0.7f, 0.3f),
                  new Color(0.73f, 0.84f, 1f, 0.3f) };

            foreach (var profile in World.AvailableThemes ?? Array.Empty<BiomeThemeProfile>())
            {
                if (profile == null || profile.ThemeId <= 0) continue;
                int existing = resultIds.IndexOf(profile.ThemeId);
                if (existing >= 0)
                {
                    resultNames[existing] = profile.DisplayName;
                    resultColors[existing] = profile.MapOverlayColor;
                }
                else
                {
                    resultIds.Add(profile.ThemeId);
                    resultNames.Add(profile.DisplayName);
                    resultColors.Add(profile.MapOverlayColor);
                }
            }
            ids = resultIds.ToArray();
            names = resultNames.ToArray();
            colors = resultColors.ToArray();
        }

        private void OnSceneGUI()
        {
            if (!enabledBrush || target == null) return;
            var e = Event.current;
            if (e == null) return;

            // Preserve ALL existing SceneView navigation (scroll, orbit, pan, fly).
            if (e.alt || e.button == 1 || e.button == 2)
            {
                if (strokeActive && (e.type == EventType.MouseUp ||
                    e.type == EventType.Ignore))
                    FinishStroke();
                return;
            }

            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(
                    GUIUtility.GetControlID(FocusType.Passive));

            if (showBiomeOverlay && e.type == EventType.Repaint)
                DrawBiomeOverview();
            bool hasHit = TryHit(e.mousePosition, out var point,
                out var normal);
            if (hasHit && (e.type == EventType.Repaint ||
                e.type == EventType.MouseMove))
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                Color c = SelectedColor();
                c.a = 0.15f;
                Handles.color = c;
                Handles.DrawSolidDisc(point, normal, radius);
                c.a = 0.85f;
                Handles.color = c;
                Handles.DrawWireDisc(point, normal, radius);
                Handles.color = Color.white;
                if (e.type == EventType.MouseMove) SceneView.RepaintAll();
            }

            if (e.type == EventType.MouseDown && e.button == 0 && hasHit)
            {
                Undo.RegisterCompleteObjectUndo(World, "Paint environment biome");
                strokeActive = true;
                strokeChanged = false;
                previousStamp = point;
                PaintAt(point);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && strokeActive && e.button == 0)
            {
                if (hasHit) PaintSegment(previousStamp, point);
                e.Use();
            }
            else if ((e.type == EventType.MouseUp && e.button == 0) ||
                     e.type == EventType.Ignore)
            {
                if (strokeActive)
                {
                    FinishStroke();
                    e.Use();
                }
            }
        }

        private void DrawBiomeOverview()
        {
            // Bounded, coarse authoring preview; NEVER spawn per-cell GameObjects.
            var view = SceneView.currentDrawingSceneView;
            if (view == null) return;
            CollectPalette(out var ids, out var names, out var colors);
            float halfSpan = Mathf.Clamp(view.size, 8f, 2000f);
            float step = Mathf.Max(World.Themes.CellSize,
                2f * halfSpan / 22f);
            Rect bounds = World.SimulationBounds;
            Vector3 pivot = view.pivot;

            for (int row = -11; row <= 11; row++)
            for (int col = -11; col <= 11; col++)
            {
                float x = pivot.x + col * step;
                float z = pivot.z + row * step;
                if (!bounds.Contains(new Vector2(x, z))) continue;
                int dominant = -1;
                float best = 0.025f;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] == EnvironmentThemeIds.Summer) continue;
                    float weight = World.Themes.Sample(ids[i], x, z);
                    if (weight <= best) continue;
                    best = weight;
                    dominant = i;
                }
                if (dominant < 0) continue;

                // Ground projection is approximate and capped to 529 raycasts per
                // repaint. Full real-time terrain paint preview belongs to the
                // production chunk/renderer integration.
                Ray ray = new Ray(new Vector3(x, 10000f, z), Vector3.down);
                if (!Physics.Raycast(ray, out RaycastHit hit, 20000f))
                    continue;

                Color tint = colors[dominant];
                tint.a = Mathf.Lerp(0f, 0.23f, best);
                Handles.color = tint;
                Handles.DrawSolidDisc(hit.point + hit.normal * 0.03f,
                    hit.normal, step * 0.55f);
            }
            Handles.color = Color.white;
        }

        private bool TryHit(Vector2 mousePosition, out Vector3 point,
            out Vector3 normal)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000000f))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }

            if (fallbackToYZero &&
                new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                normal = Vector3.up;
                return true;
            }
            point = default;
            normal = Vector3.up;
            return false;
        }

        private void PaintSegment(Vector3 from, Vector3 to)
        {
            // World-space dab spacing, independent of the zoom or resolution.
            float step = Mathf.Max(World.Themes.CellSize * 0.5f, radius * 0.25f);
            float distance = Vector2.Distance(
                new Vector2(from.x, from.z), new Vector2(to.x, to.z));
            int count = Mathf.Clamp(Mathf.CeilToInt(distance / step), 1, 512);
            if (distance > step * 512f)
                Debug.LogWarning("Biome brush skipped part of a very long drag; " +
                    "pan or move more slowly for accurate results.", World);

            for (int i = 1; i <= count; i++)
            {
                Vector3 position = Vector3.Lerp(from, to, (float)i / count);
                PaintAt(position);
            }
            previousStamp = to;
        }

        private void PaintAt(Vector3 point)
        {
            if (target == null) return;
            int theme = operation == BrushOperation.EraseToSummer ||
                Event.current.shift ? EnvironmentThemeIds.Summer : selectedTheme;
            bool changed;
            if (operation == BrushOperation.Smooth && !Event.current.shift)
                changed = World.SmoothTheme(point, radius, selectedTheme,
                    strength, hardness);
            else
                changed = World.PaintTheme(point, radius, theme,
                    strength, hardness);
            strokeChanged |= changed;
        }

        private Color SelectedColor()
        {
            CollectPalette(out var ids, out var names, out var colors);
            int index = Array.IndexOf(ids, selectedTheme);
            return index >= 0 ? colors[index] : Color.white;
        }

        private void FinishStroke()
        {
            if (!strokeActive) return;
            strokeActive = false;
            if (!strokeChanged || target == null) return;
            World.SaveAuthoringState();
            EditorUtility.SetDirty(World);
            if (World.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(World.gameObject.scene);
            SceneView.RepaintAll();
        }

        private void FrameMap()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            if (savedView == null)
            {
                savedView = view;
                savedPivot = view.pivot;
                savedRotation = view.rotation;
                savedSize = view.size;
                savedOrthographic = view.orthographic;
            }
            Rect bounds = World.SimulationBounds;
            view.pivot = new Vector3(bounds.center.x, 0f, bounds.center.y);
            view.rotation = Quaternion.Euler(90f, 0f, 0f);
            view.orthographic = true;
            view.size = Mathf.Max(bounds.width, bounds.height) * 0.8f;
            view.Repaint();
        }

        private void RestoreView()
        {
            if (savedView == null) return;
            savedView.pivot = savedPivot;
            savedView.rotation = savedRotation;
            savedView.size = savedSize;
            savedView.orthographic = savedOrthographic;
            savedView.Repaint();
            savedView = null;
        }
    }
}
