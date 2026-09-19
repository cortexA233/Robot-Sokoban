"""v0.11.0 additive art pass on the existing workshop, invoked through Blender MCP.

Never rebuilds the original kit. Existing renderer names, material slots, pivots,
and preview mesh links are retained. A revision tag prevents accidental stacking.
"""
from pathlib import Path
import json, math, runpy
import bpy
from mathutils import Vector

BASE = Path(__file__).resolve().parents[1]
PROJECT = BASE.parents[1]
kit = runpy.run_path(str(BASE / 'scripts/build_station_kit.py'))
Mesh, merge, rotate = kit['Mesh'], kit['merge_mesh'], kit['rotate_mesh']
REVISION = 'v0.11.0'
CHANGED = ['EnergyCrate', 'CargoCrate', 'GoalSocket', 'UtilitySocket', 'PowerGate']
NEW = ['RedirectorPlate', 'LowFrictionDeck']


def find(root, name):
    return next(o for o in root.children_recursive if o.get('nodeName') == name)


def update_mesh(obj, additions, replace=False):
    """Edit the shared datablock, so the saved display scenes stay in sync."""
    data = obj.data
    mats = list(data.materials)
    vertices = [] if replace else [tuple(v.co) for v in data.vertices]
    faces = [] if replace else [tuple(p.vertices) for p in data.polygons]
    indices = [] if replace else [p.material_index for p in data.polygons]
    offset = len(vertices)
    vertices.extend(additions.v)
    mapping = []
    for key in additions.mats:
        material = kit['material'](key)
        if material not in mats:
            mats.append(material)
        mapping.append(mats.index(material))
    faces.extend(tuple(i + offset for i in f) for f in additions.f)
    indices.extend(mapping[i] for i in additions.mi)
    data.clear_geometry()
    data.from_pydata(vertices, [], faces)
    data.materials.clear()
    for material in mats:
        data.materials.append(material)
    for polygon, material_index in zip(data.polygons, indices):
        polygon.material_index = material_index
    data.update()


def cylinder(mesh, center, radius, depth, key='Metal', sides=12, axis='Y'):
    points = []
    for side in (-.5, .5):
        for i in range(sides):
            a = math.tau * i / sides
            v = (radius * math.cos(a), side * depth, radius * math.sin(a))
            if axis == 'Z':
                v = (v[0], v[2], v[1])
            if axis == 'X':
                v = (v[1], v[0], v[2])
            points.append(Vector(center) + Vector(v))
    faces = [tuple(range(sides)), tuple(reversed(range(sides, sides * 2)))]
    faces += [(i, i+sides, (i+1)%sides+sides, (i+1)%sides) for i in range(sides)]
    # Recompute winding below after all geometry is assembled.
    mesh.add(points, faces, key)


def planar_uv(data):
    uv = data.uv_layers.get('SurfaceUV') or data.uv_layers.new(name='SurfaceUV')
    for polygon in data.polygons:
        axis = max(range(3), key=lambda i: abs(polygon.normal[i]))
        a, b = [i for i in range(3) if i != axis]
        for loop_index in polygon.loop_indices:
            point = data.vertices[data.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (point[a] * 2, point[b] * 2)


def finish(root):
    import bmesh
    for obj in root.children_recursive:
        if obj.type != 'MESH':
            continue
        bm = bmesh.new(); bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data); bm.free()
        obj.data.update(); planar_uv(obj.data)
    root['artRevision'] = REVISION


def crates(aid):
    root = bpy.data.objects[aid + 'Root']
    if root.get('artRevision') == REVISION:
        return
    cargo = aid == 'CargoCrate'
    details = Mesh()
    for side in range(4):
        face = Mesh()
        # Recessed closure locks and long edge splines leave the push pad clear.
        for x in (-.293, .293):
            face.box((x, .405, .393), (.026, .40, .012), 'Metal', .004)
        for y in (.102, .707):
            face.box((0, y, .392), (.195, .052, .014), 'Dark', .006)
            face.box((0, y, .399), (.105, .031, .002), 'Metal', .001)
        if not cargo:
            # Frame the luminous bank as a physical window, with recessed louvers.
            for x in (-.255, .255):
                face.box((x, .618, .394), (.023, .117, .008), 'Metal', .002)
            for x in (-.185, .185):
                face.box((x, .543, .397), (.095, .011, .003), 'Dark', .001)
        merge(details, rotate(face, side * math.pi / 2))
    update_mesh(find(root, 'Shell'), details)
    if cargo:
        ribs = Mesh()
        for side in range(4):
            face = Mesh()
            for sx in (-1, 1):
                face.bar((sx*.245, .676, .393), (sx*.077, .505, .393), .057, .011, 'Brace')
                face.bar((sx*.245, .124, .393), (sx*.077, .295, .393), .057, .011, 'Brace')
            face.box((0, .4, .400), (.08, .055, .0008), 'Metal', 0)
            merge(ribs, rotate(face, side * math.pi / 2))
        ribs.bar((-.25, .794, -.25), (.25, .794, .25), .009, .06, 'Brace')
        ribs.bar((-.25, .794, .25), (.25, .794, -.25), .009, .06, 'Brace')
        # Beam roll can raise its corners; all top ribs stay inside the contract.
        ribs.v = [(x, y, min(z, .799)) for x, y, z in ribs.v]
        ribs.box((0, .795, 0), (.15, .009, .15), 'Dark', .003)
        ribs.box((0, .7997, 0), (.075, .0005, .085), 'Metal', 0)
        update_mesh(find(root, 'CargoBraces'), ribs, replace=True)
    else:
        top = Mesh()
        for x in (-.272, .272):
            top.box((x, .797, 0), (.02, .004, .37), 'Metal', .001)
        for z in (-.276, .276):
            top.box((0, .797, z), (.20, .004, .024), 'Dark', .001)
        update_mesh(find(root, 'Shell'), top)
    finish(root)


def sockets(aid):
    root = bpy.data.objects[aid + 'Root']
    if root.get('artRevision') == REVISION:
        return
    details = Mesh()
    for side in range(4):
        edge = Mesh()
        for z in (.407, .468):
            edge.box((0, .0005, z), (.81, .003, .012), 'Metal', .001)
        for x in (-.352, .352):
            edge.box((x, .0003, .438), (.056, .003, .042), 'Dark', .001)
            for dx in (-.017, 0, .017):
                edge.box((x+dx, .002, .438), (.007, .001, .027), 'Metal', .0003)
        merge(details, rotate(edge, side*math.pi/2))
    for x in (-.443, .443):
        for z in (-.443, .443):
            details.box((x, .006, z), (.059, .012, .059), 'Metal', .007)
            cylinder(details, (x, .0122, z), .012, .001, 'Dark', 8)
            details.box((x, .013, z), (.014, .0006, .003), 'Metal', 0)
    update_mesh(find(root, 'Rim'), details)
    finish(root)


def gate():
    root = bpy.data.objects['PowerGateRoot']
    if root.get('artRevision') == REVISION:
        return
    clear_gate_vent_plates(root)
    details = Mesh()
    for x in (-.458, .458):
        for z in (-.458, .458):
            for y in (.18, 1.38):
                details.box((x, y, z), (.061, .16, .061), 'Dark', .005)
            # Metal guide faces stay outside both orthogonal crate sweeps.
            details.box((x, .78, z), (.031, 1.01, .065), 'Body', .003)
    # Separated drive housings sit beside, never inside, the shutter tracks.
    for x in (-.35, .35):
        for z in (-.116, .116):
            details.box((x, 1.43, z), (.125, .23, .037), 'Dark', .008)
            cylinder(details, (x, 1.52, z), .041, .034, 'Metal', 12, 'Z')
    for x in (-.32, -.24, -.16, -.08, 0, .08, .16, .24, .32):
        details.box((x, 1.648, 0), (.015, .012, .205), 'Metal', .003)
    for z in (-.458, .458):
        details.box((0, 1.515, z), (.42, .063, .045), 'Dark', .005)
        details.box((0, 1.516, z*1.008), (.29, .020, .038), 'Metal', .003)
    update_mesh(find(root, 'FrameAndCassette'), details)
    for name in ('LowerPanelMesh', 'UpperPanelMesh'):
        panel = Mesh()
        for z in (-.040, .040):
            # Thin flush seams keep the two 90 mm-spaced tracks independent.
            for y in (-.12, .12):
                panel.box((0, y, z), (.54, .012, .001), 'Metal', 0)
            for x in (-.28, .28):
                for y in (-.185, .185):
                    panel.box((x, y, z), (.037, .024, .001), 'Dark', 0)
        update_mesh(find(root, name), panel)
    finish(root)


def clear_gate_vent_plates(root):
    """Replace the five old flat top strips with the new raised vent fins."""
    import bmesh
    data=find(root,'FrameAndCassette').data
    bm=bmesh.new(); bm.from_mesh(data)
    faces=[f for f in bm.faces if data.materials[f.material_index].name=='M_StationMetal'
           and all(1.649 <= v.co.z <= 1.651 for v in f.verts)]
    bmesh.ops.delete(bm,geom=faces,context='FACES')
    bm.to_mesh(data); bm.free(); data.update()


def new_surface(aid):
    if bpy.data.objects.get(aid + 'Root'):
        return
    col = bpy.data.collections.new(aid)
    col['owner'] = 'station-kit-v1'
    bpy.data.collections['StationKit_Assets'].children.link(col)
    root = kit['empty'](aid + 'Root', None, col)
    root['assetId'] = aid
    if aid == 'LowFrictionDeck':
        kit['floor'](root, col, 'Plain')
    frame = Mesh(); surface = Mesh()
    surface.box((0, .0004, 0), (.87, .0008, .87), 'Metal', 0)
    for x in (-.456, .456):
        frame.box((x, .0005, 0), (.031, .003, .90), 'Dark', .001)
    for z in (-.456, .456):
        frame.box((0, .0005, z), (.90, .003, .031), 'Dark', .001)
    for x in (-.462, .462):
        for z in (-.462, .462):
            frame.box((x, .002, z), (.04, .008, .04), 'Metal', .004)
            cylinder(frame, (x, .0065, z), .008, .001, 'Dark', 8)
    if aid == 'LowFrictionDeck':
        marks = Mesh()
        for x in (-.22, 0, .22):
            marks.box((x, .0013, 0), (.021, .0008, .73), 'Dark', 0)
        marks.object('GlideInlays', root, col)
    else:
        for z in (-.355, .355):
            frame.box((0, .001, z), (.25, .001, .016), 'Metal', 0)
    frame.object('Frame', root, col)
    surface.object('Surface', root, col)
    finish(root)


def refine():
    assert Path(bpy.data.filepath).resolve() == (BASE/'StationKit.blend').resolve()
    scene = bpy.data.scenes['StationKit_Workshop']
    bpy.context.window.scene = scene; scene.frame_set(1)
    for aid in CHANGED:
        if aid.endswith('Crate'): crates(aid)
        elif aid.endswith('Socket'): sockets(aid)
        else: gate()
    for aid in NEW: new_surface(aid)
    # Store editable source material intent; Unity is mapped explicitly on import.
    for name, metal, rough in [('M_StationBody', .18, .39), ('M_StationDark', .12, .72),
                             ('M_StationMetal', .78, .29), ('M_StationCargoBody', .1, .63),
                             ('M_StationCargoBrace', .48, .43), ('M_StationEnergyWindow', .22, .19)]:
        material = bpy.data.materials[name]
        node = next(n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        node.inputs['Metallic'].default_value = metal
        node.inputs['Roughness'].default_value = rough
        material['metallic'] = metal; material['roughness'] = rough
    bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'StationKit.blend'))
    print(json.dumps({'artRevision':REVISION, 'refined':CHANGED, 'new':NEW}))


if __name__ == '__main__': refine()
