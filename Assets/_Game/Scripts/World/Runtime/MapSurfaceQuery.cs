using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Ray/lattice traversal against the current authoritative triangles, independent of collider cooking or camera zoom.</summary>
    public static class MapSurfaceQuery
    {
        public static bool Raycast(MapEditSession s,Ray ray,out RaycastHit hit,float maxDistance=float.PositiveInfinity)
        {
            hit=default;if(ray.direction.sqrMagnitude<1e-12f)return false;var b=s.Bounds;float enter=0,exit=maxDistance;
            if(!Clip(ray.origin.x,ray.direction.x,b.x,b.x+b.z,ref enter,ref exit)||
               !Clip(ray.origin.z,ray.direction.z,b.y,b.y+b.w,ref enter,ref exit)||
               !Clip(ray.origin.y,ray.direction.y,s.MinimumHeight-.001f,s.MaximumHeight+.001f,ref enter,ref exit))return false;
            // Rebase at the finite map envelope before triangle math. Far camera origins
            // otherwise lose sub-metre barycentric precision through float cancellation.
            float rayOffset=enter;ray=new Ray(ray.GetPoint(enter),ray.direction);exit-=enter;enter=0;
            float spacing=s.HeightSpacing;var origin=s.HeightOrigin;
            var start=ray.GetPoint(enter+.00001f);
            int x=Mathf.FloorToInt((start.x-origin.x)/spacing),z=Mathf.FloorToInt((start.z-origin.y)/spacing);
            int sx=ray.direction.x>=0?1:-1,sz=ray.direction.z>=0?1:-1;
            float dx=Mathf.Abs(ray.direction.x)<1e-10f?float.PositiveInfinity:spacing/Mathf.Abs(ray.direction.x);
            float dz=Mathf.Abs(ray.direction.z)<1e-10f?float.PositiveInfinity:spacing/Mathf.Abs(ray.direction.z);
            float tx=float.IsInfinity(dx)?dx:(origin.x+(x+(sx>0?1:0))*spacing-ray.origin.x)/ray.direction.x;
            float tz=float.IsInfinity(dz)?dz:(origin.y+(z+(sz>0?1:0))*spacing-ray.origin.z)/ray.direction.z;
            int limit=Mathf.CeilToInt((b.z+b.w)/spacing)+4;
            for(int i=0;i<limit&&enter<=exit+.0001f;i++)
            {
                float wx=origin.x+x*spacing,wz=origin.y+z*spacing;
                var a=new Vector3(wx,s.SampleHeight(wx,wz),wz);
                var c=new Vector3(wx+spacing,s.SampleHeight(wx+spacing,wz),wz);
                var d=new Vector3(wx,s.SampleHeight(wx,wz+spacing),wz+spacing);
                var e=new Vector3(wx+spacing,s.SampleHeight(wx+spacing,wz+spacing),wz+spacing);
                float distance=float.PositiveInfinity;Vector3 normal=Vector3.up;
                Triangle(ray,a,d,c,ref distance,ref normal);Triangle(ray,c,d,e,ref distance,ref normal);
                if(distance<=exit+.0001f&&distance>=enter-.0001f)
                {
                    var p=ray.GetPoint(distance);
                    if(p.x>=b.x-.0001f&&p.z>=b.y-.0001f&&p.x<=b.x+b.z+.0001f&&p.z<=b.y+b.w+.0001f)
                    {hit=new RaycastHit{point=p,normal=normal,distance=distance+rayOffset};return true;}
                }
                if(float.IsInfinity(tx)&&float.IsInfinity(tz))break;
                if(tx<tz){enter=tx;tx+=dx;x+=sx;}else{enter=tz;tz+=dz;z+=sz;}
            }
            return false;
        }
        static bool Clip(float p,float d,float lo,float hi,ref float enter,ref float exit)
        {
            if(Mathf.Abs(d)<1e-10f)return p>=lo&&p<=hi;
            float a=(lo-p)/d,b=(hi-p)/d;if(a>b){float swap=a;a=b;b=swap;}
            enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);return enter<=exit;
        }
        static void Triangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,ref float nearest,ref Vector3 normal)
        {
            var e1=b-a;var e2=c-a;var p=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,p);
            if(Mathf.Abs(det)<1e-8f)return;
            var t=ray.origin-a;float u=Vector3.Dot(t,p)/det;if(u<-.00001f||u>1.00001f)return;
            var q=Vector3.Cross(t,e1);float v=Vector3.Dot(ray.direction,q)/det;if(v<-.00001f||u+v>1.00001f)return;
            float distance=Vector3.Dot(e2,q)/det;if(distance<0||distance>=nearest)return;
            nearest=distance;normal=Vector3.Cross(e1,e2).normalized;
        }
    }
}
