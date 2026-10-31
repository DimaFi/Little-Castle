using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Editor
{
    /// <summary>Disposable private preview scene. Never reads/writes MapEditSession or saved scenes.</summary>
    public sealed class MountainBorderPreview : IDisposable
    {
        public const string RockPath = "Assets/_Game/Art/Studies/ValleyArt_Pass03/Textures/Rock_BaseColor.png";
        public const string NormalPath = "Assets/_Game/Art/Studies/ValleyArt_Pass03/Textures/Rock_Normal.png";
        public readonly Scene scene;
        public Camera Camera { get; private set; }
        public Mesh TerrainMesh { get; private set; }
        public Material MountainMaterial { get; private set; }
        public Material WaterMaterial { get; private set; }
        readonly List<Object> owned = new List<Object>();
        bool disposed;

        public MountainBorderPreview(MountainPreviewSettings settings)
        {
            if (!settings) throw new ArgumentNullException(nameof(settings));
            MountainRidgeSampler.Validate(settings.ridge);
            if (settings.showWaterfall && !WaterfallLinkDraft.Validate(settings.waterfall, out string error))
                throw new ArgumentException(error);
            scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var template=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Experimental/MountainBorder/Materials/Mountain_AlpineGray_Preview.mat");
                MountainMaterial = Own(template ? new Material(template) : new Material(RequireShader("Little Castle/Experimental/Mountain Border Preview")));
                MountainMaterial.name = "Transient alpine gray - original textures untouched";
                MountainMaterial.SetTexture("_RockTex", AssetDatabase.LoadAssetAtPath<Texture2D>(RockPath));
                MountainMaterial.SetTexture("_RockNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
                MountainMaterial.SetColor("_RockTint", settings.rockTint);
                MountainMaterial.SetFloat("_RockSaturation", settings.rockSaturation);
                MountainMaterial.SetFloat("_SnowLine", settings.snowLine);
                MountainMaterial.SetFloat("_SnowBlend", settings.snowBlend);
                MountainMaterial.SetFloat("_NormalStrength", settings.normalStrength);
                MountainMaterial.SetFloat("_UseSharedLighting", 0); // No global LC state is changed.
                TerrainMesh = Own(BuildMesh(settings.ridge, settings.meshMinimum, settings.meshMaximum, settings.cellSize));
                AddMesh("Decorative ridge - no collision or navigation", TerrainMesh, MountainMaterial);

                if (settings.showWaterfall)
                {
                    WaterMaterial = Own(new Material(RequireShader("Little Castle/Experimental/Waterfall Preview")));
                    AddMesh("Waterfall visual v0 - two explicit levels", Own(BuildWaterfall(settings.waterfall)), WaterMaterial);
                    // The upper channel terminates exactly at the lip; the lower pool begins at landing.
                    var w = settings.waterfall;
                    // Explicit test shelf, not inferred river carving. Primitive collider is removed immediately.
                    var ledge=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ledge.name="Isolated test ledge - not a generated river bank";
                    ledge.hideFlags=HideFlags.HideAndDontSave;SceneManager.MoveGameObjectToScene(ledge,scene);
                    Object.DestroyImmediate(ledge.GetComponent<Collider>());
                    ledge.transform.position=new Vector3(w.top.x,(w.top.y+w.bottom.y)*.5f-.08f,w.top.z+5);
                    ledge.transform.localScale=new Vector3(w.width+4,(float)w.DropHeight,10);
                    var ledgeRenderer=ledge.GetComponent<MeshRenderer>();ledgeRenderer.sharedMaterial=MountainMaterial;
                    ledgeRenderer.shadowCastingMode=ShadowCastingMode.Off;ledgeRenderer.receiveShadows=false;
                    AddMesh("Upper level placeholder", Own(Quad(w.top+new Vector3(-w.width*.5f,0,0),
                        w.top+new Vector3(w.width*.5f,0,0), w.top+new Vector3(-w.width*.5f,0,10),
                        w.top+new Vector3(w.width*.5f,0,10))), WaterMaterial);
                    AddMesh("Landing pool placeholder", Own(Quad(w.bottom+new Vector3(-w.width,0,-12),
                        w.bottom+new Vector3(w.width,0,-12), w.bottom+new Vector3(-w.width,0,0),
                        w.bottom+new Vector3(w.width,0,0))), WaterMaterial);
                }
                var sun = AddObject("Preview sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.15f; sun.color = new Color(1,.94f,.85f);
                sun.shadows = LightShadows.None; sun.transform.rotation = Quaternion.Euler(42,-32,0);
                Camera = AddObject("Isolated preview camera").AddComponent<Camera>();
                Camera.enabled = false; Camera.scene = scene;
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.38f,.49f,.63f);
                Camera.nearClipPlane = .2f; Camera.farClipPlane = 600; Camera.fieldOfView = 44;
                SetView(false);
            }
            catch { Dispose(); throw; }
        }

        public void SetView(bool waterfallCloseup)
        {
            Camera.transform.position = waterfallCloseup ? new Vector3(-70,32,-55) : new Vector3(150,106,-210);
            Camera.transform.LookAt(waterfallCloseup ? new Vector3(-43,16,-8) : new Vector3(0,27,35));
        }

        public static Mesh BuildMesh(MountainRidgeRecipe recipe, Vector2 minimum, Vector2 maximum, float spacing)
        {
            MountainRidgeSampler.Validate(recipe);
            if (!Finite(minimum.x)||!Finite(minimum.y)||!Finite(maximum.x)||!Finite(maximum.y)||
                !Finite(spacing)||spacing<.25f||maximum.x<=minimum.x||maximum.y<=minimum.y)
                throw new ArgumentException("Invalid preview mesh bounds/spacing.");
            double dx=Math.Ceiling(((double)maximum.x-minimum.x)/spacing);
            double dz=Math.Ceiling(((double)maximum.y-minimum.y)/spacing);
            if (dx>1024||dz>1024||(dx+1)*(dz+1)>150000)
                throw new ArgumentException("Preview mesh exceeds its bounded vertex budget.");
            int nx=(int)dx,nz=(int)dz;
            var vertices=new Vector3[(nx+1)*(nz+1)]; var normals=new Vector3[vertices.Length];
            var uv=new Vector2[vertices.Length]; var triangles=new int[nx*nz*6];
            for(int z=0;z<=nz;z++) for(int x=0;x<=nx;x++)
            {
                int i=z*(nx+1)+x;
                var p=new Vector2(Mathf.Min(maximum.x,minimum.x+x*spacing),Mathf.Min(maximum.y,minimum.y+z*spacing));
                vertices[i]=new Vector3(p.x,MountainRidgeSampler.SampleHeight(recipe,p),p.y);
                normals[i]=MountainRidgeSampler.SampleNormal(recipe,p,spacing*.5f);
                uv[i]=p*.02f;
            }
            int t=0;
            for(int z=0;z<nz;z++) for(int x=0;x<nx;x++)
            {int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;}
            var mesh=new Mesh { name="Bounded deterministic mountain preview",indexFormat=vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16 };
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh BuildWaterfall(WaterfallLinkDraft link)
        {
            if (!WaterfallLinkDraft.Validate(link,out string error)) throw new ArgumentException(error);
            Vector3 direction=link.bottom-link.top;
            Vector3 lateral=Vector3.Cross(direction,Vector3.up);
            if(lateral.sqrMagnitude<.0001f) lateral=Vector3.right;
            lateral=lateral.normalized*(link.width*.5f);
            return Quad(link.bottom-lateral,link.bottom+lateral,link.top-lateral,link.top+lateral);
        }

        static Mesh Quad(Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft, Vector3 topRight)
        {
            var mesh=new Mesh { name="Waterfall ribbon/level placeholder" };
            mesh.vertices=new[]{bottomLeft,bottomRight,topLeft,topRight};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
            mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        public void Render(RenderTexture destination)
        {
            if(disposed)throw new ObjectDisposedException(nameof(MountainBorderPreview));
            if(!destination)throw new ArgumentNullException(nameof(destination));
            if(WaterMaterial)WaterMaterial.SetFloat("_PreviewTime",(float)(EditorApplication.timeSinceStartup%3600));
            Camera.aspect=(float)destination.width/destination.height;
            if(GraphicsSettings.currentRenderPipeline==null)
            {var old=Camera.targetTexture;try{Camera.targetTexture=destination;Camera.Render();}finally{Camera.targetTexture=old;}}
            else
            {
                var request=new RenderPipeline.StandardRequest { destination=destination };
                if(!RenderPipeline.SupportsRenderRequest(Camera,request)) throw new NotSupportedException("Pipeline cannot render the private preview.");
                RenderPipeline.SubmitRenderRequest(Camera,request);
            }
        }

        public void SaveCapture(string path, bool closeup=false)
        {
            SetView(closeup);
            var target=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);
            Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                Render(target);Render(target);RenderTexture.active=target;
                pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;if(pixels)Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(target);}
        }

        static Shader RequireShader(string name)
        {var shader=Shader.Find(name);if(!shader)throw new InvalidOperationException("Missing preview shader: "+name);return shader;}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        T Own<T>(T value) where T:Object {value.hideFlags=HideFlags.HideAndDontSave;owned.Add(value);return value;}
        GameObject AddObject(string name)
        {var obj=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(obj,scene);return obj;}
        void AddMesh(string name,Mesh mesh,Material material)
        {
            var obj=AddObject(name);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
            for(int i=owned.Count-1;i>=0;i--)if(owned[i])Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }
    }
}
