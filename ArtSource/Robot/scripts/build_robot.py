"""Rebuild only the robot-asset-v1 owned scene. Run in Blender or via MCP.

Stage A: build(stage='blockout'); final: build(). No external art dependencies.
All coordinates in this script are Blender metres (+Z up, -Y forward).
"""
from pathlib import Path
import math
import bpy
import bmesh
from mathutils import Vector

BASE = Path(__file__).resolve().parents[1]
OWNER = 'robot-asset-v1'
COLORS = {
    'M_RobotBody': ('D9DFE5', .1, .55),
    'M_RobotDark': ('202A35', .2, .65),
    'M_RobotAccent': ('F29D38', 0, .5),
    'M_RobotEmission': ('63DCEB', 0, .4),
}


def enum_set(obj, prop, value):
    allowed = [i.identifier for i in obj.bl_rna.properties[prop].enum_items]
    if value not in allowed:
        raise ValueError(f'{prop}: {value} unavailable in {allowed}')
    setattr(obj, prop, value)


def linear_color(hex_color):
    values = [int(hex_color[i:i+2], 16)/255 for i in (0, 2, 4)]
    return tuple(v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in values) + (1,)


def material(name, spec):
    mat = bpy.data.materials.get(name)
    if mat and mat.get('owner') != OWNER:
        raise RuntimeError(f'Refusing to replace unrelated material {name}')
    mat = mat or bpy.data.materials.new(name)
    mat['owner'] = OWNER
    mat.use_nodes = True
    node = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    color, metal, rough = spec
    rgba = linear_color(color)
    mat.diffuse_color = rgba
    node.inputs['Base Color'].default_value = rgba
    node.inputs['Metallic'].default_value = metal
    node.inputs['Roughness'].default_value = rough
    if name == 'M_RobotEmission':
        node.inputs['Emission Color'].default_value = rgba
        node.inputs['Emission Strength'].default_value = 2
    return mat


def setup_scene():
    existing = bpy.data.scenes.get('Robot_Workshop')
    if existing and existing.get('owner') != OWNER:
        raise RuntimeError('Robot_Workshop belongs to another task')
    # Remove only explicitly tagged objects; refuse collisions rather than guessing ownership.
    for obj in list(bpy.data.objects):
        if obj.get('owner') == OWNER:
            bpy.data.objects.remove(obj, do_unlink=True)
    for col in list(bpy.data.collections):
        if col.get('owner') == OWNER:
            bpy.data.collections.remove(col)
    scene = existing or bpy.data.scenes.new('Robot_Workshop')
    scene['owner'] = OWNER
    bpy.context.window.scene = scene
    cols = []
    for name in ('Robot_Asset', 'Robot_Reference'):
        if bpy.data.collections.get(name):
            raise RuntimeError(f'Unrelated collection: {name}')
        col = bpy.data.collections.new(name)
        col['owner'] = OWNER
        scene.collection.children.link(col)
        cols.append(col)
    enum_set(scene.unit_settings, 'system', 'METRIC')
    scene.unit_settings.scale_length = 1
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = 0, 128
    scene.timeline_markers.clear()
    for name, frame in [('Idle',0),('Idle end',60),('Move',70),('Move end',100),('Push',110),('Contact',113),('Release',125),('Push end',128)]:
        scene.timeline_markers.new(name, frame=frame)
    return scene, cols[0], cols[1]


def link(obj, col):
    obj['owner'] = OWNER
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    col.objects.link(obj)
    return obj


def parent_world(obj, parent):
    bpy.context.view_layer.update()
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world
    return obj


def empty(name, location, col, parent=None):
    if bpy.data.objects.get(name):
        raise RuntimeError(f'Object name collision: {name}')
    obj = bpy.data.objects.new(name, None)
    link(obj, col)
    obj.location = location
    obj.empty_display_size = .04
    if parent:
        parent_world(obj, parent)
    return obj


def finish(obj, name, mat, col, bevel=0, segments=2):
    obj.name = name
    link(obj, col)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Machined corners', 'BEVEL')
        mod.width, mod.segments = bevel, segments
        bpy.ops.object.modifier_apply(modifier=mod.name)
        # Keep planar faces flat and curved corner faces smooth.
        for face in obj.data.polygons:
            face.use_smooth = face.area < .0006
        normal = obj.modifiers.new('Face weighted normals', 'WEIGHTED_NORMAL')
        normal.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=normal.name)
    return obj


def box(name, loc, size, mat, col, bevel=.008, segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.dimensions = size
    return finish(obj, name, mat, col, min(bevel,min(size)*.45), segments)


def cylinder(name, loc, radius, depth, mat, col, axis='X', vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    obj.rotation_euler = (0, math.pi/2, 0) if axis == 'X' else (math.pi/2,0,0) if axis == 'Y' else (0,0,0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(obj, name, mat, col)


def join(parts, name):
    if len(parts)==1:
        parts[0].name=name
        return parts[0]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    parts[0].name = name
    return parts[0]


def track(name, x, mat, col, detail):
    # Closed extruded capsule ring, with a true open side window.
    loops = []
    for radius in (.15, .121):
        contour = []
        for center_y, start in ((-.17, math.pi/2),(.17,-math.pi/2)):
            for i in range(13):
                angle = start + math.pi*i/12
                contour.append((center_y + radius*math.cos(angle), .15+radius*math.sin(angle)))
        loops.append(contour)
    n = len(loops[0])
    verts = [(xx, y, z) for xx in (x-.07,x+.07) for contour in loops for y,z in contour]
    faces = []
    for i in range(n):
        j = (i+1)%n
        faces.extend([(i,j,2*n+j,2*n+i),(n+j,n+i,3*n+i,3*n+j),
                      (j,i,n+i,n+j),(2*n+i,2*n+j,3*n+j,3*n+i)])
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name,mesh)
    link(obj,col)
    mesh.materials.append(mat)
    parts = [obj]
    if detail:
        # Treads stay inside the specified 0.70 x 0.68 x 0.80 envelope.
        for y in [-.16,-.08,0,.08,.16]:
            for z in (.007,.293):
                parts.append(box('Tread', (x,y,z),(.14,.032,.014),mat,col,0))
        for cy, sign in ((-.17,-1),(.17,1)):
            for angle in [-60,-30,0,30,60]:
                a=math.radians(angle)
                block=box('Tread',(x,cy+sign*.144*math.cos(a),.15+.144*math.sin(a)),(.14,.013,.028),mat,col,0)
                block.rotation_euler.x=sign*a
                parts.append(block)
    return join(parts,name)


def build(stage='final'):
    scene, asset, reference = setup_scene()
    detail = stage != 'blockout'
    mats = {n:material(n,s) for n,s in COLORS.items()}
    white,dark,orange,glow = [mats[n] for n in COLORS]
    if not detail:
        dark=orange=glow=white
    root=empty('RobotRoot',(0,0,0),asset)
    body_p=empty('BodyPivot',(0,0,.30),asset,root)
    head_p=empty('HeadYaw',(0,0,.56),asset,body_p)
    slide=empty('PushSlide',(0,-.34,.40),asset,root)
    empty('PushContact',(0,-.36,.40),asset,slide)
    empty('EmotionAnchor',(0,0,1.05),asset,root)
    parent_world(box('Chassis',(0,0,.24),(.50,.50,.16),dark,asset,.018),root)
    for side,x in [('L',-.28),('R',.28)]:
        parent_world(track('Track_'+side,x,dark,asset,detail),root)
        sign=-1 if x<0 else 1
        for pos,y in [('F',-.17),('B',.17)]:
            pivot=empty(f'Wheel_{side}{pos}_Pivot',(x,y,.15),asset,root)
            parts=[cylinder('WheelBase',(x,y,.15),.114,.123,dark,asset)]
            parts.append(cylinder('Rim',(x+sign*.062,y,.15),.094,.006,white,asset))
            parts.append(cylinder('Hub',(x+sign*.067,y,.15),.040,.004,dark,asset))
            if detail:
                for angle in (0,120,240):
                    a=math.radians(angle)
                    spoke=box('Spoke',(x+sign*.067,y+.055*math.cos(a),.15+.055*math.sin(a)),(.004,.063,.015),dark,asset,0)
                    spoke.rotation_euler.x=a
                    parts.append(spoke)
                parts.append(box('WheelIndex',(x+sign*.069,y+.070,.15),(.002,.026,.027),orange,asset,0))
            parent_world(join(parts,f'Wheel_{side}{pos}'),pivot)
    parts=[box('Body',(0,0,.43),(.46,.40,.27),white,asset,.021,3)]
    if detail:
        for x in (-.20,.20):
            parts.append(box('ServiceRail',(x,0,.56),(.028,.27,.014),orange,asset,.004))
        parts.append(box('RearAccess',(0,.199,.432),(.31,.012,.15),dark,asset,.008))
        for i in range(5):
            parts.append(box('Vent',(0,.207,.387+i*.022),(.21,.006,.008),white,asset,0))
        parts.append(box('RearBadge',(.166,.205,.442),(.018,.01,.058),orange,asset,.003))
    parent_world(join(parts,'Body'),body_p)
    parts=[box('Head',(0,0,.68),(.42,.24,.22),white,asset,.027,3)]
    parts.append(cylinder('Neck',(0,0,.565),.068,.024,dark,asset,axis='Z'))
    parts.append(box('RoofStripe',(0,0,.793),(.105,.18,.014),orange,asset,.004))
    if detail:
        parts.append(box('HeadRear',(0,.119,.68),(.23,.013,.10),dark,asset,.009))
        parts.append(box('HeadRearMark',(0,.128,.68),(.072,.007,.017),orange,asset,.004))
    parent_world(join(parts,'Head'),head_p)
    parent_world(box('Screen',(0,-.120,.687),(.352,.022,.142),dark,asset,.017,3),head_p)
    for side,x in [('L',-.079),('R',.079)]:
        parent_world(box('Eye_'+side,(x,-.134,.69),(.049,.009,.067),glow,asset,.008,3),head_p)
    parts=[box('PusherHousing',(0,-.175,.40),(.35,.25,.105),dark,asset,.012)]
    if detail:
        parts.append(box('HousingBand',(0,-.274,.453),(.27,.030,.010),orange,asset,.003))
    parent_world(join(parts,'PusherHousing'),root)
    parts=[cylinder('Stem',(x,-.15,.40),.019,.38,white,asset,axis='Y',vertices=16) for x in (-.125,.125)]
    parent_world(join(parts,'PusherStem'),slide)
    parts=[box('PushPlate',(0,-.339,.40),(.46,.038,.20),dark,asset,.009,3)]
    # Contact face is exactly -Y=.36 even with a beveled border.
    parts.append(box('ContactPad',(0,-.355,.40),(.407,.01,.148),white,asset,.004))
    if detail:
        for x in (-.2175,.2175):
            parts.append(box('PlateMark',(x,-.3575,.40),(.012,.002,.105),orange,asset,.0005))
    parent_world(join(parts,'PushPlate'),slide)
    # Recalculate normals after joining tread solids.
    for obj in asset.objects:
        if obj.type=='MESH':
            bm=bmesh.new(); bm.from_mesh(obj.data)
            bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
            bm.to_mesh(obj.data); bm.free()
    setup_reference(scene,reference,mats)
    animate(scene)
    scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT')
    BASE.mkdir(parents=True,exist_ok=True)
    (BASE/'previews').mkdir(exist_ok=True)
    filename='Robot_blockout.blend' if not detail else 'Robot.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(BASE/filename))
    print(f'Built {stage}: {len(asset.objects)} objects in {scene.name}')


def smoothstep(t):
    t=max(0,min(1,t))
    return t*t*(3-2*t)


def animate(scene):
    body=bpy.data.objects['BodyPivot']; head=bpy.data.objects['HeadYaw']; slide=bpy.data.objects['PushSlide']
    wheels=[bpy.data.objects[f'Wheel_{s}{p}_Pivot'] for s in ('L','R') for p in ('F','B')]
    moving=[body,head,slide]+wheels
    for obj in moving:
        obj.animation_data_clear()
        enum_set(obj,'rotation_mode','XYZ')
    # Dense Euler authoring deliberately retains the full wheel revolution.
    # Every frame has explicit neutral values, including isolation ranges.
    for frame in range(129):
        yaw=lean=stroke=angle=0
        if frame<=60:
            # Zero velocity at both clip boundaries, repeatable without a snap.
            segment=min(frame//15,3); t=(frame-segment*15)/15
            values=(0,-6,0,6,0)
            yaw=values[segment]+(values[segment+1]-values[segment])*smoothstep(t)
        elif 70<=frame<=100:
            lean=3; angle=2*math.pi*(frame-70)/30
        elif 110<=frame<=128:
            factor=smoothstep((frame-110)/3) if frame<113 else 1 if frame<=125 else 1-smoothstep((frame-125)/3)
            stroke=.24*factor; lean=3*factor
            angle=2*math.pi*max(0,min(1,(frame-113)/12))
        head.rotation_euler=(0,0,math.radians(yaw))
        body.rotation_euler=(math.radians(lean),0,0)
        slide.location=(0,-.34-stroke,.40)
        head.keyframe_insert('rotation_euler',frame=frame)
        body.keyframe_insert('rotation_euler',frame=frame)
        slide.keyframe_insert('location',frame=frame)
        for wheel in wheels:
            wheel.rotation_euler=(angle,0,0)
            wheel.keyframe_insert('rotation_euler',frame=frame)
    for obj in moving:
        action=obj.animation_data.action
        action.name='RobotTimeline_'+obj.name
        # Blender 4.4+ slots/layers; avoid removed Action.fcurves API.
        for layer in action.layers:
            for strip in layer.strips:
                bag=strip.channelbag(obj.animation_data.action_slot)
                if bag:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            enum_set(key,'interpolation','LINEAR')


def setup_reference(scene,col,mats):
    dark=mats['M_RobotDark']; accent=mats['M_RobotAccent']
    ground=box('Reference_Ground',(0,0,-.025),(200,200,.05),mats['M_RobotBody'],col,0)
    cube=box('Reference_EnergyBox',(0,-1,.4),(.8,.8,.8),accent,col,.015)
    cube.hide_render=True
    cube.display_type='WIRE'
    cube.hide_set(True)
    # Metre grid, hidden from clean product renders.
    grid=[]
    for i in range(-2,3):
        grid.append(box('GridLine',(i,0,.001),(.003,4,.001),dark,col,0))
        grid.append(box('GridLine',(0,i,.001),(4,.003,.001),dark,col,0))
    grid_obj=join(grid,'Reference_1mGrid'); grid_obj.hide_render=True; grid_obj.hide_set(True)
    arrow=empty('Reference_FRONT',(0,-.75,.02),col)
    enum_set(arrow,'empty_display_type','SINGLE_ARROW')
    arrow.rotation_euler.x=math.pi/2; arrow.empty_display_size=.3
    arrow.hide_set(True)
    curve=bpy.data.curves.new('FRONT_label','FONT'); curve.body='FRONT'; curve.size=.1
    text=bpy.data.objects.new('Reference_FRONT_Label',curve); link(text,col)
    text.location=(-.18,-.78,.008); text.hide_render=True; text.hide_set(True)
    camera_data=bpy.data.cameras.new('RobotPreviewCamera')
    camera=bpy.data.objects.new('RobotPreviewCamera',camera_data); link(camera,col)
    enum_set(camera_data,'type','ORTHO'); camera_data.ortho_scale=1.65
    scene.camera=camera
    set_camera(scene,(1.3,-1.9,1.25))
    for name,loc,power,size in [('Key',(-2,-3,4),420,4),('Fill',(3,-1,2),220,3),('Rim',(0,3,3),400,2)]:
        data=bpy.data.lights.new('Robot_'+name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
        obj=bpy.data.objects.new('Robot_'+name,data); link(obj,col); obj.location=loc
        obj.rotation_euler=(Vector((0,0,.4))-obj.location).to_track_quat('-Z','Y').to_euler()
    world=bpy.data.worlds.new('Robot_Studio'); world['owner']=OWNER; world.use_nodes=True
    background=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
    background.inputs['Color'].default_value=(.18,.22,.28,1)
    background.inputs['Strength'].default_value=.35
    scene.world=world
    try:
        scene.render.engine='BLENDER_EEVEE'
    except TypeError as error:
        raise RuntimeError(f'EEVEE unavailable: {error}')
    scene.render.resolution_x=800; scene.render.resolution_y=800; scene.render.resolution_percentage=100
    enum_set(scene.render.image_settings,'file_format','PNG')
    # OCIO transforms are dynamic; retain the scene's valid default.
    scene['previewViewTransform'] = scene.view_settings.view_transform
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=1.7
            area.spaces.active.region_3d.view_location=(0,0,.4)
            area.spaces.active.region_3d.view_rotation=scene.camera.rotation_euler.to_quaternion()
            area.spaces.active.shading.color_type='MATERIAL'
            area.spaces.active.overlay.show_extras=False


def set_camera(scene,loc,target=(0,0,.4),scale=1.4):
    scene.camera.location=loc
    scene.camera.rotation_euler=(Vector(target)-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    scene.camera.data.ortho_scale=scale


if __name__=='__main__':
    build()
