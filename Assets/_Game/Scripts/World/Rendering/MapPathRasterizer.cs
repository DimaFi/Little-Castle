using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>RGBA weight slices, four catalog entries per slice. IDs are resolved only in this derived cache.</summary>
    public static class MapPathRasterizer
    {
        public static int SliceCount(int count)=>(count+3)/4;
        public static void Rebuild(byte[][] weights,Vector4 bounds,int width,int height,
            List<SurfacePaintStroke> strokes,MapPathCatalog catalog,RectInt dirty)
        {
            int count=catalog.Count;
            foreach(var slice in weights)for(int y=dirty.yMin;y<dirty.yMax;y++)
                Array.Clear(slice,(y*width+dirty.xMin)*4,dirty.width*4);
            // Union a complete stroke before compositing: extra dabs must not harden its soft edge.
            var coverage=new float[dirty.width*dirty.height];
            foreach(var stroke in strokes)
            {
                if(stroke.erase||stroke.points.Count==0)continue;
                Array.Clear(coverage,0,coverage.Length);
                RectInt affected=default;
                for(int p=0;p<stroke.points.Count;p++)
                {
                    var a=stroke.points[Mathf.Max(0,p-1)];var b=stroke.points[p];float r=stroke.radius;
                    var area=Rect.MinMaxRect(Mathf.Min(a.x,b.x)-r,Mathf.Min(a.y,b.y)-r,Mathf.Max(a.x,b.x)+r,Mathf.Max(a.y,b.y)+r);
                    var pixels=SurfacePaintRasterizer.PixelRect(bounds,width,height,area);
                    int x0=Mathf.Max(pixels.xMin,dirty.xMin),x1=Mathf.Min(pixels.xMax,dirty.xMax);
                    int y0=Mathf.Max(pixels.yMin,dirty.yMin),y1=Mathf.Min(pixels.yMax,dirty.yMax);
                    if(x1<=x0||y1<=y0)continue;
                    if(affected.width==0)affected=new RectInt(x0,y0,x1-x0,y1-y0);
                    else affected=new RectInt(Mathf.Min(affected.xMin,x0),Mathf.Min(affected.yMin,y0),
                        Mathf.Max(affected.xMax,x1)-Mathf.Min(affected.xMin,x0),Mathf.Max(affected.yMax,y1)-Mathf.Min(affected.yMin,y0));
                    for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
                    {
                        var point=new Vector2(bounds.x+(x+.5f)*bounds.z/width,bounds.y+(y+.5f)*bounds.w/height);
                        float d=ChunkTerrainVertexMasks.DistanceToSegment(point,a,b,out _);
                        int k=(y-dirty.y)*dirty.width+x-dirty.x;
                        coverage[k]=Mathf.Max(coverage[k],MapBrushFalloff.SurfaceWeight(stroke,d));
                    }
                }
                int selected=catalog.IndexOf(stroke.pathMaterialId);
                for(int y=affected.yMin;y<affected.yMax;y++)for(int x=affected.xMin;x<affected.xMax;x++)
                {
                    float amount=coverage[(y-dirty.y)*dirty.width+x-dirty.x];if(amount<=0)continue;
                    int pixel=(y*width+x)*4;
                    for(int layer=0;layer<count;layer++)
                    {
                        var slice=weights[layer/4];int k=pixel+layer%4;
                        float old=slice[k]/255f;
                        float value=stroke.road&&layer==selected?Mathf.Lerp(old,1,amount):old*(1-amount);
                        slice[k]=(byte)Mathf.RoundToInt(value*255);
                    }
                }
            }
        }
    }

    /// <summary>View-owned derived weights. The caller chooses full preview bounds or a resident chunk with gutters.</summary>
    public sealed class MapPathWeightTexture : IDisposable
    {
        public readonly Texture2DArray texture;
        public readonly MapPathCatalog catalog;
        readonly byte[][] weights;
        readonly Vector4 bounds;
        readonly int width,height;
        public MapPathWeightTexture(MapPathCatalog catalog,Vector4 bounds,int width,int height)
        {
            this.catalog=catalog;this.bounds=bounds;this.width=width;this.height=height;
            int slices=MapPathRasterizer.SliceCount(catalog.Count);weights=new byte[slices][];
            for(int i=0;i<slices;i++)weights[i]=new byte[width*height*4];
            texture=new Texture2DArray(width,height,slices,TextureFormat.RGBA32,false,true)
            {name="Derived path material weights",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        }
        public void Rebuild(MapEditSession session,RectInt dirty)
        {
            if(dirty.width==0||dirty.height==0)return;
            var area=new Rect(bounds.x+dirty.x*bounds.z/width,bounds.y+dirty.y*bounds.w/height,dirty.width*bounds.z/width,dirty.height*bounds.w/height);
            MapPathRasterizer.Rebuild(weights,bounds,width,height,session.QuerySurface(area),catalog,dirty);
            for(int i=0;i<weights.Length;i++)texture.SetPixelData(weights[i],0,i);
            texture.Apply(false,false);
        }
        public void Bind(MaterialPropertyBlock properties)
        {
            catalog.Bind(properties);properties.SetTexture("_LC_PathWeights",texture);
            properties.SetVector("_LC_PathBounds",new Vector4(bounds.x,bounds.y,1/bounds.z,1/bounds.w));
        }
        public void Dispose(){if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}
    }
}
