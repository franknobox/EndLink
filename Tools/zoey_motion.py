"""Author initial in-place motion from Zoey_Rig.blend; never overwrite the bind source."""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector, Quaternion, Matrix

ROOT = Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'Tools/Zoey_Rig.blend'))
rig = bpy.data.objects['Zoey_Rig']
body = bpy.data.objects['Zoey_Body']
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
rig.animation_data_clear()
scene = bpy.context.scene
scene.render.fps = 30
MOVE_SPEED = 3.5
WALK_FRAMES = 18
STANCE = .30
TRAVEL = MOVE_SPEED * (WALK_FRAMES / scene.render.fps) * STANCE
rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}

def rotate(name, axis, degrees):
    pb = rig.pose.bones[name]
    local = rest[name].to_quaternion().inverted() @ Vector(axis)
    pb.rotation_quaternion = Quaternion(local, math.radians(degrees))

def aim(name, head, tail):
    direction = rig.data.bones[name].tail_local - rig.data.bones[name].head_local
    turn = direction.rotation_difference(tail-head)
    rig.pose.bones[name].matrix = Matrix.Translation(head) @ (turn @ rest[name].to_quaternion()).to_matrix().to_4x4()
    bpy.context.view_layer.update()

def solve_leg(side, target, pitch):
    upper = rig.pose.bones[side+'UpperLeg']
    lower = rig.pose.bones[side+'LowerLeg']
    hip = upper.head.copy()
    delta = target-hip
    length = delta.length
    a, b = upper.length, lower.length
    assert abs(a-b) < length < a+b, 'Unreachable foot target'
    direction = delta.normalized()
    pole = Vector((0,-1,0))
    pole = (pole-direction*pole.dot(direction)).normalized()
    along = (a*a-b*b+length*length)/(2*length)
    knee = hip+direction*along+pole*math.sqrt(max(0,a*a-along*along))
    aim(side+'UpperLeg', hip, knee)
    aim(side+'LowerLeg', knee, target)
    foot = side+'Foot'
    rig.pose.bones[foot].matrix = Matrix.Translation(target) @ (
        Quaternion((1,0,0), math.radians(pitch)) @ rest[foot].to_quaternion()).to_matrix().to_4x4()
    bpy.context.view_layer.update()

def pose(kind, phase):
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.location = (0,0,0)
        pb.rotation_quaternion = Quaternion()
        pb.scale = (1,1,1)
    angle = phase*2*math.pi
    walk = kind == 'Walk'
    sway = math.sin(angle)
    hips = rig.pose.bones['Hips']
    offset = Vector((.018*math.sin(angle-.4) if walk else .012+.003*sway,
                     0 if walk else -.003,
                     -.080-.012*math.cos(2*angle) if walk else -.010+.0015*math.sin(angle)))
    hips.location = rest['Hips'].to_quaternion().inverted() @ offset
    rotate('Hips',(0,0,1), 5*math.cos(angle) if walk else 2+.5*sway)
    rotate('Spine',(1,0,0), 5 if walk else -.5+.6*math.sin(angle))
    rotate('Chest',(0,0,1), -7*math.cos(angle-.12) if walk else -2-.5*sway)
    rotate('Head',(0,0,1), 2*math.cos(angle-.2) if walk else -1+.4*math.sin(angle+.5))
    for side, sign in [('Left',1),('Right',-1)]:
        swing = math.cos(angle-.12)*sign
        # Lower the A-pose arms, then add a restrained contralateral swing.
        rotate(side+'UpperArm',(0,1,0), sign*(43 if walk else 44+(1 if sign==1 else -1)))
        pb = rig.pose.bones[side+'UpperArm']
        axis = rest[pb.name].to_quaternion().inverted() @ Vector((1,0,0))
        pb.rotation_quaternion = Quaternion(axis,math.radians(-24*swing if walk else (2 if sign==1 else -2)+.7*math.sin(angle+.3))) @ pb.rotation_quaternion
        rotate(side+'LowerArm',(0,0,1), -sign*(28+8*swing if walk else (9 if sign==1 else 13)))
        for finger in ['Index','Middle','Ring','Little']:
            for segment in ['Proximal','Intermediate','Distal']:
                rotate(side+finger+segment,(0,1,0),sign*14)
        for region in ['Front','Back']:
            for column in ['Left','Center','Right']:
                for part in ['Upper','Lower']:
                    name = 'Cape'+region+column+part
                    rotate(name,(1,0,0), (4*math.sin(angle-.5) if walk else .5*math.sin(angle-.4)))
    bpy.context.view_layer.update()
    for side, sign in [('Left',1),('Right',-1)]:
        t = (phase+(0 if sign==1 else .5)) % 1
        y, lift, pitch = (-.012 if sign==1 else .038), 0, 0
        if walk:
            y = .015
            if t < STANCE:
                y += -TRAVEL/2+TRAVEL*t/STANCE
            else:
                u = (t-STANCE)/(1-STANCE)
                smooth = u*u*(3-2*u)
                y += TRAVEL/2-TRAVEL*smooth
                lift = .145*math.sin(math.pi*u)**2
                pitch = -18*math.sin(2*math.pi*u)
        solve_leg(side,Vector((sign*(.14 if walk else .15),y,.095+lift)),pitch)

report = {}
actions = []
for name, frames in [('Idle',120),('Walk',WALK_FRAMES)]:
    rig.animation_data_create()
    action = bpy.data.actions.new(name)
    rig.animation_data.action = action
    action.use_fake_user = True
    scene.frame_start, scene.frame_end = 1, frames+1
    previous = {}
    start = None
    for frame in range(1,frames+2):
        scene.frame_set(frame)
        pose(name,(frame-1)/frames)
        for pb in rig.pose.bones:
            q = pb.rotation_quaternion.copy()
            if pb.name in previous and q.dot(previous[pb.name]) < 0:
                q.negate()
                pb.rotation_quaternion = q
            previous[pb.name] = q
            pb.keyframe_insert('location',frame=frame,group=pb.name)
            pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name)
        mesh = body.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
        points = [v.co.copy() for v in mesh.vertices]
        assert all(math.isfinite(c) for p in points for c in p)
        if start is None: start = points
        if frame == frames+1:
            seam = max((a-b).length for a,b in zip(start,points))
            assert seam < .0001, (name,seam)
        body.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(ROOT / ('Assets/Art/Characters/ANI_Zoey_'+name+'.fbx')),
        use_selection=True, object_types={'ARMATURE','MESH'}, add_leaf_bones=False,
        axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',
        use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,mesh_smooth_type='FACE')
    report[name] = dict(frames=frames+1,seconds=frames/30,seam_meters=seam,root_motion=False,
                        reference_speed=MOVE_SPEED if name=='Walk' else 0)
    actions.append(action)

rig.animation_data.action = actions[0]
scene.frame_start,scene.frame_end = 1,121
scene.frame_set(1)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Tools/Zoey_Motion.blend'))
bpy.ops.object.mode_set(mode='OBJECT')

# Render contact/passing poses from the actual exported rig, without saving preview objects.
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.world = bpy.data.worlds.new('MotionPreview')
scene.world.color = (.16,.16,.16)
scene.render.resolution_x = 700
scene.render.resolution_y = 850
scene.render.resolution_percentage = 100
bpy.ops.object.camera_add(location=(3,-5,1.5))
cam = bpy.context.object
cam.rotation_euler = (Vector((0,0,.85))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 2.1
scene.camera = cam
for action in actions:
    rig.animation_data.action = action
    for frame in ([1,31] if action.name=='Idle' else [1,5,10,14]):
        scene.frame_set(frame)
        scene.render.filepath = str(ROOT/('Logs/Zoey_'+action.name+'_'+str(frame)+'.png'))
        bpy.ops.render.render(write_still=True)
(ROOT/'Logs/Zoey_MotionReport.json').write_text(json.dumps(report,indent=2))
print('ZOEY_MOTION_OK',report)
