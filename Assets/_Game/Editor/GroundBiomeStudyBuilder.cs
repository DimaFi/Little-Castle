using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using LittleCastle.World;

namespace LittleCastle.Editor
{
    /// <summary>Build-time authored fixture. Its baked assets do not run the world generator.</summary>
    public static class GroundBiomeStudyBuilder
    {
        public const string Root = "Assets/_Game/Art/Studies/GroundBiomes_20261025";
        public const string ScenePath = "Assets/_Game/Scenes/GroundBiomes_20261025.unity";
        private const string Starter = "Assets/_Game/Art/Imported/TerrainStarter_v001";
        public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Research/Ground_Biomes_URP_2026-10-25/Prototype"));

        [MenuItem("Little Castle/Research/Ground Biomes/Build Isolated Sample")]
        public static void Build()
        {
            if (!Path.GetFileName(Path.GetDirectoryName(Application.dataPath)).StartsWith("Little-Castle_URP_"))
                throw new InvalidOperationException("This study is restricted to the isolated URP project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before rebuilding.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving an unsaved open scene. Save it before building this sample.");
            Directory.CreateDirectory(Root); Directory.CreateDirectory(Output);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Ground Biomes — authored surface sample";
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.50f, .59f, .68f);
            RenderSettings.ambientEquatorColor = new Color(.37f, .42f, .43f);
            RenderSettings.ambientGroundColor = new Color(.23f, .25f, .20f);
            RenderSettings.fog = false; RenderSettings.skybox = null;
            var sun = new GameObject("Soft afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1, .95f, .84f); sun.intensity = .95f;
            sun.shadows = LightShadows.Soft; sun.shadowBias = .04f; sun.shadowNormalBias = .25f;
            sun.transform.rotation = Quaternion.Euler(38, -38, 0); RenderSettings.sun = sun;
            var globals = new GameObject("Shared LC lighting").AddComponent<LittleCastle.Rendering.StylizedLightingGlobals>(); globals.ApplyGlobals();

            var control = Save(BuildSnowMask(), "SnowCoverage.asset");
            var terrain = new Material(Shader.Find("Little Castle/Terrain/LC Terrain")) { name = "GroundStudy_Terrain", enableInstancing = true };
            Texture2D Tex(string relative) => AssetDatabase.LoadAssetAtPath<Texture2D>(relative);
            terrain.SetTexture("_GrassTex", Tex(Starter + "/Study/Textures/Grass_MeadowSoft_BaseColor.png"));
            terrain.SetTexture("_DirtTex", Tex(Starter + "/Textures/DirtPath_A_BaseColor.png"));
            terrain.SetTexture("_SoilTex", Tex(Starter + "/Textures/DirtGround_A_BaseColor.png"));
            terrain.SetTexture("_RockTex", Tex(Starter + "/Textures/DirtGround_A_BaseColor.png"));
            terrain.SetTexture("_GrassNormal", Tex(Starter + "/Study/Textures/Grass_Base_A_Normal.png"));
            terrain.SetTexture("_DirtNormal", Tex(Starter + "/Study/Textures/DirtPath_A_Normal.png"));
            terrain.SetTexture("_GrassAO", Tex(Starter + "/Study/Textures/Grass_Base_A_AO.png"));
            terrain.SetTexture("_DirtAO", Tex(Starter + "/Study/Textures/DirtPath_A_AO.png"));
            terrain.SetTexture("_GrassHeight", Tex(Starter + "/Study/Textures/Grass_Base_A_Height.png"));
            terrain.SetColor("_GrassColor", new Color(.32f, .43f, .18f));
            terrain.SetColor("_DryGrassColor", new Color(.44f, .48f, .24f));
            terrain.SetColor("_DirtColor", new Color(.48f, .36f, .23f));
            terrain.SetColor("_SoilColor", new Color(.40f, .37f, .23f));
            terrain.SetColor("_RockColor", new Color(.58f, .56f, .48f));
            terrain.SetFloat("_GroundPalette", 1); terrain.SetFloat("_GrassDetail", .19f); terrain.SetFloat("_DirtDetail", .45f);
            terrain.SetFloat("_SurfaceDetailStrength", .28f); terrain.SetFloat("_LightResponse", 1);
            terrain.SetFloat("_WorldTiling", .18f); terrain.SetFloat("_UseVertexMasks", 1); terrain.SetFloat("_PathEdgeSharpness", .4f);
            terrain.SetFloat("_MacroStrength", .065f); terrain.SetFloat("_WetnessStrength", .75f);
            terrain.SetFloat("_RockSlopeStart", .32f); terrain.SetFloat("_RockSlopeEnd", .65f);
            terrain.SetFloat("_UseSnow", 1); terrain.SetTexture("_SnowControl", control);
            terrain.SetVector("_SnowMapBounds", new Vector4(-48, -36, 1f / 96, 1f / 80));
            terrain.SetTexture("_SnowTex", Tex(Root + "/Textures/Snow_01_BaseColor.png"));
            terrain.SetColor("_SnowColor", new Color(.60f, .68f, .77f)); terrain.SetFloat("_SnowTiling", .14f);
            terrain.SetFloat("_SnowEdgeNoise", .34f); terrain.SetFloat("_SnowPathClearance", .76f);
            terrain = Save(terrain, "GroundStudy_Terrain.mat");

            var ground = Save(GroundMesh(), "Ground_96x80m.asset");
            AddMesh("Authored ground · 96 × 80m", ground, terrain, true);
            var water = new Material(Shader.Find("Little Castle/Water/LC Water")) { name = "GroundStudy_Water" };
            water.SetTexture("_DetailTex", Tex(Root + "/Textures/Water_04_BaseColor.png"));
            water = Save(water, "GroundStudy_Water.mat");
            var river = Save(WaterMesh(), "River_Surface.asset"); AddMesh("River · authored curved channel", river, water, false);

            var rock = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Shaders/Surface/LC_StylizedLit.shader"));
            // Resolve the exact shared solid-family name from an existing shader asset.
            rock.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Shaders/Surface/LC_StylizedLit.shader");
            rock.name = "GroundStudy_Limestone";
            rock.SetColor("_Color", new Color(.56f,.55f,.48f)); rock = Save(rock, "GroundStudy_Limestone.mat");
            for (int i = 0; i < 9; i++)
            {
                float x = -36 + i * 8.8f;
                float z = RiverCenter(x) + (i % 2 == 0 ? 1 : -1) * RiverHalfWidth(x) * 1.11f;
                Mesh stone = Save(Stone(i), "ShoreStone_" + i + ".asset");
                var obj = AddMesh("Shore stone " + (i + 1), stone, rock, false);
                obj.transform.position = new Vector3(x, Height(x,z) - .10f, z);
                obj.transform.rotation = Quaternion.Euler(0,i*47,0);
            }

            var camera = new GameObject("Ground sample · strategy camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.fieldOfView = 46; camera.nearClipPlane = .15f; camera.farClipPlane = 400;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.29f,.38f,.42f);
            camera.allowHDR = false;
            var controller = camera.gameObject.AddComponent<LittleCastle.CameraSystem.StrategyCameraController>(); controller.enabled = false;
            var serializedCamera = new SerializedObject(controller); serializedCamera.FindProperty("enableEdgeScroll").boolValue = false; serializedCamera.ApplyModifiedPropertiesWithoutUndo();
            var controls = camera.gameObject.AddComponent<GroundBiomeStudyControls>(); controls.studyCamera = camera; controls.terrainMaterial = terrain; controls.sun = sun; controls.SetView(0);
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            Capture(camera, globals);
            var messages = new List<string>();
            foreach (var shader in new[] { terrain.shader, water.shader, rock.shader })
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) messages.Add(shader.name + ": " + message.severity + ": " + message.message);
            File.WriteAllLines(Path.Combine(Output, "shader_messages.txt"), messages);
            if (messages.Any(m => m.Contains(": Error:"))) throw new InvalidOperationException("Shader compilation errors; see shader_messages.txt");
            File.WriteAllText(Path.Combine(Output, "scene_result.txt"), "Scene=" + ScenePath + "\nGround vertices=" + ground.vertexCount + "\nWater vertices=" + river.vertexCount +
                "\nSnow mask=512x512 linear; separate from terrain RGBA masks.\nFixed baked geometry and authored coverage; no runtime world generation.\n");
        }
        public static void BuildAndPlayer()
        {
            Build();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
                locationPathName = Path.Combine(Output, "Player/GroundBiomes.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            File.WriteAllText(Path.Combine(Output, "build_result.txt"), "Result=" + report.summary.result + "\nErrors=" + report.summary.totalErrors + "\nWarnings=" + report.summary.totalWarnings + "\nTime=" + report.summary.totalTime);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 2);
        }
        private static T Save<T>(T obj, string filename) where T : UnityEngine.Object
        {
            string path = Root + "/" + filename;
            obj.name = Path.GetFileNameWithoutExtension(filename);
            var previous = AssetDatabase.LoadMainAssetAtPath(path);
            if (previous)
            {
                // Native mesh buffers need an explicit copy when updating a baked asset.
                // Keep its GUID so scene references survive rebuilding this sample.
                if (previous is Mesh target && obj is Mesh source)
                {
                    target.Clear(); target.indexFormat = source.indexFormat;
                    target.vertices = source.vertices; target.triangles = source.triangles;
                    target.normals = source.normals; target.colors = source.colors;
                    target.uv = source.uv; target.bounds = source.bounds; target.name = obj.name;
                }
                else EditorUtility.CopySerialized(obj, previous);
                EditorUtility.SetDirty(previous);
                AssetDatabase.SaveAssetIfDirty(previous);
                UnityEngine.Object.DestroyImmediate(obj);
            }
            else AssetDatabase.CreateAsset(obj, path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        private static GameObject AddMesh(string name, Mesh mesh, Material material, bool collision)
        {
            var obj = new GameObject(name); obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
            return obj;
        }
        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        // These curves author this sample only; the saved player loads baked meshes.
        private static float RiverCenter(float x) => -14 + 5.0f * Mathf.Sin(x*.065f) + 1.2f * Mathf.Sin(x*.14f+.8f);
        private static float RiverHalfWidth(float x) => 4.6f + .7f * Mathf.Sin(x*.09f+1.4f);
        private static float RiverRadius(float x, float z) => Mathf.Abs(z-RiverCenter(x))/RiverHalfWidth(x);
        private static float Height(float x, float z)
        {
            float hill = 4.8f * Mathf.Exp(-((x+25)*(x+25)/235+(z-28)*(z-28)/165)) +
                3.5f * Mathf.Exp(-((x-29)*(x-29)/185+(z-30)*(z-30)/195));
            float undulation = .20f * Mathf.Sin(x*.12f)*Mathf.Cos(z*.13f) + .1f;
            float dry = .55f + hill + undulation;
            float bed = -2.2f + Smooth(.38f,1.20f,RiverRadius(x,z))*3.05f;
            return Mathf.Lerp(bed, dry, Smooth(1.0f,1.4f,RiverRadius(x,z)));
        }
        private static float PathMask(float x, float z)
        {
            float center = 7 + 3.4f*Mathf.Sin(x*.063f) + .7f*Mathf.Sin(x*.16f);
            return 1-Smooth(1.5f,2.7f,Mathf.Abs(z-center));
        }
        private static Texture2D BuildSnowMask()
        {
            const int size=512; var tex=new Texture2D(size,size,TextureFormat.R8,true,true) { name="Authored Snow Coverage", wrapMode=TextureWrapMode.Clamp, filterMode=FilterMode.Bilinear, anisoLevel=4 };
            var pixels=new Color[size*size];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float wx=-48+96*x/(float)(size-1), wz=-36+80*z/(float)(size-1);
                float boundary=4+3.5f*Mathf.Sin(wz*.11f)+1.1f*Mathf.Cos(wz*.29f);
                float weight=Smooth(-9,11,wx-boundary);
                float patch=(Mathf.PerlinNoise((wx+70)*.29f,(wz+80)*.29f)-.5f)*.65f;
                weight=Mathf.Clamp01(weight+patch*4*weight*(1-weight));
                pixels[z*size+x]=new Color(weight,0,0,1);
            }
            tex.SetPixels(pixels);tex.Apply(true,false);return tex;
        }
        private static Mesh GroundMesh()
        {
            const int nx=192,nz=160; var vertices=new Vector3[(nx+1)*(nz+1)];var colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];var tris=new int[nx*nz*6];
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                int i=z*(nx+1)+x;float wx=-48+x*.5f,wz=-36+z*.5f;vertices[i]=new Vector3(wx,Height(wx,wz),wz);
                float bank=(1-Smooth(1.00f,1.26f,RiverRadius(wx,wz)))*Smooth(.65f,.99f,RiverRadius(wx,wz));
                colors[i]=new Color(PathMask(wx,wz)*(1-bank),bank,0,1);uv[i]=new Vector2(x/(float)nx,z/(float)nz);
            }
            int k=0;for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;tris[k++]=a;tris[k++]=c;tris[k++]=b;tris[k++]=b;tris[k++]=c;tris[k++]=d;}
            var mesh=new Mesh{name="Authored ground 96x80m, 0.5m heightfield"};mesh.vertices=vertices;mesh.colors=colors;mesh.uv=uv;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh WaterMesh()
        {
            const int along=256,across=32;
            var vertices=new Vector3[(along+1)*(across+1)];var colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];var tris=new int[along*across*6];
            float arcLength=0;
            for(int x=0;x<=along;x++)
            {
                float wx=-48+96*x/(float)along;
                if(x>0){float previousX=-48+96*(x-1)/(float)along;arcLength+=Vector2.Distance(new Vector2(previousX,RiverCenter(previousX)),new Vector2(wx,RiverCenter(wx)));}
                for(int z=0;z<=across;z++)
                {
                    int i=x*(across+1)+z;float cross=Mathf.Lerp(-.94f,.94f,z/(float)across)*RiverHalfWidth(wx),wz=RiverCenter(wx)+cross;
                    vertices[i]=new Vector3(wx,.12f,wz);uv[i]=new Vector2(arcLength,cross);
                    colors[i]=new Color(Mathf.Clamp01((.12f-Height(wx,wz))/2.2f),0,0,1);
                }
            }
            int k=0;
            for(int x=0;x<along;x++)for(int z=0;z<across;z++)
            {
                int a=x*(across+1)+z,b=a+1,c=a+across+1,d=c+1;
                tris[k++]=a;tris[k++]=b;tris[k++]=c;tris[k++]=b;tris[k++]=d;tris[k++]=c;
            }
            var mesh=new Mesh{name="Authored river, curved UV flow"};mesh.vertices=vertices;mesh.colors=colors;mesh.uv=uv;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh Stone(int seed)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            float width=.9f+(seed%3)*.3f,height=.55f+(seed%4)*.19f;
            for(int ring=0;ring<2;ring++)for(int i=0;i<6;i++){float a=i*Mathf.PI/3;vertices.Add(new Vector3(Mathf.Cos(a)*width*(ring==0?1:.65f),ring==0?0:height,Mathf.Sin(a)*width*.72f));}
            vertices.Add(new Vector3(.07f,height*1.18f,-.04f));
            for(int i=0;i<6;i++){int j=(i+1)%6;triangles.AddRange(new[]{i,j,i+6,j,j+6,i+6,i+6,j+6,12});}
            for(int i=0;i<triangles.Count;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
            var mesh=new Mesh{name="Faceted shoreline limestone"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
        private static void Capture(Camera camera, LittleCastle.Rendering.StylizedLightingGlobals globals)
        {
            var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
            try
            {
                for(int frame=0;frame<2;frame++) { globals.ApplyGlobals();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt}); }
                RenderTexture.active=rt;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(Output,"editor_overview.png"),tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
            }
            finally { RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt); }
        }
    }
}
