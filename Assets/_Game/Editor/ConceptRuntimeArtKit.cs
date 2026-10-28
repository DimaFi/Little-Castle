using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>Copies only reviewed runtime dependencies, not the ignored art library.</summary>
    public static class ConceptRuntimeArtKit
    {
        public const string Source = "Assets/_Game/Art/Imported/TerrainStarter_v001/";
        public const string Destination = "Assets/_Game/Art/Imported/ConceptWorldKit_v001/";

        [Serializable] private sealed class Entry
        {
            public string source, destination, sourceGuid, runtimeGuid, sourceSha256;
        }
        [Serializable] private sealed class Manifest
        {
            public string purpose = "Approved minimal runtime copy; original authoring library untouched. Source model/material bytes unchanged except remapped Unity GUID references.";
            public List<Entry> dependencies = new List<Entry>();
        }

        public static void CreateMissing()
        {
            string[] selected = {
                "Prefabs/oak.prefab", "Prefabs/house.prefab", "Prefabs/well.prefab",
                "Prefabs/SM_Stone_Medium_A.prefab", "Study/Textures/Grass_MeadowSoft_BaseColor.png",
                "Textures/DirtPath_A_BaseColor.png", "Textures/DirtGround_A_BaseColor.png",
                "Textures/T_WallStoneSurface_A_BaseColor.png", "Study/Textures/DirtPath_A_Normal.png",
                "Study/Textures/DirtPath_A_AO.png" };
            for (int i = 0; i < selected.Length; i++) selected[i] = Source + selected[i];
            var runtimeRoots = new string[selected.Length];
            bool complete = true;
            for (int i = 0; i < selected.Length; i++)
            {
                runtimeRoots[i] = selected[i].Replace(Source, Destination);
                complete &= AssetDatabase.LoadMainAssetAtPath(runtimeRoots[i]) != null;
            }
            if (complete) { ValidateDependencies(runtimeRoots); return; }
            string[] paths = AssetDatabase.GetDependencies(selected, true);
            Array.Sort(paths, StringComparer.Ordinal);
            var manifest = new Manifest();
            foreach (string path in paths)
            {
                if (!path.StartsWith(Source, StringComparison.Ordinal)) continue;
                string destination = Destination + path.Substring(Source.Length);
                EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
                if (AssetDatabase.LoadMainAssetAtPath(destination) == null && !AssetDatabase.CopyAsset(path, destination))
                    throw new IOException("Could not copy approved runtime dependency: " + path);
                using (var hash = SHA256.Create())
                    manifest.dependencies.Add(new Entry { source = path, destination = destination,
                        sourceGuid = AssetDatabase.AssetPathToGUID(path), runtimeGuid = AssetDatabase.AssetPathToGUID(destination),
                        sourceSha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() });
            }
            // Mechanical GUID remap only inside newly isolated, copied text assets.
            // FBX/PNG/model bytes and every source file remain unchanged.
            foreach (var entry in manifest.dependencies)
            {
                string extension = Path.GetExtension(entry.destination);
                if (extension != ".prefab" && extension != ".mat" && extension != ".asset") continue;
                string value = File.ReadAllText(entry.destination);
                if (!value.StartsWith("%YAML", StringComparison.Ordinal))
                    throw new InvalidOperationException("Expected text-serialized approved runtime asset: " + entry.destination);
                string rewritten = value;
                foreach (var mapping in manifest.dependencies)
                    rewritten = rewritten.Replace("guid: " + mapping.sourceGuid, "guid: " + mapping.runtimeGuid);
                if (rewritten != value) File.WriteAllText(entry.destination, rewritten);
                AssetDatabase.ImportAsset(entry.destination, ImportAssetOptions.ForceSynchronousImport);
            }
            File.WriteAllText(Destination + "Provenance.json", JsonUtility.ToJson(manifest, true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateDependencies(runtimeRoots);
            Debug.Log("[Concept Runtime Kit] Portable reviewed dependencies=" + manifest.dependencies.Count + "; original art untouched.");
        }

        private static void ValidateDependencies(string[] runtimeRoots)
        {
            foreach (string dependency in AssetDatabase.GetDependencies(runtimeRoots, true))
                if (dependency.StartsWith(Source, StringComparison.Ordinal) || dependency.Contains("/Models/Ready/") || dependency.Contains("/Textures/Ready/"))
                    throw new InvalidOperationException("Runtime kit still depends on ignored local art: " + dependency);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
