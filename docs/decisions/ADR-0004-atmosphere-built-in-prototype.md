# ADR-0004: Isolated Built-in atmosphere prototype

Date: 2026-10-26
Status: Experimental feature branch only; not approved for main/production.

## Context

Little Castle currently uses Unity 6000.5.5f1 Built-in Render Pipeline and Gamma space, own material families, WorldTimeSystem, DayNightLightingController and StylizedLightingGlobals. User wants Tiny Glade-like atmosphere without copied code, with a separate GitHub branch and modular folder.

## Decision

Develop original opt-in camera-depth height/distance fog with sunlight-scattering approximation, sparse local fog volumes, profile data and biome camera-focus blending. Respect current _LC_* contract. Keep the prototype self-contained in an isolated Git branch; add docs and editor tests. Do not touch scenes, GraphicsSettings, Packages, global shared shaders or gameplay time/fog-of-war.

A future true froxel/volumetric shadow/indirect GI implementation is explicitly tracked but not represented as delivered.

## Why not alternatives

- Switch whole game to HDRP now: enormous conversion cost for an atmosphere experiment.
- Switch to URP now: current project shader compatibility and lighting contracts need independent measured migration.
- Use only legacy RenderSettings.fog: no interesting valley or local effects.
- Copy a different game: no access to authorised proprietary renderer source and no assurance of compatibility.
- Implement real ray-traced GI/froxels without editor benchmarks: premature performance and architecture risk.

## Safety / rollback

Camera component must be explicitly attached, can be disabled without losing project lighting; main stays unmodified. Graphics effect is never authoritative fog of war. Code is unverified until compiled and profiled in the pinned editor.

Full implementation design and gate list: docs/experiments/atmosphere/IMPLEMENTATION.md.
