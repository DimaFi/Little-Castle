using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>Derived resident water-flow coordinates; bank geometry is intentionally separate.</summary>
    public static class RiverBankMeshBuilder
    {
        public static Texture2D FlowMap(MapRiverPath path,Rect area,out Vector4 shaderBounds)
        {
            const float step=.5f;float x0=Mathf.Floor(area.xMin/step)*step,z0=Mathf.Floor(area.yMin/step)*step;
            int width=Mathf.CeilToInt((area.xMax-x0)/step)+2,height=Mathf.CeilToInt((area.yMax-z0)/step)+2;
            var pixels=new Color[width*height];
            for(int z=0;z<height;z++)for(int x=0;x<width;x++)
            {var p=new Vector2(x0+x*step,z0+z*step);if(path.Nearest(p,out var f,out var signed))pixels[z*width+x]=new Color(signed,path.TravelTime(f.station)+Vector2.Dot(p-f.center,f.tangent)/f.speed,f.speed,1);else pixels[z*width+x]=Color.clear;}
            var texture=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true){name="Derived resident river flow coordinates",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(pixels);texture.Apply(false,true);shaderBounds=new Vector4(x0-step*.5f,z0-step*.5f,1/(width*step),1/(height*step));return texture;
        }
    }
}
