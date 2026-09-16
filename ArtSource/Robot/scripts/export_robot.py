"""Export only RobotRoot's hierarchy, sampling its mechanical transforms per frame."""
from pathlib import Path
import json
import bpy
import bmesh
from mathutils import Matrix

BASE = Path(__file__).resolve().parents[1]
PROJECT = BASE.parents[1]


def export(filepath=None):
    scene=bpy.context.scene
    root=scene.objects.get('RobotRoot')
    if not root or root.get('owner') != 'robot-asset-v1':
        raise RuntimeError('Select the owned Robot_Workshop scene before export')
    selected=list(bpy.context.selected_objects)
    active=bpy.context.view_layer.objects.active
    old_frame=scene.frame_current
    path=Path(filepath) if filepath else PROJECT/'Assets/Art/Robot/Meshes/Robot.fbx'
    path.parent.mkdir(parents=True,exist_ok=True)
    settings=dict(use_selection=True,object_types={'MESH','EMPTY'},global_scale=1.0,
                  apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
                  axis_forward='-Z',axis_up='Y',use_space_transform=False,bake_space_transform=False,
                  use_mesh_modifiers=True,use_triangles=True,add_leaf_bones=False,
                  bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,
                  bake_anim_force_startend_keying=True,bake_anim_step=1.0,bake_anim_simplify_factor=0.0,
                  path_mode='RELATIVE',embed_textures=False)
    props=bpy.ops.export_scene.fbx.get_rna_type().properties
    for key,value in settings.items():
        if key not in props:
            raise RuntimeError(f'Exporter missing required option {key}')
        if props[key].type=='ENUM':
            valid={i.identifier for i in props[key].enum_items}
            requested=value if isinstance(value,set) else {value}
            if not requested<=valid:
                raise RuntimeError(f'Exporter does not support {key}={value}')
    # Author a coherent Y-up FBX copy. Unity 2022.3's verified reader reflects Z
    # for this FBX axis metadata, so raw coordinates are (x, source.z, source.y).
    # Conjugate EVERY local transform (including animated pivots), transform mesh
    # vertices once, and reverse winding. This avoids exporter bake-space bugs
    # on parented object animation and leaves RobotRoot identity after import.
    basis=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
    inverse=basis.inverted()
    originals=[root]+list(root.children_recursive)
    names={obj:obj.name for obj in originals}
    copies={}
    temporary=bpy.data.scenes.new('Robot_Export_Temporary')
    temporary.render.fps=30; temporary.frame_start=0; temporary.frame_end=128
    temporary.unit_settings.scale_length=1
    try:
        scene.frame_set(0)
        for obj,name in names.items():
            obj.name='__RobotSource__'+name
        for obj in originals:
            clone=obj.copy(); clone.name=names[obj]; clone.animation_data_clear()
            if obj.type=='MESH':
                clone.data=obj.data.copy(); clone.data.transform(basis)
                bm=bmesh.new(); bm.from_mesh(clone.data)
                bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
                bm.to_mesh(clone.data); bm.free()
            temporary.collection.objects.link(clone)
            copies[obj]=clone
        for obj,clone in copies.items():
            clone.parent=copies.get(obj.parent)
            clone.matrix_parent_inverse=Matrix.Identity(4)
            clone.matrix_local=basis@obj.matrix_local@inverse
        prior={}
        for frame in range(129):
            scene.frame_set(frame)
            for obj,clone in copies.items():
                if not obj.animation_data or not obj.animation_data.action:
                    continue
                local=basis@obj.matrix_local@inverse
                clone.location=local.to_translation()
                rotation=local.to_euler('XYZ',prior[obj]) if obj in prior else local.to_euler('XYZ')
                clone.rotation_mode='XYZ'; clone.rotation_euler=rotation; prior[obj]=rotation.copy()
                clone.scale=local.to_scale()
                for prop in ('location','rotation_euler','scale'):
                    clone.keyframe_insert(prop,frame=frame)
        for clone in copies.values():
            if clone.animation_data:
                for layer in clone.animation_data.action.layers:
                    for strip in layer.strips:
                        bag=strip.channelbag(clone.animation_data.action_slot)
                        for curve in bag.fcurves:
                            for key in curve.keyframe_points:
                                key.interpolation='LINEAR'
        bpy.context.window.scene=temporary
        temporary.frame_set(0)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in copies.values():
            if obj.type not in ('MESH','EMPTY'):
                raise RuntimeError(f'Unexpected export object: {obj.name}')
            obj.select_set(True)
        bpy.context.view_layer.objects.active=copies[root]
        bpy.ops.export_scene.fbx(filepath=str(path),**settings)
        serial={k:sorted(v) if isinstance(v,set) else v for k,v in settings.items()}
        serial['rawFbxFromBlender']=[list(row) for row in basis]
        serial['axisHandling']='Explicit per-node/mesh conversion; Unity bakeAxisConversion=true'
        (BASE/'export_settings.json').write_text(json.dumps(serial,indent=2)+'\n',encoding='utf-8')
        print(f'Exported {path}')
    finally:
        bpy.context.window.scene=scene
        for clone in copies.values():
            mesh=clone.data if clone.type=='MESH' else None
            action=clone.animation_data.action if clone.animation_data else None
            bpy.data.objects.remove(clone,do_unlink=True)
            if mesh and mesh.users==0: bpy.data.meshes.remove(mesh)
            if action and action.users==0: bpy.data.actions.remove(action)
        bpy.data.scenes.remove(temporary)
        for obj,name in names.items(): obj.name=name
        bpy.ops.object.select_all(action='DESELECT')
        for obj in selected:
            obj.select_set(True)
        bpy.context.view_layer.objects.active=active
        scene.frame_set(old_frame)
    return path


if __name__=='__main__':
    export()
