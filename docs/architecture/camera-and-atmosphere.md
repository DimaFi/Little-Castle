# Strategy Camera and Stylized Atmosphere

## Camera goal

Little Castle uses a ground-focused strategy camera inspired by classic RTS
controls and close settlement viewers.

The player can:
- move with WASD / arrow keys;
- move by pushing the cursor against screen edges;
- zoom with the mouse wheel;
- rotate around the ground focus with right mouse;
- rotate with Q / E;
- drag the map with middle mouse.

The camera is not free-look. Pitch is clamped to a downward-looking range, so
the player can inspect buildings closely without ever tilting upward into an
FPS-style sky view.

Current defaults:
- pitch: 34-68 degrees downward;
- closest distance: 11 units;
- farthest distance: 190 units;
- perspective FOV: 50 degrees.

The camera moves the same `TestFocus` transform used by `WorldStreamer`, so
chunk streaming follows the area the player is actually inspecting.

## Atmosphere goal

The presentation aims for a soft stylized settlement look rather than
photorealistic lighting.

The current stack deliberately stays on Unity's built-in render pipeline:
- one directional sun;
- one weak directional moon;
- Trilight ambient lighting;
- linear distance fog;
- one procedural skybox shader;
- no sky textures;
- no URP/HDRP dependency;
- no full-screen post-processing dependency.

## Procedural sky

`Assets/_Game/Shaders/Sky/StylizedDayNightSky.shader` draws:
- zenith / horizon / lower-sky gradients;
- sun disc and glow;
- moon disc and glow;
- procedural stars;
- subtle procedural high clouds.

`DayNightLightingController` synchronizes shader sun/moon directions with the
real directional lights and changes the palette through day, twilight and
night.

Night is intentionally readable. Direct sunlight disappears, but moonlight and
ambient light preserve terrain/building readability.

## Performance rule

Atmosphere should remain cheap enough that world objects, animation, simulation
and large settlements dominate the frame budget.

Do not add a heavy render pipeline solely to improve the sky. Prefer:
1. cheap shader work;
2. baked/material detail;
3. carefully budgeted local lights;
4. post-processing only when it provides a measured visual benefit.

## Future visual passes

Safe next improvements without changing architecture:
- emissive windows and lantern activation after dusk;
- soft material response tuning for the stylized terrain/buildings;
- optional very low-cost color grading;
- weather-specific sky profiles;
- cloud coverage controlled by weather;
- subtle moon phase presentation;
- camera focus-on-selected-building / unit;
- collision-aware camera distance around tall structures.
