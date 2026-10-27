using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LittleCastle.Tests
{
    public sealed class FlowerStreamingBudgetPlayModeTests
    {
        static void Set(Object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
        [UnityTest] public IEnumerator Flowers_FiniteMapsKeepBootstrapLazyFor2_8_16Players()
        {
#if UNITY_EDITOR
            var report=new StringBuilder("players,mapChunks,initIncludingMacroMs,detailsAfterInit,visibleReadyMs,activeAtReady,cachedAtReady,gameObjectsAtReady,lastVisitLoadMs,worstStageMs,allocatedAtReady,allocatedAfterExplore\n");
            foreach(int size in new[]{16,32})foreach(int players in new[]{2,8,16})
            {
                var definition=Object.Instantiate(AssetDatabase.LoadAssetAtPath<WorldDefinition>("Assets/_Game/Settings/World/MainWorldDefinition.asset"));
                var rules=ScriptableObject.CreateInstance<WorldMapRules>();
                Set(rules,"presets",new List<WorldMapSizePreset>{new WorldMapSizePreset{presetId="flower-audit",minimumPlayers=1,maximumPlayers=16,widthChunks=size,heightChunks=size,visualPaddingChunks=2}});
                Set(definition,"mapRules",rules);
                var root=new GameObject("Flower streaming budget audit");var focus=new GameObject("Audit focus");
                var streamer=root.AddComponent<WorldStreamer>();Set(streamer,"streamOnStart",false);Set(streamer,"worldDefinition",definition);Set(streamer,"focus",focus.transform);Set(streamer,"chunkPresentationRoot",root.transform);
                try
                {
                    Assert.That(streamer.ConfigureFiniteSession(1729,players,"flower-audit"),Is.True);
                    double start=Time.realtimeSinceStartupAsDouble;streamer.InitializeStreaming();double init=(Time.realtimeSinceStartupAsDouble-start)*1000;
                    int initialDetails=streamer.CachedChunkCount;Assert.That(initialDetails,Is.Zero);
                    double deadline=Time.realtimeSinceStartupAsDouble+120;
                    while(!streamer.IsVisibleAreaReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                    Assert.That(streamer.IsVisibleAreaReady,Is.True);double ready=(Time.realtimeSinceStartupAsDouble-start)*1000;
                    int active=streamer.ActiveChunkCount,cached=streamer.CachedChunkCount,objects=root.GetComponentsInChildren<Transform>().Length;
                    Assert.That(active,Is.LessThan(size*size));long memory=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                    focus.transform.position=new Vector3(definition.GenerationSettings.ChunkWorldSize*5,0,0);streamer.RefreshStreamingNow();deadline=Time.realtimeSinceStartupAsDouble+120;
                    while(!streamer.IsVisibleAreaReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                    Assert.That(streamer.IsVisibleAreaReady,Is.True);
                    report.AppendLine(string.Join(",",players,size,init.ToString("F2",CultureInfo.InvariantCulture),initialDetails,ready.ToString("F2",CultureInfo.InvariantCulture),active,cached,objects,streamer.LastChunkLoadMilliseconds.ToString("F2",CultureInfo.InvariantCulture),streamer.WorstGenerationStageMilliseconds.ToString("F2",CultureInfo.InvariantCulture),memory,UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()));
                }
                finally{streamer.ShutdownStreaming();Object.Destroy(root);Object.Destroy(focus);Object.Destroy(definition);Object.Destroy(rules);}
                yield return null;
            }
            string output=Path.GetFullPath(Application.dataPath+"/../../Research/Flowers_20261026");Directory.CreateDirectory(output);File.WriteAllText(output+"/streaming-budget.csv",report.ToString());
#endif
            yield return null;
        }
    }
}
