using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// At most two shared Point Lights for visible clusters, admitted only through
    /// NightLightBudgetManager. They never reserve slots ahead of authored lanterns.
    /// No shadows and no pooled emitter per individual insect.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(FireflyWorldRenderer))]
    public sealed class FireflyClusterLightPool : MonoBehaviour
    {
        [SerializeField] bool enableRealtimeLighting = true;
        [SerializeField, Range(0,2)] int mediumLights = 1;
        [SerializeField, Range(0,2)] int highLights = 2;
        [SerializeField, Range(0,4)] float intensity = .42f;
        [SerializeField, Range(1,16)] float radius = 5f;
        [SerializeField, Range(.1f,3)] float updateInterval = .65f;
        [SerializeField, Range(.1f,5)] float repositionSeconds = 1.2f;
        [SerializeField] int selectionPriority = -100;

        readonly Vector3[] target=new Vector3[2];
        readonly Vector3[] destination=new Vector3[2];
        readonly NightLightEmitter[] emitters=new NightLightEmitter[2];
        readonly Light[] lights=new Light[2];
        FireflyWorldRenderer rendererSource;
        MapEditSession session;
        NightLightBudgetManager manager;
        int wanted;
        float nextUpdate,nextManagerSearch;

        public void Connect(MapEditSession map)
        {
            session=map;
            rendererSource=GetComponent<FireflyWorldRenderer>();
        }

        void LateUpdate()
        {
            if(!Application.isPlaying || session==null)return;
            if(!enableRealtimeLighting){SetCount(0);return;}
            if(!rendererSource)rendererSource=GetComponent<FireflyWorldRenderer>();
            if(!manager)
            {
                // Missing optional scene manager must not cause a scene-wide search each frame.
                if(Time.unscaledTime<nextManagerSearch)return;
                nextManagerSearch=Time.unscaledTime+2f;
                manager=FindAnyObjectByType<NightLightBudgetManager>();
                if(!manager)return; // No unbudgeted lights are allowed.
            }
            var settings=session.FirefliesForRendering;
            if(settings==null||!settings.enabled){SetCount(0);return;}
            var preset=rendererSource.quality.preset;
            if(preset==FireflyQuality.Off){SetCount(0);return;}
            int capacity=preset==FireflyQuality.High?highLights:
                preset==FireflyQuality.Medium?mediumLights:
                preset==FireflyQuality.Custom?highLights:0;
            if(Time.unscaledTime>=nextUpdate)
            {
                nextUpdate=Time.unscaledTime+Mathf.Max(.1f,updateInterval);
                var camera=Camera.main;
                int count=camera&&capacity>0?rendererSource.CopyClusterTargets(camera.transform.position,target):0;
                wanted=Mathf.Min(capacity,count);
                for(int i=0;i<wanted;i++)
                {
                    destination[i]=target[i]+Vector3.up*1.5f;
                    if(lights[i]==null)CreateSlot(i,settings.color);
                    var light=lights[i];
                    light.color=settings.color;
                    light.range=radius;
                    if(emitters[i]!=null)emitters[i].enabled=true;
                }
                SetCount(wanted);
            }
            float alpha=1-Mathf.Exp(-Time.unscaledDeltaTime/Mathf.Max(.1f,repositionSeconds));
            for(int i=0;i<wanted;i++)if(lights[i])
                lights[i].transform.position=Vector3.Lerp(lights[i].transform.position,destination[i],alpha);
        }

        void CreateSlot(int index,Color color)
        {
            var go=new GameObject("Firefly cluster light (shared budget)");
            go.transform.SetParent(transform,false);
            go.transform.position=destination[index];
            var light=go.AddComponent<Light>();
            light.type=LightType.Point;
            light.shadows=LightShadows.None;
            light.range=radius;light.intensity=0;light.color=color;
            light.enabled=false;
            var emitter=go.AddComponent<NightLightEmitter>();
            emitter.ConfigureFireflyCluster(light,intensity,140,Mathf.Min(-1,selectionPriority));
            lights[index]=light;emitters[index]=emitter;
        }

        void SetCount(int count)
        {
            wanted=count;
            for(int i=0;i<2;i++)if(emitters[i])
            {
                bool active=i<count;
                if(emitters[i].enabled!=active)emitters[i].enabled=active;
            }
        }

        void OnDisable()
        {
            SetCount(0);
            for(int i=0;i<2;i++)
            {
                if(lights[i])
                {
                    if(Application.isPlaying)Destroy(lights[i].gameObject);
                    else DestroyImmediate(lights[i].gameObject);
                }
                lights[i]=null;emitters[i]=null;
            }
            manager=null;
        }
    }
}
