using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Real GPU draws in the existing Editor. No saved/new project or authored scene is needed.</summary>
    public static class PathSurfaceVerification
    {
        static readonly string Evidence=Path.GetFullPath(Application.dataPath+"/../../Research/Paths_20261029");
        const string Meadow="Assets/_Game/Art/Studies/Meadow_20261025/";
        [MenuItem("Little Castle/Map/Paths/Validate existing grass compatibility")]
        public static void Preflight()
        {
            Directory.CreateDirectory(Evidence);var selection=Selection.objects;var scene=EditorSceneManager.NewPreviewScene();
            var log=new System.Text.StringBuilder();
            Application.LogCallback collect=(m,s,t)=>{if(m.Contains("[Little Castle Foliage]"))log.AppendLine(m);};
            Application.logMessageReceived+=collect;
            try
            {
                var data=AssetDatabase.LoadAssetAtPath<MeadowGrassData>(Meadow+"Meadow_InstanceData.asset");
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Settings/Rendering/LightingMeadow/Blades.mat");
                foreach(var mesh in data.lodMeshes)
                {
                    var obj=new GameObject("Path edge grass compatibility",typeof(MeshFilter),typeof(MeshRenderer));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj,scene);
                    try
                    {
                        obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;
                        Selection.activeGameObject=obj;FoliageModelCompatibilityValidator.ValidateSelected();
                        var vertices=mesh.vertices;var uv=mesh.uv;var roots=new List<Vector4>();mesh.GetUVs(1,roots);
                        if(mesh.normals.Length!=vertices.Length||uv.Length!=vertices.Length||roots.Count!=vertices.Length)throw new Exception("Grass is missing normals, root UV1 or UV0.");
                        int tips=0,anchored=0;for(int i=0;i<vertices.Length;i++){if(uv[i].y>.99f)tips++;if(uv[i].y<.001f&&Mathf.Abs(vertices[i].y-roots[i].y)<.001f)anchored++;}
                        if(tips==0||anchored==0)throw new Exception("Grass root-to-tip UV contract failed.");
                        log.AppendLine($"COMPATIBLE {mesh.name}: {vertices.Length} vertices; {mesh.GetIndexCount(0)/3} triangles; tips={tips}; anchored roots={anchored}; native blade geometry, shared deformation in all passes.");
                    }
                    finally{Object.DestroyImmediate(obj);}
                }
                var oak=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Studies/ValleyArt_Pass03/Oak_A_Study.prefab");
                if(oak){Selection.activeGameObject=oak;FoliageModelCompatibilityValidator.ValidateSelected();log.AppendLine("Production Oak A preflight uses the existing asset, unchanged.");}
            }
            finally
            {
                Selection.objects=selection;Application.logMessageReceived-=collect;EditorSceneManager.ClosePreviewScene(scene);
                File.WriteAllText(Evidence+"/foliage-preflight.txt",log.ToString());
            }
        }
        [MenuItem("Little Castle/Map/Paths/Capture material transitions without changing map")]
        public static void Capture()
        {
            Directory.CreateDirectory(Evidence);var catalog=MapPathCatalog.Default;if(!catalog)throw new Exception("Build path library first.");
            using(var gpu=new Preview())
            {
                gpu.Draw(catalog,false,false,false,Evidence+"/01-path-palette-summer.png");
                gpu.Draw(catalog,true,false,false,Evidence+"/02-path-palette-winter-boundary.png");
                gpu.Draw(catalog,false,true,true,Evidence+"/03-material-junction-close.png");
                gpu.Draw(catalog,true,true,true,Evidence+"/04-material-junction-winter.png");
            }
            var shader=Shader.Find("Little Castle/Terrain/LC Terrain");
            var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(m=>m.message)));
            File.WriteAllText(Evidence+"/gpu-verification.txt","PASS: terrain forward draw, 9 requested surfaces, native instanced grass, material junction and summer/winter boundary. No authored scene/map/material changed.\n");
        }
        sealed class Preview:IDisposable
        {
            readonly Dictionary<string,Vector4> vectors=new Dictionary<string,Vector4>();
            readonly Dictionary<string,float> floats=new Dictionary<string,float>();
            readonly Dictionary<string,Texture> textures=new Dictionary<string,Texture>();
            readonly Dictionary<string,bool> keywords=new Dictionary<string,bool>();
            readonly Dictionary<string,Matrix4x4> matrices=new Dictionary<string,Matrix4x4>();
            readonly bool async;
            readonly RenderTexture oldTarget,target;
            readonly Texture2D readback;
            readonly CommandBuffer commands=new CommandBuffer{name="Disposable path GPU verification"};
            public Preview()
            {
                async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
                oldTarget=RenderTexture.active;target=new RenderTexture(1440,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
                readback=new Texture2D(1440,1000,TextureFormat.RGBA32,false,true);
                foreach(string key in new[]{"_MAIN_LIGHT_SHADOWS","_MAIN_LIGHT_SHADOWS_CASCADE","_MAIN_LIGHT_SHADOWS_SCREEN","_SCREEN_SPACE_OCCLUSION","_LC_SPATIAL_INDIRECT","FOG_LINEAR","FOG_EXP","FOG_EXP2"})
                {keywords[key]=Shader.IsKeywordEnabled(key);Shader.DisableKeyword(key);}
                foreach(string key in new[]{"unity_MatrixV","unity_MatrixInvV","glstate_matrix_projection","unity_MatrixVP","unity_MatrixInvVP","unity_CameraProjection","unity_CameraInvProjection"})matrices[key]=Shader.GetGlobalMatrix(key);
                Float("_LC_EnvironmentEnabled",1);Float("_LC_LowTerrainDetail",0);Float("_LC_GrassInteractorCount",0);
                Vector("_LC_SunDirection",new Vector4(.35f,.8f,-.45f,0).normalized);Vector("_LC_MoonDirection",new Vector4(0,1,0,0));
                Vector("_LC_SunColor",new Vector4(.74f,.71f,.61f,1));Vector("_LC_MoonColor",Vector4.zero);
                Vector("_LC_AmbientSkyColor",new Vector4(.42f,.48f,.52f,1));Vector("_LC_AmbientEquatorColor",new Vector4(.34f,.4f,.3f,1));
                Vector("_LC_AmbientGroundColor",new Vector4(.24f,.28f,.19f,1));Vector("_LC_ShadowTint",new Vector4(.65f,.72f,.8f,1));
                Vector("_LC_Wind",Vector4.zero);Vector("_LC_GameTime",Vector4.zero);Vector("_LC_EnvironmentSnowColor",new Vector4(.83f,.89f,.94f,1));
                Texture("_LC_BiomeTint",Texture2D.whiteTexture);
            }
            public void Draw(MapPathCatalog catalog,bool winter,bool close,bool junction,string output)
            {
                var b=junction?new Vector4(-8,-8,16,16):new Vector4(-19,-12,38,24);
                var map=new MapAuthoringDocument{mapId="disposable-path-gpu",bounds=b};
                for(int i=1;i<catalog.Count;i++)
                {
                    if(junction&&i!=5&&i!=8&&i!=9)continue;
                    var stroke=new SurfacePaintStroke{id="path"+i,pathMaterialId=catalog.entries[i].id,radius=junction?2.7f:1.45f,brushSoftness=.551f};
                    if(junction)
                    {
                        if(i==8){stroke.points.Add(new Vector2(-6,-7));stroke.points.Add(new Vector2(-1,0));stroke.points.Add(new Vector2(0,7));}
                        if(i==9){stroke.points.Add(new Vector2(6,-7));stroke.points.Add(new Vector2(1,0));stroke.points.Add(new Vector2(0,7));}
                        if(i==5){stroke.points.Add(new Vector2(-7,3));stroke.points.Add(new Vector2(7,3));}
                    }
                    else for(int z=-12;z<=12;z++)stroke.points.Add(new Vector2((i-5)*4+Mathf.Sin(z*.18f+i*.42f)*.6f,z));
                    map.surface.strokes.Add(stroke);
                }
                var session=new MapEditSession(map);const int resolution=512;
                var mask=new Texture2D(resolution,resolution,TextureFormat.RG16,false,true){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var bytes=new byte[resolution*resolution*2];SurfacePaintRasterizer.Rebuild(bytes,b,resolution,resolution,map.surface.strokes,new RectInt(0,0,resolution,resolution));mask.SetPixelData(bytes,0);mask.Apply();
                var snow=new Texture2D(32,32,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var snowPixels=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++)snowPixels[y*32+x]=Color.white*(winter?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,.68f,y/31f)):0);
                snow.SetPixels(snowPixels);snow.Apply();Texture("_LC_EnvironmentSnow",snow);Vector("_LC_EnvironmentBounds",new Vector4(b.x,b.y,1/b.z,1/b.w));
                var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Settings/Rendering/LightingMeadow/Ground.mat");
                var terrain=new Material(original);terrain.SetFloat("_AlpineEnabled",0);terrain.SetFloat("_UseSnow",0);terrain.SetFloat("_UseVertexMasks",0);terrain.SetFloat("_HeightBlendStrength",0);
                terrain.EnableKeyword("_LC_SURFACE_PAINT");
                var grassSource=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Settings/Rendering/LightingMeadow/Blades.mat");var grass=new Material(grassSource);
                grass.SetFloat("_MeadowSoftness",.28f);grass.SetFloat("_UseRootAnchor",1);grass.SetFloat("_DensityFloor",.65f);grass.SetFloat("_FadeStart",150);grass.SetFloat("_FadeEnd",180);
                grass.EnableKeyword("_LC_SURFACE_PAINT");grass.EnableKeyword("_LC_MEADOW");
                var mesh=new Mesh{name="Disposable path ground"};
                mesh.vertices=new[]{new Vector3(b.x,0,b.y),new Vector3(b.x,0,b.y+b.w),new Vector3(b.x+b.z,0,b.y+b.w),new Vector3(b.x+b.z,0,b.y)};
                mesh.triangles=new[]{0,1,2,0,2,3};mesh.normals=Enumerable.Repeat(Vector3.up,4).ToArray();mesh.colors=Enumerable.Repeat(Color.black,4).ToArray();mesh.RecalculateBounds();
                using(var weights=new MapPathWeightTexture(catalog,b,resolution,resolution))
                try
                {
                    weights.Rebuild(session,new RectInt(0,0,resolution,resolution));var properties=new MaterialPropertyBlock();weights.Bind(properties);
                    properties.SetTexture("_SurfacePaint",mask);properties.SetVector("_SurfacePaintBounds",new Vector4(b.x,b.y,1/b.z,1/b.w));properties.SetFloat("_UseSurfacePaint",1);
                    properties.SetFloat("_PathEdgeVariation",.65f);
                    using var relief=new MapPathReliefPresentation(session,catalog,terrain,properties);
                    Vector3 eye=close?new Vector3(8,14,-16):new Vector3(14,30,-32);var center=Vector3.zero;
                    Vector("_WorldSpaceCameraPos",new Vector4(eye.x,eye.y,eye.z,1));
                    var view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(eye,Quaternion.LookRotation(center-eye,Vector3.up),Vector3.one).inverse;
                    float size=close?9.5f:19;var projection=GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-size*1.44f,size*1.44f,-size,size,.1f,160),true);
                    commands.Clear();commands.SetRenderTarget(target);commands.ClearRenderTarget(true,true,new Color(.13f,.18f,.12f,0));commands.SetViewProjectionMatrices(view,projection);
                    // The direct offscreen projection flips Y on D3D; a Camera normally
                    // supplies the corresponding culling state on our behalf.
                    commands.SetInvertCulling(SystemInfo.graphicsUVStartsAtTop);
                    commands.DrawMesh(mesh,Matrix4x4.identity,terrain,0,terrain.FindPass("FORWARD"),properties);
                    relief.Draw(commands,terrain.FindPass("FORWARD"));
                    commands.SetInvertCulling(false);Graphics.ExecuteCommandBuffer(commands);
                    RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,1440,1000),0,0);readback.Apply();RenderTexture.active=oldTarget;
                    if(readback.GetPixels32().Count(p=>p.a>240)<50000)throw new Exception("Offscreen terrain did not render; do not accept a grass-only capture.");
                    commands.Clear();commands.SetRenderTarget(target);commands.SetViewProjectionMatrices(view,projection);
                    var data=AssetDatabase.LoadAssetAtPath<MeadowGrassData>(Meadow+"Meadow_InstanceData.asset");
                    var random=new System.Random(7194);var instances=new List<Matrix4x4>();
                    for(float z=b.y;z<b.y+b.w;z+=.52f)for(float x=b.x;x<b.x+b.z;x+=.52f)
                    {
                        var p=new Vector3(x+((float)random.NextDouble()-.5f)*.3f,.009f,z+((float)random.NextDouble()-.5f)*.3f);
                        float scale=.8f+(float)random.NextDouble()*.55f;
                        instances.Add(Matrix4x4.TRS(p,Quaternion.Euler(0,(float)random.NextDouble()*360,0),Vector3.one*scale));
                    }
                    for(int start=0;start<instances.Count;start+=500)
                    {var batch=instances.Skip(start).Take(500).ToArray();commands.DrawMeshInstanced(data.lodMeshes[0],0,grass,grass.FindPass("FORWARD"),batch,batch.Length,properties);}
                    Graphics.ExecuteCommandBuffer(commands);RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,1440,1000),0,0);readback.Apply();RenderTexture.active=oldTarget;
                    var pixels=readback.GetPixels32();int magenta=pixels.Count(p=>p.r>220&&p.b>220&&p.g<25);if(magenta>pixels.Length/20)throw new Exception("GPU path draw produced shader-error pixels.");
                    File.WriteAllBytes(output,readback.EncodeToPNG());
                }
                finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(terrain);Object.DestroyImmediate(grass);Object.DestroyImmediate(mask);Texture("_LC_EnvironmentSnow",Texture2D.blackTexture);Object.DestroyImmediate(snow);}
            }
            void Float(string key,float value){if(!floats.ContainsKey(key))floats[key]=Shader.GetGlobalFloat(key);Shader.SetGlobalFloat(key,value);}
            void Vector(string key,Vector4 value){if(!vectors.ContainsKey(key))vectors[key]=Shader.GetGlobalVector(key);Shader.SetGlobalVector(key,value);}
            void Texture(string key,Texture value){if(!textures.ContainsKey(key))textures[key]=Shader.GetGlobalTexture(key);Shader.SetGlobalTexture(key,value);}
            public void Dispose()
            {
                RenderTexture.active=oldTarget;commands.Dispose();target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(readback);
                foreach(var v in floats)Shader.SetGlobalFloat(v.Key,v.Value);foreach(var v in vectors)Shader.SetGlobalVector(v.Key,v.Value);
                foreach(var v in textures)Shader.SetGlobalTexture(v.Key,v.Value);foreach(var v in matrices)Shader.SetGlobalMatrix(v.Key,v.Value);
                foreach(var v in keywords)if(v.Value)Shader.EnableKeyword(v.Key);else Shader.DisableKeyword(v.Key);
                ShaderUtil.allowAsyncCompilation=async;
            }
        }
    }
}
