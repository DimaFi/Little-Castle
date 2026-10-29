using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>Presentation-only tile overlays; shares the water mesh and owns one material.</summary>
    public sealed class RiverSurfaceEffects : IDisposable
    {
        readonly Material material;
        readonly RiverVisualQualityProfile profile;
        readonly MapRockCatalog rocks;
        readonly MapEditSession session;
        public RiverSurfaceEffects(MapEditSession session,MapRockCatalog rocks,RiverVisualQualityProfile profile)
        {
            this.session=session;this.rocks=rocks;this.profile=profile;
            var source=Resources.Load<Material>("MapAuthoring/RiverSurfaceEffects");
            var shader=Resources.Load<Shader>("MapAuthoring/RiverSurfaceEffectsShader");if(!shader)shader=Shader.Find("Little Castle/Rivers/Surface Effects");
            if(source||shader)material=source?new Material(source):new Material(shader);
            if(material){material.name="River surface effects (owned)";material.hideFlags=HideFlags.HideAndDontSave;}
        }
        public bool Available=>material;
        // Built once on a residency refresh, then queried only in each tile's bounds.
        public sealed class RockIndex
        {
            const float CellSize=32;
            readonly Dictionary<Vector2Int,List<MapRockInstance>> cells=new Dictionary<Vector2Int,List<MapRockInstance>>();
            public void Add(MapRockInstance rock)
            {
                if(rock==null||rock.removed)return;
                var key=new Vector2Int(Mathf.FloorToInt(rock.position.x/CellSize),Mathf.FloorToInt(rock.position.y/CellSize));
                if(!cells.TryGetValue(key,out var cell))cells.Add(key,cell=new List<MapRockInstance>());
                cell.Add(rock);
            }
            public IEnumerable<MapRockInstance> Query(Rect area)
            {
                int x0=Mathf.FloorToInt(area.xMin/CellSize),x1=Mathf.FloorToInt(area.xMax/CellSize);
                int y0=Mathf.FloorToInt(area.yMin/CellSize),y1=Mathf.FloorToInt(area.yMax/CellSize);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    if(cells.TryGetValue(new Vector2Int(x,y),out var cell))foreach(var rock in cell)if(area.Contains(rock.position))yield return rock;
            }
        }
        public bool Attach(GameObject root,Mesh mesh,MapRiverPath path,Rect bounds,IEnumerable<MapRockInstance> riverRocks,IEnumerable<MapRockInstance> sectionRocks,Material water,bool waterOnly)
        {
            if(!material||!profile||profile.quality==RiverEffectsQuality.Off||mesh.GetIndexCount(0)/3>Mathf.Max(0,profile.maximumTrianglesPerTile))return false;
            var obstacles=new List<Vector4>(profile.ObstacleLimit);
            var obstacleFlows=new List<Vector4>(profile.ObstacleLimit);
            void Consider(MapRockInstance rock,bool isRiverRock)
            {
                if(rock==null||rock.removed||obstacles.Count>=profile.ObstacleLimit||!bounds.Contains(rock.position))return;
                if(isRiverRock&&waterOnly&&path.record.layoutVersion!=1&&!rock.manual)return;
                if(!path.Nearest(rock.position,out var frame,out var distance)||Mathf.Abs(distance)>frame.width*.5f+1)return;
                if(isRiverRock&&!rock.manual&&(distance>=0?frame.leftSand:frame.rightSand)>.12f)return;
                var entry=rocks?rocks.Resolve(rock.archetypeId,rock.catalogVersion):null;
                if(entry==null||!entry.prefab)return;
                float bottom=session.SampleBaseHeight(rock.position.x,rock.position.y)+rock.topOffset-entry.definition.embed-entry.definition.height*rock.scale;
                float top=bottom+entry.definition.height*rock.scale;
                if(bottom>frame.water+.3f||top<frame.water-.3f)return;
                float radius=Mathf.Clamp(Mathf.Max(entry.bounds.extents.x,entry.bounds.extents.z)*rock.scale,.35f,6f);
                obstacles.Add(new Vector4(rock.position.x,rock.position.y,radius,frame.station));
                obstacleFlows.Add(new Vector4(frame.tangent.x,frame.tangent.y,frame.speed,0));
            }
            if(riverRocks!=null)foreach(var rock in riverRocks)Consider(rock,true);
            if(sectionRocks!=null)foreach(var rock in sectionRocks)Consider(rock,false);
            var child=new GameObject("Living river surface"){hideFlags=HideFlags.HideAndDontSave,layer=4};child.transform.SetParent(root.transform,false);
            child.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;
            var block=new MaterialPropertyBlock();block.SetColor("_FoamColor",profile.foamColor);block.SetVector("_Effects",new Vector4(profile.shorelineFoam,profile.obstacleFoam,profile.rapids,profile.shorelineDepth));block.SetFloat("_AnimationSpeed",profile.animationSpeed);
            block.SetFloat("_ObstacleCount",obstacles.Count);
            while(obstacles.Count<8){obstacles.Add(Vector4.zero);obstacleFlows.Add(Vector4.zero);}
            block.SetVectorArray("_Obstacles",obstacles);block.SetVectorArray("_ObstacleFlows",obstacleFlows);
            if(water&&water.IsKeywordEnabled("_ENABLEWAVE"))
            {
                for(int i=1;i<=2;i++){string prefix="_"+(i==1?"1st":"2nd")+"_Wave_";block.SetVector("_Wave"+i,new Vector4(water.GetFloat(prefix+"Length"),water.GetFloat(prefix+"Height"),water.GetFloat(prefix+"Speed"),water.GetFloat(prefix+"Sharpness")));block.SetVector("_WaveDirection"+i,water.GetVector(prefix+"Direction"));}
                block.SetFloat("_UseWaves",1);
            }
            renderer.SetPropertyBlock(block);return true;
        }
        public void ApplyWetRock(GameObject root,MapRockInstance record,MapRockCatalog.Entry entry)
        {
            if(!profile||profile.quality==RiverEffectsQuality.Off||profile.wetRock<=0||!session.QueryWater(record.position,out float water,out _,out _))return;
            float bottom=root.transform.position.y,top=bottom+entry.definition.height*record.scale;
            if(bottom>water+.3f||top<water-.3f)return;
            float amount=Mathf.Clamp01(profile.wetRock);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var m=materials[i];if(!m)continue;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);bool changed=false;
                    if(m.HasProperty("_Color")){var c=m.GetColor("_Color");c.r*=1-.22f*amount;c.g*=1-.18f*amount;c.b*=1-.12f*amount;block.SetColor("_Color",c);changed=true;}
                    if(m.HasProperty("_Roughness")){block.SetFloat("_Roughness",Mathf.Lerp(m.GetFloat("_Roughness"),.3f,amount));changed=true;}
                    if(m.HasProperty("_SpecularStrength")){block.SetFloat("_SpecularStrength",Mathf.Lerp(m.GetFloat("_SpecularStrength"),.35f,amount));changed=true;}
                    if(changed)renderer.SetPropertyBlock(block,i);
                }
            }
        }
        public void Dispose(){if(material){if(Application.isPlaying)UnityEngine.Object.Destroy(material);else UnityEngine.Object.DestroyImmediate(material);}}
    }
}
