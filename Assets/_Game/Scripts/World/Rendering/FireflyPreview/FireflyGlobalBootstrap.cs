using UnityEngine;

namespace LittleCastle.Rendering.Fireflies
{
    /// <summary>
    /// Config-driven, scene-independent activation without altering existing prefabs/scenes.
    /// Runs once per Play session. The renderer observes map/session changes as scenes load.
    /// </summary>
    internal static class FireflyGlobalBootstrap
    {
        private static GameObject instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatic()
        {
            instance=null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfEnabled()
        {
            var config=Resources.Load<FireflyGlobalSettings>("Fireflies/GlobalFireflies");
            if(!config || !config.enabledEverywhere || !config.profile ||
               !config.profile.enabled || !config.profile.automaticWorldPlacement ||
               config.quality==null || config.quality.quality==FireflyPreviewQuality.Off)
                return;
            if(instance || Object.FindAnyObjectByType<FireflyPreviewRenderer>())
                return;

            instance=new GameObject("Little Castle Fireflies (automatic, unsaved)");
            instance.hideFlags=HideFlags.DontSave;
            Object.DontDestroyOnLoad(instance);
            var renderer=instance.AddComponent<FireflyPreviewRenderer>();
            renderer.Connect(config.profile);
            renderer.quality=config.quality;
            renderer.UseShader(config.previewShader);
        }
    }
}
