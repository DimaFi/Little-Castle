using System;
using System.Collections.Generic;
using System.Globalization;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    [CreateAssetMenu(menuName="Little Castle/Map/Tree Catalog",fileName="TreeCatalog")]
    public sealed class MapTreeCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string title;
            public MapTreeDefinition definition=new MapTreeDefinition();
            public GameObject prefab;
            public Texture2D thumbnail;
            public Bounds localBounds;
        }
        public Entry[] entries=Array.Empty<Entry>();
        public MapTreePack[] packs=Array.Empty<MapTreePack>();
        public static readonly string[] GroupNames={"Дубы Little Castle","Луговые · лето","Луговые · осень","Хвойные","Сухостой","Искривлённые"};
        public static int Group(Entry entry)
        {
            string id=entry.definition.archetypeId;
            if(id.StartsWith("quaternius.CommonTree",StringComparison.Ordinal))return id.EndsWith(".autumn",StringComparison.Ordinal)?2:1;
            if(id.StartsWith("quaternius.Pine_",StringComparison.Ordinal))return 3;
            if(id.StartsWith("quaternius.DeadTree_",StringComparison.Ordinal))return 4;
            if(id.StartsWith("quaternius.TwistedTree_",StringComparison.Ordinal))return 5;
            return 0;
        }
        public List<int> GroupIndices(int group)
        {var result=new List<int>();for(int i=0;i<entries.Length;i++)if(Group(entries[i])==group)result.Add(i);return result;}
        public static string PackTitle(MapTreePack pack)
        {
            if(pack.id=="oak.abcd")return "Дубовая роща";
            if(pack.id=="quaternius.meadow.mixed")return "Летне-осенняя роща";
            const string prefix="quaternius.group.";
            if(pack.id.StartsWith(prefix,StringComparison.Ordinal)&&int.TryParse(pack.id.Substring(prefix.Length),out int group)&&group>=0&&group<GroupNames.Length)return GroupNames[group]+" · роща";
            return pack.id;
        }
        public static MapTreeCatalog Default=>Resources.Load<MapTreeCatalog>("MapAuthoring/MeadowTreeCatalog");
        public IEnumerable<MapTreeDefinition> Definitions {get{foreach(var e in entries)yield return e.definition;}}
        public Entry Resolve(string id){foreach(var e in entries)if(e.definition.archetypeId==id)return e;return null;}
        public static string ThumbnailName(Entry entry)
        {
            string id=entry.definition.archetypeId;
            return id.Length==5&&id.StartsWith("oak.",StringComparison.Ordinal)&&"abcd".IndexOf(id[4])>=0?
                "Oak_"+char.ToUpperInvariant(id[4])+".png":"Tree_"+MapObjectIds.FromKey(id).ToString("x16",CultureInfo.InvariantCulture)+".png";
        }
        public List<GameObject> SceneSources()
        {
            var result=new List<GameObject>();var seen=new HashSet<GameObject>();
            foreach(var group in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include))
            {
                if(group.GetComponentInParent<MapTreeInstanceView>())continue;
                for(var node=group.transform;node!=null;node=node.parent)
                {
                    bool match=false;foreach(var e in entries)if(e.prefab&&(node.name==e.prefab.name||node.name==e.prefab.name+"(Clone)")){match=true;break;}
                    if(match){if((node.gameObject.hideFlags&HideFlags.DontSave)==0&&seen.Add(node.gameObject))result.Add(node.gameObject);break;}
                }
            }
            return result;
        }
        public void ImportSceneBase(MapAuthoringDocument map,MeshFilter ground)
        {
            MapHeightField reference=ground?new MapHeightField(MapHeightMeshImport.Read(ground.sharedMesh,ground.transform,out _,out _)):new MapHeightField(map.heightField);
            foreach(var root in SceneSources())
            {
                var p=root.transform.position;var b=map.bounds;if(p.x<b.x||p.z<b.y||p.x>b.x+b.z||p.z>b.y+b.w)continue;
                Entry entry=null;foreach(var e in entries)if(e.prefab&&(root.name==e.prefab.name||root.name==e.prefab.name+"(Clone)")){entry=e;break;}if(entry==null)continue;
                float scale=root.transform.lossyScale.x/entry.prefab.transform.localScale.x;
                string key=map.mapId+"/scene/"+entry.definition.archetypeId+"/"+Mathf.RoundToInt(p.x*1000).ToString(CultureInfo.InvariantCulture)+"/"+Mathf.RoundToInt(p.z*1000).ToString(CultureInfo.InvariantCulture);
                map.trees.Add(new MapTreeInstance{stableId=MapObjectIds.FromKey(key),archetypeId=entry.definition.archetypeId,catalogVersion=entry.definition.version,
                    position=new Vector2(p.x,p.z),yawDegrees=root.transform.eulerAngles.y,uniformScale=scale,groundOffset=p.y-reference.Height(p.x,p.z),
                    clearanceRadius=entry.definition.minimumSpacing*.5f*scale,playerEditable=entry.definition.playerPlaceable&&!entry.definition.authorOnly});
            }
            map.trees.Sort((a,b)=>a.stableId.CompareTo(b.stableId));
        }
    }
}
