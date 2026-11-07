using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static class FenceAuthoringVerification
    {
        public static readonly string Output=Path.GetFullPath(Application.dataPath+"/../../Research/FenceBrush_20261029");
        [MenuItem("Little Castle/World Editor/Verify Fence Assets and Capture")]
        public static void Capture()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before verification.");
            var catalog=FenceAuthoringSetup.Ensure();Directory.CreateDirectory(Output);
            var active=SceneManager.GetActiveScene();int count=SceneManager.sceneCount;bool wasDirty=active.isDirty;
            var scene=EditorSceneManager.NewPreviewScene();MapFencePresentation view=null;
            var ownedMaterials=new List<Material>();
            try
            {
                GameObject Make(string name)
                {var go=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(go,scene);return go;}
                var map=new MapAuthoringDocument{mapId="fence-visual-proof",bounds=new Vector4(-32,-32,64,64)};
                map.fences.Add(new MapFenceRecord{id=1,points=new List<Vector2>{new Vector2(-5,-2),new Vector2(5,-2)}});
                map.fences.Add(new MapFenceRecord{id=2,variantId="fence.wood02",points=new List<Vector2>{new Vector2(-5,3),new Vector2(-2,3),new Vector2(2,5),new Vector2(5,7)}});
                var session=new MapEditSession(map);view=new MapFencePresentation(session,catalog,80,scene);view.Tick(Vector3.zero);
                if(view.VisibleSpans==0)throw new InvalidOperationException("Fence presentation is empty.");
                var ground=Make("Private fence verification ground");var mf=ground.AddComponent<MeshFilter>();
                var plane=Resources.GetBuiltinResource<Mesh>("Plane.fbx");mf.sharedMesh=plane;
                ground.transform.position=new Vector3(0,-.012f,2);ground.transform.localScale=Vector3.one*5;
                var material=new Material(catalog.entries[0].material);ownedMaterials.Add(material);material.SetTexture("_MainTex",Texture2D.whiteTexture);material.SetColor("_Color",new Color(.24f,.30f,.17f));
                ground.AddComponent<MeshRenderer>().sharedMaterial=material;
                var light=Make("Private fence key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.color=new Color(1,.91f,.79f);light.transform.rotation=Quaternion.Euler(48,-25,0);
                var camera=Make("Private fence verification camera").AddComponent<Camera>();camera.scene=scene;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.28f,.33f,.24f);camera.nearClipPlane=.1f;camera.farClipPlane=100;
                camera.orthographic=true;camera.orthographicSize=8;camera.transform.position=new Vector3(11,12,-15);camera.transform.LookAt(new Vector3(0,.4f,2));
                Save(camera,Path.Combine(Output,"unity-straight-and-curve.png"));
                camera.orthographicSize=3.9f;camera.transform.position=new Vector3(7,5,-1);camera.transform.LookAt(new Vector3(1,.5f,4.6f));
                Save(camera,Path.Combine(Output,"unity-curved-rails-detail.png"));
                File.WriteAllText(Path.Combine(Output,"asset-verification.json"),"{\"status\":\"PASS\",\"variants\":2,\"visibleSpans\":"+view.VisibleSpans+",\"chunkBatches\":"+view.VisibleBatches+",\"scope\":\"Real Unity meshes and shared shader; private preview only\"}");
            }
            finally
            {
                view?.Dispose();EditorSceneManager.ClosePreviewScene(scene);foreach(var material in ownedMaterials)Object.DestroyImmediate(material);
            }
            if(SceneManager.GetActiveScene()!=active||SceneManager.sceneCount!=count||active.isDirty!=wasDirty)
                throw new InvalidOperationException("Fence verification changed the authoring scene lifecycle.");
            Debug.Log("FENCE_ASSETS_AND_CAPTURE_PASS "+Output);
        }
        internal static void Save(Camera camera,string path)
        {
            var target=RenderTexture.GetTemporary(1600,1050,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;float previousAspect=camera.aspect;Texture2D pixels=null;
            try
            {
                camera.aspect=(float)target.width/target.height;
                var request=new RenderPipeline.StandardRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new NotSupportedException("URP preview render request unavailable.");
                RenderPipeline.SubmitRenderRequest(camera,request);RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=target;pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{camera.aspect=previousAspect;RenderTexture.active=previous;if(pixels)Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(target);}
        }
    }
}
