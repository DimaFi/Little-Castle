using LittleCastle.Rendering.Fireflies;
using UnityEditor;

namespace LittleCastle.Editor
{
    // Render requests in Edit Mode need not advance Time.frameCount.
    [InitializeOnLoad]
    static class FireflyPreviewEditorClock
    {
        static FireflyPreviewEditorClock()
        {
            EditorApplication.update+=FireflyPreviewRenderer.AdvanceEditorBuildFrame;
            AssemblyReloadEvents.beforeAssemblyReload+=Detach;
        }
        static void Detach()
        {
            EditorApplication.update-=FireflyPreviewRenderer.AdvanceEditorBuildFrame;
            AssemblyReloadEvents.beforeAssemblyReload-=Detach;
        }
    }
}
