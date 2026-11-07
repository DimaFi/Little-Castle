namespace LittleCastle.World
{
    /// <summary>Freehand developer brush; release commits one undoable fence recipe.</summary>
    public sealed class MapFenceTool : MapStrokeTool
    {
        public override string Id=>"fence.draw";
        public override string Title=>"Нарисовать забор";
        public override string Category=>"Заборы";
        public override MapToolControls Controls=>MapToolControls.None;
        public override void Begin(MapToolContext c,MapToolPointer p)=>c.session.BeginFenceStroke(c.fenceVariantId,c.fenceSpacing,c.fenceSmoothing,c.fenceScale);
        public override void Apply(MapToolContext c,MapToolPointer p,float seconds)=>c.session.AppendFencePoint(p.position);
    }
}
