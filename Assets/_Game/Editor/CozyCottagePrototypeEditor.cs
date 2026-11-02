#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Prototypes.Editor
{
    [CustomEditor(typeof(CozyCottagePrototype))]
    public sealed class CozyCottagePrototypeEditor : UnityEditor.Editor
    {
        private void OnEnable() => Undo.undoRedoPerformed += RefreshAfterUndo;
        private void OnDisable() => Undo.undoRedoPerformed -= RefreshAfterUndo;

        private void RefreshAfterUndo()
        {
            var house = target as CozyCottagePrototype;
            if (house != null) house.Rebuild();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Experimental cottage: the house is rebuilt as one combined mesh. " +
                "Edit dimensions here or drag the colored Scene View handles. " +
                "Window handles slide on the wall plane. No changes to gameplay buildings.",
                MessageType.Info);

            DrawDefaultInspector();
            var house = (CozyCottagePrototype)target;

            EditorGUILayout.Space(7);
            if (GUILayout.Button("Add editable window"))
            {
                Undo.RecordObject(house, "Add cottage window");
                house.AddWindow();
                EditorUtility.SetDirty(house);
            }

            using (new EditorGUI.DisabledScope(house.Windows == null || house.Windows.Count == 0))
            {
                if (GUILayout.Button("Remove last window"))
                {
                    Undo.RecordObject(house, "Remove cottage window");
                    house.Windows.RemoveAt(house.Windows.Count - 1);
                    house.Rebuild();
                    EditorUtility.SetDirty(house);
                }
            }

            if (GUILayout.Button("Rebuild geometry"))
                house.Rebuild();
        }

        private void OnSceneGUI()
        {
            var house = (CozyCottagePrototype)target;
            Transform transform = house.transform;
            Vector3 Point(Vector3 local) => transform.TransformPoint(local);
            Vector3 right = transform.right, forward = transform.forward, up = transform.up;

            // Symmetric house stretch; do not stretch the old triangles.
            EditScalar(house, "House width", Point(new Vector3(house.Width * .5f,
                house.WallHeight * .45f, 0f)), right, new Color(.35f, .84f, .95f),
                moved => house.Width = 2f * transform.InverseTransformPoint(moved).x);

            EditScalar(house, "House depth", Point(new Vector3(0f,
                house.WallHeight * .45f, -house.Depth * .5f)),
                -forward, new Color(.35f, .84f, .95f),
                moved => house.Depth = -2f * transform.InverseTransformPoint(moved).z);

            EditScalar(house, "Wall height", Point(new Vector3(-house.Width * .5f,
                house.WallHeight, -house.Depth * .5f)), up, Color.yellow,
                moved => house.WallHeight = transform.InverseTransformPoint(moved).y);

            EditScalar(house, "Roof peak", Point(new Vector3(0f,
                house.WallHeight + house.RoofRise, 0f)), up,
                new Color(1f, .58f, .30f),
                moved => house.RoofRise =
                    transform.InverseTransformPoint(moved).y - house.WallHeight);

            float yEave = house.WallHeight -
                house.RoofRise * house.RoofOverhang / (house.Depth * .5f);
            EditScalar(house, "Roof eave", Point(new Vector3(0f, yEave,
                house.Depth * .5f + house.RoofOverhang)), forward,
                new Color(1f, .58f, .30f),
                moved => house.RoofOverhang =
                    transform.InverseTransformPoint(moved).z - house.Depth * .5f);

            if (house.ShowExtension)
            {
                Vector3 end = house.GetExtensionEnd();
                EditScalar(house, "Wall / fence length", end, right,
                    new Color(.44f, .90f, .54f),
                    moved => house.ExtensionLength =
                        transform.InverseTransformPoint(moved).x - house.Width * .5f);
                EditScalar(house, "Wall / fence height", end, up,
                    new Color(.44f, .90f, .54f),
                    moved => house.ExtensionHeight =
                        transform.InverseTransformPoint(moved).y);
            }

            if (house.Windows == null) return;
            for (int i = 0; i < house.Windows.Count; i++)
            {
                var w = house.Windows[i];
                if (w == null) continue;
                Vector3 center = house.GetWindowCenter(w);
                Vector3 normal = w.face == CozyCottagePrototype.WallFace.Front
                    ? -forward : w.face == CozyCottagePrototype.WallFace.Back
                    ? forward : w.face == CozyCottagePrototype.WallFace.Left
                    ? -right : right;
                Vector3 tangent = Vector3.Cross(up, normal);
                Handles.color = new Color(.9f, .42f, .88f);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider2D(center, normal, tangent, up,
                    HandleUtility.GetHandleSize(center) * .13f,
                    Handles.RectangleHandleCap, Vector2.zero);
                Handles.Label(center + up * .24f, "Window " + (i + 1));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(house, "Move cottage window");
                    house.SetWindowCenter(w, moved);
                    EditorUtility.SetDirty(house);
                    house.Rebuild();
                }
            }
        }

        private static void EditScalar(CozyCottagePrototype house, string label,
            Vector3 worldPoint, Vector3 direction, Color color,
            System.Action<Vector3> apply)
        {
            Handles.color = color;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(worldPoint, direction,
                HandleUtility.GetHandleSize(worldPoint) * .14f,
                Handles.CubeHandleCap, .05f);
            Handles.Label(worldPoint + Vector3.up * .23f, label);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(house, "Edit cozy cottage: " + label);
            apply(moved);
            EditorUtility.SetDirty(house);
            house.Rebuild();
        }

        [MenuItem("GameObject/Little Castle/Prototype/Cozy Cottage", false, 10)]
        private static void CreateCottage(MenuCommand command)
        {
            var go = new GameObject("Cozy Cottage - Procedural Study");
            Undo.RegisterCreatedObjectUndo(go, "Create cozy cottage prototype");
            if (command.context is GameObject parent)
                GameObjectUtility.SetParentAndAlign(go, parent);
            else if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;

            go.AddComponent<CozyCottagePrototype>();
            Selection.activeGameObject = go;
            SceneView.FrameLastActiveSceneView();
        }
    }
}
#endif
