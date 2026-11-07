using System;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Editor recovery is separate from the valid map and never becomes runtime source data.</summary>
    [Serializable] public sealed class MapAuthoringRecovery
    {
        public int version=1;
        public MapAuthoringDocument map;
        public string riverDraft;
        public static string Companion(string path)=>path+".recovery.json";
        public static void Write(string path,MapAuthoringDocument map,string draft)
        {AtomicWrite(path,JsonUtility.ToJson(new MapAuthoringRecovery{map=map,riverDraft=draft},true));}
        public static MapAuthoringRecovery Read(string path,MapAuthoringDocument map)
        {
            var saved=JsonUtility.FromJson<MapAuthoringRecovery>(File.ReadAllText(path));
            if(saved==null||saved.version!=1||saved.map==null||JsonUtility.ToJson(saved.map)!=JsonUtility.ToJson(map))
                throw new InvalidDataException("Восстановительная копия не совпадает с JSON карты. Исходные файлы сохранены.");
            return saved;
        }
        public static void AtomicWrite(string path,string text)
        {
            string temp=path+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temp,text);
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
