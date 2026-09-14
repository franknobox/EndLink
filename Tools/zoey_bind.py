"""Zoey v02 binding source. Run with Blender --background --python Tools/zoey_bind.py.

Island assignments are specific to the supplied OBJ; they keep rigid trims intact.
Native authoring file lives outside Assets to avoid implicit Blender FBX conversion.
"""
import bpy
import bmesh
import json
import math
import hashlib
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Assets/Art/Characters/Zoey_Body_v02.obj'
OUTPUT = ROOT / 'Assets/Art/Characters/Zoey_Body_Rig.fbx'
NATIVE = ROOT / 'Tools/Zoey_Rig.blend'
LOG = ROOT / 'Logs'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=str(SOURCE), forward_axis='Y', up_axis='Z')
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
body.name = 'Zoey_Body'
body.data.name = 'Zoey_Skin'
for vertex in body.data.vertices:
    x, y, z = vertex.co
    vertex.co = (x * .01, -z * .01, y * .01)
body.data.update()

# Keep disconnected garment shells, UVs and original positions; only fix face orientation.
bm = bmesh.new()
bm.from_mesh(body.data)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(body.data)
bm.free()
body.data.update()

adjacent = [set() for _ in body.data.vertices]
for e in body.data.edges:
    a, b = e.vertices
    adjacent[a].add(b)
    adjacent[b].add(a)
islands, visited = [], set()
for first in range(len(adjacent)):
    if first in visited:
        continue
    pending, ids = [first], []
    visited.add(first)
    while pending:
        index = pending.pop()
        ids.append(index)
        for other in adjacent[index]:
            if other not in visited:
                visited.add(other)
                pending.append(other)
    islands.append(ids)
assert len(islands) == 127, 'Source topology changed; review island weight assignments.'

material = bpy.data.materials.new('MAT_Zoey_Test')
material.diffuse_color = (.72, .75, .8, 1)
material.use_nodes = True
shader = material.node_tree.nodes.get('Principled BSDF')
shader.inputs['Base Color'].default_value = (.72, .75, .8, 1)
shader.inputs['Metallic'].default_value = .05
shader.inputs['Roughness'].default_value = .65
body.data.materials.clear()
body.data.materials.append(material)
body.color = (.58, .65, .73, 1)

arm = bpy.data.armatures.new('Zoey_Skeleton')
rig = bpy.data.objects.new('Zoey_Rig', arm)
bpy.context.collection.objects.link(rig)
rig.show_in_front = True
arm.display_type = 'OCTAHEDRAL'
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
body.select_set(False)
bpy.ops.object.mode_set(mode='EDIT')
bone_specs = {}

def bone(name, head, tail, parent=None, deform=True, collection='Body'):
    eb = arm.edit_bones.new(name)
    eb.head, eb.tail = head, tail
    eb.use_deform = deform
    if parent:
        eb.parent = arm.edit_bones[parent]
        eb.use_connect = (eb.head-eb.parent.tail).length < .0001
    eb.align_roll(Vector((0, -1, 0)))
    bone_specs[name] = dict(head=list(head), tail=list(tail), parent=parent, collection=collection)
    return eb

bone('Root', (0, 0, 0), (0, 0, .15), deform=False)
bone('Hips', (0, .015, .80), (0, .012, .925), 'Root')
bone('Spine', (0, .012, .925), (0, .008, 1.035), 'Hips')
bone('Chest', (0, .008, 1.035), (0, .005, 1.14), 'Spine')
bone('UpperChest', (0, .005, 1.14), (0, .002, 1.22), 'Chest')
bone('Neck', (0, .002, 1.22), (0, .018, 1.315), 'UpperChest')
bone('Head', (0, .018, 1.315), (0, .024, 1.62), 'Neck')

for side, s in [('Left', 1), ('Right', -1)]:
    def p(x, y, z): return (s*x, y, z)
    bone(side+'Shoulder', p(.035, .005, 1.16), p(.177, .003, 1.134), 'UpperChest')
    bone(side+'UpperArm', p(.177, .003, 1.134), p(.333, .003, 1.014), side+'Shoulder')
    bone(side+'LowerArm', p(.333, .003, 1.014), p(.467, -.010, .941), side+'UpperArm')
    bone(side+'Hand', p(.467, -.010, .941), p(.521, -.017, .915), side+'LowerArm')
    for finger, y, endx in [('Index', -.077, .585), ('Middle', -.038, .593),
                             ('Ring', .002, .580), ('Little', .032, .552)]:
        points = [p(.515, y, .915), p(.54, y, .91), p((.54+endx)/2, y, .906), p(endx, y, .901)]
        for i, segment in enumerate(['Proximal', 'Intermediate', 'Distal']):
            bone(side+finger+segment, points[i], points[i+1],
                 side+'Hand' if i == 0 else side+finger+['Proximal', 'Intermediate'][i-1], collection='Hands')
    points = [p(.482, -.043, .916), p(.456, -.077, .887), p(.444, -.090, .872), p(.431, -.1, .859)]
    for i, segment in enumerate(['Proximal', 'Intermediate', 'Distal']):
        bone(side+'Thumb'+segment, points[i], points[i+1],
             side+'Hand' if i == 0 else side+'Thumb'+['Proximal','Intermediate'][i-1], collection='Hands')
    bone(side+'UpperLeg', p(.10, .015, .79), p(.133, -.009, .438), 'Hips')
    bone(side+'LowerLeg', p(.133, -.009, .438), p(.184, .055, .095), side+'UpperLeg')
    bone(side+'Foot', p(.184, .055, .095), p(.183, -.023, .040), side+'LowerLeg')
    bone(side+'Toes', p(.183, -.023, .040), p(.183, -.078, .025), side+'Foot')

for side, x in [('Left', .225), ('Center', 0), ('Right', -.225)]:
    for region, y, hem in [('Back', .14, .44), ('Front', -.14, .79)]:
        name = 'Cape'+region+side
        bone(name+'Upper', (x*.5, y*.4, 1.18), (x, y, 1.0), 'UpperChest', collection='Cloth')
        bone(name+'Lower', (x, y, 1.0), (x*1.1, y*1.4, hem), name+'Upper', collection='Cloth')

bpy.ops.object.mode_set(mode='OBJECT')
for name, spec in bone_specs.items():
    collection = arm.collections.get(spec['collection']) or arm.collections.new(spec['collection'])
    collection.assign(arm.bones[name])
    pb = rig.pose.bones[name]
    pb.rotation_mode = 'QUATERNION'
    pb.lock_scale = (True, True, True)
    if name != 'Root' and name != 'Hips':
        pb.lock_location = (True, True, True)
    pb.color.palette = 'THEME04' if name.startswith('Left') else 'THEME03' if name.startswith('Right') else 'THEME09' if name.startswith('Cape') else 'THEME02'

groups = {b.name: body.vertex_groups.new(name=b.name) for b in arm.bones if b.use_deform}
assignments = {}

def smooth(a, b, v):
    t = max(0, min(1, (v-a)/(b-a)))
    return t*t*(3-2*t)

def weights(index, values):
    values = sorted([(k,v) for k,v in values.items() if v > .0001], key=lambda kv: -kv[1])[:4]
    total = sum(v for _,v in values)
    assert total > 0
    assignments[index] = {k:v/total for k,v in values}
    for name, value in assignments[index].items():
        groups[name].add([index], value, 'REPLACE')

def segment_distance(point, name):
    spec = bone_specs[name]
    h, t = Vector(spec['head']), Vector(spec['tail'])
    d = t-h
    alpha = max(0, min(1, (point-h).dot(d)/d.length_squared))
    return (point-(h+d*alpha)).length

def nearby(point, names, power=4, count=2):
    items = sorted([(n,segment_distance(point,n)) for n in names], key=lambda kv:kv[1])[:count]
    return {n:1/max(.008,d)**power for n,d in items}

def torso(point):
    z = point.z
    levels = [('Hips', .88), ('Spine', .985), ('Chest', 1.09), ('UpperChest', 1.195), ('Neck', 1.30)]
    if z <= levels[0][1]: return {'Hips':1}
    for (a,za),(b,zb) in zip(levels, levels[1:]):
        if z <= zb:
            t = smooth(za,zb,z)
            return {a:1-t,b:t}
    return {'Neck':1}

def leg(point, side):
    z = point.z
    if z > .73:
        t = smooth(.73,.83,z)
        return {side+'UpperLeg':1-t, 'Hips':t}
    if z > .50: return {side+'UpperLeg':1}
    if z > .38:
        t = smooth(.38,.50,z)
        return {side+'UpperLeg':t,side+'LowerLeg':1-t}
    if z > .135: return {side+'LowerLeg':1}
    t = smooth(.06,.135,z)
    return {side+'LowerLeg':t,side+'Foot':1-t}

def arm_weight(point, side):
    x = abs(point.x)
    if x < .295: return {side+'UpperArm':1}
    if x < .370:
        t = smooth(.295,.370,x)
        return {side+'UpperArm':1-t,side+'LowerArm':t}
    if x < .449: return {side+'LowerArm':1}
    if x < .489:
        t = smooth(.449,.489,x)
        return {side+'LowerArm':1-t,side+'Hand':t}
    return {side+'Hand':1}

def hand(point, side):
    if abs(point.x) < .505 and point.y > -.05:
        return arm_weight(point,side)
    if point.z < .906 and point.y < -.05 and abs(point.x) < .493:
        return nearby(point,[side+'Thumb'+n for n in ('Proximal','Intermediate','Distal')]+[side+'Hand'])
    if abs(point.x) > .522:
        finger = min([('Index',-.077),('Middle',-.038),('Ring',.002),('Little',.032)], key=lambda item:abs(point.y-item[1]))[0]
        return nearby(point,[side+finger+n for n in ('Proximal','Intermediate','Distal')]+[side+'Hand'])
    return arm_weight(point,side)

def cape(point):
    region = 'Back' if point.y > .015 else 'Front'
    side = 'Left' if point.x > .10 else 'Right' if point.x < -.10 else 'Center'
    prefix = 'Cape'+region+side
    t = smooth(.94,1.21,point.z)
    upper = smooth(.83,1.06,point.z)
    return {'UpperChest':t,prefix+'Upper':(1-t)*upper,prefix+'Lower':(1-t)*(1-upper)}

HEAD = set(range(0,6)) | {7,8,9,10,11,32,33,34,37,38,39,40,42,51,52,53,54,55,56}
TORSO = {15,16,17,22,23,24,25,26,27,28,29,30,57,102,105}
ARMS = {44,48,76,77,78,79,83,86,41,45,46,47,49,50}
SOFT_LEGS = {6,43,58,61,108,124,125,20,18}
RIGID_CHEST = set(range(106,121)) - {108}
island_report = []
for island_id, ids in enumerate(islands):
    coords = [body.data.vertices[i].co for i in ids]
    center = sum(coords, Vector())/len(coords)
    side = 'Left' if center.x >= 0 else 'Right'
    if island_id in HEAD: kind='head'
    elif island_id in TORSO: kind='torso'
    elif island_id in {35,36}: kind='hair_tails'
    elif island_id == 84: kind='cape'
    elif island_id in ARMS: kind='arms'
    elif island_id in RIGID_CHEST: kind='chest_accessory'
    elif island_id in SOFT_LEGS: kind='soft_legs'
    elif center.z < .15: kind='feet'
    elif center.z < .55: kind='boot'
    else: kind='thigh_accessory'
    for index in ids:
        point = body.data.vertices[index].co
        vertex_side = 'Left' if point.x >= 0 else 'Right'
        if kind in ('head','hair_tails'): values={'Head':1}
        elif kind == 'torso': values=torso(point)
        elif kind == 'cape': values=cape(point)
        elif kind == 'arms': values=hand(point,side)
        elif kind == 'chest_accessory': values={'UpperChest':1}
        elif kind == 'soft_legs': values=leg(point,vertex_side)
        elif kind == 'feet': values={side+'Foot':1}
        elif kind == 'boot':
            values = leg(point,side) if island_id in {31,98,88,95} else {side+'LowerLeg':1}
        else: values={side+'UpperLeg':1}
        weights(index,values)
    island_report.append(dict(id=island_id, kind=kind, count=len(ids)))

modifier = body.modifiers.new('Zoey Skin', 'ARMATURE')
modifier.object = rig
modifier.use_deform_preserve_volume = False
body.parent = rig
rig['binding_version'] = 'Zoey v02 initial skin binding; no animation clips'
rig['source_sha256'] = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
rig['units'] = 'meters; source centimeters converted once'
rig['pose_usage'] = 'Pose Mode: rotate Body/Hands/Cloth bones. Alt+R resets pose. Root/Hips can translate.'
rig['cloth_usage'] = 'Cape bones are manually posed, with no cloth simulation.'

# Export only mesh and skeleton, with no animation or autogenerated end bones.
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(OUTPUT), use_selection=True, object_types={'ARMATURE','MESH'},
    global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z', axis_up='Y', add_leaf_bones=False, use_armature_deform_only=True,
    bake_anim=False, mesh_smooth_type='FACE', use_mesh_modifiers=True)

for area in bpy.context.screen.areas if bpy.context.screen else []:
    if area.type == 'VIEW_3D':
        area.spaces.active.region_3d.view_distance = 3.4
        area.spaces.active.region_3d.view_location = (0,0,.85)
        area.spaces.active.region_3d.view_rotation = Quaternion((1,0,0), math.radians(78))
        area.spaces.active.shading.color_type = 'MATERIAL'
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.wm.save_as_mainfile(filepath=str(NATIVE))
bpy.ops.object.mode_set(mode='OBJECT')

scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'OBJECT'
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = 'BOTH'
scene.display.shading.background_type = 'WORLD'
scene.world = bpy.data.worlds.new('Preview')
scene.world.color = (.12,.12,.12)
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
bpy.ops.object.camera_add(location=(2.8,-5,1.65))
camera = bpy.context.object
scene.camera = camera
center = Vector((0,0,.88))
camera.rotation_euler = (center-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 2.4

def rotate(name, axis, degrees):
    local_axis = arm.bones[name].matrix_local.to_quaternion().inverted() @ Vector(axis)
    rig.pose.bones[name].rotation_quaternion = Quaternion(local_axis, math.radians(degrees))

checks = {}
for pose in ('Rest','Arms','Knee','Torso','Cape','Fist'):
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = (0,0,0)
    if pose == 'Arms':
        rotate('LeftUpperArm',(0,1,0),-65)
        rotate('RightUpperArm',(0,1,0),55)
        rotate('LeftLowerArm',(0,0,1),-85)
        rotate('RightLowerArm',(0,0,1),85)
    elif pose == 'Knee':
        rotate('LeftUpperLeg',(1,0,0),-60)
        rotate('LeftLowerLeg',(1,0,0),100)
        rotate('LeftFoot',(1,0,0),-20)
    elif pose == 'Torso':
        rotate('Spine',(0,0,1),20)
        rotate('Chest',(0,0,1),15)
        rotate('Head',(0,0,1),-30)
    elif pose == 'Cape':
        for n in bone_specs:
            if n.startswith('CapeBack'):
                rotate(n,(1,0,0),20)
    elif pose == 'Fist':
        for n in bone_specs:
            if any(f in n for f in ('Index','Middle','Ring','Little')):
                rotate(n,(0,1,0),60 if n.startswith('Left') else -60)
    bpy.context.view_layer.update()
    evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    points = [v.co for v in mesh.vertices]
    assert all(math.isfinite(c) for p in points for c in p)
    checks[pose] = {'min':[min(p[i] for p in points) for i in range(3)], 'max':[max(p[i] for p in points) for i in range(3)]}
    evaluated.to_mesh_clear()
    scene.render.filepath = str(LOG / ('Zoey_Rig_'+pose+'.png'))
    bpy.ops.render.render(write_still=True)

assert len(assignments)==len(body.data.vertices)
assert all(abs(sum(v.values())-1)<1e-6 and len(v)<=4 for v in assignments.values())
report = dict(vertices=len(body.data.vertices), faces=len(body.data.polygons), bones=len(arm.bones),
              islands=island_report, poses=checks, unweighted=0, max_influences=max(map(len,assignments.values())),
              source_sha256=rig['source_sha256'], bones_map=bone_specs)
(LOG / 'Zoey_RigReport.json').write_text(json.dumps(report,indent=2))
print('ZOEY_BINDING_OK',len(body.data.vertices),'vertices',len(arm.bones),'bones', 'no animations')
