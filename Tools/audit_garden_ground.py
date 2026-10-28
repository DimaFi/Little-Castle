"""Read a generated albedo and render a 4x4 repeat on a native Blender plane.
Does not modify the source image or derive pretend PBR maps from its colour.
"""
from pathlib import Path
import json
import bpy
import numpy as np

GAME=Path(__file__).resolve().parents[1]
SOURCE=GAME.parent/'CozySettlement/LocalHandoffs/GardenGround-v001/Grass_MeadowSoft_BaseColor.png'
OUT=GAME/'Logs/GardenPlants'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
image=bpy.data.images.load(str(SOURCE))
w,h=image.size
a=np.asarray(image.pixels[:],dtype=np.float32).reshape(h,w,4)[:,:,:3]
data=dict(size=[w,h],rgb_mean=a.mean(axis=(0,1)).tolist(),
    edge_mae_x=float(abs(a[:,0]-a[:,-1]).mean()),edge_mae_y=float(abs(a[0]-a[-1]).mean()),
    adjacent_mae_x=float(abs(a[:,1:]-a[:,:-1]).mean()),adjacent_mae_y=float(abs(a[1:]-a[:-1]).mean()),
    note='A seam statistic is not proof of seamless visual repetition; inspect tiled plane.')
(OUT/'ground-texture-audit.json').write_text(json.dumps(data,indent=2))
bpy.ops.mesh.primitive_plane_add(size=8)
obj=bpy.context.object
for d in obj.data.uv_layers.active.data:
    d.uv*=4
material=bpy.data.materials.new('Albedo review: unlit 4x4 repeat')
material.use_nodes=True
nodes=material.node_tree.nodes
nodes.clear()
tex=nodes.new('ShaderNodeTexImage')
tex.image=image
emission=nodes.new('ShaderNodeEmission')
out=nodes.new('ShaderNodeOutputMaterial')
material.node_tree.links.new(tex.outputs['Color'],emission.inputs['Color'])
material.node_tree.links.new(emission.outputs[0],out.inputs['Surface'])
obj.data.materials.append(material)
bpy.ops.object.camera_add(location=(0,0,10))
camera=bpy.context.object
camera.data.type='ORTHO'
camera.data.ortho_scale=8
scene=bpy.context.scene
scene.camera=camera
scene.render.engine='CYCLES'
scene.cycles.samples=1
scene.view_settings.view_transform='Standard'
scene.render.resolution_x=1200
scene.render.resolution_y=1200
scene.render.resolution_percentage=100
scene.render.filepath=str(OUT/'ground-albedo-4x4.png')
bpy.ops.render.render(write_still=True)
print('GROUND_ALBEDO_AUDIT',json.dumps(data))
