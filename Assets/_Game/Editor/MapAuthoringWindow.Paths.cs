using LittleCastle.Rendering;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        [SerializeField] string pathMaterialId="ground.2";
        void DrawPathPalette()
        {
            var catalog=MapPathCatalog.Default;
            if(!catalog){EditorGUILayout.HelpBox("Каталог дорожек пока не импортирован.",MessageType.Info);return;}
            EditorGUILayout.LabelField("Покрытие дорожки",EditorStyles.boldLabel);
            int columns=Mathf.Clamp(Mathf.FloorToInt((position.width-36)/112),1,4);
            for(int row=0;row<(catalog.Count+columns-1)/columns;row++)
            {
                using(new EditorGUILayout.HorizontalScope())for(int col=0;col<columns;col++)
                {
                    int index=row*columns+col;if(index>=catalog.Count)break;var entry=catalog.entries[index];
                    var previous=GUI.backgroundColor;
                    if(catalog.IndexOf(pathMaterialId)==index)GUI.backgroundColor=new Color(.55f,.85f,.6f);
                    if(GUILayout.Button(new GUIContent(entry.title,entry.thumbnail),new GUIStyle(GUI.skin.button){imagePosition=ImagePosition.ImageAbove,wordWrap=true},GUILayout.Width(104),GUILayout.Height(104)))
                    {tools.Complete();pointerHeld=false;pathMaterialId=entry.id;}
                    GUI.backgroundColor=previous;
                }
            }
            EditorGUILayout.HelpBox("Снег ложится поверх выбранного покрытия автоматически. Смена биома сохраняет рисунок дорожки.",MessageType.None);
        }
    }
}
