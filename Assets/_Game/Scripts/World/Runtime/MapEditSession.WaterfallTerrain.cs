using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        bool BeyondRoutedTerminal(MapRiverPath path, Vector2 p, long plannedUpper = 0, long plannedLower = 0)
        {
            var first=path.frames[0];var last=path.frames[path.frames.Count-1];long id=path.record.id;
            if((plannedUpper==id||riverNetwork.HasRoutedEndpoint(id,false))&&Vector2.Dot(p-last.center,last.tangent)>.0001f)return true;
            if((plannedLower==id||riverNetwork.HasRoutedEndpoint(id,true))&&Vector2.Dot(p-first.center,first.tangent)<-.0001f)return true;
            return false;
        }
        // Preview the same terminal openings that committing this routed link will produce.
        // This avoids projecting water onto the old river's radial closing bank.
        public float SampleWaterfallSupportHeight(float x,float z,long plannedUpper=0,long plannedLower=0)
        {
            if(plannedUpper==0&&plannedLower==0)return SampleHeight(x,z);
            var g=heightField.Grid(x,z);int ix=Mathf.FloorToInt(g.x),iz=Mathf.FloorToInt(g.y);float u=g.x-ix,v=g.y-iz;
            float a=RiverGridSample(ix,iz,plannedUpper,plannedLower),b=RiverGridSample(ix+1,iz,plannedUpper,plannedLower),
                c=RiverGridSample(ix,iz+1,plannedUpper,plannedLower),d=RiverGridSample(ix+1,iz+1,plannedUpper,plannedLower);
            return u+v<=1?a+(b-a)*u+(c-a)*v:d+(c-d)*(1-u)+(b-d)*(1-v);
        }
    }
}
