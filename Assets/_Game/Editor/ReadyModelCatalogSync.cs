using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Registers externally prepared, game-ready models in WorldSpawnCatalog.
    ///
    /// Expected folder shape:
    /// Assets/_Game/Models/Ready/Category/ArchetypeId/VariantFile
    ///
    /// Unity does not convert or optimize models here. The external modeling
    /// pipeline owns that job. This tool only connects ready assets to the
    /// procedural world's existing archetype IDs.
    /// </summary>
    public static class ReadyModelCatalogSync
    {
        public const string ReadyRoot =
            "Assets/_Game/Models/Ready";

        public const string SpawnCatalogPath =
            "Assets/_Game/Settings/World/MainWorldSpawnCatalog.asset";

        [MenuItem("Little Castle/Assets/Sync Ready Models")]
        public static void Sync()
        {
            WorldSpawnCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(
                    SpawnCatalogPath);

            if (catalog == null)
            {
                Debug.LogError(
                    "WorldSpawnCatalog not found: " +
                    SpawnCatalogPath);

                return;
            }

            if (!AssetDatabase.IsValidFolder(ReadyRoot))
            {
                Debug.LogWarning(
                    "Ready model folder does not exist yet: " +
                    ReadyRoot);

                return;
            }

            Dictionary<string, List<GameObject>> groups =
                FindGroups();

            SerializedObject serialized =
                new SerializedObject(catalog);

            SerializedProperty entries =
                serialized.FindProperty("entries");

            int variantsRegistered = 0;

            foreach (
                KeyValuePair<string, List<GameObject>> pair
                in groups)
            {
                int index =
                    FindEntry(
                        entries,
                        pair.Key);

                if (index < 0)
                {
                    index =
                        entries.arraySize;

                    entries.InsertArrayElementAtIndex(
                        index);

                    SerializedProperty created =
                        entries.GetArrayElementAtIndex(
                            index);

                    created.FindPropertyRelative(
                        "archetypeId").stringValue =
                        pair.Key;

                    created.FindPropertyRelative(
                        "scaleMultiplier").floatValue =
                        1f;

                    created.FindPropertyRelative(
                        "rotationOffsetEuler").vector3Value =
                        Vector3.zero;
                }

                SerializedProperty entry =
                    entries.GetArrayElementAtIndex(
                        index);

                SerializedProperty prefabs =
                    entry.FindPropertyRelative(
                        "prefabs");

                prefabs.arraySize =
                    pair.Value.Count;

                for (int i = 0;
                     i < pair.Value.Count;
                     i++)
                {
                    prefabs.GetArrayElementAtIndex(
                        i).objectReferenceValue =
                        pair.Value[i];

                    variantsRegistered++;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "Ready models synced. Archetypes: " +
                groups.Count +
                ", variants: " +
                variantsRegistered +
                ".");
        }

        private static Dictionary<string, List<GameObject>>
            FindGroups()
        {
            var groups =
                new Dictionary<string, List<GameObject>>(
                    StringComparer.Ordinal);

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:GameObject",
                    new[] { ReadyRoot });

            Array.Sort(
                guids,
                StringComparer.Ordinal);

            for (int i = 0;
                 i < guids.Length;
                 i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                if (!TryReadArchetypeId(
                        path,
                        out string archetypeId))
                {
                    continue;
                }

                GameObject model =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        path);

                if (model == null)
                    continue;

                if (!groups.TryGetValue(
                        archetypeId,
                        out List<GameObject> variants))
                {
                    variants =
                        new List<GameObject>();

                    groups.Add(
                        archetypeId,
                        variants);
                }

                variants.Add(model);
            }

            return groups;
        }

        private static bool TryReadArchetypeId(
            string path,
            out string archetypeId)
        {
            archetypeId = null;

            string prefix =
                ReadyRoot + "/";

            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts =
                path.Substring(
                    prefix.Length).Split('/');

            if (parts.Length < 3)
                return false;

            archetypeId =
                parts[1];

            return
                !string.IsNullOrWhiteSpace(
                    archetypeId);
        }

        private static int FindEntry(
            SerializedProperty entries,
            string archetypeId)
        {
            for (int i = 0;
                 i < entries.arraySize;
                 i++)
            {
                SerializedProperty entry =
                    entries.GetArrayElementAtIndex(i);

                if (entry.FindPropertyRelative(
                        "archetypeId").stringValue ==
                    archetypeId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
