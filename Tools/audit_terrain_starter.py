"""Read-only source audit. Run with Blender --background --python this_file.

Writes a preparation manifest beside the handoff, never saves source blends.
This is not an asset_sync import manifest or Unity production approval.
"""
import hashlib
import json
from pathlib import Path
import struct
import bpy

GAME = Path(__file__).resolve().parents[1]
ROOT = GAME.parent
ART = ROOT / 'CozySettlement'
WALLS = ROOT / 'Little-Castle_Assets'


def file_record(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=path.stat().st_size,
                sha256=digest.hexdigest())


terrain = []
for path in sorted((ART / 'Mat/Material_terrain').rglob('*.png')):
    record = file_record(path)
    with path.open('rb') as stream:
        header = stream.read(24)
    assert header[:8] == b'\x89PNG\r\n\x1a\n', path
    record['width'], record['height'] = struct.unpack('>II', header[16:24])
    record['family'], record['channel'] = path.stem.rsplit('_', 1)
    record['first_pass'] = path.stem in ('Grass_Base_A_BaseColor', 'DirtPath_A_BaseColor')
    terrain.append(record)

selected = []
for base, catalog, prefixes in (
    (ART, 'AssetsDatabase/AssetBook.json', ('Art/Vegetation/Oak_Kit/', 'Art/House_Cottage_A/')),
    (WALLS, 'AssetBook/AssetBook.json', ('Source/Architecture/Wall_Stone_Modular/v004/',)),
):
    for asset in json.loads((base / catalog).read_text(encoding='utf-8-sig'))['assets']:
        if not any(asset.get('path', '').startswith(p) for p in prefixes):
            continue
        source = base / asset['path'] / asset.get('source_file', '')
        if source.suffix.lower() not in ('.fbx', '.blend'):
            continue
        assert source.is_file(), source
        selected.append(dict(id=asset['id'], catalog_status=asset['status'],
                             source=file_record(source), unity_verified=False))

house_root = ART / 'Art/House_Cottage_A'
current = json.loads((house_root / 'CURRENT.json').read_text())
house = {key: file_record(house_root / current[key])
         for key in ('assembly', 'house', 'yard_kit')}
house['yard_module_overrides'] = {
    key: file_record(house_root / value)
    for key, value in current['yard_module_overrides'].items()
}

oak = []
for name in ('SM_Tree_Oak_A.fbx', 'SM_Tree_Oak_A_LOD1.fbx', 'SM_Tree_Oak_A_LOD2.fbx'):
    path = ART / 'Art/Vegetation/Oak_Kit/Exports' / name
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    meshes = []
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH':
            continue
        mesh = obj.data
        mesh.calc_loop_triangles()
        tangents_ok = False
        if mesh.uv_layers:
            try:
                mesh.calc_tangents()
                tangents_ok = True
            except RuntimeError:
                pass
        meshes.append(dict(name=obj.name, vertices=len(mesh.vertices),
                           triangles=len(mesh.loop_triangles),
                           uv_layers=[u.name for u in mesh.uv_layers],
                           color_attributes=[a.name for a in mesh.color_attributes],
                           materials=[m.name if m else None for m in mesh.materials],
                           triangles_per_material={str(i): sum(t.material_index == i for t in mesh.loop_triangles)
                                                   for i in range(len(mesh.materials))},
                           normals_nonzero=all(v.normal.length > 0.5 for v in mesh.vertices),
                           tangents_calculable=tangents_ok,
                           dimensions_m=list(obj.dimensions)))
    assert meshes, path
    oak.append(dict(source=file_record(path), meshes=meshes))

result = dict(schema_version=1, status='preparation_only_not_unity_import',
              terrain_textures=terrain, selected_catalog_models=selected,
              house_current=house, oak_fbx_inspection=oak,
              excluded_families=['Art/Cottage', 'Art/Vegetation/Tree_01', 'Art/Vegetation/Tree02'],
              restrictions=['No manual terraforming', 'Prefer naturally buildable plains',
                            'No ordinary building on mountains', '60 FPS target requires profiling'])
target = GAME / 'docs/handoffs/terrain-starter-inventory.json'
target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(dict(output=str(target), terrain_files=len(terrain),
                      terrain_families=len({t['family'] for t in terrain}),
                      selected_models=len(selected), oak=oak), ensure_ascii=False))
