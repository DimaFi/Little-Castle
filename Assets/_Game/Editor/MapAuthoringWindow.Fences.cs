using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        [SerializeField] int fenceVariant;
        [SerializeField] float fenceSpacing=1.6f,fenceSmoothing=.85f;
        [SerializeField] float fenceScale=1;
        [SerializeField] long selectedFence;
        MapFencePresentation fencePreview;
        static readonly string[] FenceNames={"Дерево №1","Дерево №2"};
        string FenceVariantId=>fenceVariant==0?"fence.wood01":"fence.wood02";

        [MenuItem("Little Castle/World Editor/Fence Brush")]
        static void OpenFenceBrush()
        {
            var window=GetWindow<MapAuthoringWindow>("World Editor");
            if(window.session==null)window.RestoreWindowSession();
            if(window.session!=null)window.SetTool("fence.draw");
            window.Show();
        }
        void RegisterFenceTools(MapToolRegistry registry)
        {
            registry.Register(new MapFenceTool());
            registry.Register(new MapClickTool("fence.pick","Выбрать забор","Заборы",p=>
            {
                selectedFence=0;float distance=1;
                foreach(var path in session.QueryFencePaths(new Rect(p.position.x-1,p.position.y-1,2,2)))
                {
                    float next=path.Distance(p.position);if(next>=distance)continue;distance=next;selectedFence=path.record.id;
                }
                if(selectedFence!=0){var r=session.GetFence(selectedFence);fenceVariant=r.variantId=="fence.wood01"?0:1;fenceSpacing=r.spacing;fenceSmoothing=r.smoothing;fenceScale=r.scale;}
            }));
        }
        void DrawFenceTools()
        {
            if(!MapFenceCatalog.Default)
            {
                EditorGUILayout.HelpBox("Для заборов нужны два выбранных материала и модели из Blender.",MessageType.Info);
                if(GUILayout.Button("Подготовить модели забора"))Run(()=>{FenceAuthoringSetup.Ensure();BindFencePreview();});
                return;
            }
            fenceVariant=EditorGUILayout.Popup("Дерево",fenceVariant,FenceNames);
            fenceScale=EditorGUILayout.Slider("Масштаб",fenceScale,.5f,1.5f);
            EditorGUILayout.LabelField("Высота над землёй: "+(1.1f*fenceScale).ToString("F2")+" м",EditorStyles.miniLabel);
            fenceSpacing=EditorGUILayout.Slider("Шаг столбиков, м",fenceSpacing,.8f,2.5f);
            fenceSmoothing=EditorGUILayout.Slider("Плавность линии",fenceSmoothing,.2f,1);
            EditorGUILayout.HelpBox("Зажмите ЛКМ и проведите линию по земле. Отпустите — забор готов. Начните в пределах 35 см от свободного конца, чтобы продолжить существующий забор с его деревом и размером. Прямые участки остаются прямыми, повороты плавно изгибают перекладины. Подведите к противоположному концу, чтобы замкнуть линию.",MessageType.None);
            EditorGUILayout.LabelField("Линий на карте: "+session.FenceCount,EditorStyles.miniLabel);
            if(selectedFence==0||!session.HasFence(selectedFence))return;
            var record=session.GetFence(selectedFence);var path=new MapFencePath(record);
            EditorGUILayout.LabelField("Выбран: "+FenceNames[record.variantId=="fence.wood01"?0:1]+" · "+path.Length.ToString("F1")+" м",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Применить дерево и форму"))Run(()=>
                {record.variantId=FenceVariantId;record.spacing=fenceSpacing;record.smoothing=fenceSmoothing;record.scale=fenceScale;session.UpsertFence(record);});
                if(GUILayout.Button("Удалить выбранный"))Run(()=>{session.DeleteFence(selectedFence);selectedFence=0;});
            }
            EditorGUILayout.HelpBox("Заборы сохраняются теми же кнопками JSON и «Применить к авторской базе», что и остальная карта. Отмена / повтор работают для целого штриха.",MessageType.None);
        }
        void BindFencePreview()
        {fencePreview?.Dispose();fencePreview=null;var catalog=MapFenceCatalog.Default;if(session!=null&&catalog)fencePreview=new MapFencePresentation(session,catalog);}
        void TickFencePreview()
        {
            if(EditorApplication.isPlaying)return;
            var view=SceneView.lastActiveSceneView;
            if(fencePreview!=null&&view)fencePreview.Tick(view.pivot);
        }
        void DrawFenceDraft()
        {
            if(toolCategory!=5)return;var draft=session.FenceDraft;
            if(draft==null&&selectedFence!=0&&session.HasFence(selectedFence))draft=session.GetFence(selectedFence);
            if(draft==null||draft.points.Count<2)return;
            try
            {
                draft.points=MapFencePath.Simplify(draft.points);var path=new MapFencePath(draft);
                var line=new List<Vector3>();int step=Mathf.Max(1,path.frames.Count/2048);
                for(int i=0;i<path.frames.Count;i+=step)
                {var p=path.frames[i].position;line.Add(new Vector3(p.x,session.SampleHeight(p.x,p.y)+.06f,p.y));}
                var end=path.At(path.Length).position;line.Add(new Vector3(end.x,session.SampleHeight(end.x,end.y)+.06f,end.y));
                Handles.color=new Color(.95f,.72f,.2f,1);Handles.DrawAAPolyLine(3,line.ToArray());
                for(int i=0;i<Mathf.Min(200,path.PostCount);i++)
                {var p=path.At(i*path.ActualSpacing).position;Handles.DrawWireDisc(new Vector3(p.x,session.SampleHeight(p.x,p.y)+.05f,p.y),Vector3.up,.12f);}
            }
            catch(InvalidDataException){/* A still-short/unfinished pointer stroke has no mesh yet. */}
        }
    }
}
