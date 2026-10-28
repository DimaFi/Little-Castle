"""Blender: deterministic, versioned garden candidates; no source asset overwrite.
blender --background --factory-startup --python Tools/build_garden_plant_kit.py -- --version v001
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import sys
import bpy
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
args = argparse.ArgumentParser()
args.add_argument('--version', required=True)
opt = args.parse_args(sys.argv[sys.argv.index('--') + 1:])
if not opt.version.startswith('v') or not opt.version[1:].isdigit():
    raise ValueError('Use a version such as v001')
OUT = ROOT / 'CozySettlement/LocalHandoffs' / ('GardenPlantKit-' + opt.version)
if OUT.exists():
    raise FileExistsError('Immutable package already exists: ' + str(OUT))
OUT.mkdir(parents=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

# Solid colour lookup, not a painted/baked lighting texture. Wide gutters prevent
# mip bleed; UVs sample swatch centres. All species share one material per LOD tier.
COLORS = [(0.22, .31, .11, 1), (.30, .41, .16, 1), (.40, .51, .22, 1),
          (.50, .59, .29, 1), (.43, .23, .59, 1), (.61, .39, .72, 1),
          (.92, .89, .73, 1), (.87, .62, .17, 1)]
image = bpy.data.images.new('GardenPalette', width=256, height=32, alpha=False)
image.colorspace_settings.name = 'sRGB'
image.pixels = [c for y in range(32) for x in range(256) for c in COLORS[x // 32]]
image.filepath_raw = str(OUT / 'GardenPalette.png')
image.file_format = 'PNG'
image.save()
material = bpy.data.materials.new('Garden_Foliage')
material.use_nodes = True
bsdf = material.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Roughness'].default_value = .9
tex = material.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = image
material.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])

SOURCE_LEAVES=ROOT/'CozySettlement/Art/Vegetation/Oak_Kit/Exports/SM_LeafCluster_Small_A.fbx'
SOURCE_TEXTURE=ROOT/'CozySettlement/Mat/T_Foliage/T_Foliage_BaseColor.png'
bpy.ops.import_scene.fbx(filepath=str(SOURCE_LEAVES))
source=bpy.data.objects['SM_LeafCluster_Small_A']
source_vertices=[source.matrix_world@v.co for v in source.data.vertices]
source_faces=[tuple(p.vertices) for p in source.data.polygons]
source_uv=[tuple(v.uv) for v in source.data.uv_layers.active.data]
bpy.data.objects.remove(source,do_unlink=True)
leaf_material=bpy.data.materials.new('Garden_Oak_Foliage')
leaf_material.use_nodes=True
leaf_bsdf=leaf_material.node_tree.nodes.get('Principled BSDF')
leaf_bsdf.inputs['Roughness'].default_value=.9
leaf_tex=leaf_material.node_tree.nodes.new('ShaderNodeTexImage')
leaf_tex.image=bpy.data.images.load(str(SOURCE_TEXTURE))
leaf_tex.image.pack()
leaf_material.node_tree.links.new(leaf_tex.outputs['Color'],leaf_bsdf.inputs['Base Color'])
cutout=leaf_material.node_tree.nodes.new('ShaderNodeMath')
cutout.operation='GREATER_THAN'
cutout.inputs[1].default_value=.45
leaf_material.node_tree.links.new(leaf_tex.outputs['Alpha'],cutout.inputs[0])
leaf_material.node_tree.links.new(cutout.outputs[0],leaf_bsdf.inputs['Alpha'])


def textured_shrub(lod, name):
    """Reuse actual Oak Kit cards/UV islands, assembled into a compact rounded bush."""
    layouts=[[(0,0,.12),(-.22,-.12,0),(.22,-.12,.01),(0,.22,.025)],
             [(-.18,0,0),(.18,.08,.04)], [(0,0,0)]][lod]
    vertices, faces, uvs=[],[],[]
    centre=Vector(((min(v.x for v in source_vertices)+max(v.x for v in source_vertices))*.5,
                   (min(v.y for v in source_vertices)+max(v.y for v in source_vertices))*.5,
                   min(v.z for v in source_vertices)))
    for i,p in enumerate(layouts):
        rotation=Matrix.Rotation(i*2.39996,3,'Z')
        offset=len(vertices)
        vertices.extend([rotation@(v-centre)*.76+Vector(p) for v in source_vertices])
        faces.extend([tuple(offset+j for j in f) for f in source_faces])
        uvs.extend(source_uv)
    low=Vector(tuple(min(v[k] for v in vertices) for k in range(3)))
    high=Vector(tuple(max(v[k] for v in vertices) for k in range(3)))
    center=Vector(((high.x+low.x)*.5,(high.y+low.y)*.5,low.z))
    extent=high-low
    vertices=[Vector(((v.x-center.x)/extent.x*1.20,(v.y-center.y)/extent.y*1.10,
                      (v.z-center.z)/extent.z*.75)) for v in vertices]
    mesh=bpy.data.meshes.new(name)
    # Match triangle winding to the outward canopy normals. Keep UV corner order.
    repaired_faces, repaired_uvs=[],[]
    cursor=0
    for face in faces:
        a,b,c=(vertices[j] for j in face[:3])
        normal=(b-a).cross(c-a)
        outward=sum((vertices[j] for j in face),Vector())/len(face)-Vector((0,0,.30))
        uv_face=uvs[cursor:cursor+len(face)]
        flip=normal.dot(outward)<0
        repaired_faces.append(tuple(reversed(face)) if flip else face)
        repaired_uvs.extend(reversed(uv_face) if flip else uv_face)
        cursor+=len(face)
    faces,uvs=repaired_faces,repaired_uvs
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    mesh.materials.append(leaf_material)
    mesh.uv_layers.new(name='UVMap')
    mesh.color_attributes.new(name='WindMask',type='FLOAT_COLOR',domain='CORNER')
    uv=mesh.uv_layers['UVMap']
    mask=mesh.color_attributes['WindMask']
    for i,co in enumerate(uvs):
        uv.data[i].uv=co
        z=mesh.vertices[mesh.loops[i].vertex_index].co.z
        w=max(0,min(1,(z-.04)/.71))**1.5
        mask.data[i].color=(w,w,w,1)
    for face in mesh.polygons:
        face.use_smooth=True
    # Radial normals give the cluster a broad lit/shaded volume, not flat cards.
    mesh.normals_split_custom_set_from_vertices([
        tuple((v.co-Vector((0,0,.30))).normalized()) for v in mesh.vertices])
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    return obj


class MeshBuilder:
    def __init__(self):
        self.vertices, self.faces, self.swatches = [], [], []

    def part(self, vertices, faces, color):
        offset = len(self.vertices)
        self.vertices.extend([tuple(v) for v in vertices])
        self.faces.extend([tuple(offset + i for i in face) for face in faces])
        self.swatches.extend([color] * len(faces))

    def leaf(self, base, tip, width, color, roll=0):
        base, tip = Vector(base), Vector(tip)
        axis = (tip-base).normalized()
        side = axis.cross(Vector((0, 0, 1)))
        if side.length < .01:
            side = Vector((1, 0, 0))
        side.normalize()
        normal = side.cross(axis).normalized()
        side = side * math.cos(roll) + normal * math.sin(roll)
        normal = side.cross(axis).normalized()
        middle = base.lerp(tip, .5)
        # Almond silhouette with a raised midrib, no alpha cards or double faces.
        self.part([base, base.lerp(tip, .32)-side*width*.78,
                   base.lerp(tip, .70)-side*width*.68, tip,
                   base.lerp(tip, .70)+side*width*.68,
                   base.lerp(tip, .32)+side*width*.78,
                   middle+normal*width*.22],
                  [(i, (i+1)%6, 6) for i in range(6)], color)

    def ellipsoid(self, center, radius, color, sides=7, rings=3):
        center = Vector(center)
        verts = [center + Vector((0, 0, -radius[2]))]
        for j in range(1, rings+1):
            p = -math.pi/2 + math.pi*j/(rings+1)
            for i in range(sides):
                a = 2*math.pi*i/sides
                verts.append(center+Vector((radius[0]*math.cos(p)*math.cos(a),
                    radius[1]*math.cos(p)*math.sin(a), radius[2]*math.sin(p))))
        verts.append(center+Vector((0, 0, radius[2])))
        faces = [(0, 1+(i+1)%sides, 1+i) for i in range(sides)]
        for j in range(rings-1):
            for i in range(sides):
                a, b = 1+j*sides+i, 1+j*sides+(i+1)%sides
                faces.append((a,b,b+sides,a+sides))
        faces.extend([(len(verts)-1,1+(rings-1)*sides+i,
                       1+(rings-1)*sides+(i+1)%sides) for i in range(sides)])
        self.part(verts, faces, color)

    def stem(self, base, tip, radius, sides=4):
        base, tip = Vector(base), Vector(tip)
        middle = base.lerp(tip, .45)
        verts = []
        for j, p in enumerate((base,middle,tip)):
            for i in range(sides):
                a = i*2*math.pi/sides
                verts.append(p+Vector((math.cos(a),math.sin(a),0))*radius*(1-j*.2))
        self.part(verts, [(j*sides+i,j*sides+(i+1)%sides,
                   (j+1)*sides+(i+1)%sides,(j+1)*sides+i)
                   for j in range(2) for i in range(sides)], 0)

    def finish(self, name):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        mesh.materials.append(material)
        mesh.uv_layers.new(name='UVMap')
        mesh.color_attributes.new(name='WindMask', type='FLOAT_COLOR', domain='CORNER')
        # Adding a custom data layer can invalidate earlier Blender RNA handles.
        uv = mesh.uv_layers['UVMap']
        mask = mesh.color_attributes['WindMask']
        max_z = max(v.co.z for v in mesh.vertices)
        for face, swatch in zip(mesh.polygons, self.swatches):
            face.use_smooth = True
            for loop in face.loop_indices:
                uv.data[loop].uv = ((swatch+.5)/8, .5)
                z = mesh.vertices[mesh.loops[loop].vertex_index].co.z
                weight = min(1, max(0, (z-.04)/max(.1, max_z-.04)))**1.5
                mask.data[loop].color = (weight,weight,weight,1)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        return obj


def shrub(lod):
    b, rng = MeshBuilder(), random.Random(417)
    lobes = [((-.23,.04,.31),(.33,.30,.31)), ((.20,.08,.33),(.36,.32,.33)),
             ((0,-.16,.29),(.32,.28,.29))]
    for center, radius in lobes:
        b.ellipsoid(center, radius, 1, sides=8 if lod<2 else 6, rings=3 if lod<2 else 2)
    for i in range((210,68,8)[lod]):
        a = i*2.39996
        z = .18+rng.random()*.47
        r = math.sqrt(max(.04, 1-((z-.24)/.46)**2))*(.37+rng.random()*.11)
        base = Vector((math.cos(a)*r,math.sin(a)*r*.85,z))
        tip = base+Vector((math.cos(a)*.16,math.sin(a)*.16,.06+rng.random()*.065))
        b.leaf(base, tip, .08+rng.random()*.025, 1+(i%3), rng.uniform(-.5,.5))
    return b


def flowers(lod, tall):
    b, rng = MeshBuilder(), random.Random(911 if tall else 312)
    # Keep outer stems and apex in every LOD to preserve the cluster silhouette.
    positions = [(0,0),(-.20,.02),(.15,.17),(.19,-.17),(-.13,-.19),(-.04,.21)]
    count = (5,4,3)[lod] if tall else (6,5,4)[lod]
    for i, (x,y) in enumerate(positions[:count]):
        h = ([.94,.71,.81,.67,.59][i] if tall else [.40,.31,.48,.36,.30,.39][i])
        tip = Vector((x+(i%2*2-1)*.055,y+.025,h))
        b.stem((x,y,0),tip,.012 if tall else .008)
        for j in range((5,3,2)[lod]):
            a = i*1.83+j*2.4
            base = Vector((x,y,.06+j*.032))
            b.leaf(base,base+Vector((math.cos(a)*.22,math.sin(a)*.22,.10)),
                   .036 if tall else .048,1+j%3)
        if tall:
            tiers = (7,5,3)[lod]
            for j in range(tiers):
                t = j/max(1,tiers-1)
                p = tip+Vector((0,0,-.34+t*.34))
                radius = .065*(1-t*.66)
                petals = (5,4,3)[lod]
                for k in range(petals):
                    a = k*2*math.pi/petals+j*.9
                    c = p+Vector((math.cos(a)*radius,math.sin(a)*radius,0))
                    b.ellipsoid(c,(radius*.77,radius*.77,.038 if lod<2 else .055),
                                4+(j+i)%2,sides=4,rings=1)
        else:
            petals = (7,6,5)[lod]
            for j in range(petals):
                a = j*math.pi*2/petals
                b.leaf(tip,tip+Vector((math.cos(a)*.10,math.sin(a)*.10,.024)),.032,6)
            b.ellipsoid(tip+Vector((0,0,.016)),(.032,.032,.025),7,sides=6,rings=1)
    return b


report = {'status':'SCENE_TEST_CANDIDATE', 'version':opt.version, 'assets':[],
          'palette':'flower colour lookup; shrubs reuse original Oak Kit BaseColor/alpha',
          'shrub_source':str(SOURCE_LEAVES), 'shrub_source_sha256':hashlib.sha256(SOURCE_LEAVES.read_bytes()).hexdigest(),
          'shrub_texture_source':str(SOURCE_TEXTURE), 'shrub_texture_sha256':hashlib.sha256(SOURCE_TEXTURE.read_bytes()).hexdigest(),
          'wind':'Vertex Color R, 0 at base; shared LC_Foliage visible/shadow deformation',
          'unity_validation':'PENDING', 'source_script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
hero = []
for index, key in enumerate(('GardenShrub_A','GardenDaisies_A','GardenLupins_A')):
    entry = {'id':key, 'lods':[]}
    for lod in range(3):
        name=key+'_LOD'+str(lod)
        obj = textured_shrub(lod,name) if index==0 else flowers(lod,index==2).finish(name)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        path = OUT / (obj.name+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},
            axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,
            add_leaf_bones=False,path_mode='STRIP')
        mesh = obj.data
        mesh.calc_loop_triangles()
        assert len(mesh.uv_layers)==1 and len(mesh.color_attributes)==1
        assert all(math.isfinite(c) for v in mesh.vertices for c in v.co)
        assert min(v.co.z for v in mesh.vertices)>=-.001
        entry['lods'].append({'file':path.name,'triangles':len(mesh.loop_triangles),
            'vertices':len(mesh.vertices),'dimensions_m':list(obj.dimensions),
            'uv0':True,'normals':True,'wind_mask_min':min(c.color[0] for c in mesh.color_attributes[0].data),
            'wind_mask_max':max(c.color[0] for c in mesh.color_attributes[0].data),
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
        if lod==0:
            obj.location=(index*1.5-1.5,0,0)
            hero.append(obj)
        else:
            obj.hide_render=True
            obj.hide_set(True)
    assert entry['lods'][0]['triangles']>entry['lods'][1]['triangles']>entry['lods'][2]['triangles']
    report['assets'].append(entry)
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))

# Independent Blender asset review, explicitly not a Unity/game screenshot.
bpy.ops.mesh.primitive_plane_add(size=200)
ground=bpy.context.object
ground.name='Preview ground (not exported)'
ground_mat=bpy.data.materials.new('Preview earth')
ground_mat.diffuse_color=(.24,.29,.15,1)
ground.data.materials.append(ground_mat)
ground.location.z=-.008
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.cycles.transparent_max_bounces=32
scene.world=bpy.data.worlds.new('Preview sky')
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.55,.66,.85,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35
bpy.ops.object.light_add(type='AREA',location=(-3,-4,7))
bpy.context.object.data.energy=850
bpy.context.object.data.shape='DISK'
bpy.context.object.data.size=4
bpy.context.object.rotation_euler=(Vector((0,0,.25))-bpy.context.object.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.2,-5,3.1))
camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.32))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'
camera.data.ortho_scale=5.4
scene.camera=camera
scene.render.resolution_x=1400
scene.render.resolution_y=760
scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(OUT/'Plants_BlenderPreview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'GardenPlantKit.blend'))
bpy.ops.render.render(write_still=True)
print('GARDEN_PLANTS_COMPLETE',json.dumps(report))
