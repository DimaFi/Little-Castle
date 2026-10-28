using LittleCastle.Rendering;
using LittleCastle.Research;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        MeadowScatterRenderer scatterPreview,scatterTemplate;
        bool templateScatterEnabled;

        void BindScatterPreview(MeadowSurfacePainter painter)
        {
            var template=painter.scatterRenderer;if(!template||!template.library)return;
            scatterTemplate=template;templateScatterEnabled=template.enabled;template.enabled=false;
            var root=new GameObject("World Editor flower preview"){hideFlags=HideFlags.HideAndDontSave};
            scatterPreview=root.AddComponent<MeadowScatterRenderer>();
            scatterPreview.library=template.library;scatterPreview.maximumDistance=template.maximumDistance;
            scatterPreview.lodDistances=template.lodDistances;scatterPreview.maximumInstances=template.maximumInstances;
            scatterPreview.candidateSpacing=template.candidateSpacing;scatterPreview.plantGroupSize=template.plantGroupSize;
            scatterPreview.seed=template.seed;scatterPreview.densityMultiplier=template.densityMultiplier;
            scatterPreview.isolatedChance=template.isolatedChance;scatterPreview.clearingChance=template.clearingChance;
            scatterPreview.clearingRadius=template.clearingRadius;scatterPreview.minimumSeparation=template.minimumSeparation;
            scatterPreview.heightSampler=session.SampleHeight;
            RebuildScatterPreview(null);
        }

        void RestoreScatterPreview()
        {
            if(scatterPreview)DestroyImmediate(scatterPreview.gameObject);
            scatterPreview=null;if(scatterTemplate)scatterTemplate.enabled=templateScatterEnabled;scatterTemplate=null;
        }

        float PreviewRoad(float x,float z)=>PreviewChannel(x,z,0);
        float PreviewGrass(float x,float z)=>PreviewChannel(x,z,1);
        float PreviewChannel(float x,float z,int channel)
        {
            var b=source.bounds;
            int px=Mathf.Clamp(Mathf.FloorToInt((x-b.x)*Resolution/b.z),0,Resolution-1);
            int pz=Mathf.Clamp(Mathf.FloorToInt((z-b.y)*Resolution/b.w),0,Resolution-1);
            return pixels[(pz*Resolution+px)*2+channel]/255f;
        }
        void RebuildScatterPreview(Rect? dirty)
        {
            if(scatterPreview)scatterPreview.Rebuild(session,source.bounds,PreviewRoad,PreviewGrass,dirty);
        }
    }
}
