using System.Collections.Generic;
using LittleCastle.World.Weather;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.EditorTools
{
    /// <summary>
    /// The tool writes ONLY explicitly selected .prefab assets (never scene instances).
    /// Configuring a prefab once automatically configures all existing/future instances.
    /// </summary>
    public static class RainShelterPrefabSetup
    {
        private const string Menu = "Little Castle/Weather/Configure Roof Shelter on Selected Prefabs";

        [MenuItem(Menu, true)]
        private static bool Validate() => Selection.objects != null && Selection.objects.Length > 0;

        [MenuItem(Menu)]
        private static void ConfigureSelectedPrefabs()
        {
            var unique = new HashSet<string>();
            foreach (Object selection in Selection.objects)
            {
                if (!(selection is GameObject go)) continue;
                string path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path))
                    path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    unique.Add(path);
            }
            if (unique.Count == 0)
            {
                EditorUtility.DisplayDialog("Rain Shelter",
                    "Выбери префаб здания в Project (или его экземпляр в сцене), затем повтори.",
                    "ОК");
                return;
            }
            int created = 0, already = 0, failed = 0;
            foreach (string prefabPath in unique)
            {
                GameObject prefab = null;
                try
                {
                    prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (!prefab) { failed++; continue; }
                    var existing = prefab.transform.Find("Rain Shelter");
                    var component = existing ? existing.GetComponent<RainShelterVolume>() : null;
                    if (component)
                    {
                        already++;
                        continue; // Respect all user-authored footprint/shelter values.
                    }
                    if (!TryLocalBounds(prefab, out Bounds bounds))
                    {
                        Debug.LogWarning("Rain Shelter: no supported MeshRenderer/SkinnedMeshRenderer bounds on " + prefabPath);
                        failed++;
                        continue;
                    }
                    var anchor = existing ? existing.gameObject : new GameObject("Rain Shelter");
                    if (!existing) anchor.transform.SetParent(prefab.transform, false);
                    anchor.transform.localPosition = new Vector3(
                        bounds.center.x, bounds.max.y - Mathf.Max(.12f, bounds.size.y * .20f), bounds.center.z);
                    anchor.transform.localRotation = Quaternion.identity;
                    anchor.transform.localScale = Vector3.one;
                    component = anchor.AddComponent<RainShelterVolume>();
                    component.halfWidth = Mathf.Max(.5f, bounds.extents.x * .94f);
                    component.halfDepth = Mathf.Max(.5f, bounds.extents.z * .94f);
                    component.edgeFade = Mathf.Clamp(
                        Mathf.Min(component.halfWidth, component.halfDepth) * .32f, .45f, 3.5f);
                    component.verticalFade = .9f;
                    component.irregularity = .15f;
                    component.protection = .98f;
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                    created++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    Debug.LogError("Rain Shelter prefab setup failed on " + prefabPath + ": " + e);
                }
                finally
                {
                    if (prefab) PrefabUtility.UnloadPrefabContents(prefab);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Rain Shelter: added {created}, already configured {already}, failed/skipped {failed}. " +
                "Changes were saved to prefab ASSETS only; no individual map building has been edited.");
            EditorUtility.DisplayDialog("Rain Shelter", $"Настроено префабов: {created}\n" +
                $"Уже были настроены: {already}\nПропущено: {failed}\n" +
                "При необходимости открой префаб и поправь позицию синей рамки под карнизом.", "ОК");
        }

        /// <summary>
        /// Optional one-time eave markers; all placed houses inherit them from the prefab.
        /// The automatic location is a starting suggestion. Gabled/complex roofs need manual adjustment.
        /// </summary>
        [MenuItem("Little Castle/Weather/Configure Roof Drips on Selected Prefabs")]
        private static void ConfigureRunoff()
        {
            int updated = 0, skipped = 0;
            var paths = new HashSet<string>();
            foreach (Object selection in Selection.objects)
            {
                if (!(selection is GameObject go)) continue;
                string path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path))
                    path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) paths.Add(path);
            }
            foreach (string path in paths)
            {
                GameObject prefab = null;
                try
                {
                    prefab = PrefabUtility.LoadPrefabContents(path);
                    if (!TryLocalBounds(prefab, out Bounds bounds)) { skipped++; continue; }
                    bool changed = false;
                    float y = bounds.max.y - bounds.size.y * .20f;
                    for (int side = 0; side < 2; side++)
                    {
                        string name = side == 0 ? "Rain Drip Eave A" : "Rain Drip Eave B";
                        if (prefab.transform.Find(name)) continue;
                        var marker = new GameObject(name);
                        marker.transform.SetParent(prefab.transform, false);
                        marker.transform.localPosition = new Vector3(bounds.center.x, y,
                            side == 0 ? bounds.min.z : bounds.max.z);
                        var component = marker.AddComponent<RainRoofDripSource>();
                        component.edgeLength = Mathf.Clamp(bounds.size.x * .85f, .25f, 16f);
                        changed = true;
                    }
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefab, path);
                        updated++;
                    }
                }
                catch (System.Exception e)
                {
                    skipped++;
                    Debug.LogError("Rain Drips prefab setup failed on " + path + ": " + e);
                }
                finally { if (prefab) PrefabUtility.UnloadPrefabContents(prefab); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Rain roof runoff: updated {updated} prefab assets; skipped/previously configured {skipped}.");
        }

        [MenuItem("Little Castle/Weather/Configure Roof Drips on Selected Prefabs", true)]
        private static bool CanConfigureRunoff() => Selection.objects != null && Selection.objects.Length > 0;

        private static bool TryLocalBounds(GameObject root, out Bounds result)
        {
            result = default;
            bool found = false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min, max = bounds.max;
                for (int j = 0; j < 8; j++)
                {
                    var point = new Vector3((j & 1) == 0 ? min.x : max.x,
                        (j & 2) == 0 ? min.y : max.y, (j & 4) == 0 ? min.z : max.z);
                    var local = root.transform.InverseTransformPoint(point);
                    if (!found) { result = new Bounds(local, Vector3.zero); found = true; }
                    else result.Encapsulate(local);
                }
            }
            return found && result.size.x > .01f && result.size.z > .01f && result.size.y > .01f;
        }
    }

    [CustomEditor(typeof(RainShelterVolume))]
    public sealed class RainShelterVolumeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Этот маркер живёт на ПРЕФАБЕ: его наследуют все экземпляры здания. " +
                "Синяя рамка = край крыши; зелёная = сухая область. " +
                "Переход Edge Fade и Irregularity делают край дождя плавным. " +
                "Поставь маркер по высоте НИЖНЕЙ стороны крыши, иначе кровля тоже станет сухой.",
                MessageType.Info);
            DrawDefaultInspector();
        }

        private void OnSceneGUI()
        {
            var marker = (RainShelterVolume)target;
            var transform = marker.transform;
            Vector3 pos = transform.position;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(pos, transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(transform, "Move rain shelter under eave");
                transform.position = moved;
            }
            EditorGUI.BeginChangeCheck();
            float width = Handles.ScaleSlider(marker.halfWidth, pos, transform.right, transform.rotation,
                HandleUtility.GetHandleSize(pos) * .65f, .1f);
            float depth = Handles.ScaleSlider(marker.halfDepth, pos, transform.forward, transform.rotation,
                HandleUtility.GetHandleSize(pos) * .65f, .1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(marker, "Resize rain shelter");
                marker.halfWidth = Mathf.Max(.25f, width);
                marker.halfDepth = Mathf.Max(.25f, depth);
                EditorUtility.SetDirty(marker);
            }
        }
    }
}
