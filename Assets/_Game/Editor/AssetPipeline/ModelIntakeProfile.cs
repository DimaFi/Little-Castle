using System;
using LittleCastle.Assets;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Per-model intake instructions.
    ///
    /// Create one profile for a raw source model, then run the intake command.
    /// The tool moves/copies the model into the project-owned source area,
    /// applies safe static-model import settings, builds a final prefab and
    /// registers it for runtime/procedural use.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ModelIntakeProfile",
        menuName = "Little Castle/Assets/Model Intake Profile")]
    public sealed class ModelIntakeProfile : ScriptableObject
    {
        [Header("Identity")]
        public string assetId = "tree_main_01";
        public string archetypeId = "tree_main_01";
        public GameAssetKind assetKind = GameAssetKind.Tree;
        public SpawnCategory spawnCategory = SpawnCategory.Tree;

        [Header("Source")]
        [Tooltip("Imported Unity model asset (FBX/OBJ/DAE etc.).")]
        public GameObject sourceModel;

        [Tooltip(
            "If enabled and the source is outside the canonical source folder, " +
            "the asset file is moved there. Disable to copy instead.")]
        public bool moveSourceIntoCanonicalFolder = true;

        [Header("Runtime prefab")]
        public bool proceduralSpawnAllowed = true;

        [Min(0.01f)]
        public float prefabScale = 1f;

        public Vector3 rotationOffsetEuler;

        [Tooltip(
            "Add a simple BoxCollider sized from renderer bounds. Leave false " +
            "for vegetation/decorative objects until gameplay needs collision.")]
        public bool addSimpleBoxCollider = false;

        [Header("Static model import")]
        public bool disableReadWrite = true;
        public bool disableAnimations = true;
        public bool disableBlendShapes = true;
        public bool disableCameras = true;
        public bool disableLights = true;
        public bool optimizeMesh = true;
        public ModelImporterMeshCompression meshCompression =
            ModelImporterMeshCompression.Medium;

        [Header("Catalog")]
        public string[] tags = Array.Empty<string>();
    }
}
