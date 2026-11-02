using System;
using System.Collections.Generic;
using LittleCastle.World;
using LittleCastle.Rendering;
using LittleCastle.Research;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        [SerializeField] bool riverSplineHandles,riverWholeScope,riverAutoCorrect=true,riverLinkedHandles=true;
        [SerializeField] bool riverDirectEditingEnabled;
        MapRiverEditGesture riverGesture;
        MapEditSession riverGestureSession;
        MapTerrainPresentation riverGestureTerrain;
        MapRiverPresentation riverGestureWater;
        MapTreePresentation riverGestureTrees;
        bool riverPreviewVisible,riverPreviewPending;
        double nextRiverPreview;
        long pickRevision=-1;List<MapRiverPath> pickPaths;
        string riverCurveKey;MapRiverPath riverCurve;
        Texture2D riverGestureMask;byte[] riverGesturePixels;
        Rect riverGestureMaskArea;
        double lastRiverRebindMilliseconds,lastRiverMaskMilliseconds;
        bool riverGestureTerrainWasBudgeted,riverMaskPending;
        RectInt riverMaskRect;int riverMaskRow;
        List<SurfacePaintStroke> riverMaskStrokes;
        bool RiverPreviewWorkPending=>riverMaskPending||riverGestureTerrain?.HasPendingPreview==true;
        void DrawDirectRiverInspector()
        {
            var draft=riverTools.draft;if(draft==null||draft.nodes.Count==0||riverTools.mode==5)return;
            GUILayout.Space(6);EditorGUILayout.LabelField("Прямое редактирование реки",EditorStyles.boldLabel);
            bool spline=GUILayout.SelectionGrid(riverSplineHandles?1:0,new[]{"Точки","Сплайн и ручки"},2)==1;
            if(spline!=riverSplineHandles){riverSplineHandles=spline;Repaint();GUIUtility.ExitGUI();}
            riverWholeScope=GUILayout.SelectionGrid(riverWholeScope?1:0,new[]{"Эта точка","Вся река"},2)==1;
            bool automatic=EditorGUILayout.Toggle("Автокоррекция",riverAutoCorrect);
            if(automatic!=riverAutoCorrect){riverAutoCorrect=automatic;Repaint();GUIUtility.ExitGUI();}
            if(riverSplineHandles)riverLinkedHandles=EditorGUILayout.Toggle("Связанные ручки",riverLinkedHandles);
            if(riverAutoCorrect)EditorGUILayout.HelpBox("Пределы: сужение до 15%, переход берега до 50%, обход до 1 м. Авторские точки сохраняются.",MessageType.None);
            riverTools.node=EditorGUILayout.IntSlider("Точка",riverTools.node+1,1,draft.nodes.Count)-1;
            int index=riverTools.node;var n=draft.nodes[index];
            EditorGUI.BeginChangeCheck();float width=EditorGUILayout.DelayedFloatField("Ширина, м",n.width),depth=EditorGUILayout.DelayedFloatField("Глубина, м",n.depth),speed=EditorGUILayout.DelayedFloatField("Скорость, м/с",n.speed),level=EditorGUILayout.DelayedFloatField("Отметка воды, м",n.waterHeight);
            if(EditorGUI.EndChangeCheck())
            {
                var candidate=MapRiverNodeEditing.Copy(draft);
                for(int i=0;i<candidate.nodes.Count;i++)if(riverWholeScope||i==index)
                {if(width!=n.width)candidate.nodes[i].width=width;if(depth!=n.depth)candidate.nodes[i].depth=depth;if(speed!=n.speed)candidate.nodes[i].speed=speed;if(level!=n.waterHeight)candidate.nodes[i].waterHeight=level;}
                ApplyDirectRiverCandidate(candidate);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Вставить между узлами"))
                {try{var candidate=MapRiverNodeEditing.Copy(draft);int selected=MapRiverNodeEditing.Insert(candidate,Mathf.Min(index,draft.nodes.Count-2),.5f,MapObjectIds.New());if(ApplyDirectRiverCandidate(candidate))riverTools.node=selected;}catch(Exception e){riverTools.status=e.Message;}GUIUtility.ExitGUI();}
                using(new EditorGUI.DisabledScope(draft.nodes.Count<=2))if(GUILayout.Button("Удалить точку"))
                {try{var candidate=MapRiverNodeEditing.Copy(draft);MapRiverNodeEditing.Remove(candidate,index);ApplyDirectRiverCandidate(candidate);}catch(Exception e){riverTools.status=e.Message;}GUIUtility.ExitGUI();}
            }
            if(!riverTools.IsCreatingRiver)
            {
                using(new EditorGUILayout.HorizontalScope())
                {if(GUILayout.Button("Продолжить с начала"))BeginRiverContinuation(true);if(GUILayout.Button("Продолжить с конца"))BeginRiverContinuation(false);}
            }
            if(draft.layoutVersion!=3&&riverSplineHandles)EditorGUILayout.HelpBox("Для сохранённых ручек сначала перейдите на мягкое русло кнопкой ниже.",MessageType.Info);
        }
        bool ApplyDirectRiverCandidate(MapRiverRecord candidate)
        {
            riverTools.draft=candidate;
            if(riverTools.IsCreatingRiver){Repaint();SceneView.RepaintAll();return true;}
            try
            {
                if(!MapRiverEditSolver.TrySolve(session,candidate,out var solved,out var error,riverAutoCorrect)){riverTools.status=error;return false;}
                int selected=riverTools.node;session.ApplyRiverEdit(solved.river);riverTools.SelectRiver(session,candidate.id);riverTools.node=Mathf.Clamp(selected,0,riverTools.draft.nodes.Count-1);riverTools.status=solved.message;
                MarkDocumentDirty();pickRevision=-1;Repaint();SceneView.RepaintAll();return true;
            }
            catch(Exception e){riverTools.status=e.Message;return false;}
        }
        void BeginRiverContinuation(bool upstream)
        {
            if(riverTools.HasUnappliedDraft(session)){riverTools.status="Сначала примените или отмените черновик.";return;}
            foreach(var fall in session.ExportRiverNetwork().waterfalls)
                if(upstream?fall.downstreamRiverId==riverTools.draft.id:fall.upstreamRiverId==riverTools.draft.id){riverTools.status="Этот конец связан с водопадом; выберите свободный конец.";return;}
            riverTools.BeginContinuation(upstream);SetTool("river.points");GUIUtility.ExitGUI();
        }
        bool DirectRiverSceneGUI(SceneView view)
        {
            var e=Event.current;
            bool released=e.rawType==EventType.MouseUp&&e.button==0;
            if(riverGesture!=null&&(toolCategory!=3||CurrentToolId!="river.points")){CancelRiverGesture();return false;}
            if(!riverDirectEditingEnabled||toolCategory!=3||CurrentToolId!="river.points"||riverTools.mode==3||riverTools.mode==4||riverTools.mode==5)return false;
            if(riverGesture!=null&&(e.type==EventType.Ignore||e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape)){CancelRiverGesture();e.Use();return true;}
            if(e.alt||e.button==1||e.button==2)return false;
            var draft=riverTools.draft;
            if(draft!=null&&draft.nodes.Count>=2)DrawResolvedRiverCurve(riverGesture?.LastValid?.river??draft);
            int beforeControl=GUIUtility.hotControl;
            if(draft!=null&&draft.nodes.Count>0)DrawRiverNodeHandles(draft);
            if(riverGesture!=null&&(released||beforeControl!=0&&GUIUtility.hotControl==0))
            {UpdateRiverGesturePreview(true);FinishRiverGesture();e.Use();return true;}
            if(e.type==EventType.Used||beforeControl!=GUIUtility.hotControl||GUIUtility.hotControl!=0)return true;
            if(e.type==EventType.MouseDown&&e.button==0&&riverTools.mode!=1)
            {
                if(pickPaths==null||pickRevision!=session.RiverRevision){pickPaths=session.QueryRiverPaths(new Rect(session.Bounds.x,session.Bounds.y,session.Bounds.z,session.Bounds.w));pickRevision=session.RiverRevision;}
                if(MapRiverPicking.Pick(pickPaths,HandleUtility.GUIPointToWorldRay(e.mousePosition),out long id,out var hit,out _))
                {
                    if(riverTools.HasUnappliedDraft(session)){riverTools.status="Сначала примените или отмените черновик.";e.Use();return true;}
                    riverTools.SelectRiver(session,id);draft=riverTools.draft;
                    float best=float.PositiveInfinity;int segment=0;float fraction=.5f;
                    for(int i=0;i<draft.nodes.Count-1;i++)for(int j=0;j<=32;j++)
                    {float t=j/32f;float distance=(MapRiverSpline.Evaluate(draft,i,t)-new Vector2(hit.x,hit.z)).sqrMagnitude;if(distance<best){best=distance;segment=i;fraction=t;}}
                    riverTools.node=fraction<.5f?segment:segment+1;
                    if(e.shift){try{var candidate=MapRiverNodeEditing.Copy(draft);int node=MapRiverNodeEditing.Insert(candidate,segment,fraction,MapObjectIds.New());if(ApplyDirectRiverCandidate(candidate))riverTools.node=node;}catch(Exception error){riverTools.status=error.Message;}}
                    brush=true;Repaint();view.Repaint();e.Use();return true;
                }
            }
            return false;
        }
        void DrawRiverNodeHandles(MapRiverRecord draft)
        {
            var previous=Handles.zTest;Handles.zTest=UnityEngine.Rendering.CompareFunction.Always;
            try
            {
                for(int i=0;i<draft.nodes.Count;i++)
                {
                    var n=draft.nodes[i];var p=new Vector3(n.position.x,n.waterHeight+.018f,n.position.y);float size=HandleUtility.GetHandleSize(p)*.075f;
                    Handles.color=i==riverTools.node?Color.yellow:Color.cyan;
                    if(Handles.Button(p,Quaternion.identity,size,size,Handles.SphereHandleCap)){riverTools.node=i;Repaint();}
                    Handles.Label(p+Vector3.up*size,(i+1).ToString());
                    if(i+1<draft.nodes.Count){var next=draft.nodes[i+1];Handles.DrawDottedLine(p,new Vector3(next.position.x,next.waterHeight+.018f,next.position.y),4);}
                }
                int index=Mathf.Clamp(riverTools.node,0,draft.nodes.Count-1);var point=draft.nodes[index];var center=new Vector3(point.position.x,point.waterHeight+.018f,point.position.y);
                EditorGUI.BeginChangeCheck();var moved=Handles.PositionHandle(center,Quaternion.identity);
                if(EditorGUI.EndChangeCheck())ChangeRiverHandle(r=>{r.nodes[index].position=new Vector2(moved.x,moved.z);r.nodes[index].waterHeight=moved.y-.018f;});
                if(draft.nodes.Count<2)return;
                var tangent=(index==0?draft.nodes[1].position-point.position:index==draft.nodes.Count-1?point.position-draft.nodes[index-1].position:draft.nodes[index+1].position-draft.nodes[index-1].position).normalized;
                var left=new Vector3(-tangent.y,0,tangent.x);float scale=HandleUtility.GetHandleSize(center)*.065f;
                foreach(int side in new[]{-1,1})
                {
                    EditorGUI.BeginChangeCheck();var edge=Handles.Slider(center+left*(point.width*.5f*side),left*side,scale,Handles.CubeHandleCap,0);
                    if(EditorGUI.EndChangeCheck()){float width=Mathf.Clamp(Mathf.Abs(Vector3.Dot(edge-center,left))*2,draft.layoutVersion==3?2:4,60);ChangeRiverHandle(r=>{for(int i=0;i<r.nodes.Count;i++)if(riverWholeScope||i==index)r.nodes[i].width=width;});}
                }
                Handles.color=Color.blue;Handles.DrawLine(center,center-Vector3.up*point.depth);
                EditorGUI.BeginChangeCheck();var bottom=Handles.Slider(center-Vector3.up*point.depth,Vector3.down,scale,Handles.CubeHandleCap,0);
                if(EditorGUI.EndChangeCheck()){float depth=Mathf.Clamp(center.y-bottom.y,.15f,10);ChangeRiverHandle(r=>{for(int i=0;i<r.nodes.Count;i++)if(riverWholeScope||i==index)r.nodes[i].depth=depth;});}
                if(riverSplineHandles&&draft.layoutVersion==3)
                foreach(bool outgoing in new[]{false,true})
                {
                    if(index==0&&!outgoing||index==draft.nodes.Count-1&&outgoing)continue;
                    var offset=MapRiverSpline.Handle(draft,index,outgoing);var position=center+new Vector3(offset.x,0,offset.y);Handles.color=Color.magenta;Handles.DrawLine(center,position);
                    EditorGUI.BeginChangeCheck();var handle=Handles.FreeMoveHandle(position,scale,Vector3.zero,Handles.DotHandleCap);
                    if(EditorGUI.EndChangeCheck())ChangeRiverHandle(r=>{var value=Vector2.ClampMagnitude(new Vector2(handle.x-center.x,handle.z-center.z),64);float other=MapRiverSpline.Handle(r,index,!outgoing).magnitude;MapRiverSpline.SetHandle(r,index,outgoing,value);if(riverLinkedHandles&&value.sqrMagnitude>.0001f)MapRiverSpline.SetHandle(r,index,!outgoing,-value.normalized*other);});
                }
            }
            finally{Handles.zTest=previous;}
        }
        void DrawResolvedRiverCurve(MapRiverRecord draft)
        {
            string key=JsonUtility.ToJson(draft);
            if(riverCurveKey!=key)
            {riverCurveKey=key;try{riverCurve=new MapRiverPath(draft);}catch{riverCurve=null;}}
            if(riverCurve==null)return;
            var center=new Vector3[riverCurve.frames.Count];var left=new Vector3[center.Length];var right=new Vector3[center.Length];
            for(int i=0;i<center.Length;i++)
            {
                var f=riverCurve.frames[i];center[i]=new Vector3(f.center.x,f.water+.03f,f.center.y);
                var l=f.Edge(1)+f.Left*f.leftTransition;var r=f.Edge(-1)-f.Left*f.rightTransition;
                left[i]=new Vector3(l.x,f.water+.035f,l.y);right[i]=new Vector3(r.x,f.water+.035f,r.y);
                if(i%40==0){Handles.color=Color.cyan;Handles.ArrowHandleCap(0,center[i],Quaternion.LookRotation(new Vector3(f.tangent.x,0,f.tangent.y)),.7f,EventType.Repaint);}
            }
            Handles.color=Color.cyan;Handles.DrawAAPolyLine(2,center);Handles.color=new Color(.4f,1,.3f,.55f);Handles.DrawAAPolyLine(1,left);Handles.DrawAAPolyLine(1,right);
        }
        void ChangeRiverHandle(Action<MapRiverRecord> change)
        {
            EnsureScenePreview();
            if(riverGesture==null)riverGesture=new MapRiverEditGesture(session,riverTools.draft);
            var candidate=MapRiverNodeEditing.Copy(riverTools.draft);change(candidate);riverTools.draft=candidate;riverPreviewPending=true;UpdateRiverGesturePreview(false);Repaint();SceneView.RepaintAll();
        }
        void UpdateRiverGesturePreview(bool force)
        {
            if(riverGesture==null||!riverPreviewPending||!force&&EditorApplication.timeSinceStartup<nextRiverPreview)return;
            nextRiverPreview=EditorApplication.timeSinceStartup+.2;riverPreviewPending=false;
            if(!riverGesture.Update(riverTools.draft,riverAutoCorrect)){riverTools.status=riverGesture.Message;return;}
            riverTools.status=riverGesture.Message;
            var previewWatch=MapRiverEditSolver.CaptureTimings?System.Diagnostics.Stopwatch.StartNew():null;
            if(!riverPreviewVisible)
            {
                // Reuse the already resident map. Only the edited corridor is recomposed.
                var current=new MapRiverPath(riverGesture.LastValid.river).bounds;
                var previous=new MapRiverPath(session.GetRiver(riverTools.draft.id)).bounds;
                riverGestureMaskArea=Rect.MinMaxRect(Mathf.Min(current.xMin,previous.xMin),
                    Mathf.Min(current.yMin,previous.yMin),Mathf.Max(current.xMax,previous.xMax),Mathf.Max(current.yMax,previous.yMax));
                foreach(var map in new[]{session,riverGesture.PreviewSession})
                    foreach(var fall in map.ExportRiverNetwork().waterfalls)
                        if((fall.upstreamRiverId==riverTools.draft.id||fall.downstreamRiverId==riverTools.draft.id)&&fall.route!=null&&!fall.route.IsEmpty)
                        {
                            var b=fall.route.Bounds;var a=riverGestureMaskArea;
                            riverGestureMaskArea=Rect.MinMaxRect(Mathf.Min(a.xMin,b.min.x),Mathf.Min(a.yMin,b.min.z),Mathf.Max(a.xMax,b.max.x),Mathf.Max(a.yMax,b.max.z));
                        }
                var dirty=riverGestureMaskArea;float pad=session.HeightSpacing*2;
                riverGestureMaskArea=Rect.MinMaxRect(dirty.xMin-pad,dirty.yMin-pad,dirty.xMax+pad,dirty.yMax+pad);
                riverGestureSession=riverGesture.PreviewSession;riverGestureSession.Changed+=TrackRiverGestureArea;
                riverGestureTerrain=terrainPreview;riverGestureWater=riverPreview;riverGestureTrees=treePreview;
                if(riverGestureTerrain!=null){riverGestureTerrainWasBudgeted=riverGestureTerrain.BudgetedUpdates;riverGestureTerrain.BudgetedUpdates=true;}
                riverGestureTerrain?.RebindSession(riverGesture.PreviewSession,riverGestureMaskArea);
                riverGestureWater?.RebindSession(riverGesture.PreviewSession,riverGestureMaskArea);
                riverGestureTrees?.RebindSession(riverGesture.PreviewSession,riverGestureMaskArea);
                riverGesturePixels=(byte[])pixels.Clone();riverGestureMask=preview;
                riverPreviewVisible=true;
            }
            lastRiverRebindMilliseconds=previewWatch?.Elapsed.TotalMilliseconds??0;previewWatch?.Restart();
            UpdateRiverGestureMask();
            lastRiverMaskMilliseconds=previewWatch?.Elapsed.TotalMilliseconds??0;
        }
        void TickRiverEditing()
        {
            if(riverGesture==null)return;
            if(EditorWindow.focusedWindow&&!(EditorWindow.focusedWindow is SceneView)){CancelRiverGesture();return;}
            UpdateRiverGesturePreview(false);
            FlushRiverPreviewBudget();
        }
        void FlushRiverPreviewBudget()
        {
            riverGestureTerrain?.FlushPreviewBudget(256,2);
            if(!riverMaskPending||riverGesture==null||!riverGestureMask)return;
            var watch=System.Diagnostics.Stopwatch.StartNew();bool changed=false;
            do
            {
                var row=new RectInt(riverMaskRect.x,riverMaskRow,riverMaskRect.width,1);
                SurfacePaintRasterizer.Rebuild(riverGesturePixels,source.bounds,Resolution,Resolution,riverMaskStrokes,row);
                SurfacePaintRasterizer.ApplyRiverExclusions(riverGesturePixels,source.bounds,Resolution,Resolution,riverGesture.PreviewSession,row);
                riverMaskRow++;changed=true;
            }while(riverMaskRow<riverMaskRect.yMax&&watch.Elapsed.TotalMilliseconds<2);
            if(riverMaskRow>=riverMaskRect.yMax){riverMaskPending=false;riverMaskStrokes=null;}
            if(changed){riverGestureMask.SetPixelData(riverGesturePixels,0);riverGestureMask.Apply(false,false);SceneView.RepaintAll();}
        }
        void FinishRiverGesture()
        {
            var gesture=riverGesture;if(gesture==null)return;
            bool applied=!riverTools.IsCreatingRiver&&gesture.Complete();var candidate=applied?gesture.Draft:riverTools.draft;
            string message=gesture.Message;int selected=riverTools.node;
            ClearRiverGesturePreview();riverGesture=null;
            if(applied){riverTools.SelectRiver(session,candidate.id);riverTools.node=Mathf.Clamp(selected,0,candidate.nodes.Count-1);MarkDocumentDirty();}else riverTools.draft=candidate;
            riverTools.status=message;Repaint();SceneView.RepaintAll();
        }
        void ClearRiverGesturePreview()
        {
            if(riverGestureSession!=null)riverGestureSession.Changed-=TrackRiverGestureArea;
            riverGestureSession=null;
            if(riverPreviewVisible)
            {
                if(riverGestureTerrain!=null)riverGestureTerrain.BudgetedUpdates=riverGestureTerrainWasBudgeted;
                // Unloaded scene objects cannot be rebound. Their owning window disposes and
                // recreates the complete view; the live session and authored draft are retained.
                if(riverGestureTerrain==null||riverGestureTerrain.IsValid)
                {
                    riverGestureTerrain?.RebindSession(session,riverGestureMaskArea);
                    riverGestureWater?.RebindSession(session,riverGestureMaskArea);
                    riverGestureTrees?.RebindSession(session,riverGestureMaskArea);
                }
                if(preview){preview.SetPixelData(pixels,0);preview.Apply(false,false);}
            }
            riverGestureTerrain=null;riverGestureWater=null;riverGestureTrees=null;
            riverGestureMask=null;riverGesturePixels=null;riverGestureMaskArea=default;
            riverMaskPending=false;riverMaskStrokes=null;riverMaskRect=default;riverMaskRow=0;
            riverPreviewVisible=false;riverPreviewPending=false;
        }
        void TrackRiverGestureArea(MapRegionChange change)
        {
            var a=riverGestureMaskArea;var b=change.bounds;
            riverGestureMaskArea=Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin),Mathf.Min(a.yMin,b.yMin),Mathf.Max(a.xMax,b.xMax),Mathf.Max(a.yMax,b.yMax));
        }
        void UpdateRiverGestureMask()
        {
            if(!riverGestureMask)return;var recipe=riverGesture.LastValid.river;var current=new MapRiverPath(recipe).bounds;
            if(riverGestureMaskArea.width==0)riverGestureMaskArea=new MapRiverPath(session.GetRiver(recipe.id)).bounds;
            var area=Rect.MinMaxRect(Mathf.Min(current.xMin,riverGestureMaskArea.xMin),Mathf.Min(current.yMin,riverGestureMaskArea.yMin),Mathf.Max(current.xMax,riverGestureMaskArea.xMax),Mathf.Max(current.yMax,riverGestureMaskArea.yMax));
            var rect=SurfacePaintRasterizer.PixelRect(source.bounds,Resolution,Resolution,area);
            // Coalesce pending rows to the latest detached session. The live pixels remain untouched.
            riverMaskRect=rect;riverMaskRow=rect.yMin;riverMaskStrokes=riverGesture.PreviewSession.QuerySurface(area);
            riverMaskPending=rect.width>0&&rect.height>0;riverGestureMaskArea=area;
        }
        void CancelRiverGesture(bool rebind=true)
        {
            if(riverGesture==null)return;riverTools.draft=riverGesture.Cancel();riverGesture=null;ClearRiverGesturePreview();
            riverTools.status="Перетаскивание отменено.";Repaint();SceneView.RepaintAll();
        }
    }
}
