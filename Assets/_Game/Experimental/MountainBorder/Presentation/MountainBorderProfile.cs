using UnityEngine;
using LittleCastle.Rendering;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    [CreateAssetMenu(menuName="Little Castle/Landscape/Mountain Border Profile")]
    public sealed class MountainBorderProfile : ScriptableObject
    {
        public int generatorVersion=2,seed=1729;
        public MountainRockDetailSettings rockDetail=new MountainRockDetailSettings();
        public Mesh[] lodMeshes;
        public Mesh[] outcropLodMeshes;
        public Mesh meadowJoinMesh;
        public Material groundMaterial,alpineMaterial,riverMaterial,waterfallMaterial,mistMaterial;
        public Material riverBodyMaterial;
        public Texture normalTexture,foamTexture,phaseTexture;
        public GameObject sprucePrefab;
        public MapRockCatalog studyRockCatalog;
        public MeadowGrassData grassSource;
        public Material grassMaterial;
        [Range(0,1)] public float grassDensity=.65f;
        [Range(0,60000)] public int maximumGrassClumps=30000;
        public WaterfallVisualSettings waterfall=new WaterfallVisualSettings();
        [Range(0,160)] public int maximumTrees=100;
        [Range(0,1)] public float treeDensity=.55f;
        [Min(1)] public float lod0Distance=120,lod1Distance=260;
        [Range(35,120)] public float snowAltitude=76;
        [Range(1,45)] public float snowTransition=22;
        [Header("Optional distant clouds in front of alpine peaks")]
        public bool enablePeakStormClouds = true;
        [Range(3,16)] public int peakCloudCount = 14;
        [Range(1,3)] public int peakCloudBillowRows = 3;
        [Range(.1f,1f)] public float peakCloudOpacity = .78f;
        [Range(.5f,2f)] public float peakCloudWidthMultiplier = 1.28f;
        [Range(0f,40f)] public float peakCloudFrontOffset = 22f;
        [Min(40f)] public float peakCloudMinimumDistance = 58f;
    }
}
