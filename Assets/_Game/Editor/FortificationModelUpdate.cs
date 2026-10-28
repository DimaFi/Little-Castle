using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleCastle.Editor
{
    /// <summary>Scoped migration of existing art fixtures; never regenerates terrain/world data.</summary>
    public static class FortificationModelUpdate
    {
        [Serializable] private sealed class Contract
        {
            public string version;
            public float segment_length;
            public float recommended_overlap_m;
            public Gate gate;
        }
        [Serializable] private sealed class Gate { public float hinge_x; }
        private const string Root = TerrainStarterSceneBuilder.Root;
        private static Contract Read()
        {
            var c=JsonUtility.FromJson<Contract>(File.ReadAllText(Root+"/Connections.json"));
            if(c.version!="v005" || c.segment_length<3 || c.gate==null)
                throw new InvalidOperationException("Import the reviewed fortifications v005 release first.");
            return c;
        }
        public static float SegmentLength => Read().segment_length;
        public static float Overlap => Read().recommended_overlap_m;
        public static float HingeX => Read().gate.hinge_x;
        public static float Spacing => SegmentLength-Overlap;
        public static float GateFirstCenter => 1.99f+SegmentLength*.5f-Overlap;
        public static float StudyTowerDistance => 1.99f+3*SegmentLength-4*Overlap+.90f;

        public static void ApplyAuthoredCollider(GameObject root)
        {
            // UCX covers the rigid leaf, not protruding handles. A renderer bounds box
            // can create artificial frame intersections while the actual door clears it.
            var guides=root.GetComponentsInChildren<MeshFilter>(true)
                .Where(m=>m.name.StartsWith("UCX_") && m.sharedMesh!=null).ToArray();
            if(guides.Length==0)return;
            if(root.name.Contains("Gatehouse"))return; // Existing frame boxes retain a passage.
            Bounds? combined=null;
            foreach(var mf in guides)
            {
                var b=mf.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    p=root.transform.InverseTransformPoint(mf.transform.TransformPoint(p));
                    if(!combined.HasValue)combined=new Bounds(p,Vector3.zero);
                    else {var value=combined.Value;value.Encapsulate(p);combined=value;}
                }
            }
            var box=root.GetComponent<BoxCollider>() ?? root.AddComponent<BoxCollider>();
            box.center=combined.Value.center;box.size=combined.Value.size;
        }

        [MenuItem("Little Castle/Testing/Update existing fortifications to v005")]
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save dirty scenes before migration.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            var log=new StringBuilder("Fortifications v005 update\n");
            try
            {
                Read();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach(var path in Directory.GetFiles(Root,"*.prefab",SearchOption.AllDirectories)
                    .Where(p=>Path.GetFileName(p).StartsWith("SM_Wall_") || Path.GetFileName(p).StartsWith("SM_ArcherTower_")))
                {
                    string assetPath=path.Replace('\\','/');var prefab=PrefabUtility.LoadPrefabContents(assetPath);
                    try
                    {
                        ApplyAuthoredCollider(prefab);
                        foreach(var group in prefab.GetComponentsInChildren<LODGroup>(true))group.RecalculateBounds();
                        foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                            if(mf.sharedMesh==null)throw new InvalidOperationException("Missing mesh "+assetPath+" "+mf.name);
                        var lod=prefab.GetComponent<LODGroup>();
                        if(lod==null || lod.GetLODs().Length!=3)throw new InvalidOperationException("Invalid LODs "+assetPath);
                        foreach(var level in lod.GetLODs())
                            if(level.renderers.Length==0 || level.renderers.Any(r=>r==null))throw new InvalidOperationException("Broken LOD renderer "+assetPath);
                        if(prefab.name=="SM_Wall_Stone_2m_A" && Mathf.Abs(prefab.GetComponent<BoxCollider>().size.z-SegmentLength)>.001f)
                            throw new InvalidOperationException("Imported module length mismatch");
                        PrefabUtility.SaveAsPrefabAsset(prefab,assetPath);log.AppendLine("PREFAB_PASS "+assetPath);
                    }
                    finally {PrefabUtility.UnloadPrefabContents(prefab);}
                    Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    ProductionAssetValidator.ValidateSelected();
                }
                foreach(var path in new[]{TerrainStarterSceneBuilder.ScenePath,TerrainStarterSceneBuilder.StudyPath})
                    if(File.Exists(path))UpdateScene(path,log);
                // Only definitions already pointing to this imported wall kit are eligible.
                foreach(string guid in AssetDatabase.FindAssets("t:WallPlacementDefinition"))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);
                    var so=new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
                    var reference=so.FindProperty("segmentPrefab").objectReferenceValue;
                    if(reference==null || !AssetDatabase.GetAssetPath(reference).StartsWith(Root+"/"))continue;
                    so.FindProperty("segmentLength").floatValue=reference.name.Contains("1m")?SegmentLength*.5f:SegmentLength;
                    so.FindProperty("spacingMultiplier").floatValue=Spacing/SegmentLength;
                    so.ApplyModifiedPropertiesWithoutUndo();log.AppendLine("DEFINITION_UPDATED "+path);
                }
                ValidateGate(log);AssetDatabase.SaveAssets();log.AppendLine("PASS");
            }
            finally
            {
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/fortifications-v005-update.txt",log.ToString());
                if(setup.Any(s=>s.isLoaded && s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void UpdateScene(string path,StringBuilder log)
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            bool study=path==TerrainStarterSceneBuilder.StudyPath;
            foreach(var root in scene.GetRootGameObjects().Where(o=>o.name.StartsWith("Fortifications")))
            {
                var children=root.transform.Cast<Transform>().ToArray();
                var gate=children.FirstOrDefault(t=>t.name=="SM_Wall_Gatehouse_A");
                if(gate==null)
                {
                    if(!root.name.Contains("v005"))
                    {
                        var towers=children.Where(t=>t.name=="SM_ArcherTower_A").ToArray();
                        var row=children.Where(t=>t.name=="SM_Wall_Stone_2m_A").ToArray();
                        if(towers.Length==0)throw new InvalidOperationException("No tower anchor for old fixture "+path);
                        foreach(var tower in towers)for(int side=-1;side<=1;side+=2)
                        {
                            var sections=row.Where(t=>(t.position.x-tower.position.x)*side>0 &&
                                towers.OrderBy(a=>Mathf.Abs(a.position.x-t.position.x)).First()==tower)
                                .OrderBy(t=>Mathf.Abs(t.position.x-tower.position.x)).ToArray();
                            for(int i=0;i<sections.Length;i++)
                            {
                                var p=sections[i].position;p.x=tower.position.x+side*(.90f+SegmentLength*.5f-Overlap+i*Spacing);
                                sections[i].position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(sections[i]);
                            }
                        }
                        root.name="Fortifications v005 • rigid authored modules";
                    }
                    log.AppendLine("WALL_ROWS_UPDATED_NO_GATE "+path);continue;
                }
                // Both existing fixtures have their settlement north (+world Z) of the gate.
                gate.rotation=Quaternion.Euler(0,-90,0);
                foreach(Transform child in gate)
                {
                    bool left=child.name=="SM_Wall_GateLeaf_Left_A",right=child.name=="SM_Wall_GateLeaf_Right_A";
                    if(!left&&!right)continue;
                    child.localPosition=new Vector3(HingeX,0,left?-1.04f:1.04f);
                    child.localRotation=Quaternion.Euler(0,left?90:-90,0);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
                if(!root.name.Contains("v005"))
                {
                    var walls=children.Where(t=>t.name=="SM_Wall_Stone_2m_A").ToArray();
                    float gx=gate.position.x,gz=gate.position.z;
                    var front=walls.Where(t=>Mathf.Abs(t.position.z-gz)<.25f).ToArray();
                    var lateral=walls.Except(front).OrderBy(t=>t.position.z).ToArray();
                    for(int side=-1;side<=1;side+=2)
                    {
                        var row=front.Where(t=>(t.position.x-gx)*side>0).OrderBy(t=>Mathf.Abs(t.position.x-gx)).ToArray();
                        for(int i=0;i<row.Length;i++)
                        {
                            var p=row[i].position;p.x=gx+side*((study?GateFirstCenter:3.90f+SegmentLength*.5f-Overlap)+i*Spacing);
                            row[i].position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(row[i]);
                        }
                        if(study)foreach(var tower in children.Where(t=>t.name=="SM_ArcherTower_A" && (t.position.x-gx)*side>0))
                        {
                            var p=tower.position;p.x=gx+side*StudyTowerDistance;tower.position=p;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(tower);
                        }
                    }
                    if(study)for(int i=0;i<lateral.Length;i++)
                    {
                        var p=lateral[i].position;p.x=gx-StudyTowerDistance;p.z=gz+.90f+SegmentLength*.5f-Overlap+i*Spacing;
                        lateral[i].position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(lateral[i]);
                    }
                }
                root.name="Fortifications v005 • rigid authored modules";
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            log.AppendLine("SCENE_UPDATED_SCOPED "+path);
        }

        private static void ValidateGate(StringBuilder log)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var frame=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Study/Prefabs/SM_Wall_Gatehouse_A.prefab"));
            SceneManager.MoveGameObjectToScene(frame,scene);
            try
            {
                int tests=0;
                foreach(var side in new[]{"Left","Right"})
                {
                    bool left=side=="Left";
                    var leaf=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Study/Prefabs/SM_Wall_GateLeaf_"+side+"_A.prefab"));
                    SceneManager.MoveGameObjectToScene(leaf,scene);
                    leaf.transform.position=new Vector3(HingeX,0,left?-1.04f:1.04f);
                    for(int step=0;step<=180;step++)
                    {
                        leaf.transform.rotation=Quaternion.Euler(0,(left?1:-1)*step*.5f,0);
                        foreach(var a in frame.GetComponentsInChildren<Collider>())
                        foreach(var b in leaf.GetComponentsInChildren<Collider>())
                            if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float distance) && distance>1e-5f)
                                throw new InvalidOperationException("Gate collider collision "+side+" "+step*.5f+" depth "+distance);
                        tests++;
                    }
                    UnityEngine.Object.DestroyImmediate(leaf);
                }
                log.AppendLine("UNITY_GATE_PHYSICS_PASS "+tests+" poses, 0..90 degrees, both leaves");
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
