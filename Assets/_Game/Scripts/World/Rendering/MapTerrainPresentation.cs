using System;
using System.Collections.Generic;
using LittleCastle.World;
using LittleCastle.Research;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>Imports the existing regular terrain lattice, without regenerating its shape.</summary>
    public static class MapHeightMeshImport
    {
        public static MapHeightFieldData Read(Mesh mesh,Transform transform,out int columns,out int rows)
        {
            var vertices=mesh.vertices;columns=1;
            while(columns<vertices.Length&&Mathf.Abs(vertices[columns].z-vertices[0].z)<.0001f)columns++;
            rows=vertices.Length/columns;
            if(columns<2||rows<2||columns*rows!=vertices.Length||transform.rotation!=Quaternion.identity||transform.lossyScale!=Vector3.one)
                throw new ArgumentException("Height authoring requires an unrotated regular X/Z lattice at unit scale.");
            float spacing=(vertices[columns-1].x-vertices[0].x)/(columns-1);
            var first=transform.TransformPoint(vertices[0]);var data=new MapHeightFieldData{origin=new Vector2(first.x,first.z),spacing=spacing};
            for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
            {
                var p=transform.TransformPoint(vertices[z*columns+x]);
                if(Mathf.Abs(p.x-(first.x+x*spacing))>.001f||Mathf.Abs(p.z-(first.z+z*spacing))>.001f)throw new ArgumentException("Terrain lattice is not regular/square.");
                MapHeightField.SetBase(data,x,z,p.y);
            }
            // Validate before changing any renderer/collider.
            _=new MapHeightField(data);return data;
        }
    }

    /// <summary>
    /// Derived, disposable view of a height session. Replaces one study mesh with small mesh/collider
    /// chunks; projects existing plant matrices and tree anchors. Source assets remain immutable.
    /// </summary>
    public sealed class MapTerrainPresentation : IDisposable
    {
        sealed class Chunk
        {
            public GameObject root;public Mesh mesh,collisionMesh;public MeshCollider collider;
            public Vector3[] vertices,normals;public Rect bounds;public int x,z,width,depth;
        }
        struct Anchor {public Transform transform;public Vector3 original;public float offset;}
        readonly MapEditSession session;
        readonly MapHeightField reference;
        readonly MeshFilter source;
        readonly MeshRenderer sourceRenderer;
        readonly MeshCollider sourceCollider;
        readonly bool rendererEnabled,colliderEnabled;
        readonly List<Chunk> chunks=new List<Chunk>();
        readonly Queue<Chunk> collisionQueue=new Queue<Chunk>();
        readonly HashSet<Chunk> queued=new HashSet<Chunk>();
        readonly List<Anchor> anchors=new List<Anchor>();
        readonly MeadowGrassRenderer grass;
        readonly MeadowGrassData originalGrass,derivedGrass;
        readonly InnerverseGrassStudy accents;
        readonly Matrix4x4[] originalAccents;
        readonly MeadowScatterRenderer scatter;
        readonly Func<float,float,float> previousHeightSampler;
        readonly int columns,rows;
        readonly Dictionary<int,float[]> baseGrassHeights=new Dictionary<int,float[]>();
        readonly Dictionary<int,Vector3[]> baseGrassNormals=new Dictionary<int,Vector3[]>();
        readonly HashSet<Collider> ownedColliders=new HashSet<Collider>();
        readonly RaycastHit[] rayHits=new RaycastHit[64];
        readonly Vector3[] baseVertices;
        public int LastTerrainChunks {get;private set;}
        public int LastGrassPatches {get;private set;}
        public int LastAnchors {get;private set;}
        public int PendingColliders=>collisionQueue.Count;
        public int ChunkCount=>chunks.Count;
        public MapTerrainPresentation(MapEditSession session,MeshFilter terrain,MeadowGrassRenderer grass=null,
            InnerverseGrassStudy accents=null,MeadowScatterRenderer scatter=null,bool projectSceneTrees=true)
        {
            this.session=session;source=terrain;this.grass=grass;this.accents=accents;this.scatter=scatter;
            var baseData=MapHeightMeshImport.Read(terrain.sharedMesh,terrain.transform,out columns,out rows);reference=new MapHeightField(baseData);
            var settings=session.HeightSettings;var offset=(baseData.origin-settings.origin)/settings.spacing;
            if(Mathf.Abs(settings.spacing-baseData.spacing)>.00001f||Mathf.Abs(offset.x-Mathf.Round(offset.x))>.001f||Mathf.Abs(offset.y-Mathf.Round(offset.y))>.001f)
                throw new ArgumentException("Terrain view and authoritative map must use the same world height lattice.");
            baseVertices=terrain.sharedMesh.vertices;sourceRenderer=terrain.GetComponent<MeshRenderer>();sourceCollider=terrain.GetComponent<MeshCollider>();
            rendererEnabled=sourceRenderer.enabled;colliderEnabled=sourceCollider&&sourceCollider.enabled;
            originalGrass=grass?grass.data:null;
            if(originalGrass){derivedGrass=UnityEngine.Object.Instantiate(originalGrass);derivedGrass.name="Derived edited Meadow grass";derivedGrass.hideFlags=HideFlags.HideAndDontSave;grass.data=derivedGrass;}
            originalAccents=accents?accents.authoredMatrices:null;
            if(originalAccents!=null)accents.authoredMatrices=(Matrix4x4[])originalAccents.Clone();
            if(scatter){previousHeightSampler=scatter.heightSampler;scatter.heightSampler=session.SampleHeight;}
            if(projectSceneTrees)
            {
                var candidates=new HashSet<Transform>();
                foreach(var anchor in UnityEngine.Object.FindObjectsByType<MapTerrainAnchor>())candidates.Add(anchor.transform);
                var catalog=MapTreeCatalog.Default;if(catalog)foreach(var tree in catalog.SceneSources())candidates.Add(tree.transform);
                foreach(var target in candidates)
                {
                    var p=target.position;var b=session.Bounds;
                    if(p.x<b.x||p.z<b.y||p.x>b.x+b.z||p.z>b.y+b.w)continue;
                    anchors.Add(new Anchor{transform=target,original=p,offset=p.y-reference.Height(p.x,p.z)});
                }
            }
            BuildChunks();sourceRenderer.enabled=false;if(sourceCollider)sourceCollider.enabled=false;
            var map=session.Bounds;ProjectPlants(new Rect(map.x-2,map.y-2,map.z+4,map.w+4));
            session.Changed+=Changed;Physics.SyncTransforms();
        }
        void BuildChunks()
        {
            var mesh=source.sharedMesh;var colors=mesh.colors;var uvs=mesh.uv;
            const int cells=16;
            for(int z=0;z<rows-1;z+=cells)for(int x=0;x<columns-1;x+=cells)
            {
                int width=Mathf.Min(cells,columns-1-x),depth=Mathf.Min(cells,rows-1-z),count=(width+1)*(depth+1);
                var chunk=new Chunk{x=x,z=z,width=width,depth=depth,vertices=new Vector3[count],normals=new Vector3[count]};
                chunk.root=new GameObject("Terrain view "+x+","+z){hideFlags=HideFlags.HideAndDontSave,layer=source.gameObject.layer};
                chunk.root.transform.SetParent(source.transform,false);
                var localUV=new Vector2[count];var localColors=new Color[count];var triangles=new int[width*depth*6];
                for(int dz=0;dz<=depth;dz++)for(int dx=0;dx<=width;dx++)
                {int i=dz*(width+1)+dx,original=(z+dz)*columns+x+dx;chunk.vertices[i]=baseVertices[original];if(uvs.Length>0)localUV[i]=uvs[original];if(colors.Length>0)localColors[i]=colors[original];}
                int t=0;for(int dz=0;dz<depth;dz++)for(int dx=0;dx<width;dx++)
                {int i=dz*(width+1)+dx;triangles[t++]=i;triangles[t++]=i+width+1;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+width+1;triangles[t++]=i+width+2;}
                chunk.mesh=new Mesh{name=chunk.root.name,hideFlags=HideFlags.HideAndDontSave};chunk.mesh.MarkDynamic();chunk.mesh.vertices=chunk.vertices;
                chunk.mesh.uv=localUV;if(colors.Length>0)chunk.mesh.colors=localColors;chunk.mesh.triangles=triangles;
                var first=source.transform.TransformPoint(chunk.vertices[0]);var last=source.transform.TransformPoint(chunk.vertices[count-1]);
                chunk.bounds=Rect.MinMaxRect(first.x,first.z,last.x,last.z);
                UpdateChunk(chunk);chunk.root.AddComponent<MeshFilter>().sharedMesh=chunk.mesh;
                var renderer=chunk.root.AddComponent<MeshRenderer>();renderer.sharedMaterials=sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode=sourceRenderer.shadowCastingMode;renderer.receiveShadows=sourceRenderer.receiveShadows;
                renderer.lightProbeUsage=sourceRenderer.lightProbeUsage;
                chunk.collisionMesh=UnityEngine.Object.Instantiate(chunk.mesh);chunk.collisionMesh.name=chunk.root.name+" collision";chunk.collisionMesh.hideFlags=HideFlags.HideAndDontSave;
                chunk.collider=chunk.root.AddComponent<MeshCollider>();chunk.collider.sharedMesh=chunk.collisionMesh;ownedColliders.Add(chunk.collider);chunks.Add(chunk);
            }
        }
        void UpdateChunk(Chunk chunk)
        {
            for(int i=0;i<chunk.vertices.Length;i++)
            {
                var world=source.transform.TransformPoint(chunk.vertices[i]);world.y=session.SampleHeight(world.x,world.z);
                chunk.vertices[i]=source.transform.InverseTransformPoint(world);chunk.normals[i]=source.transform.InverseTransformDirection(session.SampleNormal(world.x,world.z));
            }
            chunk.mesh.vertices=chunk.vertices;chunk.mesh.normals=chunk.normals;chunk.mesh.RecalculateBounds();
        }
        void Changed(MapRegionChange change)
        {
            // One revision can now arrive as several disjoint chunk-local notifications.
            if((change.kind&MapChangeKind.Height)==0)return;
            LastTerrainChunks=0;
            foreach(var chunk in chunks)if(chunk.bounds.Overlaps(change.bounds))
            {UpdateChunk(chunk);LastTerrainChunks++;if(queued.Add(chunk))collisionQueue.Enqueue(chunk);}
            ProjectPlants(change.bounds);scatter?.Rebuild(scatter.painter,change.bounds);
        }
        void ProjectPlants(Rect area)
        {
            LastGrassPatches=LastAnchors=0;
            if(derivedGrass)for(int p=0;p<derivedGrass.patches.Length;p++)
            {
                var original=originalGrass.patches[p];var b=original.bounds;var patchArea=new Rect(b.min.x,b.min.z,b.size.x,b.size.z);
                if(!patchArea.Overlaps(area))continue;LastGrassPatches++;
                var target=derivedGrass.patches[p];bool first=true;Bounds bounds=default;
                if(!baseGrassHeights.TryGetValue(p,out var baseHeights))
                {
                    baseHeights=new float[original.matrices.Length];var normals=new Vector3[baseHeights.Length];
                    for(int i=0;i<baseHeights.Length;i++){Vector3 pos=original.matrices[i].GetColumn(3);baseHeights[i]=reference.Height(pos.x,pos.z);normals[i]=reference.Normal(pos.x,pos.z);}
                    baseGrassHeights.Add(p,baseHeights);baseGrassNormals.Add(p,normals);
                }
                var baseNormals=baseGrassNormals[p];
                for(int i=0;i<target.matrices.Length;i++)
                {
                    Vector3 origin=original.matrices[i].GetColumn(3);var matrix=target.matrices[i];
                    if(area.Contains(new Vector2(origin.x,origin.z))){matrix=ProjectMatrix(original.matrices[i],true,baseHeights[i],baseNormals[i]);target.matrices[i]=matrix;}
                    Vector3 pos=matrix.GetColumn(3);
                    if(first){bounds=new Bounds(pos,Vector3.zero);first=false;}else bounds.Encapsulate(pos);
                }
                bounds.Expand(new Vector3(3.2f,3.2f,3.2f));target.bounds=bounds;
            }
            if(originalAccents!=null)for(int i=0;i<originalAccents.Length;i++)
            {Vector3 p=originalAccents[i].GetColumn(3);if(area.Contains(new Vector2(p.x,p.z)))accents.authoredMatrices[i]=ProjectMatrix(originalAccents[i],false);}
            foreach(var anchor in anchors)if(anchor.transform&&area.Contains(new Vector2(anchor.original.x,anchor.original.z)))
            {var p=anchor.original;p.y=session.SampleHeight(p.x,p.z)+anchor.offset;anchor.transform.position=p;LastAnchors++;}
        }
        Matrix4x4 ProjectMatrix(Matrix4x4 original,bool alignNormal,float? baseHeight=null,Vector3? baseNormal=null)
        {
            Vector3 p=original.GetColumn(3);if(!session.VegetationAllowed(p.x,p.z))return Matrix4x4.TRS(new Vector3(p.x,session.SampleHeight(p.x,p.z)-2,p.z),Quaternion.identity,Vector3.zero);float delta=session.SampleHeight(p.x,p.z)-(baseHeight??reference.Height(p.x,p.z));
            var from=baseNormal??reference.Normal(p.x,p.z);var to=alignNormal?session.SampleNormal(p.x,p.z):from;
            if(Mathf.Abs(delta)<.000001f&&(!alignNormal||(from-to).sqrMagnitude<.00000001f))return original;
            p.y+=delta;var rotation=original.rotation;if(alignNormal)rotation=Quaternion.FromToRotation(from,to)*rotation;
            return Matrix4x4.TRS(p,rotation,original.lossyScale);
        }
        /// <summary>Bounded work once per owning view update, never per plant.</summary>
        public int FlushColliderBudget(int maximumChunks=2)
        {
            int updated=0;while(updated<maximumChunks&&collisionQueue.Count>0)
            {var chunk=collisionQueue.Dequeue();queued.Remove(chunk);chunk.collisionMesh.vertices=chunk.vertices;chunk.collisionMesh.RecalculateBounds();chunk.collider.sharedMesh=null;chunk.collider.sharedMesh=chunk.collisionMesh;updated++;}return updated;
        }
        public bool Raycast(Ray ray,out RaycastHit hit,float distance=10000)
        {
            hit=default;bool found=false;
            int count=Physics.RaycastNonAlloc(ray,rayHits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count<rayHits.Length)
            {
                for(int i=0;i<count;i++)if(ownedColliders.Contains(rayHits[i].collider)&&rayHits[i].distance<distance)
                {hit=rayHits[i];distance=hit.distance;found=true;}
            }
            else foreach(var chunk in chunks)if(chunk.collider.Raycast(ray,out var current,distance)){hit=current;distance=current.distance;found=true;}
            return found;
        }
        public void Dispose()
        {
            session.Changed-=Changed;if(sourceRenderer)sourceRenderer.enabled=rendererEnabled;if(sourceCollider)sourceCollider.enabled=colliderEnabled;
            foreach(var chunk in chunks){Destroy(chunk.root);Destroy(chunk.mesh);Destroy(chunk.collisionMesh);}chunks.Clear();ownedColliders.Clear();collisionQueue.Clear();queued.Clear();
            if(grass)grass.data=originalGrass;if(derivedGrass)Destroy(derivedGrass);if(accents)accents.authoredMatrices=originalAccents;
            foreach(var anchor in anchors)if(anchor.transform)anchor.transform.position=anchor.original;
            if(scatter)scatter.heightSampler=previousHeightSampler;
        }
        static void Destroy(UnityEngine.Object obj){if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
    }
}
