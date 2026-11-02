using LittleCastle.Rendering;
using LittleCastle.World.Environment;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleCastle.World
{
    /// <summary>Local art-scene presentation. No authoring map, save, weather simulation or second clock.</summary>
    [ExecuteAlways, DefaultExecutionOrder(-20)]
    public sealed class SnowVillagePresentation : MonoBehaviour
    {
        public Camera sceneCamera;
        public WorldTimeSystem clock;
        public DayNightLightingController atmosphere;
        public StylizedLightingGlobals lightingGlobals;
        [Range(0, 24)] public float hour;
        [Range(0, 1)] public float snowAmount = .9f;
        public Vector3 villageOrigin = new Vector3(280, 0, 280);
        MapEditSession environmentSession;
        MapEnvironmentPresentation environment;
        MapEnvironmentPresentation previousEnvironment;
        float appliedHour = -1, appliedSnow = -1;

        bool IsActiveScene => gameObject.scene.IsValid() && SceneManager.GetActiveScene() == gameObject.scene;
        public int PendingSnowUploads => environment?.PendingUploadTiles ?? 0;

        void OnEnable() { if (IsActiveScene) RefreshPresentation(); }
        void Update()
        {
            if (!IsActiveScene) { Release(); return; }
            RefreshPresentation();
        }

        public void PresentAtHour(float value)
        {
            hour = Mathf.Repeat(value, 24);
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            if (!IsActiveScene || !sceneCamera || !clock || !atmosphere || !lightingGlobals) return;
            if (environment == null)
            {
                var document = new MapAuthoringDocument {mapId = "snow-village-art-scene",
                    bounds = new Vector4(villageOrigin.x - 128, villageOrigin.z - 64, 256, 256)};
                environmentSession = new MapEditSession(document);
                environmentSession.BeginEnvironmentStroke(EnvironmentThemeIds.Winter);
                environmentSession.ApplyEnvironmentBrush(new Vector2(villageOrigin.x, villageOrigin.z + 32), 128, 1, 0);
                environmentSession.CommitStroke();
                previousEnvironment = MapEnvironmentPresentation.ActivePresentation;
                environment = new MapEnvironmentPresentation(environmentSession);
            }
            if (appliedSnow != snowAmount)
            {
                environmentSession.SetEnvironmentSettings(new MapEnvironmentSettings {
                    snowAmount = Mathf.Clamp01(snowAmount), winterSnowPreview = true});
                appliedSnow = snowAmount;
            }
            // The scene's existing WorldTimeSystem remains its only clock.
            clock.RunAutomatically = false;
            if (clock.TryInitialize() && appliedHour != hour)
            {
                clock.RestoreState(new WorldTimeState(1, Mathf.Repeat(hour, 24) * 60.0));
                appliedHour = hour;
            }
            environment.Tick(sceneCamera);
            atmosphere.SetLocalEnvironmentPresentation(snowAmount, 0);
            atmosphere.ApplyLighting();
            lightingGlobals.ApplyGlobals();
            if (!Application.isPlaying)
            {
                var budget = GetComponent<NightLightBudgetManager>();
                if (budget)
                {
                    var emitters = GetComponentsInScene<NightLightEmitter>();
                    budget.EvaluatePreviewBudget(emitters);
                    // Edit-mode render requests have no LateUpdate. Advance
                    // only this scene's bounded lights to their preview state.
                    foreach (var emitter in emitters)
                        emitter.TickPresentation(.1f);
                }
            }
        }

        T[] GetComponentsInScene<T>() where T : Component
        {
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return new T[0];
            var result = new System.Collections.Generic.List<T>();
            foreach (var root in gameObject.scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
            return result.ToArray();
        }

        void Release()
        {
            if (environment != null && !Application.isPlaying)
                foreach (var emitter in GetComponentsInScene<NightLightEmitter>())
                {
                    emitter.ForceOffImmediate();
                    if (emitter.PoolVisual) emitter.PoolVisual.SetPresentationVisible(false);
                }
            bool owned = environment != null && MapEnvironmentPresentation.ActivePresentation == environment;
            environment?.Dispose(); environment = null; environmentSession = null;
            if (owned) previousEnvironment?.RestorePresentationOwnership();
            previousEnvironment = null;
            appliedHour = appliedSnow = -1;
        }
        void OnDisable() => Release();
        void OnDestroy() => Release();
    }
}
