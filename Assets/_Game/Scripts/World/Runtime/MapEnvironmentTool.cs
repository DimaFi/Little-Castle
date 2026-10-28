using UnityEngine;

namespace LittleCastle.World
{
    public enum MapEnvironmentBrushMode { Add, Erase, Smooth }

    /// <summary>Environment data uses the shared pointer, spacing and single-stroke transaction.</summary>
    public sealed class MapEnvironmentTool : MapStrokeTool
    {
        readonly bool snow;
        public MapEnvironmentTool(bool snow){this.snow=snow;}
        public override string Id=>snow?"environment.snow":"environment.biome";
        public override string Title=>snow?"Снег":"Биом";
        public override string Category=>"Биомы и снег";
        public override bool RepeatAtRest=>true;
        public override void Begin(MapToolContext c,MapToolPointer p)=>c.session.BeginEnvironmentStroke(
            c.environmentThemeId,snow,c.environmentMode==MapEnvironmentBrushMode.Erase,
            !snow&&c.environmentMode==MapEnvironmentBrushMode.Smooth);
        public override void Apply(MapToolContext c,MapToolPointer p,float seconds)=>c.session.ApplyEnvironmentBrush(
            p.position,c.settings.radius,c.settings.strength,c.settings.softness);
    }
}
