"""Blender source -> versioned local scene-test package. Never saves source blends.
Run --background --factory-startup --python ... -- --asset house|well|barrel|crate|oak.
"""
import argparse
import json
import hashlib
from pathlib import Path
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'CozySettlement'
OUT = ART / 'LocalHandoffs/TerrainStarter-v001'


def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def export(objects, path):
    select(objects)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
        object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, add_leaf_bones=False, path_mode='STRIP')


def bake_setup(image, objects):
    # Preserve original UV input even after the atlas becomes active.
    for obj in objects:
        original_uv = obj.data.uv_layers.active.name if obj.data.uv_layers else ''
        for slot in obj.material_slots:
            if not slot.material:
                continue
            mat = slot.material
            for node in list(mat.node_tree.nodes):
                if node.type == 'TEX_IMAGE' and not node.inputs['Vector'].is_linked:
                    uv = mat.node_tree.nodes.new('ShaderNodeUVMap')
                    uv.uv_map = original_uv
                    mat.node_tree.links.new(uv.outputs['UV'], node.inputs['Vector'])
                if node.type == 'UVMAP' and not node.uv_map:
                    node.uv_map = original_uv
            target = mat.node_tree.nodes.get('RuntimeBakeTarget')
            if not target:
                target = mat.node_tree.nodes.new('ShaderNodeTexImage')
                target.name = 'RuntimeBakeTarget'
            target.image = image
            mat.node_tree.nodes.active = target
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 1
    scene.cycles.device = 'CPU'
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    # Dense architecture atlases need extended gutters for compressed mipmaps.
    # Adjacent-face fill can leave black borders around disconnected roof tiles.
    scene.render.bake.margin = 32
    scene.render.bake.margin_type = 'EXTEND'
    scene.render.bake.use_clear = True


def architecture(key, asset_id, source):
    folder = OUT / key
    folder.mkdir(parents=True, exist_ok=True)
    if (folder / 'asset.json').exists():
        raise RuntimeError('Completed package exists; choose new version')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    originals = [o for o in bpy.context.scene.objects if o.type == 'MESH'
                 and not o.hide_render and not o.name.lower().startswith(('studio', 'ground'))]
    select(originals)
    # Apply authored modifiers in memory; keep object identity during baking.
    bpy.ops.object.convert(target='MESH')
    objects = list(bpy.context.selected_objects)
    for obj in objects:
        obj.data = obj.data.copy()
        # Freeze per-piece variation into mesh data before combining objects.
        attribute = obj.data.attributes.new('BakeRandom', 'FLOAT', 'POINT')
        value = int(hashlib.sha256(obj.name.encode()).hexdigest()[:8], 16) / 4294967295
        attribute.data.foreach_set('value', [value] * len(obj.data.vertices))
        preserve = obj.data.attributes.new('PreserveStructure', 'FLOAT', 'POINT')
        structural = len(obj.data.polygons) <= 24 or any(
            s.material and 'Plaster' in s.material.name for s in obj.material_slots)
        preserve.data.foreach_set('value', [float(structural)] * len(obj.data.vertices))
    for mat in {s.material for o in objects for s in o.material_slots if s.material}:
        for node in list(mat.node_tree.nodes):
            if node.type == 'OBJECT_INFO':
                attribute = mat.node_tree.nodes.new('ShaderNodeAttribute')
                attribute.attribute_name = 'BakeRandom'
                for link in list(node.outputs['Random'].links):
                    mat.node_tree.links.new(attribute.outputs['Fac'], link.to_socket)
    select(objects)
    bpy.ops.object.join()
    objects = [bpy.context.object]
    for obj in objects:
        obj.data.uv_layers.new(name='RuntimeAtlas')
        obj.data.uv_layers.active_index = len(obj.data.uv_layers) - 1
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')
    image = bpy.data.images.new(key + '_BaseColor', width=4096 if key == 'house' else 1024,
                                height=4096 if key == 'house' else 1024, alpha=False)
    # Existing materials explicitly name source UV; unconnected textures need original layer.
    for obj in objects:
        obj.data.uv_layers.active_index = 0
    bake_setup(image, objects)
    for obj in objects:
        obj.data.uv_layers.active_index = len(obj.data.uv_layers) - 1
        obj.data.uv_layers.active.active_render = True
    select(objects)
    print('BAKE_START', key, len(objects), flush=True)
    bpy.ops.object.bake(type='DIFFUSE')
    image.filepath_raw = str(folder / (key + '_BaseColor.png'))
    image.file_format = 'PNG'
    image.save()
    mat = bpy.data.materials.new(key + '_Baked')
    mat.use_nodes = True
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = image
    mat.node_tree.links.new(tex.outputs['Color'], mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    for obj in objects:
        # Resolve by name each time: removing a layer invalidates RNA collection proxies.
        for name in [uv.name for uv in obj.data.uv_layers if uv.name != 'RuntimeAtlas']:
            obj.data.uv_layers.remove(obj.data.uv_layers[name])
        assert len(obj.data.uv_layers) == 1 and obj.data.uv_layers[0].name == 'RuntimeAtlas'
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        for face in obj.data.polygons:
            face.material_index = 0
    select(objects)
    bpy.ops.object.join()
    merged = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    coords = [v.co for v in merged.data.vertices]
    low = Vector(tuple(min(v[i] for v in coords) for i in range(3)))
    high = Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    offset = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    for v in merged.data.vertices:
        v.co -= offset
    counts = []
    for level, ratio in enumerate((1, .36, .12)):
        obj = merged.copy()
        obj.data = merged.data.copy()
        bpy.context.collection.objects.link(obj)
        obj.name = asset_id + '_LOD' + str(level)
        select([obj])
        if level:
            # Keep low-poly structural panels intact. Global collapse can remove
            # entire plaster panels / openings despite a good overall triangle ratio.
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='DESELECT')
            bpy.ops.object.mode_set(mode='OBJECT')
            protection = obj.data.attributes.get('PreserveStructure')
            for face in obj.data.polygons:
                face.select = all(protection.data[v].value > .5 for v in face.vertices)
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.separate(type='SELECTED')
            bpy.ops.object.mode_set(mode='OBJECT')
            pieces = list(bpy.context.selected_objects)
            select([obj])
            modifier = obj.modifiers.new('Offline_LOD', 'DECIMATE')
            modifier.ratio = ratio
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            select(pieces)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.join()
        # Freeze Blender's triangulation: Unity can otherwise drop complex wall ngons.
        triangulate = obj.modifiers.new('Runtime_Triangulation', 'TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
        obj.data.calc_loop_triangles()
        counts.append(len(obj.data.loop_triangles))
        export([obj], folder / f'{key}_LOD{level}.fbx')
        bpy.data.objects.remove(obj, do_unlink=True)
    (folder / 'asset.json').write_text(json.dumps(dict(assetId=asset_id, source=str(source),
        sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(), lodTriangles=counts,
        sourceObjects=len(originals), renderersPerLOD=1, dimensions=list(high-low),
        status='SCENE_TEST_CANDIDATE', baked='BaseColor only; no lighting'), indent=2))
    print('EXPORTED', key, counts, flush=True)


def oak():
    folder = OUT / 'oak'
    folder.mkdir(parents=True, exist_ok=True)
    if (folder / 'asset.json').exists():
        raise RuntimeError('Completed package exists')
    root = ART / 'Art/Vegetation/Oak_Kit'
    bpy.ops.wm.open_mainfile(filepath=str(root / 'Cozy_Oak_Kit.blend'))
    bark = bpy.data.materials['M_Oak_Bark']
    bpy.ops.mesh.primitive_plane_add(size=2)
    plane = bpy.context.object
    plane.data.materials.append(bark)
    image = bpy.data.images.new('oak_BaseColor', width=2048, height=2048, alpha=False)
    bake_setup(image, [plane])
    select([plane])
    bpy.ops.object.bake(type='DIFFUSE')
    image.filepath_raw = str(folder / 'oak_BaseColor.png')
    image.file_format = 'PNG'
    image.save()
    for level in range(3):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        source = root / 'Exports' / ('SM_Tree_Oak_A' + (f'_LOD{level}' if level else '') + '.fbx')
        bpy.ops.import_scene.fbx(filepath=str(source))
        obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
        select([obj])
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.separate(type='MATERIAL')
        bpy.ops.object.mode_set(mode='OBJECT')
        models = list(bpy.context.selected_objects)
        for mesh in models:
            mesh.name = ('Leaves' if 'Foliage' in mesh.data.materials[0].name else 'Trunk') + f'_LOD{level}'
        export(models, folder / f'oak_LOD{level}.fbx')
    (folder / 'asset.json').write_text(json.dumps(dict(assetId='VEG_Tree_Oak_A',
        source=str(root), status='SCENE_TEST_CANDIDATE', lodTriangles=[12068,6858,3106],
        renderersPerLOD=2, windCompatibility='Separate leaves and trunk, Unity check pending'), indent=2))


parser = argparse.ArgumentParser()
parser.add_argument('--asset', required=True)
parser.add_argument('--version', default='v002')
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
OUT = ART / ('LocalHandoffs/TerrainStarter-' + args.version)
current = json.loads((ART / 'Art/House_Cottage_A/CURRENT.json').read_text())
if args.asset == 'oak':
    oak()
else:
    names = {'house': ('BLD_House_Cottage_A', current['house']),
             'well': ('PROP_Well_A', current['yard_module_overrides']['SM_Well_A']),
             'barrel': ('PROP_Barrel_A', current['yard_module_overrides']['SM_Barrel_A']),
             'crate': ('PROP_Crate_A', current['yard_module_overrides']['SM_Crate_A'])}
    names['fence'] = ('MOD_FenceBay_A', current['yard_module_overrides']['SM_FenceBay_A'])
    names['bench'] = ('PROP_WoodBench_A', current['yard_module_overrides']['SM_WoodBench_A'])
    identity, path = names[args.asset]
    architecture(args.asset, identity, ART / 'Art/House_Cottage_A' / path)
