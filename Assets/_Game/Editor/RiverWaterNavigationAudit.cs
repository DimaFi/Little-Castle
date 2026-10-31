using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static class RiverWaterNavigationAudit
    {
        [MenuItem("Little Castle/Map/River Water/Audit Crossing")]
        public static void Run()
        {
            OwnedStudyWorkflow.Require("Assets/_Game/Art/Studies/RiverWater_20261026", RiverBankStudyBuilder.WaterScenePath);
            Directory.CreateDirectory(RiverBankStudyBuilder.Output);
            const string root="Assets/_Game/Art/Studies/RiverWater_20261026/";var report=new List<string>();
            var ground=new GameObject("Navigation fixture ground");ground.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(root+"RiverBanks_Ground.asset");ground.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(root+"RiverBanks_Ground.mat");
            var surface=AssetDatabase.LoadAssetAtPath<SurfacePaintData>(root+"RiverBanks_Surface.asset");var session=new MapEditSession(MeadowSurfacePainter.CreateDocument(surface,ground.GetComponent<MeshFilter>()));
            var bridge=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Concept/Bridge_Stone_A/Bridge_Stone_A_v002.prefab"));bridge.transform.SetPositionAndRotation(new Vector3(-4,0,-4),Quaternion.Euler(0,RiverBankStudy.BridgeYaw,0));
            foreach(var collider in bridge.GetComponentsInChildren<MeshCollider>(true))
            {
                var readable=AssetDatabase.LoadAssetAtPath<Mesh>(root+collider.sharedMesh.name+"_Readable.asset");if(readable)collider.sharedMesh=readable;
                report.Add(collider.name+" bounds="+collider.bounds+" mesh="+collider.sharedMesh.bounds+" transform="+collider.transform.localToWorldMatrix);
            }
            try
            {
                using(var terrain=new MapTerrainPresentation(session,ground.GetComponent<MeshFilter>(),null,null,null,false))
                using(var navigation=new MapRiverNavigation())
                {
                    navigation.Build(session,bridge,new Bounds(new Vector3(-4,0,9),new Vector3(70,24,95)));
                    var from=FixedBridgeSiteProfile.LocalToWorld(new Vector2(0,-9),RiverBankStudy.BridgeOrigin,RiverBankStudy.BridgeYaw);var to=FixedBridgeSiteProfile.LocalToWorld(new Vector2(0,9),RiverBankStudy.BridgeOrigin,RiverBankStudy.BridgeYaw);
                    bool complete=navigation.Path(new Vector3(from.x,session.SampleHeight(from.x,from.y),from.y),new Vector3(to.x,session.SampleHeight(to.x,to.y),to.y),out var path);report.Add("Complete="+complete+" sources="+navigation.SourceCount);
                    float length=0;for(int i=0;i<path.corners.Length;i++){var p=path.corners[i];if(i>0)length+=Vector3.Distance(path.corners[i-1],p);report.Add("Corner localXZ="+FixedBridgeSiteProfile.WorldToLocal(new Vector2(p.x,p.z),RiverBankStudy.BridgeOrigin,RiverBankStudy.BridgeYaw)+" y="+p.y);}report.Add("Length="+length);
                    for(float z=-9;z<=9;z+=.25f)
                    {
                        var p=FixedBridgeSiteProfile.LocalToWorld(new Vector2(0,z),RiverBankStudy.BridgeOrigin,RiverBankStudy.BridgeYaw);float terrainY=session.SampleHeight(p.x,p.y),deckY=FixedBridgeSiteProfile.DeckHeight(z),y=Mathf.Abs(z)<=5.4f?deckY:terrainY;bool hit=NavMesh.SamplePosition(new Vector3(p.x,y,p.y),out var nav,.4f,NavMesh.AllAreas);
                        report.Add($"z={z:F2} terrain={terrainY:F3} deck={deckY:F3} nav={hit} navY={(hit?nav.position.y:float.NaN):F3}");
                    }
                }
            }
            finally{Object.DestroyImmediate(ground);Object.DestroyImmediate(bridge);File.WriteAllLines(Path.Combine(RiverBankStudyBuilder.Output,"navigation-audit.txt"),report);}
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
}
