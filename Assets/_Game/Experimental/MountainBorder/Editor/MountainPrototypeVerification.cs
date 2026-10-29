using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Editor
{
    public static class MountainPrototypeVerification
    {
        // Explicit batch-only entry point. No scene open/save, asset builders or map writes.
        public static void Run()
        {
            string directory=Environment.GetEnvironmentVariable("LC_MOUNTAIN_VERIFY_OUTPUT");
            if(string.IsNullOrWhiteSpace(directory))throw new InvalidOperationException("Set LC_MOUNTAIN_VERIFY_OUTPUT to an evidence directory.");
            Directory.CreateDirectory(directory);var active=SceneManager.GetActiveScene().handle;
            var settings=ScriptableObject.CreateInstance<MountainPreviewSettings>();
            try
            {
                using(var preview=new MountainBorderPreview(settings))
                {
                    preview.MountainMaterial.SetFloat("_RockSaturation",1);preview.MountainMaterial.SetFloat("_SnowEnabled",0);
                    preview.SaveCapture(Path.Combine(directory,"01-original-warm.png"));
                    preview.MountainMaterial.SetFloat("_RockSaturation",0);preview.MountainMaterial.SetFloat("_SnowEnabled",1);
                    preview.SaveCapture(Path.Combine(directory,"02-gray-alpine.png"));
                    preview.SaveCapture(Path.Combine(directory,"03-waterfall-v0.png"),true);
                    foreach(var shader in new[]{preview.MountainMaterial.shader,preview.WaterMaterial.shader})
                    {
                        var errors=ShaderUtil.GetShaderMessages(shader).Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                        if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(x=>x.message)));
                    }
                    if(preview.scene.GetRootGameObjects().Any(x=>x.GetComponentsInChildren<Collider>().Length>0))throw new Exception("Unexpected preview collider.");
                    File.WriteAllText(Path.Combine(directory,"preview-verification.json"),"{\"status\":\"PASS\",\"scope\":\"isolated visual v0 only\",\"vertices\":"+preview.TerrainMesh.vertexCount+",\"colliders\":0}");
                }
                if(SceneManager.GetActiveScene().handle!=active)throw new Exception("Active scene changed.");
                Debug.Log("MOUNTAIN_PREVIEW_VERIFY_PASS "+directory);
            }
            finally{Object.DestroyImmediate(settings);}
        }
    }
}
