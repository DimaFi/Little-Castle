using System.IO;
using LittleCastle.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Research
{
    /// <summary>Exact tree models/materials under the already-running Meadow lighting. No light/global edits.</summary>
    public static class MapTreeThumbnailCapture
    {
        public static void Capture(MapTreeCatalog catalog,Camera reference,string directory)
        {
            Directory.CreateDirectory(directory);var cameraRoot=new GameObject("Isolated tree thumbnail camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=cameraRoot.AddComponent<Camera>();camera.CopyFrom(reference);camera.enabled=false;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.20f,.16f,1);camera.orthographic=true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=reference.GetUniversalAdditionalCameraData().renderPostProcessing;
            var target=RenderTexture.GetTemporary(256,256,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
            var resolved=RenderTexture.GetTemporary(256,256,0,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
            try
            {
                for(int i=0;i<catalog.entries.Length;i++)
                {
                    var entry=catalog.entries[i];var root=Object.Instantiate(entry.prefab);root.hideFlags=HideFlags.HideAndDontSave;
                    try
                    {
                        root.transform.position=new Vector3(3000+i*40,0,3000);root.transform.rotation=Quaternion.identity;
                        foreach(var node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=30;
                        var center=root.transform.TransformPoint(entry.localBounds.center);float radius=entry.localBounds.extents.magnitude;
                        camera.orthographicSize=Mathf.Max(entry.localBounds.size.y*.6f,entry.localBounds.size.x*.7f);
                        camera.nearClipPlane=.05f;camera.farClipPlane=radius*8+10;camera.transform.position=center+new Vector3(1,.42f,-1).normalized*radius*3;camera.transform.LookAt(center);
                        camera.targetTexture=target;camera.Render();Graphics.Blit(target,resolved);RenderTexture.active=resolved;
                        var image=new Texture2D(256,256,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
                        File.WriteAllBytes(Path.Combine(directory,MapTreeCatalog.ThumbnailName(entry)),image.EncodeToPNG());Object.Destroy(image);
                    }
                    finally{Object.Destroy(root);}
                }
            }
            finally{RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);RenderTexture.ReleaseTemporary(resolved);Object.Destroy(cameraRoot);}
        }
    }
}
