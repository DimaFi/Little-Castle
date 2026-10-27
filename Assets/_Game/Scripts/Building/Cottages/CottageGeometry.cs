using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Building.Cottages
{
    public sealed class CottagePiece
    {
        public string id,signature;
        public Mesh mesh;
        public Material material;
        public CottagePhase phase;
        public float revealAt;
    }

    public static class CottageGeometry
    {
        sealed class Hole
        {
            public float u,bottom,width,height,radius;
            public CottageModule module;
            public float Top(float x)
            {
                float r=Mathf.Min(radius,width*.5f);
                return r>.001f?bottom+height-r+Mathf.Sqrt(Mathf.Max(0,r*r-(x-u)*(x-u))):bottom+height;
            }
        }
        public static List<CottagePiece> Build(CottagePlan plan,CottageDefinition d,int lod=0)
        {
            if(!CottageLayoutPlanner.Validate(plan,d,out string error))throw new ArgumentException(error);
            var pieces=new List<CottagePiece>();var bounds=CottageLayoutPlanner.Bounds(plan,d);
            float x0=bounds.min.x,x1=bounds.max.x,z0=bounds.min.z,z1=bounds.max.z;
            float top=d.wallTop+(plan.floors-1)*d.floorHeight;
            Foundation(pieces,d,x0,x1,z0,z1);
            Frame(pieces,d,x0,x1,z0,z1,top);
            Wall(pieces,plan,d,"Front",x0,x1,z1,Vector3.forward,true,top,lod);
            Wall(pieces,plan,d,"Back",x0,x1,z0,Vector3.back,true,top,lod);
            Wall(pieces,plan,d,"West",z0,z1,x0,Vector3.left,false,top,lod);
            Wall(pieces,plan,d,"East",z0,z1,x1,Vector3.right,false,top,lod);
            Roof(pieces,plan,d,bounds,top,lod);
            if(d.Chimney!=null)Module(pieces,d.Chimney,new Vector3(-2.91f,RoofHeight(plan,d,-2.91f,0.72f)-.38f,.72f),Quaternion.identity,CottagePhase.Detail,"Chimney",lod);
            if(d.Module("EntranceSteps_A")!=null)Module(pieces,d.Module("EntranceSteps_A"),new Vector3(.8f,0,z1+.05f),Quaternion.identity,CottagePhase.Foundation,"Steps",lod);
            foreach(CottagePhase phase in Enum.GetValues(typeof(CottagePhase)))
            {
                var group=pieces.FindAll(p=>p.phase==phase);
                group.Sort((a,b)=>{int c=a.mesh.bounds.center.y.CompareTo(b.mesh.bounds.center.y);return c!=0?c:string.CompareOrdinal(a.id,b.id);});
                for(int i=0;i<group.Count;i++)group[i].revealAt=Mathf.Lerp(CottageLayoutPlanner.PhaseStart(phase),CottageLayoutPlanner.PhaseEnd(phase),(i+.5f)/Math.Max(1,group.Count));
            }
            return pieces;
        }
        static void Add(List<CottagePiece> pieces,string id,CottageMeshBuffer buffer,Material material,CottagePhase phase)
        {
            if(buffer.Count==0||material==null)return;
            Mesh mesh=buffer.Build(id);
            // Stable geometry signature lets an expansion retain unchanged pieces.
            uint hash=2166136261;
            foreach(Vector3 v in mesh.vertices){hash=(hash^(uint)Mathf.RoundToInt(v.x*10000))*16777619;hash=(hash^(uint)Mathf.RoundToInt(v.y*10000))*16777619;hash=(hash^(uint)Mathf.RoundToInt(v.z*10000))*16777619;}
            pieces.Add(new CottagePiece{id=id,signature=phase+":"+material.name+":"+hash+":"+mesh.vertexCount,mesh=mesh,material=material,phase=phase});
        }
        static void Module(List<CottagePiece> pieces,CottageModule module,Vector3 position,Quaternion rotation,CottagePhase phase,string id,int lod)
        {
            if(module?.prefab==null)return;
            Matrix4x4 place=Matrix4x4.TRS(position,rotation,Vector3.one);
            ModuleMatrix(pieces,module,place,phase,id,lod);
        }
        static void ModuleMatrix(List<CottagePiece> pieces,CottageModule module,Matrix4x4 place,CottagePhase phase,string id,int lod)
        {
            if(module?.prefab==null)return;
            var group=module.prefab.GetComponent<LODGroup>();
            Renderer[] renderers=group!=null?group.GetLODs()[Mathf.Min(lod,group.GetLODs().Length-1)].renderers:module.prefab.GetComponentsInChildren<Renderer>();
            var buffers=new Dictionary<Material,CottageMeshBuffer>();
            foreach(Renderer renderer in renderers)
            {
                var filter=renderer.GetComponent<MeshFilter>();if(filter?.sharedMesh==null)continue;
                Material mat=renderer.sharedMaterial;if(mat==null)continue;
                if(!buffers.TryGetValue(mat,out var buffer)){buffer=new CottageMeshBuffer();buffers.Add(mat,buffer);}
                buffer.Append(filter.sharedMesh,place*module.prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix);
            }
            foreach(var entry in buffers)Add(pieces,id+":"+entry.Key.name,entry.Value,entry.Key,phase);
        }
        static List<float> Cuts(float min,float max,float step)
        {
            var list=new List<float>{min};int first=Mathf.FloorToInt(min/step)+1,last=Mathf.CeilToInt(max/step);
            for(int i=first;i<last;i++)if(i*step>min+.01f&&i*step<max-.01f)list.Add(i*step);
            list.Add(max);return list;
        }
        static void Foundation(List<CottagePiece> pieces,CottageDefinition d,float x0,float x1,float z0,float z1)
        {
            for(int side=0;side<4;side++)
            {
                bool alongX=side<2;float min=alongX?x0:z0,max=alongX?x1:z1,plane=side==0?z1:side==1?z0:side==2?x0:x1;
                var cuts=Cuts(min,max,.52f);
                for(int row=0;row<2;row++)for(int i=0;i<cuts.Count-1;i++)
                {
                    float a=cuts[i],b=cuts[i+1];var buffer=new CottageMeshBuffer();
                    Vector3 centre=alongX?new Vector3((a+b)*.5f,(row+.5f)*d.foundationHeight/2,plane):new Vector3(plane,(row+.5f)*d.foundationHeight/2,(a+b)*.5f);
                    var block=d.Module("FoundationBlock_A");
                    if(block?.prefab!=null)
                    {
                        Quaternion rot=alongX?Quaternion.identity:Quaternion.Euler(0,90,0);
                        Vector3 pos=centre-Vector3.up*d.foundationHeight/4;
                        Vector3 scale=new Vector3((b-a-.009f)/Mathf.Max(.01f,block.width),(d.foundationHeight/2-.009f)/Mathf.Max(.01f,block.height),.38f/Mathf.Max(.01f,block.depth));
                        ModuleMatrix(pieces,block,Matrix4x4.TRS(pos,rot,scale),CottagePhase.Foundation,$"Stone:{side}:{row}:{a:F3}",0);
                    }
                    else {buffer.Box(centre,alongX?new Vector3(b-a-.009f,d.foundationHeight/2-.009f,.38f):new Vector3(.38f,d.foundationHeight/2-.009f,b-a-.009f),Quaternion.identity);Add(pieces,$"Stone:{side}:{row}:{a:F3}",buffer,d.stone,CottagePhase.Foundation);}
                }
            }
        }
        static void Frame(List<CottagePiece> pieces,CottageDefinition d,float x0,float x1,float z0,float z1,float top)
        {
            for(int side=0;side<4;side++)
            {
                bool alongX=side<2;float min=alongX?x0:z0,max=alongX?x1:z1,plane=side==0?z1:side==1?z0:side==2?x0:x1;
                var cuts=Cuts(min,max,d.bayStep);
                for(int i=0;i<cuts.Count;i++)
                {
                    if(!alongX&&(i==0||i==cuts.Count-1))continue;
                    Vector3 p=alongX?new Vector3(cuts[i],0,plane):new Vector3(plane,0,cuts[i]);
                    for(float y=d.foundationHeight;y<top-.01f;y+=.7f)
                    {var b=new CottageMeshBuffer();b.Beam(p+Vector3.up*y,p+Vector3.up*Mathf.Min(y+.7f,top),.19f);Add(pieces,$"Post:{side}:{cuts[i]:F3}:{y:F2}",b,d.wood,CottagePhase.Frame);}
                }
                for(int i=0;i<cuts.Count-1;i++)
                {
                    var b=new CottageMeshBuffer();Vector3 a=alongX?new Vector3(cuts[i],top-.18f,plane):new Vector3(plane,top-.18f,cuts[i]);
                    Vector3 c=alongX?new Vector3(cuts[i+1],top-.18f,plane):new Vector3(plane,top-.18f,cuts[i+1]);
                    b.Beam(a,c,.2f);b.Beam(a-Vector3.up*.62f,a+((c-a).normalized*.6f),.12f);
                    Add(pieces,$"Tie:{side}:{cuts[i]:F3}",b,d.wood,CottagePhase.Frame);
                }
            }
        }
        static void Wall(List<CottagePiece> pieces,CottagePlan plan,CottageDefinition d,string side,float min,float max,float plane,Vector3 normal,bool alongX,float top,int lod)
        {
            var holes=new List<Hole>();
            void Opening(float u,float bottom,CottageModule module,string id)
            {
                if(module==null||module.prefab==null)return;
                var h=new Hole{u=u,bottom=bottom+module.openingBottomOffset,width=module.openingWidth>0?module.openingWidth:.81f,height=module.openingHeight>0?module.openingHeight:1.06f,radius=module.archRadius,module=module};
                if(u-h.width*.5f<min+.25f||u+h.width*.5f>max-.25f)return;
                holes.Add(h);Vector3 pos=alongX?new Vector3(u,bottom,plane+.012f*normal.z):new Vector3(plane+.012f*normal.x,bottom,u);
                Module(pieces,module,pos,Quaternion.LookRotation(normal,Vector3.up),CottagePhase.Walls,side+":"+id,lod);
            }
            if(side=="Front")
            {
                Opening(.8f,d.foundationHeight,d.Door,"Door");Opening(-1.56f,.95f,d.Window,"BaseWindow");Opening(2.37f,1.28f,d.SmallWindow,"SmallWindow");
                for(float u=-3.9f;u>min+.8f;u-=d.bayStep)Opening(u,.95f,d.Module("Window_Closed_A")??d.Window,$"Extra:{u:F2}");
                for(float u=3.9f;u<max-.8f;u+=d.bayStep)Opening(u,.95f,d.Module("Window_Closed_A")??d.Window,$"Extra:{u:F2}");
            }
            else
            {
                foreach(float u in CottageLayoutPlanner.WindowAnchors(min,max,d.bayStep*2))Opening(u,1.0f,d.Module("Window_Closed_A")??d.Window,$"Window:{u:F2}");
            }
            for(int floor=1;floor<plan.floors;floor++)foreach(float u in CottageLayoutPlanner.WindowAnchors(min,max,d.bayStep*2))Opening(u,1.0f+floor*d.floorHeight,d.Module("Window_Closed_A")??d.Window,$"Upper:{floor}:{u:F2}");
            Vector3 Point(float u,float y,float depth)=>alongX?new Vector3(u,y,plane)+normal*depth:new Vector3(plane,y,u)+normal*depth;
            var cellCuts=Cuts(min,max,d.bayStep);
            for(int cell=0;cell<cellCuts.Count-1;cell++)for(float y=d.foundationHeight;y<top-.001f;y+=.57f)
            {
                float a=cellCuts[cell],c=cellCuts[cell+1],yb=Mathf.Min(y+.57f,top);var buffer=new CottageMeshBuffer();
                var cuts=Cuts(a,c,.12f);
                foreach(Hole hole in holes)foreach(float edge in new[]{hole.u-hole.width*.5f,hole.u+hole.width*.5f})if(edge>a+.001f&&edge<c-.001f)cuts.Add(edge);
                cuts.Sort();
                for(int j=0;j<cuts.Count-1;j++)
                {
                    float u0=cuts[j],u1=cuts[j+1];if(u1-u0<.001f)continue;
                    Hole h=holes.Find(v=>(u0+u1)*.5f>v.u-v.width*.5f+.0001f&&(u0+u1)*.5f<v.u+v.width*.5f-.0001f);
                    if(h==null)Patch(buffer,Point,normal,u0,u1,y,y,yb,yb);
                    else
                    {
                        float lower=Mathf.Min(yb,h.bottom);if(lower>y+.001f)Patch(buffer,Point,normal,u0,u1,y,y,lower,lower);
                        float lo0=Mathf.Max(y,h.Top(u0)),lo1=Mathf.Max(y,h.Top(u1));
                        if(lo0<yb||lo1<yb)Patch(buffer,Point,normal,u0,u1,Mathf.Min(lo0,yb),Mathf.Min(lo1,yb),yb,yb);
                    }
                }
                Add(pieces,$"Wall:{side}:{a:F3}:{y:F3}",buffer,d.plaster,CottagePhase.Walls);
            }
            foreach(Hole hole in holes)
            {
                var buffer=new CottageMeshBuffer();var outline=new List<Vector2>{new Vector2(hole.u-hole.width*.5f,hole.bottom),new Vector2(hole.u+hole.width*.5f,hole.bottom)};
                for(int i=0;i<=16;i++){float u=hole.u+hole.width*.5f*Mathf.Cos(i*Mathf.PI/16);outline.Add(new Vector2(u,hole.Top(u)));}
                for(int i=0;i<outline.Count;i++)
                {
                    Vector2 a=outline[i],b=outline[(i+1)%outline.Count];Vector3 aa=Point(a.x,a.y,0),bb=Point(b.x,b.y,0);
                    Vector3 axis=alongX?Vector3.right:Vector3.forward;Vector3 n=(axis*(-(b.y-a.y))+Vector3.up*(b.x-a.x)).normalized;
                    buffer.Quad(aa,bb,bb-normal*.24f,aa-normal*.24f,n);
                }
                Add(pieces,$"Reveal:{side}:{hole.u:F3}:{hole.bottom:F3}",buffer,d.plaster,CottagePhase.Walls);
            }
        }
        static void Patch(CottageMeshBuffer b,Func<float,float,float,Vector3> point,Vector3 normal,float u0,float u1,float lo0,float lo1,float hi0,float hi1)
        {
            Vector3 a=point(u0,lo0,0),c=point(u1,lo1,0),e=point(u1,hi1,0),f=point(u0,hi0,0);
            b.Quad(a,c,e,f,normal);b.Quad(a-normal*.24f,c-normal*.24f,e-normal*.24f,f-normal*.24f,-normal);
        }
        public static float RoofHeight(CottagePlan plan,CottageDefinition d,float x,float z)
        {
            var b=CottageLayoutPlanner.Bounds(plan,d);float half=b.size.z*.5f+.54f;float t=Mathf.Clamp01(Mathf.Abs(z-b.center.z)/half);
            float pitch=2.43f+(b.size.z-d.baseDepth)*.43f;float sx=(x-b.center.x)/(b.size.x*.5f+.47f);
            return d.wallTop+(plan.floors-1)*d.floorHeight+pitch-.22f*(1-sx*sx)-(pitch+.62f)*t+.62f*t*t;
        }
        static void Roof(List<CottagePiece> pieces,CottagePlan plan,CottageDefinition d,Bounds bounds,float top,int lod)
        {
            float x0=bounds.min.x-.47f,x1=bounds.max.x+.47f,half=bounds.size.z*.5f+.54f;
            var roof=new CottageMeshBuffer();
            for(int side=-1;side<=1;side+=2)for(int row=0;row<12;row++)for(int col=0;col<20;col++)
            {
                float a=Mathf.Lerp(x0,x1,col/20f),b=Mathf.Lerp(x0,x1,(col+1)/20f),z0=bounds.center.z+side*half*row/12,z1=bounds.center.z+side*half*(row+1)/12;
                Vector3 p0=new Vector3(a,RoofHeight(plan,d,a,z0)-.045f,z0),p1=new Vector3(b,RoofHeight(plan,d,b,z0)-.045f,z0),p2=new Vector3(b,RoofHeight(plan,d,b,z1)-.045f,z1),p3=new Vector3(a,RoofHeight(plan,d,a,z1)-.045f,z1);
                Vector3 n=Vector3.Cross(p1-p0,p3-p0).normalized;if(n.y<0)n=-n;
                roof.Quad(p0,p1,p2,p3,n,new Vector2(a,z0),new Vector2(b,z0),new Vector2(b,z1),new Vector2(a,z1));
                roof.Quad(p0-Vector3.up*.1f,p1-Vector3.up*.1f,p2-Vector3.up*.1f,p3-Vector3.up*.1f,-n);
            }
            Add(pieces,"RoofShell",roof,d.roof,CottagePhase.Roof);
            for(int edge=0;edge<2;edge++)
            {
                float x=edge==0?bounds.min.x:bounds.max.x;var b=new CottageMeshBuffer();
                for(int i=0;i<28;i++)
                {
                    float z0=Mathf.Lerp(bounds.min.z,bounds.max.z,i/28f),z1=Mathf.Lerp(bounds.min.z,bounds.max.z,(i+1)/28f);
                    b.Quad(new Vector3(x,top,z0),new Vector3(x,top,z1),new Vector3(x,RoofHeight(plan,d,x,z1)-.14f,z1),new Vector3(x,RoofHeight(plan,d,x,z0)-.14f,z0),edge==0?Vector3.left:Vector3.right);
                }
                Add(pieces,"Gable:"+edge,b,d.plaster,CottagePhase.Roof);
                var beams=new CottageMeshBuffer();
                Vector3 ridge=new Vector3(x,bounds.center.z==0?RoofHeight(plan,d,x,0)-.16f:RoofHeight(plan,d,x,bounds.center.z)-.16f,bounds.center.z);
                beams.Beam(new Vector3(x,top,bounds.min.z),ridge,.18f);beams.Beam(ridge,new Vector3(x,top,bounds.max.z),.18f);beams.Beam(new Vector3(x,top,bounds.center.z),ridge,.18f);
                Add(pieces,"GableFrame:"+edge,beams,d.wood,CottagePhase.Roof);
            }
            if(lod<2 && d.Tile?.prefab!=null)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    int rows=Mathf.CeilToInt(half/.28f);
                    for(int row=0;row<rows;row++)
                    {
                        float z=bounds.center.z+side*(half-row*.28f);var buffers=new Dictionary<Material,CottageMeshBuffer>();
                        float tileWidth=d.Tile.width>0?d.Tile.width:.32f;float stride=tileWidth+.008f;
                        for(float x=x0+stride*.5f+(row%2)*stride*.25f;x<x1-stride*.28f;x+=stride)
                        {
                            float y=RoofHeight(plan,d,x,z),dy=(RoofHeight(plan,d,x,z-side*.01f)-y)/.01f;
                            Vector3 uphill=new Vector3(0,dy,-side).normalized;
                            Vector3 right=new Vector3(-side,0,0);Vector3 normal=Vector3.Cross(uphill,right).normalized;
                            if(normal.y<0)normal=-normal;
                            Matrix4x4 matrix=Matrix4x4.identity;matrix.SetColumn(0,new Vector4(right.x,right.y,right.z,0));matrix.SetColumn(1,new Vector4(normal.x,normal.y,normal.z,0));matrix.SetColumn(2,new Vector4(uphill.x,uphill.y,uphill.z,0));matrix.SetColumn(3,new Vector4(x,y+.025f,z,1));
                            AppendModule(buffers,d.Tile,matrix,lod);
                        }
                        foreach(var pair in buffers)Add(pieces,$"Tiles:{side}:{row}",pair.Value,pair.Key,CottagePhase.Roof);
                    }
                }
            }
            if(d.Ridge?.prefab!=null)
            {
                var buffers=new Dictionary<Material,CottageMeshBuffer>();float stride=Mathf.Max(.15f,d.Ridge.width*.88f);
                for(float x=x0+stride*.5f;x<x1;x+=stride)AppendModule(buffers,d.Ridge,Matrix4x4.TRS(new Vector3(x,RoofHeight(plan,d,x,bounds.center.z)-.13f,bounds.center.z),Quaternion.identity,Vector3.one),lod);
                foreach(var pair in buffers)Add(pieces,"Ridge:"+pair.Key.name,pair.Value,pair.Key,CottagePhase.Roof);
            }
            var fascia=new CottageMeshBuffer();
            for(int side=-1;side<=1;side+=2)
            {
                float z=bounds.center.z+side*half;
                for(int i=0;i<20;i++){float a=Mathf.Lerp(x0,x1,i/20f),b=Mathf.Lerp(x0,x1,(i+1)/20f);fascia.Beam(new Vector3(a,RoofHeight(plan,d,a,z)-.03f,z),new Vector3(b,RoofHeight(plan,d,b,z)-.03f,z),.17f);}
            }
            Add(pieces,"Fascia",fascia,d.wood,CottagePhase.Roof);
        }
        static void AppendModule(Dictionary<Material,CottageMeshBuffer> buffers,CottageModule module,Matrix4x4 place,int lod)
        {
            var group=module.prefab.GetComponent<LODGroup>();var renderers=group!=null?group.GetLODs()[Mathf.Min(lod,group.GetLODs().Length-1)].renderers:module.prefab.GetComponentsInChildren<Renderer>();
            foreach(var r in renderers){var f=r.GetComponent<MeshFilter>();if(f?.sharedMesh==null||r.sharedMaterial==null)continue;if(!buffers.TryGetValue(r.sharedMaterial,out var b)){b=new CottageMeshBuffer();buffers.Add(r.sharedMaterial,b);}b.Append(f.sharedMesh,place*module.prefab.transform.worldToLocalMatrix*f.transform.localToWorldMatrix);}
        }
    }
}
