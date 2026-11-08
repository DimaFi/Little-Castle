using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>Disposable binding for the existing finite-map authoring view; never edits material assets.</summary>
    public sealed class MapPathPresentation : IDisposable
    {
        readonly MapEditSession session;
        readonly MapPathWeightTexture weights;
        readonly int resolution;
        readonly Dictionary<Renderer,MaterialPropertyBlock> originals=new Dictionary<Renderer,MaterialPropertyBlock>();
        readonly MapPathReliefPresentation relief;
        public MapPathPresentation(MapEditSession session,Material terrain,int resolution=512)
        {
            this.session=session;this.resolution=resolution;
            var catalog=MapPathCatalog.Default;
            if(!catalog||catalog.Count<2||!SystemInfo.supports2DArrayTextures)return;
            weights=new MapPathWeightTexture(catalog,session.Bounds,resolution,resolution);
            weights.Rebuild(session,new RectInt(0,0,resolution,resolution));
            // Terrain view chunks use HideAndDontSave. FindObjectsByType omits
            // them, so binding only that search updates the disabled source mesh.
            MaterialPropertyBlock reliefProperties=null;
            foreach(var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if(!renderer.gameObject.scene.IsValid()||!renderer.gameObject.scene.isLoaded||renderer.sharedMaterial!=terrain)continue;
                var old=new MaterialPropertyBlock();renderer.GetPropertyBlock(old);originals.Add(renderer,old);
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);weights.Bind(block);renderer.SetPropertyBlock(block);
                reliefProperties=block;
            }
            if(reliefProperties!=null)relief=new MapPathReliefPresentation(session,catalog,terrain,reliefProperties);
            session.Changed+=Changed;
        }
        void Changed(MapRegionChange change)
        {
            if((change.kind&MapChangeKind.Surface)==0)return;
            var dirty=SurfacePaintRasterizer.PixelRect(session.Bounds,resolution,resolution,change.bounds);
            weights.Rebuild(session,dirty);
        }
        internal bool TrySampleHeight(float x,float z,out float height)
        {
            height=0;
            return relief!=null&&relief.TrySampleHeight(x,z,out height);
        }
        internal bool TryFindPathPoint(Rect area,Vector2 preferred,out Vector2 point)
        {
            point=default;
            return weights!=null&&weights.TryFindPathPoint(area,preferred,out point);
        }
        public void Dispose()
        {
            session.Changed-=Changed;
            relief?.Dispose();
            foreach(var pair in originals)if(pair.Key)pair.Key.SetPropertyBlock(pair.Value);
            originals.Clear();weights?.Dispose();
        }
    }
}
