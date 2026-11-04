using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering.ChimneySmoke
{
    /// <summary>One visual update loop, shared URP material and global wind; no scene or world writes.</summary>
    [DefaultExecutionOrder(250)]
    public sealed class ChimneySmokeManager : MonoBehaviour
    {
        private static ChimneySmokeManager instance;
        private readonly List<ChimneySmokeEmitter> emitters = new List<ChimneySmokeEmitter>(32);
        private Material material;
        private float nextTick;
        private int quality;
        private static readonly int WindId = Shader.PropertyToID("_LC_Wind");

        public static void Register(ChimneySmokeEmitter emitter)
        {
            if (!Application.isPlaying || !emitter) return;
            if (!instance)
            {
                var go = new GameObject("LC Chimney Smoke Manager (optional)");
                instance = go.AddComponent<ChimneySmokeManager>();
            }
            if (!instance.emitters.Contains(emitter)) instance.emitters.Add(emitter);
        }

        public static void Unregister(ChimneySmokeEmitter emitter)
        {
            if (!instance) return;
            instance.emitters.Remove(emitter);
            if (instance.emitters.Count == 0) Destroy(instance.gameObject);
        }

        private void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            var shader = Shader.Find("Little Castle/Effects/Chimney Smoke Ribbon");
            if (shader) material = new Material(shader) { name = "LC smoke shared (runtime)", hideFlags = HideFlags.DontSave };
            else Debug.LogWarning("Chimney smoke disabled: missing URP smoke shader.");
        }

        private void Update()
        {
            if (Time.unscaledTime < nextTick) return;
            Camera view = Camera.main;
            if (!view || !material) return;
            quality = QualitySettings.GetQualityLevel();
            float rate = 30f;
            for (int i = emitters.Count - 1; i >= 0; i--)
            {
                var emitter = emitters[i];
                if (!emitter) { emitters.RemoveAt(i); continue; }
                if (emitter.Profile) rate = Mathf.Min(rate, Mathf.Max(5f, emitter.Profile.targetUpdatesPerSecond));
            }
            nextTick = Time.unscaledTime + 1f / rate;
            Vector4 wind = Shader.GetGlobalVector(WindId);
            for (int i = 0; i < emitters.Count; i++)
                if (emitters[i]) emitters[i].Render(view, Time.time, wind, quality, material);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            for (int i = 0; i < emitters.Count; i++)
                if (emitters[i]) emitters[i].Hide();
            emitters.Clear();
            if (material)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }
    }
}
