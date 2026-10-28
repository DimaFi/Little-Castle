# Bridge Stone A v002

Source candidate for a fixed medieval stone crossing. The bridge is authored in
Blender Z-up and exported as metre-scale, Y-up FBX. It is a source-art version;
Unity consumes only a separately reviewed release package.

## Fixed world contract

- Road axis: local `+Z`; river axis: local `+X`.
- Bridge length: `10.8 m`; clear path: `2.86 m`.
- Structural width: `3.6 m`; cap extent: `3.78 m`.
- Crown: `Y=1.05`; water: `Y=-0.85`.
- Road sockets: `(0,0,-5.4)` and `(0,0,+5.4)`.
- Terrain reference/support rectangle: half extents `X=7`, `Z=8`.
- Right-hand entrance banners: `(1.64,-5.12)` and `(-1.64,+5.12)`.

`bridge_contract.py` and `bridge-site.json` are the machine-readable fixed-site
profile. The rectangular terrain mesh is an alignment reference and must be
replaced by generated world terrain in production.

## Source and exports

- `Bridge_Stone_A.blend`: editable source with packed file textures.
- `build_bridge.py`: deterministic geometry, texture, FBX and preview build.
- `validate_bridge.py`: independent FBX round-trip, bounds, normals, UV, LOD,
  arch, walkway, material and flag-anchor checks.
- `Meshes/SM_Bridge_Stone_A_LOD0..2.fbx`: structural bridge, rails and banners.
- `Meshes/SM_Bridge_Dressing_A_LOD0..2.fbx`: optional rocks and vegetation.
- `Meshes/SM_Bridge_TerrainReference_LOD0..2.fbx`: fixed-site terrain reference.
- `Meshes/SM_Bridge_Water_A.fbx`: continuous water surface and sparse accents.
- `Meshes/COL_Bridge_A.fbx`: simplified deck and rail blockers.
- `Meshes/Bridge_Sockets.fbx`: named road and river sockets.

The structure uses one limestone atlas material, one palette material for timber,
mortar, trim and small botany, and one faction-cloth material. The two banner cloth
meshes are intended for runtime faction tinting. Water, terrain and grass each use
one separate source texture/material.

## Rebuild and validation

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe' --background --factory-startup -t 8 --python-exit-code 1 -P .\build_bridge.py
& 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe' --background --factory-startup --python-exit-code 1 -P .\validate_bridge.py
```

The build writes only inside this version directory. Do not publish these files to
Unity directly; create and review a versioned `Releases/Bridge_Stone_A/v002`
package first.

See `QA/roundtrip_report.json` for measured FBX results and
`QA/visual_review.md` for the honest Cycles/Eevee visual review and limitations.
