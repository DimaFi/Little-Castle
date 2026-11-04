using System;
using System.IO;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static partial class SoftRiverAReview
    {
        static void WindowCheck()
        {
            var user=Window();string original=JsonUtility.ToJson(user.VerificationSession.ExportMap()),userDraft=RiverState(user).ExportDraftJson();
            string folder=Root+"/window-check";Directory.CreateDirectory(folder);
            var source=ScriptableObject.CreateInstance<SurfacePaintData>();MapAuthoringWindow window=null;
            var recovery="Library/WorldEditor-.recovery.json";byte[] previous=File.Exists(recovery)?File.ReadAllBytes(recovery):null;
            try
            {
                source.mapId="soft-river-window-check";source.revision=1;source.bounds=new Vector4(-64,-64,128,128);
                source.authored=new SurfacePaintDocument();source.hasHeightBase=true;source.heightField=new MapHeightFieldData{minimumHeight=-8,maximumHeight=20};
                window=ScriptableObject.CreateInstance<MapAuthoringWindow>();window.Connect(source);
                var state=RiverState(window);state.ConfigureNewRiverProfile(true);state.StartRiver();
                state.draft.nodes.Add(new MapRiverNode{id=1,position=new Vector2(0,-15),waterHeight=0,width=4,depth=1,leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand});
                state.Refresh(window.VerificationSession);string incomplete=state.ExportDraftJson();
                if(!window.SaveDocumentAtPath(folder+"/incomplete.json"))throw new InvalidOperationException("Incomplete save failed.");
                var saved=JsonUtility.FromJson<MapAuthoringDocument>(File.ReadAllText(folder+"/incomplete.json"));
                var pair=MapAuthoringRecovery.Read(folder+"/incomplete.json.recovery.json",saved);
                if(pair.riverDraft!=incomplete||saved.rivers.Count!=0)throw new InvalidOperationException("Incomplete draft contaminated map or was lost.");
                void Apply()=>typeof(MapAuthoringWindow).GetMethod("ApplyDocument",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);
                Apply();state=RiverState(window);
                if(state.ExportDraftJson()!=incomplete||source.rivers.Count!=0)throw new InvalidOperationException("Apply lost incomplete draft.");
                state.draft.nodes.Add(new MapRiverNode{id=2,position=new Vector2(0,15),waterHeight=0,width=4,depth=1,leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand});
                Apply();if(source.rivers.Count!=1||window.VerificationSession.RiverCount!=1)throw new InvalidOperationException("Ready draft was not automatically applied.");
                state=RiverState(window);state.draft.nodes[1].position=new Vector2(100,15);string invalid=state.ExportDraftJson();string last=JsonUtility.ToJson(source.rivers[0]);
                Apply();if(JsonUtility.ToJson(source.rivers[0])!=last||state.ExportDraftJson()!=invalid)throw new InvalidOperationException("Invalid draft changed valid source or lost authoring points.");
                window.SaveDocumentAtPath(folder+"/invalid.json");
                File.WriteAllText(folder+"/checks.txt","Native World Editor Save+Apply: incomplete draft recovery PASS; automatic ready river apply PASS; invalid candidate retains last valid source and draft PASS. Only in-memory test source used. No user source write.");
            }
            finally
            {
                if(window)Object.DestroyImmediate(window);Undo.ClearUndo(source);Object.DestroyImmediate(source);
                if(previous!=null)File.WriteAllBytes(recovery,previous);else if(File.Exists(recovery))File.Delete(recovery);
                if(original!=JsonUtility.ToJson(user.VerificationSession.ExportMap())||userDraft!=RiverState(user).ExportDraftJson())throw new InvalidOperationException("User map/draft changed during window check.");
            }
        }
    }
}
