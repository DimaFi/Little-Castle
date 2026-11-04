using System;
using UnityEngine;

namespace LittleCastle.Building.Cottages
{
    [Serializable]
    public sealed class CottageModule
    {
        public string id;
        public GameObject prefab;
        public float width,height,depth;
        public float openingWidth,openingHeight,openingBottomOffset;
        public float archRadius;
    }

    [CreateAssetMenu(menuName="Little Castle/Building/Cottage Definition")]
    public sealed class CottageDefinition : ScriptableObject
    {
        public float baseWidth=6.2f,baseDepth=4.8f,bayStep=1.6f;
        public float foundationHeight=.46f,wallTop=3.3f,floorHeight=2.7f;
        public int maximumLengthSteps=4,maximumDepthSteps=2,maximumFloors=2;
        public Material plaster,wood,roof,stone,scaffold,preview;
        public Material[] nearMaterials=new Material[0],farMaterials=new Material[0];
        public CottageModule[] modules=new CottageModule[0];
        public CottageModule Module(string id) => Array.Find(modules,m=>m.id==id);
        public CottageModule Door => Module("Door_Entrance_A");
        public CottageModule Window => Module("Window_Open_A");
        public CottageModule SmallWindow => Module("Window_Small_A");
        public CottageModule Tile => Module("RoofTile_A");
        public CottageModule Ridge => Module("RidgeCap_A");
        public CottageModule Chimney => Module("Chimney_A");
        public Material Far(Material near){int i=Array.IndexOf(nearMaterials,near);return i>=0&&i<farMaterials.Length&&farMaterials[i]!=null?farMaterials[i]:near;}
    }
}
