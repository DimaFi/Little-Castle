using LittleCastle.Rendering;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    [CustomEditor(typeof(MeadowScatterLibrary))]
    public sealed class MeadowScatterLibraryInspector:UnityEditor.Editor
    {
        readonly bool[] open={true,false,false,false,false};
        static readonly string[] titles={"Цветы","Камни","Лесная мелочь","Трава и клевер","Подлесок и кусты"};
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Настройки видов общие для мира и кистей. После правки в Play Mode: меню компонента MeadowScatterRenderer → Rebuild meadow flower placements. Процедурные чанки используют новые параметры при следующем создании.",MessageType.Info);
            var types=serializedObject.FindProperty("prototypes");
            for(int group=0;group<titles.Length;group++)
            {
                int count=0;for(int i=0;i<types.arraySize;i++)if(types.GetArrayElementAtIndex(i).FindPropertyRelative("category").enumValueIndex==group)count++;
                open[group]=EditorGUILayout.Foldout(open[group],titles[group]+" ("+count+")",true);
                if(!open[group])continue;EditorGUI.indentLevel++;
                for(int i=0;i<types.arraySize;i++)
                {
                    var type=types.GetArrayElementAtIndex(i);if(type.FindPropertyRelative("category").enumValueIndex!=group)continue;
                    EditorGUILayout.PropertyField(type,new GUIContent(type.FindPropertyRelative("stableId").stringValue),true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("preflightNotes"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
