using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleCastle.Experimental.MountainBorder.Editor
{
    public static class MountainArtV1Verification
    {
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("LC_MOUNTAIN_V1_OUTPUT");
            if(string.IsNullOrWhiteSpace(output))throw new InvalidOperationException("Set LC_MOUNTAIN_V1_OUTPUT.");
            Directory.CreateDirectory(output);var before=SceneManager.GetActiveScene().handle;int count=SceneManager.sceneCount;
            var foliageLog=new StringBuilder();
            void Log(string message,string stack,LogType type){if(message.Contains("Little Castle Foliage"))foliageLog.AppendLine(type+": "+message);}
            Application.logMessageReceived+=Log;
            try
            {
                using(var preview=new MountainArtPreviewV1())
                {
                    preview.SaveCapture(Path.Combine(output,"01-clay-rts42.png"),MountainArtView.RTS42,true);
                    preview.SaveCapture(Path.Combine(output,"02-clay-rts60.png"),MountainArtView.RTS60,true);
                    preview.SaveCapture(Path.Combine(output,"03-clay-side.png"),MountainArtView.Side,true);
                    preview.SaveCapture(Path.Combine(output,"04-landscape.png"),MountainArtView.Composition,false);
                    preview.SaveCapture(Path.Combine(output,"05-waterfall.png"),MountainArtView.Waterfall,false);
                    foreach(var shader in new[]{preview.Land.shader,preview.Surface.shader})
                    {
                        var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                        if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(m=>m.message)));
                    }
                    if(preview.Scene.GetRootGameObjects().Any(o=>o.GetComponentsInChildren<Collider>().Length!=0))throw new Exception("Unexpected collider.");
                }
                using(var preview=new MountainArtPreviewV1(1729,true))
                {
                    preview.SaveCapture(Path.Combine(output,"06-sector-trees.png"),MountainArtView.Composition,false);
                    preview.SaveCapture(Path.Combine(output,"07-sector-rts42.png"),MountainArtView.RTS42,false);
                    preview.SaveCapture(Path.Combine(output,"08-waterfall-trees.png"),MountainArtView.Waterfall,false);
                    File.WriteAllText(Path.Combine(output,"verification.json"),"{\"status\":\"PASS\",\"scope\":\"authored v1 visual proof, not production integration\",\"triangles\":"+preview.Terrain.triangles.Length/3+",\"staticTrees\":"+preview.TreeCount+",\"colliders\":0}");
                }
                if(SceneManager.GetActiveScene().handle!=before||SceneManager.sceneCount!=count)throw new Exception("Scene lifecycle changed.");
                File.WriteAllText(Path.Combine(output,"source-foliage-validator.txt"),foliageLog.ToString());
                Debug.Log("MOUNTAIN_ART_V1_VERIFICATION_PASS "+output);
            }
            finally{Application.logMessageReceived-=Log;}
        }
    }
}
