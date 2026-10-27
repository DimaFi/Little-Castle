using UnityEditor;

namespace LittleCastle.Editor
{
    internal static class UrpBatchExit
    {
        static int pendingCode;
        static double readyAt;
        public static void WhenIdle(int code)
        {
            pendingCode = code;
            readyAt = EditorApplication.timeSinceStartup + 5;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < readyAt || EditorApplication.isCompiling || EditorApplication.isUpdating || ShaderUtil.anythingCompiling) return;
            EditorApplication.update -= Tick;
            EditorApplication.Exit(pendingCode);
        }
    }
}
