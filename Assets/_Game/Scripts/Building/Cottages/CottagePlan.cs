using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Building.Cottages
{
    public enum CottageSide { West, East, Back, Front }
    public enum CottagePhase { Foundation, Frame, Walls, Roof, Detail }

    [Serializable]
    public sealed class CottagePlan
    {
        public long houseId = 1;
        public Vector3 position;
        public float yaw;
        public int west, east, back, front;
        public int floors = 1;
        public int seed = 731;
        public CottagePlan Copy() => (CottagePlan)MemberwiseClone();
        public bool SameShape(CottagePlan other) => other != null && west == other.west && east == other.east && back == other.back && front == other.front && floors == other.floors;
        public int Steps(CottageSide side) => side == CottageSide.West ? west : side == CottageSide.East ? east : side == CottageSide.Back ? back : front;
        public void SetSteps(CottageSide side, int value)
        {
            switch (side) { case CottageSide.West: west=value; break; case CottageSide.East: east=value; break; case CottageSide.Back: back=value; break; default: front=value; break; }
        }
    }

    [Serializable]
    public sealed class CottageRuntimeState
    {
        public CottagePlan completed;
        public CottagePlan target;
        public double workHours;
        public double requiredHours = 1;
        public bool paused;
        public bool Building => target != null;
        public float Progress => target == null ? 1f : Mathf.Clamp01((float)(workHours / Math.Max(.0001, requiredHours)));
        public bool TryBegin(CottagePlan plan, CottageDefinition definition, out string error)
        {
            if (Building) { error="Сначала закончи текущую стройку."; return false; }
            if (!CottageLayoutPlanner.Validate(plan, definition, out error)) return false;
            if (completed != null && (plan.west < completed.west || plan.east < completed.east || plan.back < completed.back || plan.front < completed.front || plan.floors < completed.floors))
            { error="Готовый дом можно расширять. Разборка пока не поддерживается."; return false; }
            if (completed != null && completed.SameShape(plan)) { error="Размер дома не изменился."; return false; }
            target=plan.Copy(); workHours=0; paused=false;
            float area=CottageLayoutPlanner.Bounds(plan,definition).size.x*CottageLayoutPlanner.Bounds(plan,definition).size.z;
            requiredHours=.65 + area*.009;
            return true;
        }
        public bool Advance(double gameHours)
        {
            if (double.IsNaN(gameHours) || double.IsInfinity(gameHours) || gameHours < 0) throw new ArgumentOutOfRangeException(nameof(gameHours));
            if (!Building || paused) return false;
            workHours=Math.Min(requiredHours,workHours+gameHours);
            if (workHours < requiredHours) return false;
            completed=target.Copy(); target=null; workHours=0; return true;
        }
        public string ToJson() => JsonUtility.ToJson(this,true);
        public static CottageRuntimeState FromJson(string json, CottageDefinition definition)
        {
            var result=JsonUtility.FromJson<CottageRuntimeState>(json);
            if (result == null || (result.completed == null && result.target == null)) throw new ArgumentException("Сохранение дома пустое.");
            if (result.completed != null && !CottageLayoutPlanner.Validate(result.completed,definition,out _)) throw new ArgumentException("Неверный построенный план.");
            if (result.target != null && !CottageLayoutPlanner.Validate(result.target,definition,out _)) throw new ArgumentException("Неверный проект стройки.");
            if (double.IsNaN(result.workHours)||double.IsInfinity(result.workHours)||result.workHours<0||double.IsNaN(result.requiredHours)||double.IsInfinity(result.requiredHours)||result.requiredHours<=0) throw new ArgumentException("Неверный прогресс стройки.");
            return result;
        }
    }

    public static class CottageLayoutPlanner
    {
        public static Bounds Bounds(CottagePlan p,CottageDefinition d)
        {
            float x0=-d.baseWidth*.5f-p.west*d.bayStep, x1=d.baseWidth*.5f+p.east*d.bayStep;
            float z0=-d.baseDepth*.5f-p.back*d.bayStep, z1=d.baseDepth*.5f+p.front*d.bayStep;
            return new Bounds(new Vector3((x0+x1)*.5f,0,(z0+z1)*.5f),new Vector3(x1-x0,0,z1-z0));
        }
        public static bool Validate(CottagePlan p,CottageDefinition d,out string error)
        {
            error=null;
            if(p==null||d==null) { error="Не задан проект дома."; return false; }
            if(!Finite(p.position.x)||!Finite(p.position.y)||!Finite(p.position.z)||!Finite(p.yaw)) { error="Неверная позиция дома."; return false; }
            if(p.west<0||p.east<0||p.back<0||p.front<0||p.west+p.east>d.maximumLengthSteps||p.back+p.front>d.maximumDepthSteps) { error="Этот размер корпуса пока недоступен."; return false; }
            if(p.floors<1||p.floors>d.maximumFloors) { error="Недопустимое число этажей."; return false; }
            return true;
        }
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        public static List<float> WindowAnchors(float min,float max,float step)
        {
            var result=new List<float>();
            int first=Mathf.CeilToInt((min+.8f)/step),last=Mathf.FloorToInt((max-.8f)/step);
            for(int i=first;i<=last;i++) result.Add(i*step);
            return result;
        }
        public static float PhaseStart(CottagePhase phase) => phase==CottagePhase.Foundation?0:phase==CottagePhase.Frame?.13f:phase==CottagePhase.Walls?.29f:phase==CottagePhase.Roof?.65f:.88f;
        public static float PhaseEnd(CottagePhase phase) => phase==CottagePhase.Foundation?.14f:phase==CottagePhase.Frame?.3f:phase==CottagePhase.Walls?.66f:phase==CottagePhase.Roof?.89f:.94f;
        public static string Stage(float p) => p>=1?"Готово":p<.13f?"Фундамент":p<.29f?"Деревянный каркас":p<.65f?"Стены и окна":p<.88f?"Кровля":p<.94f?"Детали": "Снятие лесов";
    }
}
