using LittleCastle.Rendering.Atmosphere;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.EditorTools
{
    public static class LCAtmosphereQuickSetup
    {
        private const string ProfileFolder = "Assets/_Game/Settings/Atmosphere";

        [MenuItem("Little Castle/Rendering/Atmosphere/Attach To Selected Camera")]
        private static void Attach()
        {
            Camera camera = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Camera>()
                : null;
            if (camera == null)
            {
                Debug.LogWarning("Select a perspective strategy Camera first.");
                return;
            }
            if (camera.GetComponent<LCAtmosphereRenderer>() == null)
                Undo.AddComponent<LCAtmosphereRenderer>(camera.gameObject);
            EditorUtility.SetDirty(camera.gameObject);
            Debug.Log("Atmosphere attached. Assign World Focus (TestFocus), and optionally a profile. No scene auto-save.");
        }

        [MenuItem("Little Castle/Rendering/Atmosphere/Create Four Example Profiles")]
        private static void CreateProfiles()
        {
            if (!AssetDatabase.IsValidFolder(ProfileFolder))
                AssetDatabase.CreateFolder("Assets/_Game/Settings", "Atmosphere");
            Save("Kingdom", new Color(1.04f, 1f, 0.97f), new Color(0.92f, 1f, 1.1f), 0.0022f, 0.009f, 12);
            Save("Elven", new Color(1.06f, 0.94f, 1.10f), new Color(0.82f, 0.80f, 1.17f), 0.0029f, 0.013f, 14);
            Save("Woodland", new Color(0.94f, 1.08f, 0.96f), new Color(0.87f, 1.03f, 1.08f), 0.0038f, 0.018f, 16);
            Save("Snowy", new Color(0.95f, 1.03f, 1.12f), new Color(0.83f, 0.96f, 1.19f), 0.0031f, 0.017f, 14);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Example atmosphere profiles created (not applied) in " + ProfileFolder);
        }

        private static void Save(string suffix, Color day, Color night, float distance, float height, int steps)
        {
            string path = ProfileFolder + "/LCAtmosphere_" + suffix + ".asset";
            if (AssetDatabase.LoadAssetAtPath<LCAtmosphereProfile>(path) != null) return;
            LCAtmosphereProfile profile = ScriptableObject.CreateInstance<LCAtmosphereProfile>();
            profile.dayTint = day;
            profile.nightTint = night;
            profile.distanceDensity = distance;
            profile.heightDensity = height;
            profile.sampleCount = steps;
            AssetDatabase.CreateAsset(profile, path);
        }
    }
}
