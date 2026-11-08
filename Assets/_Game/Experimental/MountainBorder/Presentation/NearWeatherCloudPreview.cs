using LittleCastle.Research;
using LittleCastle.World;
using LittleCastle.World.Weather;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>Optional high cloud banks above the valley in wet weather.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class NearWeatherCloudPreview : MonoBehaviour
    {
        static readonly int Weather = Shader.PropertyToID("_Weather");
        static readonly int Seed = Shader.PropertyToID("_Seed");
        static readonly int Daylight = Shader.PropertyToID("_Daylight");
        static readonly int Quality = Shader.PropertyToID("_CloudQuality");

        readonly MeshRenderer[] volumes = new MeshRenderer[4];
        GameObject root;
        Mesh boundsMesh;
        Material material;
        MaterialPropertyBlock properties;
        DayNightLightingController atmosphere;
        MeadowSurfacePainter painter;
        Vector2 weather;

        public void Connect(MeadowSurfacePainter map, DayNightLightingController lighting)
        {
            painter = map;
            atmosphere = lighting;
        }

        void Start()
        {
            if (!atmosphere) atmosphere = FindAnyObjectByType<DayNightLightingController>();
            if (!painter) painter = FindAnyObjectByType<MeadowSurfacePainter>();
            var shader = Resources.Load<Shader>("MapAuthoring/LC_NearWeatherCloud");
            if (!atmosphere || !painter || painter.Session == null || !shader || !shader.isSupported)
                return;

            var session = painter.Session;
            Vector4 bounds = session.Bounds;
            // Banks sit outside the camera's horizontal orbit. Keeping them lower there
            // lets a downward-looking strategy camera see them above the horizon.
            float baseAltitude = Mathf.Max(96f, session.MaximumHeight + 64f);
            float clipScale = painter.studyCamera ? Mathf.Max(1f, painter.studyCamera.farClipPlane / 880f) : 1f;
            float bankScale = Mathf.Clamp(Mathf.Max(bounds.z, bounds.w) / 200f, 1f, clipScale);
            Vector3 center = new Vector3(bounds.x + bounds.z * .5f, baseAltitude, bounds.y + bounds.w * .5f);
            root = new GameObject("High rain banks (preview)") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            boundsMesh = CreateBoundsMesh();
            properties = new MaterialPropertyBlock();

            // Broad banks leave open sky between them and sit beyond the playable valley.
            // Lead the north bank toward the flank of the ridgeline so the high
            // central peaks do not hide the whole cloud in the low camera view.
            AddVolume(0, center + new Vector3(-155, 6, 375) * bankScale, new Vector3(310, 96, 150) * bankScale, -9);
            AddVolume(1, center + new Vector3(635, 51, 155) * bankScale, new Vector3(435, 94, 150) * bankScale, 78);
            AddVolume(2, center + new Vector3(-622, 41, -143) * bankScale, new Vector3(465, 76, 188) * bankScale, -67);
            AddVolume(3, center + new Vector3(95, 56, -620) * bankScale, new Vector3(408, 103, 175) * bankScale, 21);
        }

        void AddVolume(int index, Vector3 center, Vector3 size, float yaw)
        {
            var volume = new GameObject($"Rain cloud {index + 1}");
            volume.transform.SetParent(root.transform, false);
            volume.transform.SetPositionAndRotation(center, Quaternion.Euler(0, yaw, 0));
            volume.transform.localScale = size;
            volume.AddComponent<MeshFilter>().sharedMesh = boundsMesh;
            var renderer = volume.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            volumes[index] = renderer;
            RainStormController.RegisterLightningCloud(renderer);
        }

        void LateUpdate()
        {
            if (!root || !atmosphere) return;
            Vector2 target = new Vector2(
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.10f, .66f, atmosphere.RequestedPrecipitation)),
                atmosphere.RequestedStormStrength);
            weather = Vector2.Lerp(weather, target,
                1f - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, .1f) / 2.5f));

            Material sky = RenderSettings.skybox;
            float daylight = sky && sky.HasProperty(Daylight) ? sky.GetFloat(Daylight) : 1f;
            float quality = sky && sky.HasProperty(Quality) ? sky.GetFloat(Quality) : 1f;
            for (int i = 0; i < volumes.Length; i++)
            {
                var renderer = volumes[i];
                renderer.enabled = Mathf.Max(weather.x, weather.y) > .008f;
                if (!renderer.enabled) continue;
                properties.SetVector(Weather, new Vector4(weather.x, weather.y, daylight, quality));
                properties.SetFloat(Seed, i * 3.71f + .3f);
                renderer.SetPropertyBlock(properties);
            }
        }

        static Mesh CreateBoundsMesh()
        {
            const float h = .5f;
            var mesh = new Mesh { name = "Cloud volume bounds", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-h,-h,-h), new Vector3(h,-h,-h),
                new Vector3(h,h,-h), new Vector3(-h,h,-h),
                new Vector3(-h,-h,h), new Vector3(h,-h,h),
                new Vector3(h,h,h), new Vector3(-h,h,h)
            };
            mesh.triangles = new[]
            {
                0,2,1, 0,3,2, 4,5,6, 4,6,7,
                0,4,7, 0,7,3, 1,2,6, 1,6,5,
                0,1,5, 0,5,4, 3,7,6, 3,6,2
            };
            return mesh;
        }

        void OnDestroy()
        {
            foreach (var volume in volumes) RainStormController.UnregisterLightningCloud(volume);
            if (root) Destroy(root);
            if (boundsMesh) Destroy(boundsMesh);
            if (material) Destroy(material);
        }
    }
}
