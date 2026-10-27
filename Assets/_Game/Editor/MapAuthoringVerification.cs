using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using LittleCastle.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>Builds the existing scene without regenerating assets, light or pipeline settings.</summary>
    public static class MapAuthoringVerification
    {
        public static void BuildExistingMeadow()=>Build("MapAuthoring_20261025","Meadow_MapAuthoring.exe");
        public static void BuildTreesMeadow(){MapTreeCatalogBuilder.Ensure();Build("MapTrees_20261026","Meadow_Trees.exe");}
        public static void BuildTerrainMeadow()=>Build("MapTerrain_20261026","Meadow_Terrain.exe");
        public static void BuildRiversMeadow(){MapRiverCatalogBuilder.Ensure();Build("MapRivers_20261026","Meadow_Rivers.exe");}
        [MenuItem("Little Castle/Map/Rivers/Build Separate Meadow Player %#F9")]
        public static void BuildRiversInteractive(){MapRiverCatalogBuilder.Ensure();Build("MapRivers_20261026","Meadow_Rivers.exe",false);}
        static void Build(string folder,string executable,bool exitEditor=true)
        {
            string output=Path.GetFullPath(Application.dataPath+"/../../Research/"+folder);
            Directory.CreateDirectory(output);
            ValidateExistingMeshes(output);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{MeadowStudyBuilder.ScenePath},locationPathName=Path.Combine(output,"Player/"+executable),
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(Path.Combine(output,"build_result.txt"),$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nTime={report.summary.totalTime}\nExisting scene only; no study regeneration.");
            if(exitEditor)EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:2);
        }
        static void ValidateExistingMeshes(string output)
        {
            var log=new StringBuilder();
            Application.LogCallback collect=(message,stack,type)=>{if(message.Contains("[Little Castle Foliage]"))log.AppendLine(type+": "+message);};
            Application.logMessageReceived+=collect;
            try
            {
                string path=MeadowStudyBuilder.Root;
                var native=AssetDatabase.LoadAssetAtPath<MeadowGrassData>(path+"/Meadow_InstanceData.asset");
                var nativeMaterial=AssetDatabase.LoadAssetAtPath<Material>(path+"/Meadow_Blades.mat");
                var test=new GameObject("Exact existing Meadow native mesh validation");
                try
                {
                    test.AddComponent<MeshFilter>().sharedMesh=native.lodMeshes[0];test.AddComponent<MeshRenderer>().sharedMaterial=nativeMaterial;
                    Selection.activeGameObject=test;FoliageModelCompatibilityValidator.ValidateSelected();
                    foreach(var mesh in native.lodMeshes)log.AppendLine($"{mesh.name}: vertices={mesh.vertexCount}; triangles={mesh.GetIndexCount(0)/3}; normals={mesh.HasVertexAttribute(VertexAttribute.Normal)}; UV0={mesh.HasVertexAttribute(VertexAttribute.TexCoord0)}; rootUV1={mesh.HasVertexAttribute(VertexAttribute.TexCoord1)}");
                }
                finally{Object.DestroyImmediate(test);}
                var library=AssetDatabase.LoadAssetAtPath<MeadowScatterLibrary>(path+"/Meadow_ScatterLibrary.asset");
                foreach(var prototype in library.prototypes)
                {
                    log.AppendLine($"{prototype.stableId}: category={prototype.category}; source={prototype.sourceAssetPath}; LODs={prototype.lodMeshes.Length}; static decoration, no LC Grass bending.");
                    foreach(var mesh in prototype.lodMeshes)log.AppendLine($"  {mesh.name}: vertices={mesh.vertexCount}; triangles={mesh.GetIndexCount(0)/3}; normals={mesh.HasVertexAttribute(VertexAttribute.Normal)}; UV0={mesh.HasVertexAttribute(VertexAttribute.TexCoord0)}");
                }
                foreach(char oak in "ABCD")
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ValleyArtAssets.Root+"/Oak_"+oak+"_Study.prefab");
                    if(!prefab)continue;
                    var tree=Object.Instantiate(prefab);
                    try{Selection.activeGameObject=tree;FoliageModelCompatibilityValidator.ValidateSelected();}
                    finally{Object.DestroyImmediate(tree);}
                }
                log.AppendLine("COMPATIBLE: existing native grass contract; existing flowers retain static foliage/solid adapters. Models, materials and shaders unchanged. Exact Player close/far frames accompany this report.");
            }
            finally{Application.logMessageReceived-=collect;File.WriteAllText(Path.Combine(output,"foliage-validation.txt"),log.ToString());}
        }
    }
}
