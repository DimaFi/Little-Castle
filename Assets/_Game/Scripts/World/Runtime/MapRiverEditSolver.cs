using System;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed class MapRiverEditSolution
    {
        public MapRiverRecord river;
        public MapEditSession preview;
        public string message;
        public int attempts;
    }
    public static class MapRiverEditSolver
    {
        // Fixed search order and finite budgets. Author positions and handle recipes are retained.
        public const int MaximumAttempts=12;
        public static bool TrySolve(MapEditSession session,MapRiverRecord input,out MapRiverEditSolution result,out string error,bool allowCorrection=true)
        {
            result=null;error="";
            if(session.HasPending){error="Завершите текущий штрих.";return false;}
            if((session.Access&MapEditAccess.Rivers)==0){error="Редактирование рек выключено для этой роли.";return false;}
            if(input==null||input.nodes==null||input.nodes.Count<2){error="Нужны минимум две точки.";return false;}
            var original=MapRiverNodeEditing.Copy(input);
            foreach(var n in original.nodes)
            {
                if(n==null||!float.IsFinite(n.width)||!float.IsFinite(n.depth)||!float.IsFinite(n.speed)){error="Неверные параметры точки.";return false;}
                n.width=Mathf.Clamp(n.width,original.layoutVersion==3?2:4,60);n.depth=Mathf.Clamp(n.depth,.15f,10);n.speed=Mathf.Clamp(n.speed,original.layoutVersion==3?.02f:.05f,6);
            }
            var map=session.ExportMap();
            for(int attempt=0;attempt<(allowCorrection&&original.layoutVersion==3?MaximumAttempts:1);attempt++)
            {
                var candidate=MapRiverNodeEditing.Copy(original);
                if(attempt>0)
                {
                    if(!MapRiverSpline.HasShape(candidate))candidate.shape=new MapRiverShape{enabled=true};
                    candidate.shape.smoothing=Mathf.Min(candidate.shape.smoothing,new[]{1f,.85f,.65f,.45f}[Mathf.Min(attempt,3)]);
                    if(attempt>=4)
                    {
                        for(int i=1;i<candidate.nodes.Count-1;i++)
                        {
                            var a=candidate.nodes[i].position-candidate.nodes[i-1].position;var b=candidate.nodes[i+1].position-candidate.nodes[i].position;
                            if(Vector2.Angle(a,b)<10)continue;
                            var adjustment=candidate.shape.corridor.Find(v=>v.nodeId==candidate.nodes[i].id);
                            if(adjustment==null){adjustment=new MapRiverCorridorAdjustment{nodeId=candidate.nodes[i].id};candidate.shape.corridor.Add(adjustment);}
                            adjustment.transitionScale=.5f;if(attempt>=5)adjustment.widthScale=.85f;
                        }
                    }
                    if(attempt>=6)
                    {
                        int segment=(candidate.nodes.Count-2)/2;
                        Vector2 direction=new[]{Vector2.left,Vector2.right,Vector2.up,Vector2.down,new Vector2(.7071f,.7071f),new Vector2(-.7071f,-.7071f)}[attempt-6];
                        candidate.shape.spans.RemoveAll(s=>s.startNodeId==candidate.nodes[segment].id);
                        candidate.shape.spans.Add(new MapRiverSpanAdjustment{startNodeId=candidate.nodes[segment].id,offset=direction});
                    }
                }
                try
                {
                    var preview=new MapEditSession(map,session.Access);preview.ApplyRiverEdit(candidate);
                    result=new MapRiverEditSolution{river=preview.GetRiver(candidate.id),preview=preview,attempts=attempt+1,message=attempt==0?"Готово":"Автокоррекция: сохранены точки; подстроены изгиб и локальное сечение в заданных пределах."};return true;
                }
                catch(Exception e)when(e is InvalidDataException||e is InvalidOperationException||e is ArgumentException||e is UnauthorizedAccessException)
                {
                    error=e.Message;
                    // These cases must remain explicit failures, never bypass protected content.
                    if(error.Contains("защищ")||error.Contains("Перекры")||error.Contains("Цикл")||error.Contains("ID")||error.Contains("пределами")||error.Contains("2–1024"))break;
                }
            }
            return false;
        }
    }
}
