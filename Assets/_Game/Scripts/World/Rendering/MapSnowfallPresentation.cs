using System;
using LittleCastle.World;
using LittleCastle.World.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>One bounded GPU billboard mesh; flakes are local appearance, never world entities.</summary>
    public sealed class MapSnowfallPresentation : IDisposable
    {
        readonly MapEditSession session;
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material material;
        readonly MeshRenderer renderer;
        public MapSnowfallPresentation(MapEditSession session)
        {
            this.session=session;
            var template=Resources.Load<Material>("MapAuthoring/LocalSnowfall");
            if(!template)return;
            root=new GameObject("Local biome snowfall"){hideFlags=HideFlags.HideAndDontSave};
            mesh=new Mesh{name="512 local snow billboards",hideFlags=HideFlags.HideAndDontSave};
            const int count=512;var vertices=new Vector3[count*4];var uv=new Vector2[vertices.Length];var seeds=new Vector2[vertices.Length];var indices=new int[count*6];
            uint random=741103;
            for(int i=0;i<count;i++){
                random=unchecked(random*1664525+1013904223);float x=(random&65535)/65535f;
                random=unchecked(random*1664525+1013904223);float z=(random&65535)/65535f;
                var seed=new Vector2((i*.618033989f)%1,(i*.414213562f)%1);
                for(int j=0;j<4;j++){vertices[i*4+j]=new Vector3((x-.5f)*44,-2,(z-.5f)*44);seeds[i*4+j]=seed;}
                uv[i*4]=Vector2.zero;uv[i*4+1]=Vector2.right;uv[i*4+2]=Vector2.one;uv[i*4+3]=Vector2.up;
                int at=i*6,v=i*4;indices[at]=v;indices[at+1]=v+2;indices[at+2]=v+1;indices[at+3]=v;indices[at+4]=v+3;indices[at+5]=v+2;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.uv2=seeds;mesh.triangles=indices;mesh.bounds=new Bounds(new Vector3(0,9,0),new Vector3(54,28,54));
            root.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=root.AddComponent<MeshRenderer>();
            material=new Material(template){hideFlags=HideFlags.HideAndDontSave};renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.enabled=false;
        }
        public void Tick(Vector3 groundFocus)
        {
            if(!root)return;
            var settings=session.EnvironmentSettings;
            float intensity=settings.simulateWeather?settings.snowfallIntensity*session.EnvironmentThemes.Sample(EnvironmentThemeIds.Winter,groundFocus.x,groundFocus.z):0;
            renderer.enabled=intensity>.001f;if(!renderer.enabled)return;
            root.transform.position=groundFocus;material.SetColor("_Color",new Color(.86f,.93f,1,.65f*intensity));
        }
        public void Dispose(){Destroy(root);Destroy(mesh);Destroy(material);}
        static void Destroy(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
