"""Add service panels and mechanical interfaces without replacing the robot rig."""
from pathlib import Path
import json, runpy
import bpy, bmesh
from mathutils import Vector

BASE = Path(__file__).resolve().parents[1]
PROJECT = BASE.parents[1]
shared = runpy.run_path(str(PROJECT/'ArtSource/StationKit/scripts/refine_station_kit.py'))
Mesh, cylinder = shared['Mesh'], shared['cylinder']
MATERIALS = {key:bpy.data.materials['M_Robot'+key] for key in ('Body','Dark','Accent','Emission')}


def append(name, detail):
    obj = bpy.data.objects[name]
    inverse = obj.matrix_world.inverted()
    data = obj.data
    vertices = [tuple(v.co) for v in data.vertices]
    offset = len(vertices)
    vertices.extend(tuple(inverse @ Vector(p)) for p in detail.v)
    faces = [tuple(p.vertices) for p in data.polygons]
    indices = [p.material_index for p in data.polygons]
    materials = list(data.materials)
    mapping = []
    for key in detail.mats:
        material = MATERIALS[key]
        if material not in materials: materials.append(material)
        mapping.append(materials.index(material))
    faces.extend(tuple(i+offset for i in f) for f in detail.f)
    indices.extend(mapping[i] for i in detail.mi)
    data.clear_geometry(); data.from_pydata(vertices, [], faces)
    data.materials.clear()
    for material in materials: data.materials.append(material)
    for polygon, material_index in zip(data.polygons, indices): polygon.material_index=material_index
    bm=bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); bm.to_mesh(data); bm.free()
    data.update()


def refine():
    assert Path(bpy.data.filepath).resolve() == (BASE/'Robot.blend').resolve()
    scene=bpy.data.scenes['Robot_Workshop']; bpy.context.window.scene=scene
    scene.frame_set(0); bpy.context.view_layer.update()
    root=bpy.data.objects['RobotRoot']
    if root.get('artRevision')=='v0.11.0': return
    body=Mesh()
    for side in (-1,1):
        body.box((side*.231,.435,0),(.008,.175,.277),'Dark',.003)
        body.box((side*.237,.448,.011),(.008,.139,.24),'Body',.003)
        for y in (.413,.444,.475):
            body.box((side*.242,y,-.032),(.003,.012,.112),'Dark',.001)
        body.box((side*.241,.511,.061),(.003,.017,.053),'Accent',.001)
    for x in (-.146,.146):
        body.box((x,.433,-.213),(.016,.14,.007),'Body',.002)
    for x in (-.065,0,.065):
        cylinder(body,(x,.338,-.207),.017,.006,'Dark',12,'Z')
        cylinder(body,(x,.338,-.211),.009,.002,'Body',8,'Z')
    append('Body',body)
    head=Mesh()
    cylinder(head,(0,.566,0),.087,.014,'Dark',24)
    cylinder(head,(0,.573,0),.074,.009,'Body',24)
    for side in (-1,1):
        head.box((side*.209,.68,-.004),(.008,.095,.113),'Dark',.003)
        head.box((side*.213,.68,-.004),(.004,.071,.091),'Body',.002)
        head.box((side*.216,.68,-.004),(.002,.033,.046),'Dark',.001)
        head.box((side*.147,.790,0),(.025,.005,.104),'Dark',.002)
    append('Head',head)
    for side,sign in [('L',-1),('R',1)]:
        tread=Mesh()
        for z in (-.14,-.07,0,.07,.14):
            tread.box((sign*.28,.298,z),(.118,.003,.015),'Dark',.001)
        tread.box((sign*.28,.225,-.293),(.084,.019,.01),'Accent',.003)
        append('Track_'+side,tread)
        for end,z in [('F',.17),('B',-.17)]:
            wheel=Mesh()
            cylinder(wheel,(sign*.3485,.15,z),.024,.002,'Body',12,'X')
            cylinder(wheel,(sign*.349,.15,z),.010,.001,'Dark',8,'X')
            append('Wheel_'+side+end,wheel)
    housing=Mesh()
    for x in (-.125,.125):
        cylinder(housing,(x,.4,.299),.029,.039,'Body',16,'Z')
        cylinder(housing,(x,.4,.317),.024,.006,'Dark',16,'Z')
    append('PusherHousing',housing)
    plate=Mesh()
    for x in (-.175,.175):
        for y in (.338,.462):
            plate.box((x,y,.359),(.021,.007,.001),'Dark',0)
    append('PushPlate',plate)
    for obj in root.children_recursive:
        if obj.type=='MESH': shared['planar_uv'](obj.data)
    for key,metal,rough in [('Body',.18,.41),('Dark',.025,.77),('Accent',.05,.43),('Emission',.12,.21)]:
        node=next(n for n in MATERIALS[key].node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        node.inputs['Metallic'].default_value=metal
        node.inputs['Roughness'].default_value=rough
    root['artRevision']='v0.11.0'
    bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'Robot.blend'))
    print(json.dumps({'revision':'v0.11.0','meshCount':len([o for o in root.children_recursive if o.type=='MESH'])}))


if __name__=='__main__': refine()
