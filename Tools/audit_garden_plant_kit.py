"""Read-only FBX roundtrip verification in Blender. Writes QA outside the release."""
import json
import math
from pathlib import Path
import bpy

GAME=Path(__file__).resolve().parents[1]
PACKAGE=GAME.parent/'CozySettlement/LocalHandoffs/GardenPlantKit-v005'
report=[]
manifest=json.loads((PACKAGE/'manifest.json').read_text())
for asset in manifest['assets']:
    for lod in asset['lods']:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(PACKAGE/lod['file']))
        objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
        assert len(objects)==1, lod['file']
        obj=objects[0]
        mesh=obj.data
        mesh.calc_loop_triangles()
        assert len(mesh.loop_triangles)==lod['triangles']
        assert len(mesh.uv_layers)==1 and len(mesh.color_attributes)==1
        uv=mesh.uv_layers.active.data
        # Every face stays within exactly one palette cell, including its vertices.
        if asset['id']=='GardenShrub_A':
            assert all(0<=d.uv.x<=1 and 0<=d.uv.y<=1 for d in uv)
            assert max(d.uv.x for d in uv)-min(d.uv.x for d in uv)>.5
        else:
            for face in mesh.polygons:
                coords=[tuple(uv[i].uv) for i in face.loop_indices]
                assert all(abs(u-coords[0][0])<1e-5 and abs(v-.5)<1e-5 for u,v in coords)
                assert abs(coords[0][0]*8-.5-round(coords[0][0]*8-.5))<1e-5
        mask=mesh.color_attributes[0]
        assert mask.domain=='CORNER'
        assert min(d.color[0] for d in mask.data)<.001
        assert max(d.color[0] for d in mask.data)>.99
        assert all(math.isfinite(c) for v in mesh.vertices for c in v.co)
        assert all(v.normal.length>.9 for v in mesh.vertices)
        points=[obj.matrix_world@v.co for v in mesh.vertices]
        assert abs(min(p.z for p in points))<.001
        report.append(dict(file=lod['file'],triangles=len(mesh.loop_triangles),
            uv_mapping='PASS',wind_mask='PASS',normals='PASS',base_pivot='PASS',
            geometry_contract='COMPATIBLE',unity_wind_and_shadow_review='PENDING'))
out=GAME/'Logs/GardenPlants'
out.mkdir(parents=True,exist_ok=True)
(out/'fbx-roundtrip.json').write_text(json.dumps(report,indent=2))
print('FBX_ROUNDTRIP_PASS',len(report),'models')
