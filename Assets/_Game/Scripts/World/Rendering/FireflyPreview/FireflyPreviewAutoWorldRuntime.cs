using LittleCastle.Research;
using UnityEngine;

namespace LittleCastle.Rendering.Fireflies
{
    /// <summary>
    /// Runtime-only watcher installed by an explicit Resources profile, not a scene edit.
    /// It checks at most once per second and never creates per-firefly objects.
    /// </summary>
    public sealed class FireflyPreviewAutoWorldRuntime : MonoBehaviour
    {
        FireflyPreviewProfile selectedProfile;
        FireflyPreviewRenderer ownedRenderer;
        float nextProbe;

        internal void Configure(FireflyPreviewProfile asset)
        {
            selectedProfile=asset;
            nextProbe=0f;
        }

        void Update()
        {
            if(Time.unscaledTime<nextProbe)return;
            nextProbe=Time.unscaledTime+1f;

            if(!selectedProfile || !selectedProfile.enabled ||
                selectedProfile.automaticWorld==null ||
                !selectedProfile.automaticWorld.enabled ||
                !selectedProfile.automaticWorld.runtimeAutoStart)
            {
                if(ownedRenderer)
                {
                    Destroy(ownedRenderer.gameObject);
                    ownedRenderer=null;
                }
                return;
            }

            if(ownedRenderer)return;
            // This optional boot path only targets maps exposing the existing
            // MeadowSurfacePainter/MapEditSession read API. No world write APIs.
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            if(!painter || painter.Session==null)return;
            if(FindAnyObjectByType<FireflyPreviewRenderer>())return;
            var go=new GameObject("Fireflies: automatic world (Play only)");
            ownedRenderer=go.AddComponent<FireflyPreviewRenderer>();
            ownedRenderer.ConfigureRuntime(selectedProfile);
        }

        void OnDestroy()
        {
            if(ownedRenderer)Destroy(ownedRenderer.gameObject);
            ownedRenderer=null;
        }
    }

    internal static class FireflyPreviewRuntimeBootstrap
    {
        // Opt-in only: no Resources asset means no allocations, watcher or effect.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if(!Application.isPlaying)return;
            var profile=Resources.Load<FireflyPreviewProfile>(
                "LittleCastle/FireflyAutoWorld");
            if(!profile || !profile.enabled || profile.automaticWorld==null ||
               !profile.automaticWorld.enabled ||
               !profile.automaticWorld.runtimeAutoStart)return;
            if(Object.FindAnyObjectByType<FireflyPreviewAutoWorldRuntime>())return;
            var go=new GameObject("Fireflies: optional runtime activation");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<FireflyPreviewAutoWorldRuntime>().Configure(profile);
        }
    }
}
