# Benchmarks — what established games actually use

Evidence checked 2026-10-23. **Do not infer game-specific renderer from Unity logo alone.** Art observations are qualitative, not reverse-engineered studio settings.

## Verified pipeline and takeaways

| Game / sample | Proven technology | Useful for Little Castle | Do not over-generalize |
| --- | --- | --- | --- |
| **Lost in Random** (Thunderful/Unity) | **URP**; started in HDRP, shifted for broad platforms; used Shader Graph, selective ambient occlusion/decals and bespoke lighting tricks | Art-directed silhouette, storytelling and quality without mandatory HDRP | Their asset, lighting, commercial plugins and styles aren't freely reusable |
| **Fantasy Kingdom Unity 6 demo** | Original HDRP demo; **URP version** for wider devices; APVs, GPU Resident Drawer, GPU occlusion, SpeedTree/VFX Graph examples | Analyze Unity project's URP architecture, material/lighting and profiling approach | Demo content has **non-commercial license** and third-party Synty assets; reference only until licensed appropriately |
| **V Rising** (Stunlock/Unity) | **HDRP**; volumetric fog, DOTS streaming and batched scene assets | A strong reference for distant atmosphere, strong silhouettes, costly effects budgeted against large-world perf | We do *not* need to move to HDRP or implement their fog stack |
| **Slime Rancher 2** (Monomi/Unity) | **HDRP** with customized shaders and rendering | Shows convincing vivid stylization requires deliberate shader/art work beyond default pipeline | HDRP is not proof URP cannot achieve attractive stylized visuals |
| **Tiny Glade** (Pounce Light) | **NOT a Unity URP/HDRP example**; Rust/Bevy is reported by public community modding docs; no authoritative basis here for its exact internal render settings | Main *visual* target: cozy shapes, tactile surface response, convincing grass/path/stone integration, scale/lighting | Do not claim Tiny Glade uses URP or copy code, meshes, screenshots/textures |

## Direct links and specific verified statements

- Official Unity render-pipeline strategy 2026: https://unity.com/topics/render-pipelines-strategy-for-2026 (URP priority; Built-in deprecation starts in Unity 6.5, removal timing unspecified).
- Unity 6000.5 URP: https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/universal-render-pipeline
- Unity 6000.5 Feature Comparison: https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/choose-a-render-pipeline/feature-comparison
- Unity Lost in Random study: https://unity.com/resources/case-study-lost-in-random
- Unity Fantasy Kingdom URP demo: https://unity.com/demos/fantasy-kingdom (explicit non-commercial demo-license warning).
- Unity V Rising study: https://unity.com/vn/resources/stunlock-studios-v-rising
- Unity Slime Rancher 2 study: https://unity.com/resources/slime-rancher-2
- Tiny Glade community modding docs (secondary, not engine vendor): https://hbeau.github.io/tiny-docs/
- Unity 6000.5 GPU Resident Drawer prerequisites: https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer
- Unity 6000.5 Render Graph docs: https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/universal-render-pipeline/customizing-urp/render-graph
- Unity AA limitations: https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/universal-render-pipeline/anti-aliasing
- Unity custom shader migration: https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/introduction/installing-and-configuring-urp/upgrading-from-birp/upgrade-shaders/birp-urp-custom-shader-upgrade-guide

## What to learn rather than copy

- **Lost in Random:** manually art-direct spatial hierarchy, choose selective shadow/occlusion and stylized materials; the migration to URP can be visually convincing if designed, not checkbox-driven.
- **Fantasy Kingdom:** perform disciplined hardware/pipeline benchmarking, use APV only where it fits baked/static content, use GPU-driven rendering only if compatible and profiled.
- **V Rising:** reserve custom effects for clear atmospheric and gameplay function; strategically manage dense-world object/light costs; don't assume big map can simply run cinematic effects everywhere.
- **Slime Rancher 2:** stylization still depends on source art, material tuning and intentional custom effects; default PBR not sufficient.
- **Tiny Glade:** focus on silhouette rhythm, crafted roof/wall/stone joints, organic contact/debris and quiet lighting; build our own original implementation.

## Sources of uncertainty

No shipped executable was reverse engineered here; most studios do not publish exact shader features, fog implementation, AA mode, draw budgets or LOD ratios. Do not advertise any such as established facts. Verify real Little Castle use with standalone benchmarks.
