using System;
using System.IO;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>Dedicated asset fixture + bounded source terrain audit. Never edits the existing concept scene.</summary>
    public static class CliffKitDesktopReview
    {
        [Serializable] private class SeedRecord
        {
            public int seed; public float maxScarpMask; public Vector2 sampledHill;
            public bool negativeHeightSeam, regenerationIdentical, rampAccepted, cliffAccepted;
            public string rampReason, cliffReason;
        }
        [Serializable] private class Review
        {
            public string unity, status="SOURCE_TERRAIN_AUDIT_ONLY", macro="NOT_SUPPLIED", nav="NOT_RUN", playerGpu="NOT_RUN";
            public List<SeedRecord> seeds=new List<SeedRecord>();
        }

        public static void Run()
        {
            var definition=AssetDatabase.LoadAssetAtPath<WorldDefinition>("Assets/_Game/Settings/World/ConceptWorld_v001/ConceptWorldDefinition.asset");
            if(definition==null)throw new InvalidOperationException("Actual saved concept settings required.");
            var stage=AssetDatabase.LoadAssetAtPath<LayeredTerrainStage>("Assets/_Game/Settings/World/ConceptWorld_v001/Stages/01_LayeredTerrain.asset");
            var report=new Review{unity=Application.unityVersion};
            foreach(int seed in new[]{12345,54321,-10101,777})
            {
                var pipeline=new WorldGenerationPipeline(definition.GenerationSettings);
                var west=pipeline.GenerateChunkThroughPhase(seed,new ChunkCoordinate(-1,-1),WorldGenerationStagePhase.TerrainAnalysis);
                var east=pipeline.GenerateChunkThroughPhase(seed,new ChunkCoordinate(0,-1),WorldGenerationStagePhase.TerrainAnalysis);
                var repeat=pipeline.GenerateChunkThroughPhase(seed,new ChunkCoordinate(-1,-1),WorldGenerationStagePhase.TerrainAnalysis);
                var record=new SeedRecord{seed=seed,negativeHeightSeam=true,regenerationIdentical=true};
                int n=west.CellsPerSide;
                for(int z=0;z<=n;z++)record.negativeHeightSeam &= west.GetHeight(n,z)==east.GetHeight(0,z);
                for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)record.regenerationIdentical &= west.GetHeight(x,z)==repeat.GetHeight(x,z);
                // Search only the actual 1024m concept_slice interior, not a fabricated highland.
                for(int z=-480;z<=480;z+=16)for(int x=-480;x<=480;x+=16)
                {
                    var sample=StylizedLandformSampler.Sample(seed,x,z,stage.StylizedLandforms);
                    if(sample.ScarpMask>record.maxScarpMask){record.maxScarpMask=sample.ScarpMask;record.sampledHill=new Vector2(x,z);}
                }
                var probe=new WorldTerrainProbe(pipeline,definition.GenerationSettings,seed);
                bool Ground(Vector2 point,out float height){height=probe.Sample(point).height;return true;}
                Ground(record.sampledHill,out float toeHeight);
                var toe=new Vector3(record.sampledHill.x,toeHeight,record.sampledHill.y);
                var ramp=CliffTerrainContactValidator.ValidateRamp(toe,0,7,24,6.5f,3.5f,.25f,30,Ground,p=>false);
                var cliff=CliffTerrainContactValidator.ValidateStraightCliff(toe,0,16,5,6,.45f,Ground,p=>false);
                record.rampAccepted=ramp.Accepted;record.rampReason=ramp.Reason;
                record.cliffAccepted=cliff.Accepted;record.cliffReason=cliff.Reason;
                if(!record.negativeHeightSeam||!record.regenerationIdentical)throw new InvalidOperationException("Terrain regression at seed "+seed);
                report.seeds.Add(record);probe.Clear();
            }
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CliffKitTerrainAudit.json",JsonUtility.ToJson(report,true));
            RenderFixture();
            Debug.Log("CLIFFKIT_DESKTOP_REVIEW 4 source seeds; negative seams/regeneration checked. Fixture is NOT procedural-world or navigation acceptance.");
        }

        private static void RenderFixture()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var catalog=JsonUtility.FromJson<CliffKitAssetIntake.Catalog>(File.ReadAllText(CliffKitAssetIntake.Destination+"/ASSET_CATALOG.json"));
            var originals=new List<GameObject>();
            foreach(var entry in catalog.assets)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(CliffKitAssetIntake.Destination+"/Prefabs/"+entry.id+".prefab");
                if(prefab==null)throw new InvalidDataException("Missing real prefab: "+entry.id);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.SetActive(false);originals.Add(instance);
            }
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="QA fixture ground (not a world heightfield)";
            ground.transform.localScale=Vector3.one*20;ground.transform.position=Vector3.down*.46f;
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            var groundMaterial=new Material(Shader.Find("Little Castle/Surface/LC Stylized Lit"));groundMaterial.SetColor("_Color",new Color(.34f,.42f,.22f));
            ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
            var camera=new GameObject("Fixture Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.55f,.65f,.75f);camera.nearClipPlane=.05f;camera.farClipPlane=600;
            var light=new GameObject("Fixture Sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,-35,0);light.shadows=LightShadows.Soft;
            foreach(string family in new[]{"RockA","RockB","GrassCap"})
            {
                var far=AssetDatabase.LoadAssetAtPath<Material>(CliffKitAssetIntake.Destination+"/Materials/CliffKit_"+family+"_Far.mat");
                far.SetFloat("_TopLightStrength",.9f);far.SetFloat("_AmbientStrength",1.25f);EditorUtility.SetDirty(far);
            }
            for(int i=0;i<catalog.assets.Length;i++)
            {
                var entry=catalog.assets[i];var ob=originals[i];ob.SetActive(true);ob.GetComponent<LODGroup>().ForceLOD(0);
                Vector3 target=new Vector3(0,entry.height*.4f,entry.depth*.5f);float size=Mathf.Max(entry.width,entry.depth,entry.height);
                camera.orthographic=true;camera.orthographicSize=size*.65f;
                camera.transform.position=target+new Vector3(size*.7f,size,-size*1.5f);camera.transform.LookAt(target);
                Palette(light,false);Capture(camera,"Logs/CliffKit_"+entry.id+"_Day.png");
                Palette(light,true);Capture(camera,"Logs/CliffKit_"+entry.id+"_Night.png");
                ob.SetActive(false);
            }
            // Two actual same-height cliff modules; socket alignment and LOD contacts are visible.
            var left=originals[0];left.SetActive(true);left.transform.position=new Vector3(-8,0,0);
            var right=UnityEngine.Object.Instantiate(left);right.transform.position=new Vector3(8,0,0);
            camera.orthographicSize=17;camera.transform.position=new Vector3(18,22,-36);camera.transform.LookAt(new Vector3(0,3,2));Palette(light,false);
            for(int lod=0;lod<3;lod++)
            {left.GetComponent<LODGroup>().ForceLOD(lod);right.GetComponent<LODGroup>().ForceLOD(lod);Capture(camera,"Logs/CliffKit_Seam_LOD"+lod+".png");}
            UnityEngine.Object.DestroyImmediate(right);left.SetActive(false);
            // Save only the new inspectable gallery, with all assets laid out at correct metre scale.
            for(int i=0;i<originals.Count;i++){var ob=originals[i];ob.SetActive(true);ob.transform.position=new Vector3((i%4)*25,0,(i/4)*38);ob.GetComponent<LODGroup>().ForceLOD(-1);}
            ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(CliffKitAssetIntake.Destination+"/Materials/CliffKit_GrassCap.mat");
            camera.orthographicSize=65;camera.transform.position=new Vector3(70,110,-60);camera.transform.LookAt(new Vector3(35,2,55));
            var globals=light.gameObject.AddComponent<LittleCastle.Rendering.StylizedLightingGlobals>();
            var globalState=new SerializedObject(globals);globalState.FindProperty("sun").objectReferenceValue=light;globalState.ApplyModifiedPropertiesWithoutUndo();
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.5f,.6f,.72f);RenderSettings.ambientEquatorColor=new Color(.4f,.48f,.58f);RenderSettings.ambientGroundColor=new Color(.25f,.3f,.36f);
            string scene="Assets/_Game/Scenes/CliffKit_v001_Review.unity";
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
            AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(groundMaterial);
        }

        private static void Palette(Light light,bool night)
        {
            light.intensity=night?.45f:1.1f;light.color=night?new Color(.57f,.67f,1):new Color(1,.94f,.82f);
            Shader.SetGlobalVector("_LC_SunDirection",-light.transform.forward);Shader.SetGlobalColor("_LC_SunColor",night?Color.black:new Color(1,.92f,.8f));
            Shader.SetGlobalVector("_LC_MoonDirection",-light.transform.forward);Shader.SetGlobalColor("_LC_MoonColor",night?new Color(.28f,.34f,.5f):Color.black);
            Color ambient=night?new Color(.27f,.34f,.5f):new Color(.5f,.6f,.72f);
            Shader.SetGlobalColor("_LC_AmbientSkyColor",ambient);Shader.SetGlobalColor("_LC_AmbientEquatorColor",ambient*.8f);Shader.SetGlobalColor("_LC_AmbientGroundColor",ambient*.5f);Shader.SetGlobalColor("_LC_AmbientColor",ambient);
            Shader.SetGlobalColor("_LC_ShadowTint",new Color(.7f,.8f,1));Shader.SetGlobalFloat("_LC_Daylight",night?0:1);Shader.SetGlobalFloat("_LC_NightAmount",night?1:0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=ambient;
        }
        private static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(800,600,24);camera.targetTexture=rt;camera.Render();
            var prior=RenderTexture.active;RenderTexture.active=rt;var pixels=new Texture2D(800,600,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,800,600),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
