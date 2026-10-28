using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    public static class GroundBiomeSecondPassBuilder
    {
        public const string Root=GroundBiomeVegetationPreflight.Root;
        public const string Scene="Assets/_Game/Scenes/GroundBiomes_PNG_Pass02.unity";
        static string Output=>GroundBiomeVegetationPreflight.Output;
        static MeshCollider ground;
        static RaycastHit Sample(float x,float z)
        {
            if(!ground.Raycast(new Ray(new Vector3(x,100,z),Vector3.down),out var hit,200))throw new Exception("Missing ground sample "+x+", "+z);
            return hit;
        }
        static T Save<T>(T obj,string name)where T:UnityEngine.Object
        {
            string path=Root+"/"+name;obj.name=Path.GetFileNameWithoutExtension(name);
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old){EditorUtility.CopySerialized(obj,old);EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);UnityEngine.Object.DestroyImmediate(obj);return old;}
            AssetDatabase.CreateAsset(obj,path);return obj;
        }
        static Texture2D Texture(string prefix,string suffix)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+prefix+"_"+suffix+".png");
        public static void BuildAndPlayer()
        {
            Build();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=Path.Combine(Output,"Player/GroundBiomes_PNG.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(Path.Combine(Output,"build_result.txt"),"Result="+report.summary.result+"\nErrors="+report.summary.totalErrors+"\nWarnings="+report.summary.totalWarnings+"\nTime="+report.summary.totalTime);
            if(Application.isBatchMode)EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:2);
        }
        [MenuItem("Little Castle/Research/Ground Biomes/Build PNG and Vegetation Comparison")]
        public static void Build()
        {
            if(!Path.GetFileName(Path.GetDirectoryName(Application.dataPath)).StartsWith("Little-Castle_URP_"))throw new Exception("URP study copy only.");
            if(!File.Exists(Path.Combine(Output,"preflight_playmode.txt")))throw new Exception("Complete real-model foliage preflight first.");
            for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene preserved.");
            Directory.CreateDirectory(Output);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var file in Directory.GetFiles(Root+"/Textures","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                bool normal=file.Contains("_normal"),color=file.Contains("diffuseOriginal");
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=color;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
                importer.anisoLevel=8;importer.maxTextureSize=1024;importer.isReadable=false;
                if(normal)importer.flipGreenChannel=true;
                importer.SaveAndReimport();
            }
            var scene=EditorSceneManager.OpenScene(GroundBiomeStudyBuilder.ScenePath);
            EditorSceneManager.SaveScene(scene,Scene);
            var controls=UnityEngine.Object.FindAnyObjectByType<GroundBiomeStudyControls>();
            ground=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude).Single();
            var renderer=ground.GetComponent<MeshRenderer>();var baseline=renderer.sharedMaterial;
            var terrain=new Material(baseline);
            foreach(string layer in new[]{"Grass","Dirt"})
            {
                terrain.SetTexture("_"+layer+"Tex",Texture(layer,"diffuseOriginal"));
                terrain.SetTexture("_"+layer+"Normal",Texture(layer,"normal"));terrain.SetTexture("_"+layer+"AO",Texture(layer,"ao"));
            }
            terrain.SetTexture("_GrassHeight",Texture("Grass","height"));
            terrain.SetTexture("_SnowTex",Texture("Snow","diffuseOriginal"));terrain.SetTexture("_SnowNormal",Texture("Snow","normal"));terrain.SetTexture("_SnowAO",Texture("Snow","ao"));
            terrain.SetFloat("_WorldTiling",.26f);terrain.SetFloat("_SnowTiling",.22f);
            terrain.SetFloat("_SurfaceDetailStrength",.82f);terrain.SetFloat("_GrassNormalScale",.60f);terrain.SetFloat("_SurfaceAOStrength",.45f);
            terrain.SetFloat("_DetailFadeStart",55);terrain.SetFloat("_DetailFadeEnd",135);
            terrain.SetFloat("_SnowDetailStrength",.85f);terrain.SetFloat("_SnowSurfaceFlattening",.08f);
            terrain.SetFloat("_GrassDetail",.38f);terrain.SetFloat("_DirtDetail",.62f);terrain.SetFloat("_MacroStrength",.08f);
            terrain.SetColor("_GrassColor",new Color(.31f,.42f,.17f));terrain.SetColor("_DryGrassColor",new Color(.42f,.44f,.21f));
            terrain.SetColor("_DirtColor",new Color(.48f,.37f,.25f));
            terrain=Save(terrain,"PNG_Terrain.mat");renderer.sharedMaterial=terrain;

            var grassMaterial=new Material(AssetDatabase.LoadAssetAtPath<Material>(GroundBiomeVegetationPreflight.Study+"/Study_UNS_Grass.mat"));
            grassMaterial.SetColor("_Color",new Color(.27f,.37f,.12f));grassMaterial.SetColor("_RootColor",new Color(.15f,.23f,.06f));grassMaterial.SetColor("_TipColor",new Color(.36f,.46f,.16f));
            grassMaterial.SetColor("_DryColor",new Color(.35f,.39f,.18f));grassMaterial.SetFloat("_TransmissionStrength",.25f);
            grassMaterial.SetFloat("_WindAmplitude",.075f);grassMaterial.SetFloat("_MicroFlutter",.012f);grassMaterial.SetFloat("_MeadowPaletteStrength",.7f);
            grassMaterial=Save(grassMaterial,"PNG_Grass.mat");grassMaterial.enableInstancing=true;
            var grassRoot=new GameObject("Authored grass patches · instanced 4 LODs");
            var grass=grassRoot.AddComponent<LittleCastle.Research.InnerverseGrassStudy>();
            grass.maximumDistance=145;grass.receiveGroundShadows=true;
            var grassPrefab=GroundBiomeVegetationPreflight.Source("UNS_Grass");
            grass.lodMeshes=grassPrefab.GetComponentInChildren<LODGroup>().GetLODs().Select(l=>l.renderers[0].GetComponent<MeshFilter>().sharedMesh).ToArray();grass.grassMaterial=grassMaterial;
            var matrices=new List<Matrix4x4>();var coverage=(Texture2D)terrain.GetTexture("_SnowControl");
            for(int zi=0;zi<69;zi++)for(int xi=0;xi<91;xi++)
            {
                float x=-44+xi*.96f+(DeterministicHash.Hash01(17,xi,zi,0)-.5f)*.75f;
                float z=-32+zi*1.03f+(DeterministicHash.Hash01(17,xi,zi,1)-.5f)*.75f;
                float pathCenter=7+3.4f*Mathf.Sin(x*.063f)+.7f*Mathf.Sin(x*.16f);
                if(Mathf.Abs(z-pathCenter)<3.1f)continue;
                float snow=coverage.GetPixelBilinear((x+48)/96,(z+36)/80).r;
                float density=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.42f,snow)))*Mathf.Lerp(.28f,.70f,Mathf.PerlinNoise((x+90)*.055f,(z+90)*.055f));
                if(DeterministicHash.Hash01(17,xi,zi,2)>density)continue;
                var hit=Sample(x,z);if(hit.point.y<.40f)continue;
                float scale=Mathf.Lerp(.65f,1.02f,DeterministicHash.Hash01(17,xi,zi,3));
                matrices.Add(Matrix4x4.TRS(hit.point+Vector3.up*.005f,Quaternion.FromToRotation(Vector3.up,hit.normal)*Quaternion.Euler(0,DeterministicHash.Hash01(17,xi,zi,4)*360,0),Vector3.one*scale));
            }
            grass.authoredMatrices=matrices.ToArray();

            var leaf=new Material(AssetDatabase.LoadAssetAtPath<Material>(GroundBiomeVegetationPreflight.Study+"/Study_UNS_Spruce_Tree_Branch.mat"));
            leaf.SetFloat("_WindAmplitude",.08f);leaf.SetFloat("_LeafFlutter",.045f);leaf.SetFloat("_LightResponse",1);
            leaf=Save(leaf,"Study_Fir_Foliage.mat");leaf.enableInstancing=true;
            var bark=Save(new Material(AssetDatabase.LoadAssetAtPath<Material>(GroundBiomeVegetationPreflight.Study+"/Study_UNS_Color_Palette.mat")),"Study_Fir_Bark.mat");
            var treeRoot=new GameObject("Authored tree groups · existing fir meshes");
            Vector2[] trees={new Vector2(-37,25),new Vector2(-31,32),new Vector2(-23,25),new Vector2(-39,0),new Vector2(-27,-4),new Vector2(-14,26),new Vector2(-8,33),new Vector2(-27,-29),new Vector2(-38,-28),new Vector2(24,26),new Vector2(31,32),new Vector2(39,23),new Vector2(33,17)};
            for(int i=0;i<trees.Length;i++)
            {
                var tree=(GameObject)PrefabUtility.InstantiatePrefab(GroundBiomeVegetationPreflight.Source(i%3==0?"UNS_Spruce_01":"UNS_Spruce_02"));
                tree.transform.SetParent(treeRoot.transform);tree.transform.position=Sample(trees[i].x,trees[i].y).point;tree.transform.rotation=Quaternion.Euler(0,i*71,0);
                tree.transform.localScale=Vector3.one*(.55f+.07f*(i%4));
                foreach(var r in tree.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains("Branch")?leaf:bark).ToArray();
                var bounds=tree.GetComponentsInChildren<Renderer>(true).First().bounds;
                foreach(var r in tree.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(r.bounds);
                tree.transform.localScale*=(6.5f+.8f*(i%4))/bounds.size.y;
                bounds=tree.GetComponentsInChildren<Renderer>(true).First().bounds;
                foreach(var r in tree.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(r.bounds);
                tree.transform.position+=Vector3.up*(Sample(trees[i].x,trees[i].y).point.y-bounds.min.y-.03f);
            }
            controls.terrainRenderer=renderer;controls.baselineTerrain=baseline;controls.terrainMaterial=terrain;controls.grassRoot=grassRoot;controls.treesRoot=treeRoot;controls.ApplyComparison(3);controls.SetView(0);
            EditorSceneManager.SaveScene(scene,Scene);AssetDatabase.SaveAssets();
            var globals=UnityEngine.Object.FindAnyObjectByType<LittleCastle.Rendering.StylizedLightingGlobals>();globals.ApplyGlobals();
            GroundBiomeVegetationPreflight.Capture(controls.studyCamera,Path.Combine(Output,"editor_overview.png"));
            var messages=new StringBuilder();
            foreach(var shader in new[]{terrain.shader,leaf.shader,grassMaterial.shader})foreach(var message in ShaderUtil.GetShaderMessages(shader))messages.AppendLine(shader.name+": "+message.severity+": "+message.message);
            File.WriteAllText(Path.Combine(Output,"shader_messages.txt"),messages.ToString());
            if(messages.ToString().Contains(": Error:"))throw new Exception("Shader errors.");
            File.WriteAllText(Path.Combine(Output,"scene_result.txt"),"Scene="+Scene+"\nAuthored grass clumps="+matrices.Count+"\nAuthored trees="+trees.Length+"\nSource maps=PNG library Grass 8 / Mud 5 / Snow 10.\nNormal green flipped on copied imports; source files unchanged.\nBaseline camera, lighting and ground geometry preserved.\n");
        }
    }
}
