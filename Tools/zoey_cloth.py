"""Separate the v02 shoulder cape for Unity Cloth, preserving the original rig file."""
import bpy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Tools/Zoey_Rig.blend'))
if bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
body = bpy.data.objects['Zoey_Body']
rig = bpy.data.objects['Zoey_Rig']
adjacent = [set() for _ in body.data.vertices]
for edge in body.data.edges:
    a,b = edge.vertices
    adjacent[a].add(b)
    adjacent[b].add(a)
islands, seen = [], set()
for first in range(len(adjacent)):
    if first in seen: continue
    pending, ids = [first], []
    seen.add(first)
    while pending:
        i = pending.pop()
        ids.append(i)
        for other in adjacent[i]:
            if other not in seen:
                pending.append(other)
                seen.add(other)
    islands.append(ids)
assert len(islands)==127 and len(islands[84])==716, 'Review source topology before separating cape'
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
bpy.context.view_layer.objects.active = body
selected = set(islands[84])
for v in body.data.vertices: v.select = v.index in selected
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.separate(type='SELECTED')
bpy.ops.object.mode_set(mode='OBJECT')
cape = next(o for o in bpy.context.selected_objects if o != body)
cape.name = 'Zoey_Cape'
cape.data.name = 'Zoey_Cape'
# Cloth constraints reference the animated chest, not the old cape animation chains.
cape.vertex_groups.clear()
cape.vertex_groups.new(name='UpperChest').add(list(range(len(cape.data.vertices))),1,'REPLACE')
for obj in (body,cape,rig): obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(ROOT/'Assets/Art/Characters/Zoey_Body_Cloth.fbx'),
    use_selection=True, object_types={'ARMATURE','MESH'}, add_leaf_bones=False,
    axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',
    use_armature_deform_only=True,bake_anim=False,mesh_smooth_type='FACE')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Tools/Zoey_Cloth.blend'))
print('ZOEY_CLOTH_SPLIT_OK',len(body.data.vertices),len(cape.data.vertices))
