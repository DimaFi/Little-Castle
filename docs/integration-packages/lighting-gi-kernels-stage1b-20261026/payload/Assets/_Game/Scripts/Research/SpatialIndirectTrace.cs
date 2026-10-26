using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LittleCastle.Research
{
    /// <summary>Bounded experimental proxy transport, independent of camera/Physics/save state.</summary>
    public sealed class SpatialIndirectTrace
    {
        public readonly struct Proxy
        {
            public readonly Bounds bounds;
            public readonly Vector3 min,max;
            public readonly Vector3 albedo;
            public readonly int stableId;
            public Proxy(Bounds bounds, Vector3 linearAlbedo,int stableId=-1)
            {
                this.bounds = bounds;
                min=bounds.min;max=bounds.max;
                this.stableId=stableId;
                albedo = new Vector3(Mathf.Clamp01(linearAlbedo.x), Mathf.Clamp01(linearAlbedo.y), Mathf.Clamp01(linearAlbedo.z));
            }
        }
        public readonly struct Hit
        {
            public readonly int index;
            public readonly float distance;
            public readonly Vector3 position, normal;
            public Hit(int index, float distance, Vector3 position, Vector3 normal)
            { this.index=index; this.distance=distance; this.position=position; this.normal=normal; }
        }
        public readonly struct Lighting
        {
            public readonly Vector3 sky, equator, ground, sunDirection, sunColor, moonDirection, moonColor;
            public Lighting(Vector3 sky,Vector3 equator,Vector3 ground,Vector3 sunDirection,Vector3 sunColor,Vector3 moonDirection,Vector3 moonColor)
            {this.sky=sky;this.equator=equator;this.ground=ground;this.sunDirection=sunDirection.normalized;this.sunColor=sunColor;this.moonDirection=moonDirection.normalized;this.moonColor=moonColor;}
            public Vector3 Environment(Vector3 direction)
            {float up=Mathf.Max(0,direction.y),down=Mathf.Max(0,-direction.y);return sky*up+ground*down+equator*(1-up-down);}
        }
        // Irradiance/pi represented as L1 polynomials: c0 + cx*Nx + cy*Ny + cz*Nz.
        // For uniform sphere samples, c0=mean(L), c_axis=2*mean(L*direction_axis).
        // No pi factor is applied again in the material shader.
        [StructLayout(LayoutKind.Sequential)]
        public struct Probe
        {
            public Vector4 c0,cx,cy,cz; // c0.w: 0=uninitialised, -1=inside proxy, 1=valid
            public Vector3 Evaluate(Vector3 normal)
            {var v=c0+cx*normal.x+cy*normal.y+cz*normal.z;return new Vector3(Mathf.Max(0,v.x),Mathf.Max(0,v.y),Mathf.Max(0,v.z));}
        }
        readonly Proxy[] geometry;
        readonly Vector3[] directions;
        readonly Vector3[] inverseDirections;
        public SpatialIndirectProfile Profile {get;set;}
        public int RayCount=>directions.Length;
        public Vector3 Direction(int index)=>directions[index];
        public Vector3 InverseDirection(int index)=>inverseDirections[index];
        readonly Dictionary<SurfaceKey,Vector3> skyTransport;
        float skyTransportDistance=-1;
        readonly struct SurfaceKey : IEquatable<SurfaceKey>
        {
            public readonly int proxy,face,x,y,z;
            public SurfaceKey(int proxy,int face,Vector3 point){this.proxy=proxy;this.face=face;x=Mathf.FloorToInt(point.x*2);y=Mathf.FloorToInt(point.y*2);z=Mathf.FloorToInt(point.z*2);}
            public bool Equals(SurfaceKey b)=>proxy==b.proxy&&face==b.face&&x==b.x&&y==b.y&&z==b.z;
            public override bool Equals(object obj)=>obj is SurfaceKey b&&Equals(b);
            public override int GetHashCode(){unchecked{return (((proxy*31+face)*31+x)*31+y)*31+z;}}
        }
        public int SkyTransportEntries=>skyTransport.Count;
        // Two bounded rays plus the half-metre surface-cell footprint and ray bias.
        // The extra footprint matters when a changed blocker is near a cached cell edge.
        public static float InfluenceRadius(float rayDistance)=>2*rayDistance+.52f;
        // Cache identity must not inherit Vector3/Bounds operator tolerance.
        public static bool SameGeometryBounds(Bounds a,Bounds b)=>a.center.Equals(b.center)&&a.extents.Equals(b.extents);
        public int ProxyCount=>geometry.Length;
        public Proxy GetProxy(int index)=>geometry[index];
        public int StableId(int index)=>geometry[index].stableId<0?index:geometry[index].stableId;
        public SpatialIndirectTrace(Proxy[] source,int rayCount=128,bool reserveReferenceSky=true)
        {
            if(source==null||source.Length>8)throw new ArgumentException("Prototype supports up to eight simplified proxy boxes.");
            if(rayCount<16||rayCount>2048)throw new ArgumentOutOfRangeException(nameof(rayCount));
            geometry=(Proxy[])source.Clone();directions=new Vector3[rayCount];inverseDirections=new Vector3[rayCount];
            skyTransport=new Dictionary<SurfaceKey,Vector3>(reserveReferenceSky?4096:0);
            for(int i=0;i<rayCount;i++)
            {
                float y=1-2*(i+.5f)/rayCount,r=Mathf.Sqrt(Mathf.Max(0,1-y*y));
                float angle=i*2.39996322972865332f;
                directions[i]=new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);
                inverseDirections[i]=Inverse(directions[i]);
            }
        }
        public static bool IntersectBox(Vector3 origin,Vector3 direction,Bounds bounds,float limit,out float distance,out Vector3 normal)
        {
            return Slab(origin,Inverse(direction),bounds.min,bounds.max,limit,out distance,out normal);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector3 Inverse(Vector3 d)=>new Vector3(1/(d.x>=0?Mathf.Max(d.x,1e-8f):Mathf.Min(d.x,-1e-8f)),1/(d.y>=0?Mathf.Max(d.y,1e-8f):Mathf.Min(d.y,-1e-8f)),1/(d.z>=0?Mathf.Max(d.z,1e-8f):Mathf.Min(d.z,-1e-8f)));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool Slab(Vector3 p,Vector3 inv,Vector3 lo,Vector3 hi,float limit,out float distance,out Vector3 normal)
        {
            float ax=(lo.x-p.x)*inv.x,bx=(hi.x-p.x)*inv.x,ay=(lo.y-p.y)*inv.y,by=(hi.y-p.y)*inv.y,az=(lo.z-p.z)*inv.z,bz=(hi.z-p.z)*inv.z;
            float nx=Mathf.Min(ax,bx),ny=Mathf.Min(ay,by),nz=Mathf.Min(az,bz),fx=Mathf.Max(ax,bx),fy=Mathf.Max(ay,by),fz=Mathf.Max(az,bz);
            float near=Mathf.Max(nx,Mathf.Max(ny,nz)),far=Mathf.Min(fx,Mathf.Min(fy,fz));distance=near>.001f?near:far;normal=Vector3.zero;
            if(far<near||distance<=.001f||distance>=limit)return false;
            if(near>.001f){if(nx>=ny&&nx>=nz)normal.x=inv.x>0?-1:1;else if(ny>=nz)normal.y=inv.y>0?-1:1;else normal.z=inv.z>0?-1:1;}
            else{if(fx<=fy&&fx<=fz)normal.x=inv.x>0?1:-1;else if(fy<=fz)normal.y=inv.y>0?1:-1;else normal.z=inv.z>0?1:-1;}
            return true;
        }
        public bool Trace(Vector3 origin,Vector3 direction,float distance,out Hit hit,int ignore=-1)
            =>TraceInverse(origin,direction,Inverse(direction),distance,out hit,ignore);
        // Conservative finite-range broad phase. This only removes impossible hits.
        public int CandidateMask(Vector3 p,float radius)
        {
            // Float-normalised directions can be a few ULPs longer than one.
            // Keep the broad phase conservative at the exact finite-ray boundary.
            int mask=0;float safeRadius=radius+Mathf.Max(.0001f,Mathf.Abs(radius)*.000002f),r2=safeRadius*safeRadius;
            for(int i=0;i<geometry.Length;i++)
            {
                var g=geometry[i];float x=Mathf.Max(g.min.x-p.x,Mathf.Max(0,p.x-g.max.x)),y=Mathf.Max(g.min.y-p.y,Mathf.Max(0,p.y-g.max.y)),z=Mathf.Max(g.min.z-p.z,Mathf.Max(0,p.z-g.max.z));
                if(x*x+y*y+z*z<=r2)mask|=1<<i;
            }
            return mask;
        }
        public bool TraceCandidates(Vector3 origin,Vector3 direction,Vector3 inverse,float distance,int mask,out Hit hit,int ignore=-1)
        {
            hit=default;int nearest=-1;float best=distance;Vector3 normal=default;
            for(int i=0;i<geometry.Length;i++)
            {
                if((mask&(1<<i))==0||i==ignore)continue;
                if(Profile!=null&&Profile.enabled)Profile.boxTests++;
                ref readonly var g=ref geometry[i];
                if(!CandidateSlab(origin,inverse,g.min,g.max,best,out float t,out var n))continue;
                best=t;normal=n;nearest=i;
            }
            if(nearest<0)return false;
            hit=new Hit(nearest,best,origin+direction*best,normal);return true;
        }
        // Visibility needs any blocker, not a nearest-hit position/normal. Keep the
        // reference Trace/Slab independent so numerical regressions remain visible.
        public bool OccludedCandidates(Vector3 origin,Vector3 inverse,float distance,int mask,int ignore=-1)
        {
            for(int i=0;i<geometry.Length;i++)
            {
                if((mask&(1<<i))==0||i==ignore)continue;
                if(Profile!=null&&Profile.enabled)Profile.boxTests++;
                ref readonly var g=ref geometry[i];
                if(CandidateSlab(origin,inverse,g.min,g.max,distance,out _,out _,false))return true;
            }
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool CandidateSlab(Vector3 p,Vector3 inv,Vector3 lo,Vector3 hi,float limit,out float distance,out Vector3 normal,bool needNormal=true)
        {
            // Same finite inverse and strict endpoint rules as Slab, with early
            // axis rejection. No reciprocal approximation or direction quantisation.
            distance=0;normal=default;
            float ax=(lo.x-p.x)*inv.x,bx=(hi.x-p.x)*inv.x;
            float nx=ax<bx?ax:bx,fx=ax>bx?ax:bx;
            if(fx<=.001f)return false;
            float ay=(lo.y-p.y)*inv.y,by=(hi.y-p.y)*inv.y;
            float ny=ay<by?ay:by,fy=ay>by?ay:by;
            float near=nx>ny?nx:ny,far=fx<fy?fx:fy;
            if(far<near||far<=.001f)return false;
            float az=(lo.z-p.z)*inv.z,bz=(hi.z-p.z)*inv.z;
            float nz=az<bz?az:bz,fz=az>bz?az:bz;
            near=near>nz?near:nz;far=far<fz?far:fz;distance=near>.001f?near:far;
            if(far<near||distance<=.001f||distance>=limit)return false;
            if(!needNormal)return true;
            if(near>.001f){if(nx>=ny&&nx>=nz)normal.x=inv.x>0?-1:1;else if(ny>=nz)normal.y=inv.y>0?-1:1;else normal.z=inv.z>0?-1:1;}
            else{if(fx<=fy&&fx<=fz)normal.x=inv.x>0?1:-1;else if(fy<=fz)normal.y=inv.y>0?1:-1;else normal.z=inv.z>0?1:-1;}
            return true;
        }
        public static Vector3 InverseRay(Vector3 d)=>Inverse(d);
        bool TraceInverse(Vector3 origin,Vector3 direction,Vector3 inverse,float distance,out Hit hit,int ignore=-1)
        {
            bool found=false;hit=default;float best=distance;
            for(int i=0;i<geometry.Length;i++)
            {
                if(i==ignore)continue;
                if(Profile!=null&&Profile.enabled)Profile.boxTests++;
                if(!Slab(origin,inverse,geometry[i].min,geometry[i].max,best,out float t,out var n))continue;
                best=t;hit=new Hit(i,t,origin+direction*t,n);found=true;
            }
            return found;
        }
        public bool Contains(Vector3 p)
        {
            foreach(var proxy in geometry)
            {var b=proxy.bounds;if(p.x>b.min.x&&p.x<b.max.x&&p.y>b.min.y&&p.y<b.max.y&&p.z>b.min.z&&p.z<b.max.z)return true;}
            return false;
        }
        public Probe Solve(Vector3 position,Lighting light,float maximumDistance,bool bounce=true)
        {
            if(skyTransportDistance!=maximumDistance){skyTransport.Clear();skyTransportDistance=maximumDistance;}
            if(Contains(position))return new Probe{c0=new Vector4(0,0,0,-1)};
            Vector3 sum=default,x=default,y=default,z=default;
            var inverseSun=Inverse(light.sunDirection);var inverseMoon=Inverse(light.moonDirection);
            for(int ray=0;ray<directions.Length;ray++)
            {
                var direction=directions[ray];
                bool profile=Profile!=null&&Profile.enabled;
                long stamp=profile?SpatialIndirectProfile.Stamp():0;
                Vector3 incoming;
                bool found=TraceInverse(position,direction,inverseDirections[ray],maximumDistance,out var hit);
                if(profile){Profile.primaryRays++;Profile.primaryTicks+=SpatialIndirectProfile.Stamp()-stamp;}
                if(found)
                {
                    incoming=Vector3.zero;
                    if(bounce)
                    {
                        var origin=hit.position+hit.normal*.01f;
                        float sun=Mathf.Max(0,Vector3.Dot(hit.normal,light.sunDirection));
                        float moon=Mathf.Max(0,Vector3.Dot(hit.normal,light.moonDirection));
                        if(profile)stamp=SpatialIndirectProfile.Stamp();
                        if(profile){if(sun>0)Profile.shadowRays++;if(moon>0)Profile.shadowRays++;}
                        if(sun>0&&TraceInverse(origin,light.sunDirection,inverseSun,maximumDistance,out _,hit.index))sun=0;
                        if(moon>0&&TraceInverse(origin,light.moonDirection,inverseMoon,maximumDistance,out _,hit.index))moon=0;
                        if(profile){Profile.shadowTicks+=SpatialIndirectProfile.Stamp()-stamp;stamp=SpatialIndirectProfile.Stamp();}
                        var sky=SkyTransport(hit,maximumDistance);
                        if(profile)Profile.skyTicks+=SpatialIndirectProfile.Stamp()-stamp;
                        incoming=Vector3.Scale(geometry[hit.index].albedo,light.sunColor*sun+light.moonColor*moon+light.sky*sky.x+light.equator*sky.y+light.ground*sky.z);
                    }
                }
                else incoming=light.Environment(direction);
                if(profile)stamp=SpatialIndirectProfile.Stamp();
                sum+=incoming;x+=incoming*direction.x;y+=incoming*direction.y;z+=incoming*direction.z;
                if(profile)Profile.projectionTicks+=SpatialIndirectProfile.Stamp()-stamp;
            }
            float inv=1f/directions.Length;
            return new Probe{c0=new Vector4(sum.x*inv,sum.y*inv,sum.z*inv,1),cx=V4(x*(2*inv)),cy=V4(y*(2*inv)),cz=V4(z*(2*inv))};
        }
        Vector3 SkyTransport(Hit hit,float maximumDistance)
        {
            int axis=Mathf.Abs(hit.normal.x)>.5f?0:Mathf.Abs(hit.normal.y)>.5f?1:2;
            var key=new SurfaceKey(hit.index,axis*2+(hit.normal[axis]>0?1:0),hit.position);
            if(skyTransport.TryGetValue(key,out var value)){if(Profile!=null&&Profile.enabled)Profile.skyHits++;return value;}
            if(Profile!=null&&Profile.enabled)Profile.skyMisses++;
            // Geometry-only half-metre surface cache. Light colours/intensity are injected
            // later, so a time-of-day change does not invalidate this transport cache.
            var p=new Vector3((key.x+.5f)*.5f,(key.y+.5f)*.5f,(key.z+.5f)*.5f);
            var g=geometry[hit.index];p=Vector3.Min(g.max,Vector3.Max(g.min,p));p[axis]=hit.normal[axis]>0?g.max[axis]:g.min[axis];p+=hit.normal*.01f;
            Vector3 tangent=Vector3.Cross(Mathf.Abs(hit.normal.y)>.9f?Vector3.forward:Vector3.up,hit.normal).normalized,bitangent=Vector3.Cross(hit.normal,tangent);
            const int samples=12;
            for(int i=0;i<samples;i++)
            {
                if(Profile!=null&&Profile.enabled)Profile.skyRays++;
                float r=Mathf.Sqrt((i+.5f)/samples),angle=i*2.39996322972865332f;
                var d=tangent*(r*Mathf.Cos(angle))+bitangent*(r*Mathf.Sin(angle))+hit.normal*Mathf.Sqrt(1-r*r);
                if(Trace(p,d,maximumDistance,out _,hit.index))continue;
                float up=Mathf.Max(0,d.y),down=Mathf.Max(0,-d.y);value+=new Vector3(up,1-up-down,down)/samples;
            }
            skyTransport.Add(key,value);return value;
        }
        static Vector4 V4(Vector3 v)=>new Vector4(v.x,v.y,v.z,0);
    }
}
