"""Measured source / FBX regression checks. Throws if any contract check fails.

blender -b Robot.blend -P scripts/validate_robot.py
blender -b --factory-startup -P scripts/validate_robot.py -- --roundtrip
"""
from pathlib import Path
import json
import math
import sys
import bpy
import bmesh
import hashlib
from mathutils import Vector, Matrix

BASE=Path(__file__).resolve().parents[1]
PROJECT=BASE.parents[1]
WHEELS=[f'Wheel_{s}{p}_Pivot' for s in ('L','R') for p in ('F','B')]
MOVING=['HeadYaw','BodyPivot','PushSlide']+WHEELS
EXPECTED_PARENTS={
    'Chassis':'RobotRoot','Track_L':'RobotRoot','Track_R':'RobotRoot',
    'BodyPivot':'RobotRoot','Body':'BodyPivot','HeadYaw':'BodyPivot',
    'Head':'HeadYaw','Screen':'HeadYaw','Eye_L':'HeadYaw','Eye_R':'HeadYaw',
    'PushSlide':'RobotRoot','PushPlate':'PushSlide','PusherStem':'PushSlide',
    'PushContact':'PushSlide','PusherHousing':'RobotRoot','EmotionAnchor':'RobotRoot',
    **{name:'RobotRoot' for name in WHEELS},
    **{name.replace('_Pivot',''):name for name in WHEELS},
}


def unity(v):
    return [v.x,v.z,-v.y]


def bounds(objects):
    pts=[obj.matrix_world@v.co for obj in objects if obj.type=='MESH' for v in obj.data.vertices]
    lo=Vector([min(v[i] for v in pts) for i in range(3)])
    hi=Vector([max(v[i] for v in pts) for i in range(3)])
    return lo,hi


def close(a,b,tol=1e-5):
    return max(abs(x-y) for x,y in zip(a,b))<tol


def matrix_error(a,b):
    return max(abs(a[i][j]-b[i][j]) for i in range(4) for j in range(4))


def validate(roundtrip=False, output=None):
    scene=bpy.context.scene
    root=scene.objects.get('RobotRoot')
    assert root, 'Missing RobotRoot'
    objects=[root]+list(root.children_recursive)
    meshes=[obj for obj in objects if obj.type=='MESH']
    checks=[]
    def check(condition,label):
        checks.append({'name':label,'passed':bool(condition)})
        return condition
    for name,parent in EXPECTED_PARENTS.items():
        obj=scene.objects.get(name)
        check(obj is not None and obj.parent is not None and obj.parent.name==parent,'hierarchy/'+name)
    scene.frame_set(0)
    lo,hi=bounds(meshes)
    size=hi-lo
    check(.68<=size.x<=.72 and .66<=size.y<=.70 and .78<=size.z<=.82,'rest_dimensions')
    check(abs(lo.z)<=.005,'ground_contact')
    check(all(obj.type in ('MESH','EMPTY') for obj in objects),'export_types')
    check(all(min(obj.scale)>0 for obj in objects),'positive_scales')
    check(all(not obj.hide_render for obj in meshes),'all_meshes_visible')
    materials=sorted({m.name for obj in meshes for m in obj.data.materials})
    check(len(materials)<=4,'material_budget')
    triangles=0; degenerate=0; duplicate=0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles+=len(obj.data.loop_triangles)
        degenerate+=sum(t.area<1e-12 for t in obj.data.loop_triangles)
        faces=[tuple(sorted(tuple(round(float(x),7) for x in obj.data.vertices[i].co) for i in p.vertices)) for p in obj.data.polygons]
        duplicate+=len(faces)-len(set(faces))
    check(triangles<=10000,'triangle_budget')
    check(len(meshes)<=25,'mesh_budget')
    check(degenerate==0 and duplicate==0,'nondegenerate_unique_faces')
    root_m=root.matrix_world.copy()
    check(matrix_error(root_m,Matrix.Identity(4))<1e-4,'root_origin_unit_axes')
    contact=scene.objects['PushContact']; plate=scene.objects['PushPlate']
    contacts=[]; root_errors=[]; anchor_errors=[]; face_errors=[]
    poses={}; wheel_steps={name:[] for name in WHEELS}
    for frame in range(129):
        scene.frame_set(frame)
        root_errors.append(matrix_error(root.matrix_world,root_m))
        anchor_errors.append((scene.objects['EmotionAnchor'].matrix_world.translation-Vector((0,0,1.05))).length)
        if 113<=frame<=125:
            point=contact.matrix_world.translation.copy()
            contacts.append(unity(point))
            plo,phi=bounds([plate])
            face_errors.append(abs(plo.y+.60))
        if frame in (0,60,70,100,110,113,125,128):
            poses[frame]={name:scene.objects[name].matrix_local.copy() for name in MOVING}
        if frame in (0,70,100,110,128):
            check(close(contact.matrix_world.translation,(0,-.36,.40),1e-4),f'retracted_contact/{frame}')
        if 70<=frame<=100:
            for name in WHEELS:
                wheel_steps[name].append(scene.objects[name].matrix_local.to_quaternion().copy())
    check(max(root_errors)<1e-5,'root_static_all_129_frames')
    check(max(anchor_errors)<1e-4,'emotion_stable_all_129_frames')
    check(all(close(v,(0,.4,.6),1e-4) for v in contacts),'push_contact_all_hold_frames')
    check(max(face_errors)<1e-4,'physical_push_face_all_hold_frames')
    for start,end,label in ((0,60,'Idle'),(70,100,'Move'),(0,128,'Push_returns_idle')):
        check(max(matrix_error(poses[start][name],poses[end][name]) for name in MOVING)<1e-4,'loop_pose/'+label)
    for name,qs in wheel_steps.items():
        steps=[min(a.rotation_difference(b).angle,2*math.pi-a.rotation_difference(b).angle) for a,b in zip(qs,qs[1:])]
        check(abs(sum(steps)-2*math.pi)<.003 and min(steps)>.1,'move_full_visible_revolution/'+name)
    # Validate Push wheel turn independently of Move and moving reference contact.
    moving_contact_error=0
    for name in WHEELS:
        qs=[]
        for frame in range(113,126):
            scene.frame_set(frame)
            qs.append(scene.objects[name].matrix_local.to_quaternion().copy())
            t=(frame-113)/12; progress=t*t*(3-2*t)
            world_face=contact.matrix_world.translation+Vector((0,-progress,0))
            box_back=Vector((0,-(.60+progress),.4))
            moving_contact_error=max(moving_contact_error,(world_face-box_back).length)
        total=sum(min(a.rotation_difference(b).angle,2*math.pi-a.rotation_difference(b).angle) for a,b in zip(qs,qs[1:]))
        check(abs(total-2*math.pi)<.003,'push_full_revolution/'+name)
    check(moving_contact_error<=.01,'synchronized_one_metre_push')
    frame_ranges={}
    for name in MOVING:
        animation=scene.objects[name].animation_data
        check(bool(animation and animation.action),'animation_present/'+name)
        if animation and animation.action:
            frame_ranges[name]=list(animation.action.curve_frame_range)
    check(all(close(r,(0,128),.01) for r in frame_ranges.values()),'animation_range_0_128')
    if roundtrip:
        check(all(close(r,(0,128),.01) for r in RAW_RANGES.values()) and len(RAW_RANGES)==len(objects),'raw_fbx_animation_ranges')
    report={'kind':'fbx-roundtrip' if roundtrip else 'blender-source','blenderVersion':bpy.app.version_string,
            'passed':all(c['passed'] for c in checks),'checks':checks,
            'metrics':{'dimensionsUnityXYZ':unity(Vector((size.x,-size.y,size.z))),
                       'boundsBlenderMin':list(lo),'boundsBlenderMax':list(hi),
                       'triangles':triangles,'meshObjects':len(meshes),'totalExportNodes':len(objects),
                       'materials':materials,'degenerateTriangles':degenerate,'duplicateFaces':duplicate,
                       'maxRootMatrixError':max(root_errors),'maxEmotionErrorMeters':max(anchor_errors),
                       'maxPushFaceErrorMeters':max(face_errors),'oneMetrePushContactError':moving_contact_error,
                       'holdContactSamplesUnity':contacts,'animationFrameRanges':frame_ranges}}
    artifact=PROJECT/'Assets/Art/Robot/Meshes/Robot.fbx' if roundtrip else Path(bpy.data.filepath)
    report['artifactSha256']=hashlib.sha256(artifact.read_bytes()).hexdigest()
    if roundtrip:
        report['rawFbxFrameRanges']=RAW_RANGES
        report['measurementBasis']='Raw FBX read with manual Y-forward/Z-up (identity), then inverse rawFbxFromBlender applied to all local transforms and mesh vertices.'
    scene.frame_set(0)
    path=Path(output) if output else BASE/('fbx_validation.json' if roundtrip else 'blender_validation.json')
    path.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'passed':report['passed'],'metrics':report['metrics'],'failures':[c for c in checks if not c['passed']]}))
    assert report['passed'], 'Robot contract validation failed; see '+str(path)
    return report


def import_roundtrip():
    """Read the canonical raw FBX basis, then express measurements in source axes.

    This explicit fixed basis conversion is independent of any pose/dimension
    result. No measured value is adjusted to fit the contract.
    """
    assert bpy.data.objects.get('RobotRoot') is None
    scene=bpy.context.scene
    scene.render.fps=30
    bpy.ops.import_scene.fbx(filepath=str(PROJECT/'Assets/Art/Robot/Meshes/Robot.fbx'),anim_offset=0,
                             use_manual_orientation=True,axis_forward='Y',axis_up='Z')
    root=scene.objects['RobotRoot']; objects=[root]+list(root.children_recursive)
    global RAW_RANGES
    RAW_RANGES={obj.name:list(obj.animation_data.action.curve_frame_range) for obj in objects if obj.animation_data and obj.animation_data.action}
    settings=json.loads((BASE/'export_settings.json').read_text())
    basis=Matrix(settings['rawFbxFromBlender']); inverse=basis.inverted()
    frames={}
    for frame in range(129):
        scene.frame_set(frame)
        frames[frame]={obj:inverse@obj.matrix_local@basis for obj in objects}
    for obj in objects:
        obj.animation_data_clear()
        obj.matrix_parent_inverse=Matrix.Identity(4)
        if obj.type=='MESH':
            obj.data.transform(inverse)
            if inverse.determinant()<0:
                bm=bmesh.new(); bm.from_mesh(obj.data)
                bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
                bm.to_mesh(obj.data); bm.free()
    previous={}
    for frame,matrices in frames.items():
        for obj,local in matrices.items():
            obj.location=local.to_translation(); obj.scale=local.to_scale()
            obj.rotation_mode='XYZ'
            euler=local.to_euler('XYZ',previous[obj]) if obj in previous else local.to_euler('XYZ')
            obj.rotation_euler=euler; previous[obj]=euler.copy()
            for prop in ('location','rotation_euler','scale'):
                obj.keyframe_insert(prop,frame=frame)
    scene.frame_set(0)


if __name__=='__main__':
    roundtrip='--roundtrip' in sys.argv
    if roundtrip:
        import_roundtrip()
    else:
        bpy.context.window.scene=bpy.data.scenes['Robot_Workshop']
    validate(roundtrip)
