using LittleCastle.Rendering;
using UnityEngine;

namespace LittleCastle.Research
{
    public sealed partial class MeadowSurfacePainter
    {
        public MapFencePresentation FenceView {get;private set;}
        void InitializeFencePresentation()
        {FenceView?.Dispose();var catalog=MapFenceCatalog.Default;FenceView=Session!=null&&catalog?new MapFencePresentation(Session,catalog):null;}
        void TickFencePresentation()
        {
            if(FenceView==null||!studyCamera)return;
            var ray=studyCamera.ViewportPointToRay(new Vector3(.5f,.5f));var plane=new Plane(Vector3.up,0);
            var focus=plane.Raycast(ray,out float distance)?ray.GetPoint(distance):studyCamera.transform.position;
            FenceView.Tick(focus);
        }
    }
}
