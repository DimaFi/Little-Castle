using System;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>One optional decorative sector in the existing map scene. No map/clock/camera writes.</summary>
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class LandscapeBorderPresentation : MonoBehaviour
    {
        public MountainBorderProfile profile;
        public bool showWaterfall=true,showTrees=true;
        [NonSerialized] public int activeLandLod,treeCount,landTriangles,grassClumps;
        [NonSerialized] public string lastError;
        GameObject generated;
        MeshRenderer landRenderer;
        MeshFilter landFilter;
        MeshRenderer joinRenderer;
        MeshRenderer outcropRenderer;
        MeshFilter outcropFilter;
        MountainRockDetailBuilder.HeightSampler groundQuery;
        readonly List<Object> owned=new List<Object>();
        readonly List<MapRockInstance> studyRocks=new List<MapRockInstance>();
        readonly List<Renderer> studyRockRenderers=new List<Renderer>();
        readonly List<Transform> plantedTrees=new List<Transform>();
        MountainRockDetailBuilder.HeightSampler[] lodQueries;
        WaterfallGeometryV1 water;
        WaterfallVisualEffects fall;
        RiverSurfaceEffects riverEffects;
        bool rebuildPending;
        float lastEditTime;
        float captureTime=-1;
        MaterialPropertyBlock landProperties;
        Material riverBodyOwned;

        void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeginCamera;Rebuild();}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeginCamera;Release();}
        void OnValidate(){rebuildPending=true;}
        void Update(){if(rebuildPending){rebuildPending=false;Rebuild();}}
        [ContextMenu("Rebuild presentation from profile")]
        public void Rebuild()
        {
            Release();lastError=null;
            if(!profile||profile.lodMeshes==null||profile.lodMeshes.Length<3||!profile.alpineMaterial)return;
            try
            {
                generated=Child("Mountain border · derived presentation",transform);
                var land=Child("Existing terrain palette · detailed alpine rock",generated.transform);
                landFilter=land.AddComponent<MeshFilter>();landFilter.sharedMesh=profile.lodMeshes[0];
                landRenderer=land.AddComponent<MeshRenderer>();landRenderer.sharedMaterial=profile.alpineMaterial;
                landRenderer.shadowCastingMode=ShadowCastingMode.On;landRenderer.receiveShadows=true;
                landRenderer.lightProbeUsage=LightProbeUsage.Off;landRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                landProperties=new MaterialPropertyBlock();activeLandLod=-1;
                if(profile.outcropLodMeshes!=null&&profile.outcropLodMeshes.Length==3&&profile.outcropLodMeshes[0])
                {
                    var outcrops=Child("Fractured cliff shoulders · shared rock material",generated.transform);
                    outcropFilter=outcrops.AddComponent<MeshFilter>();outcropFilter.sharedMesh=profile.outcropLodMeshes[0];
                    outcropRenderer=outcrops.AddComponent<MeshRenderer>();outcropRenderer.sharedMaterial=profile.alpineMaterial;
                    outcropRenderer.shadowCastingMode=ShadowCastingMode.On;outcropRenderer.receiveShadows=true;
                }
                groundQuery=new MountainRockDetailBuilder.HeightSampler(new List<Vector3>(profile.lodMeshes[0].vertices),new List<int>(profile.lodMeshes[0].triangles));
                lodQueries=new MountainRockDetailBuilder.HeightSampler[3];lodQueries[0]=groundQuery;
                BuildStudyRocks();
                if(profile.meadowJoinMesh)
                {
                    var join=Child("Meadow edge · narrow geometric join",generated.transform);join.AddComponent<MeshFilter>().sharedMesh=profile.meadowJoinMesh;
                    joinRenderer=join.AddComponent<MeshRenderer>();joinRenderer.sharedMaterial=profile.alpineMaterial;joinRenderer.receiveShadows=true;joinRenderer.shadowCastingMode=ShadowCastingMode.Off;
                }
                if(showWaterfall&&profile.riverMaterial&&profile.waterfallMaterial)
                {
                    water=WaterfallGeometryV1.Build();
                    var waterMaterial=Own(new Material(profile.riverBodyMaterial?profile.riverBodyMaterial:profile.riverMaterial));riverBodyOwned=waterMaterial;
                    waterMaterial.name="Mountain river · owned existing river material";
                    var upper=WaterMesh(water.upper);var lower=WaterMesh(water.lower);
                    var upperRoot=MeshObject("Upper river",upper,waterMaterial);
                    var lowerRoot=MeshObject("Landing pool and downstream river",lower,waterMaterial);
                    BindFlow(upperRoot,false);BindFlow(lowerRoot,true);
                    fall=new WaterfallVisualEffects(generated.transform,water,profile.waterfall,profile.waterfallMaterial,
                        profile.mistMaterial,profile.normalTexture,profile.foamTexture,profile.phaseTexture,profile.seed);
                    if(profile.waterfall.quality==WaterfallVisualQuality.Off)
                        MeshObject("Basic cascade · additional effects off",water.fall,profile.waterfallMaterial);
                    // Reuse the same shore/rapids overlay as ordinary newly authored rivers.
                    var quality=Own(ScriptableObject.CreateInstance<RiverVisualQualityProfile>());
                    var session=new MapEditSession(new MapAuthoringDocument{mapId="decorative-border",bounds=new Vector4(-500,-500,1000,1000)});
                    riverEffects=new RiverSurfaceEffects(session,profile.studyRockCatalog,quality);
                    var upperPath=WorldFlowPath(false);var lowerPath=WorldFlowPath(true);
                    var worldRocks=new List<MapRockInstance>();
                    foreach(var rock in studyRocks){var point=transform.TransformPoint(new Vector3(rock.position.x,0,rock.position.y));worldRocks.Add(new MapRockInstance{id=rock.id,archetypeId=rock.archetypeId,position=new Vector2(point.x,point.z),scale=rock.scale,topOffset=rock.topOffset+transform.position.y,manual=true});}
                    riverEffects.Attach(upperRoot,upper,upperPath,upperPath.bounds,worldRocks,Array.Empty<MapRockInstance>(),waterMaterial,true);
                    riverEffects.Attach(lowerRoot,lower,lowerPath,lowerPath.bounds,worldRocks,Array.Empty<MapRockInstance>(),waterMaterial,true);
                }
                if(showTrees&&profile.sprucePrefab)BuildTrees();
                if(profile.grassSource&&profile.grassMaterial&&profile.maximumGrassClumps>0)BuildGrass();
                TickPresentation(null,Time.realtimeSinceStartup);
            }
            catch(Exception e){lastError=e.Message;Release();Debug.LogException(e,this);}
        }
        Mesh WaterMesh(Mesh source)
        {
            var mesh=Own(Object.Instantiate(source));
            var vertices=mesh.vertices;var uv=mesh.uv;var data=new List<Vector4>(vertices.Length);
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];float edge=Mathf.Min(uv[i].x,1-uv[i].x);
                float depth=Mathf.Max(0,p.y-GroundHeight(new Vector2(p.x,p.z)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.18f,edge));
                data.Add(new Vector4(depth,1.4f,Mathf.Clamp01(1-depth),0));
            }
            mesh.SetUVs(1,data);return mesh;
        }
        void BuildStudyRocks()
        {
            var catalog=profile.studyRockCatalog;if(!catalog||catalog.entries.Length==0)return;
            var positions=new[]{new Vector2(-61,3),new Vector2(-53,-2),new Vector2(-58,-15),new Vector2(-48,-32),new Vector2(-40,-49),new Vector2(-60,43),new Vector2(-55,57),new Vector2(-71,6),new Vector2(-46,4)};
            var parent=Child("Seeded lightweight stones · existing cliff texture",generated.transform);
            for(int i=0;i<positions.Length;i++)
            {
                var entry=catalog.entries[i%catalog.entries.Length];if(!entry.prefab)continue;
                var p=positions[i];float scale=.75f+Hash(profile.seed+83,i,0)*.45f;
                float radius=Mathf.Max(entry.bounds.extents.x,entry.bounds.extents.z)*scale*.8f;
                float center=GroundHeight(p),a=GroundHeight(p+new Vector2(radius,radius)),b=GroundHeight(p+new Vector2(-radius,radius)),c=GroundHeight(p+new Vector2(radius,-radius)),d=GroundHeight(p-new Vector2(radius,radius));
                float y=Mathf.Min(center,a,b,c,d);if(Mathf.Max(center,a,b,c,d)-y>entry.definition.height*scale)continue;
                var rock=Object.Instantiate(entry.prefab,parent.transform);rock.transform.localPosition=new Vector3(p.x,y-.15f,p.y);rock.transform.localRotation=Quaternion.Euler(0,Hash(profile.seed+89,i,0)*360,0);rock.transform.localScale=Vector3.one*scale;
                MarkDerived(rock);studyRockRenderers.AddRange(rock.GetComponentsInChildren<Renderer>(true));
                studyRocks.Add(new MapRockInstance{id=10000+i,archetypeId=entry.definition.id,position=p,scale=scale,topOffset=y-.15f+entry.definition.height*scale+entry.definition.embed,manual=true});
            }
        }
        static MapRiverPath LocalFlowPath(bool lower)=>new MapRiverPath(new MapRiverRecord{id=lower?948:947,layoutVersion=2,nodes=lower?new List<MapRiverNode>{
            new MapRiverNode{id=1,position=new Vector2(-58,11),waterHeight=1,width=6.5f},
            new MapRiverNode{id=2,position=new Vector2(-58,-8),waterHeight=1,width=13},
            new MapRiverNode{id=3,position=new Vector2(-35,-55),waterHeight=.8f,width=7.5f}}:new List<MapRiverNode>{
            new MapRiverNode{id=1,position=new Vector2(-55.8f,65),waterHeight=24.15f,width=6.2f},
            new MapRiverNode{id=2,position=new Vector2(-58,24),waterHeight=24.15f,width=5.5f}}});
        void BindFlow(GameObject root,bool lower)
        {
            var path=WorldFlowPath(lower);var area=path.bounds;
            var flow=Own(RiverBankMeshBuilder.FlowMap(path,area,out var flowBounds));
            var block=new MaterialPropertyBlock();block.SetTexture("_LC_RiverFlowMap",flow);block.SetVector("_LC_FlowBounds",flowBounds);block.SetFloat("_LC_UseFlowMap",1);
            root.GetComponent<Renderer>().SetPropertyBlock(block);
        }
        MapRiverPath WorldFlowPath(bool lower)
        {
            var record=LocalFlowPath(lower).record;
            foreach(var node in record.nodes){Vector3 p=transform.TransformPoint(new Vector3(node.position.x,node.waterHeight,node.position.y));node.position=new Vector2(p.x,p.z);node.waterHeight=p.y;}
            return new MapRiverPath(record);
        }
        void BuildTrees()
        {
            var root=Child("Existing spruce prefabs · native materials and LODs",generated.transform);
            for(int z=-35;z<100&&treeCount<profile.maximumTrees;z+=12)
                for(int x=-135;x<135&&treeCount<profile.maximumTrees;x+=12)
                {
                    float key=Hash(profile.seed,x,z);if(key>profile.treeDensity)continue;
                    var p=new Vector2(x+(key-.5f)*9,z+(Hash(profile.seed+1,x,z)-.5f)*9);
                    if(OnOutcropShoulder(p))continue;
                    float y=GroundHeight(p);
                    if(y<1||y>52||Mathf.Abs(p.x+58)<(p.y<15?22:13))continue;
                    float dx=GroundHeight(p+Vector2.right)-GroundHeight(p-Vector2.right);
                    float dz=GroundHeight(p+Vector2.up)-GroundHeight(p-Vector2.up);
                    if(Mathf.Abs(dx)>1.5f||Mathf.Abs(dz)>1.5f)continue;
                    var tree=Object.Instantiate(profile.sprucePrefab,root.transform);tree.name="Border spruce "+treeCount;
                    tree.transform.localPosition=new Vector3(p.x,y,p.y);tree.transform.localRotation=Quaternion.Euler(0,key*360,0)*profile.sprucePrefab.transform.localRotation;tree.transform.localScale=profile.sprucePrefab.transform.localScale*(1.5f+key*1.1f);
                    foreach(var collider in tree.GetComponentsInChildren<Collider>(true))DestroyOwned(collider);
                    foreach(var renderer in tree.GetComponentsInChildren<Renderer>(true)){renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;}
                    MarkDerived(tree);plantedTrees.Add(tree.transform);treeCount++;
                }
        }
        void BuildGrass()
        {
            var data=Own(ScriptableObject.CreateInstance<MeadowGrassData>());data.lodMeshes=profile.grassSource.lodMeshes;
            var patches=new Dictionary<Vector2Int,List<Matrix4x4>>();
            for(int z=-66;z<72&&grassClumps<profile.maximumGrassClumps;z++)for(int x=-145;x<145&&grassClumps<profile.maximumGrassClumps;x++)
            {
                float key=Hash(profile.seed+37,x,z);if(key>profile.grassDensity)continue;
                var p=new Vector2(x+(key-.5f)*.9f,z+(Hash(profile.seed+41,x,z)-.5f)*.9f);
                if(OnOutcropShoulder(p))continue;
                float y=GroundHeight(p);if(y<1||y>50)continue;
                float dx=GroundHeight(p+Vector2.right)-GroundHeight(p-Vector2.right);
                float dz=GroundHeight(p+Vector2.up)-GroundHeight(p-Vector2.up);
                if(dx*dx+dz*dz>.9f)continue;
                float riverX=p.y>=-8?-58:Mathf.Lerp(-58,-35,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-8,-55,p.y)));
                float halfWidth=p.y<11?4+5*Mathf.Exp(-Mathf.Pow((p.y-2)/8,2)):p.y<27?4.2f:3.8f;
                if(p.y>-58&&p.y<66&&Mathf.Abs(p.x-riverX)<halfWidth+1)continue;
                var cell=new Vector2Int(Mathf.FloorToInt(p.x/16),Mathf.FloorToInt(p.y/16));
                if(!patches.TryGetValue(cell,out var matrices))patches.Add(cell,matrices=new List<Matrix4x4>());
                var position=transform.TransformPoint(new Vector3(p.x,y+.015f,p.y));
                matrices.Add(Matrix4x4.TRS(position,transform.rotation*Quaternion.Euler(0,key*360,0),Vector3.one*(.8f+key*.5f)));grassClumps++;
            }
            var result=new List<MeadowGrassData.Patch>();
            foreach(var pair in patches)
            {
                var center=transform.TransformPoint(new Vector3(pair.Key.x*16+8,0,pair.Key.y*16+8));
                result.Add(new MeadowGrassData.Patch{bounds=new Bounds(center+Vector3.up*27,new Vector3(22,60,22)),matrices=pair.Value.ToArray()});
            }
            data.patches=result.ToArray();data.authoredClumps=grassClumps;
            var material=Own(new Material(profile.grassMaterial));material.enableInstancing=true;
            material.SetFloat("_UseSurfacePaint",0);material.DisableKeyword("_LC_SURFACE_PAINT");
            var grass=Child("Existing native meadow grass · extended foothills",generated.transform).AddComponent<MeadowGrassRenderer>();
            grass.data=data;grass.material=material;grass.maximumDistance=125;grass.lodDistances=new Vector3(22,70,100);
        }
        void BeginCamera(ScriptableRenderContext context,Camera camera){if(generated)TickPresentation(camera,captureTime>=0?captureTime:Time.realtimeSinceStartup);}
        float GroundHeight(Vector2 point)=>groundQuery.Height(point.x,point.y);
        public void TickPresentation(Camera camera,float presentationTime)
        {
            if(!generated||!landRenderer)return;
            if(riverBodyOwned&&riverBodyOwned.HasProperty("_PreviewTime"))riverBodyOwned.SetFloat("_PreviewTime",captureTime>=0?captureTime:-1);
            float distance=camera?Vector3.Distance(camera.transform.position,transform.TransformPoint(new Vector3(0,55,80))):0;
            int lod=distance>profile.lod1Distance?2:distance>profile.lod0Distance?1:0;
            if(lod!=activeLandLod)
            {
                activeLandLod=lod;landFilter.sharedMesh=profile.lodMeshes[lod];landTriangles=landFilter.sharedMesh.triangles.Length/3;
                landRenderer.shadowCastingMode=lod==2?ShadowCastingMode.Off:ShadowCastingMode.On;landRenderer.receiveShadows=lod!=2;
                if(outcropFilter){outcropFilter.sharedMesh=profile.outcropLodMeshes[lod];if(outcropFilter.sharedMesh)landTriangles+=outcropFilter.sharedMesh.triangles.Length/3;}
                if(outcropRenderer){outcropRenderer.shadowCastingMode=lod==2?ShadowCastingMode.Off:ShadowCastingMode.On;outcropRenderer.receiveShadows=lod!=2;}
                if(plantedTrees.Count>0)
                {
                    if(lodQueries[lod]==null)lodQueries[lod]=new MountainRockDetailBuilder.HeightSampler(new List<Vector3>(profile.lodMeshes[lod].vertices),new List<int>(profile.lodMeshes[lod].triangles));
                    foreach(var tree in plantedTrees)if(tree){var position=tree.localPosition;position.y=lodQueries[lod].Height(position.x,position.z);tree.localPosition=position;}
                }
            }
            landProperties.SetFloat("_AlpineBaseHeight",transform.position.y);
            landProperties.SetFloat("_AlpineSnowLine",profile.snowAltitude);
            landProperties.SetFloat("_AlpineSnowBlend",profile.snowTransition);
            if(fall!=null){landProperties.SetVector("_AlpineWaterLip",fall.WetnessLip);landProperties.SetVector("_AlpineWaterLanding",fall.WetnessLanding);fall.UpdatePresentation(camera,presentationTime);}
            landRenderer.SetPropertyBlock(landProperties);
            if(joinRenderer)joinRenderer.SetPropertyBlock(landProperties);
            if(outcropRenderer)outcropRenderer.SetPropertyBlock(landProperties);
            foreach(var renderer in studyRockRenderers)if(renderer)renderer.SetPropertyBlock(landProperties);
            if(!Application.isPlaying&&fall!=null)
            {
                float delta=Mathf.Clamp(presentationTime-lastEditTime,0,.05f);lastEditTime=presentationTime;
                foreach(var particles in fall.Root.GetComponentsInChildren<ParticleSystem>())if(particles.isPlaying)particles.Simulate(delta,false,false,false);
            }
        }
        public void PrepareCapture(float time)
        {
            captureTime=time;
            fall?.SetPreviewTime(time);
            if(fall!=null)foreach(var particles in fall.Root.GetComponentsInChildren<ParticleSystem>())particles.Simulate(2f,false,true,false);
        }
        public void ReleaseCapture(){captureTime=-1;}
        public void Release()
        {
            fall?.Dispose();fall=null;riverEffects?.Dispose();riverEffects=null;
            if(generated)DestroyOwned(generated);generated=null;
            water?.Dispose();water=null;
            for(int i=owned.Count-1;i>=0;i--)if(owned[i])DestroyOwned(owned[i]);owned.Clear();
            landRenderer=null;landFilter=null;joinRenderer=null;outcropRenderer=null;outcropFilter=null;groundQuery=null;treeCount=0;grassClumps=0;
            studyRocks.Clear();studyRockRenderers.Clear();
            plantedTrees.Clear();lodQueries=null;
            riverBodyOwned=null;
        }
        static void DestroyOwned(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
        T Own<T>(T value)where T:Object{value.hideFlags=HideFlags.DontSave;owned.Add(value);return value;}
        static GameObject Child(string name,Transform parent){var root=new GameObject(name){hideFlags=HideFlags.DontSave};root.transform.SetParent(parent,false);return root;}
        GameObject MeshObject(string name,Mesh mesh,Material material){var root=Child(name,generated.transform);root.layer=4;root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.lightProbeUsage=LightProbeUsage.Off;return root;}
        static void MarkDerived(GameObject root){root.hideFlags=HideFlags.DontSave;foreach(Transform child in root.transform)MarkDerived(child.gameObject);}
        static float Hash(int seed,int x,int z){unchecked{uint h=(uint)(seed+x*73856093+z*19349663);h^=h>>16;h*=0x7feb352d;h^=h>>15;return(h&0xffffff)/16777215f;}}
        static bool OnOutcropShoulder(Vector2 p)=>p.y>=12&&p.y<=28&&((p.x>=-124&&p.x<=-77)||(p.x>=-23&&p.x<=0));
    }
}
