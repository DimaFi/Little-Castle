using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Building.Cottages
{
    public sealed class CottagePresenter : MonoBehaviour
    {
        public CottageDefinition definition;
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly List<GameObject> visuals=new List<GameObject>();
        readonly List<Tuple<CottagePiece,GameObject,bool>> parts=new List<Tuple<CottagePiece,GameObject,bool>>();
        GameObject scaffolding;
        string cacheKey;
        CottagePlan scaffoldPlan;
        int scaffoldLevel=-1;
        public int VisibleScaffoldRenderers => scaffolding!=null&&scaffolding.activeSelf?scaffolding.GetComponentsInChildren<Renderer>().Length:0;
        public int OwnedMeshCount=>ownedMeshes.Count;
        public void Show(CottageRuntimeState state)
        {
            if(definition==null)return;
            var plan=state.target??state.completed;if(plan==null){Clear();return;}
            transform.SetPositionAndRotation(plan.position,Quaternion.Euler(0,plan.yaw,0));
            string key=ShapeKey(state.completed)+"/"+ShapeKey(state.target);
            if(key!=cacheKey)
            {
                Clear();cacheKey=key;
                if(!state.Building)BuildFinished(plan);
                else BuildWork(state.completed,state.target);
            }
            if(state.Building)
            {
                float p=state.Progress;
                foreach(var part in parts)
                {
                    float removal=part.Item1.phase==CottagePhase.Foundation?.15f:part.Item1.phase==CottagePhase.Frame?.3f:part.Item1.phase==CottagePhase.Walls?.65f:part.Item1.phase==CottagePhase.Roof?.66f:.88f;
                    part.Item2.SetActive(part.Item3?p<removal:p>=part.Item1.revealAt);
                }
                UpdateScaffolding(state.target,p);
            }
        }
        public void ShowPreview(CottagePlan plan)
        {
            if(definition==null)return;
            transform.SetPositionAndRotation(plan.position,Quaternion.Euler(0,plan.yaw,0));
            string key="preview:"+ShapeKey(plan);
            if(cacheKey==key)return;Clear();cacheKey=key;
            var pieces=CottageGeometry.Build(plan,definition,1);
            foreach(var p in pieces)ownedMeshes.Add(p.mesh);
            Combine(pieces,"Preview",true,0);
        }
        static string ShapeKey(CottagePlan p)=>p==null?"none":$"{p.west}:{p.east}:{p.back}:{p.front}:{p.floors}:{p.seed}";
        void BuildFinished(CottagePlan plan)
        {
            var levels=new LOD[3];var group=gameObject.AddComponent<LODGroup>();
            for(int lod=0;lod<3;lod++)
            {
                var pieces=CottageGeometry.Build(plan,definition,lod);foreach(var p in pieces)ownedMeshes.Add(p.mesh);
                var renderers=Combine(pieces,"LOD"+lod,false,lod);
                levels[lod]=new LOD(lod==0?.42f:lod==1?.13f:.025f,renderers);
            }
            group.SetLODs(levels);group.RecalculateBounds();
        }
        void BuildWork(CottagePlan baseline,CottagePlan target)
        {
            var old=baseline==null?new List<CottagePiece>():CottageGeometry.Build(baseline,definition);
            var next=CottageGeometry.Build(target,definition);
            var oldSignatures=new HashSet<string>();var newSignatures=new HashSet<string>();
            foreach(var p in old){oldSignatures.Add(p.signature);ownedMeshes.Add(p.mesh);}
            foreach(var p in next){newSignatures.Add(p.signature);ownedMeshes.Add(p.mesh);}
            foreach(var p in old)
            {
                var obj=CreateMesh(p.mesh,p.material,"Existing "+p.id,0);
                if(!newSignatures.Contains(p.signature))parts.Add(Tuple.Create(p,obj,true));
            }
            foreach(var p in next)if(!oldSignatures.Contains(p.signature))
            {var obj=CreateMesh(p.mesh,p.material,"Build "+p.id,0);obj.SetActive(false);parts.Add(Tuple.Create(p,obj,false));}
            scaffoldPlan=target;
        }
        Renderer[] Combine(List<CottagePiece> pieces,string label,bool preview,int lod)
        {
            var buffers=new Dictionary<Material,CottageMeshBuffer>();
            foreach(var p in pieces)
            {
                Material mat=preview?definition.preview:lod==2?definition.Far(p.material):p.material;
                if(!buffers.TryGetValue(mat,out var buffer)){buffer=new CottageMeshBuffer();buffers.Add(mat,buffer);}
                buffer.Append(p.mesh,Matrix4x4.identity);
            }
            var result=new List<Renderer>();
            foreach(var pair in buffers){var mesh=pair.Value.Build(label+":"+pair.Key.name);ownedMeshes.Add(mesh);result.Add(CreateMesh(mesh,pair.Key,label,lod,preview).GetComponent<Renderer>());}
            // Source section meshes are no longer needed after the static batch.
            foreach(var p in pieces){ownedMeshes.Remove(p.mesh);Release(p.mesh);}
            return result.ToArray();
        }
        GameObject CreateMesh(Mesh mesh,Material material,string label,int lod,bool preview=false)
        {
            var obj=new GameObject(label);obj.transform.SetParent(transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=lod==2||preview?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=lod<2&&!preview;
            r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            visuals.Add(obj);return obj;
        }
        void UpdateScaffolding(CottagePlan plan,float p)
        {
            if(p<.12f||p>=1){if(scaffolding!=null)scaffolding.SetActive(false);return;}
            float top=definition.wallTop+(plan.floors-1)*definition.floorHeight+2.6f;
            int levels=p>.94f?Mathf.CeilToInt((1-p)/.06f*(top/1.05f)):Mathf.Clamp(1+Mathf.FloorToInt((p-.12f)/.75f*(top/1.05f)),1,Mathf.CeilToInt(top/1.05f));
            if(levels==scaffoldLevel&&scaffolding!=null){scaffolding.SetActive(true);return;}
            if(scaffolding!=null){var oldMesh=scaffolding.GetComponent<MeshFilter>().sharedMesh;ownedMeshes.Remove(oldMesh);Release(oldMesh);visuals.Remove(scaffolding);Release(scaffolding);}
            scaffoldLevel=levels;
            var b=CottageLayoutPlanner.Bounds(plan,definition);var buffer=new CottageMeshBuffer();
            float x0=b.min.x-.55f,x1=b.max.x+.55f,z0=b.min.z-.55f,z1=b.max.z+.55f;
            for(int side=0;side<4;side++)
            {
                bool ax=side<2;float min=ax?x0:z0,max=ax?x1:z1,plane=side==0?z1:side==1?z0:side==2?x0:x1;
                int bays=Mathf.CeilToInt((max-min)/1.65f);
                for(int i=0;i<bays;i++)
                {
                    float a=Mathf.Lerp(min,max,i/(float)bays),c=Mathf.Lerp(min,max,(i+1)/(float)bays);
                    // Keep the entrance clear on the front scaffold.
                    if(side==0&&a<1.5f&&c>.1f)continue;
                    Vector3 P(float u,float y)=>ax?new Vector3(u,y,plane):new Vector3(plane,y,u);
                    buffer.Beam(P(a,0),P(a,levels*1.05f),.075f);buffer.Beam(P(c,0),P(c,levels*1.05f),.075f);
                    for(int level=0;level<levels;level++)
                    {
                        float y=.15f+level*1.05f;buffer.Beam(P(a,y),P(c,y),.065f);buffer.Beam(P(a,y),P(c,Mathf.Min(y+.8f,levels*1.05f)),.045f);
                        Vector3 centre=P((a+c)*.5f,y);buffer.Box(centre,ax?new Vector3(c-a,.06f,.42f):new Vector3(.42f,.06f,c-a),Quaternion.identity);
                    }
                }
            }
            var mesh=buffer.Build("Scaffolding");ownedMeshes.Add(mesh);scaffolding=CreateMesh(mesh,definition.scaffold,"Scaffolding",0);
        }
        public void Clear()
        {
            var group=GetComponent<LODGroup>();if(group!=null)Release(group);
            foreach(var obj in visuals)if(obj!=null){obj.SetActive(false);Release(obj);}visuals.Clear();parts.Clear();
            foreach(var mesh in ownedMeshes)if(mesh!=null)Release(mesh);ownedMeshes.Clear();scaffolding=null;scaffoldLevel=-1;cacheKey=null;
        }
        static void Release(UnityEngine.Object obj){if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        void OnDestroy()=>Clear();
    }
}
