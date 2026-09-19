"""Reopen source; independently import each FBX; check geometry and interfaces.
Run: blender -b StationKit.blend -P scripts/validate_station_kit.py
"""
from pathlib import Path
import bpy, json, runpy, math, datetime
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
BASE=Path(__file__).resolve().parents[1]; PROJECT=BASE.parents[1]
ex=runpy.run_path(str(BASE/'scripts/export_station_kit.py'))
metrics=ex['metrics']; IDS=ex['IDS']; unity=ex['unity']; sha=ex['sha']; uname=ex['uname']
checks=[]
def check(test,name,detail=None): checks.append({'name':name,'passed':bool(test),'detail':detail})
def verts(obj,root,raw=False):
    mat=root.matrix_world.inverted()@obj.matrix_world
    points=[mat@v.co for v in obj.data.vertices]
    return [Vector((v.x,v.y,-v.z)) if raw else unity(v) for v in points]
def mesh_objects(root): return [o for o in root.children_recursive if o.type=='MESH']
def find(root,name): return next(o for o in root.children_recursive if uname(o)==name)

def triangle_box(tri,lo,hi):
    center=(lo+hi)*.5; ext=(hi-lo)*.5; v=[p-center for p in tri]
    edges=[v[(i+1)%3]-v[i] for i in range(3)]
    axes=[Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))]
    tests=axes+[edges[0].cross(edges[1])]+[e.cross(a) for e in edges for a in axes]
    for a in tests:
        if a.length_squared<1e-15: continue
        dots=[a.dot(p) for p in v]; r=sum(abs(a[i])*ext[i] for i in range(3))
        if min(dots)>r+1e-7 or max(dots)<-r-1e-7: return False
    return True

def intersections(objects,root,lo,hi):
    count=0
    for obj in objects:
        ps=verts(obj,root); obj.data.calc_loop_triangles()
        for t in obj.data.loop_triangles:
            tri=[ps[i] for i in t.vertices]
            if any(max(v[i] for v in tri)<lo[i] or min(v[i] for v in tri)>hi[i] for i in range(3)): continue
            if triangle_box(tri,lo,hi): count+=1
    return count

def measure(root,aid,stage,raw=False):
    m=metrics(root,raw); pref=stage+'/'+aid+'/'
    check(m['degenerateTriangles']==0,pref+'nondegenerate')
    check(all(min(o.scale)>0 for o in [root]+list(root.children_recursive)),pref+'positive_scales')
    check(all(o.type in ('EMPTY','MESH') for o in [root]+list(root.children_recursive)),pref+'no_reference_camera_or_light')
    check(all(o.data.materials and all(o.data.materials) for o in mesh_objects(root)),pref+'no_missing_material')
    check(max(abs(root.matrix_world[i][j]-(1 if i==j else 0)) for i in range(4) for j in range(4))<1e-4,pref+'identity_root')
    # v0.11.0 budgets include the authored locks, guide faces and socket contacts.
    budgets={'EnergyCrate':6500,'CargoCrate':4500,'PowerGate':6000,
        'GoalSocket':3500,'UtilitySocket':3500,'RedirectorPlate':1000,'LowFrictionDeck':1200}
    budget=budgets.get(aid,2500 if aid.endswith('Socket') else 600 if aid.startswith('Floor') else 1500)
    check(m['triangles']<=budget,pref+'triangle_budget',m['triangles'])
    size=m['bounds']['size']; lo=m['bounds']['min']; hi=m['bounds']['max']
    if aid.endswith('Crate'):
        check(max(abs(x-.8) for x in size)<.005,pref+'0.8m_cube',size)
        check(abs(lo[1])<.001,pref+'ground_origin')
        vs=[]; faces=[]
        for o in mesh_objects(root):
            ps=verts(o,root,raw); offset=len(vs); vs+=ps
            o.data.calc_loop_triangles(); faces += [tuple(offset+i for i in t.vertices) for t in o.data.loop_triangles]
        tree=BVHTree.FromPolygons(vs,faces,all_triangles=True)
        errors=[]
        for axis,side in [(0,1),(0,-1),(2,1),(2,-1)]:
            for h in (.315,.4,.485):
                for u in (-.22,0,.22):
                    origin=Vector((0,h,0)); origin[axis]=side*.6; origin[2 if axis==0 else 0]=u
                    direction=Vector((0,0,0)); direction[axis]=-side
                    hit=tree.ray_cast(origin,direction,.3)
                    errors.append(abs(hit[0][axis]-side*.4) if hit[0] else 1)
        check(max(errors)<.01,pref+'four_push_faces_36_rays',{'maxContactError':max(errors)})
        roles={x['material'].split('.')[0] for r in m['renderers'] for x in r['materials']}
        check(not any('LinkAccent' in k or 'StatusEmission' in k for k in roles),pref+'fixed_appearance_only')
        if aid=='CargoCrate': check(not any('EnergyWindow' in k for k in roles),pref+'no_energy_window')
    elif aid.startswith('Floor'):
        check(max(abs(a-b) for a,b in zip(size,(.98,.24,.98)))<.001 and abs(hi[1])<.001,pref+'floor_contract',m['bounds'])
    elif aid.startswith('Wall'):
        check(max(abs(a-b) for a,b in zip(size,(.98,1.54,.98)))<.001,pref+'whole_cell_wall',size)
    elif aid=='PowerGate': check(size[0]<=.981 and size[2]<=.981,pref+'one_cell_footprint')
    elif aid in ('RedirectorPlate','LowFrictionDeck'):
        check(size[0]<=.981 and size[2]<=.981 and hi[1]<=.008,pref+'flush_transport_surface',m['bounds'])
    return m

def source_sweeps():
    scene=bpy.data.scenes['StationKit_Workshop']; bpy.context.window.scene=scene; scene.frame_set(1)
    for aid in ['GoalSocket','UtilitySocket','PowerGate']:
        root=bpy.data.objects[aid+'Root']
        if aid=='PowerGate':
            for n in ('LowerPanel','UpperPanel'):
                o=find(root,n); p=json.loads(o['Open'])['position']; o.location=(p[0],-p[2],p[1])
            bpy.context.view_layer.update()
        for subject,half,depth in [('EnergyCrate',.40025,.40025),('CargoCrate',.4004,.4004),('Robot',.35,.36)]:
            for axis in (0,2):
                lo=Vector((-half,.003,-depth)); hi=Vector((half,.801,depth)); lo[axis]=-1.41; hi[axis]=1.41
                hits=intersections(mesh_objects(root),root,lo,hi)
                check(hits==0,'source/'+aid+'/sweep_'+subject+('_EW' if axis==0 else '_NS'),{'intersectingTriangles':hits,'overlayToleranceMetres':.003})
    # Sample every frame of the mechanical open/close demo, including end stops.
    gate=bpy.data.objects['PowerGateRoot']; upper=find(gate,'FrameAndCassette')
    max_hits=0; overlaps=0
    for frame in range(1,101):
        scene.frame_set(frame); bpy.context.view_layer.update(); boxes=[]
        for n in ('LowerPanelMesh','UpperPanelMesh'):
            o=find(gate,n); ps=verts(o,gate)
            lo=Vector([min(p[i] for p in ps) for i in range(3)]); hi=Vector([max(p[i] for p in ps) for i in range(3)])
            boxes.append((lo,hi)); max_hits=max(max_hits,intersections([upper],gate,lo+Vector((.0001,.0001,.0001)),hi-Vector((.0001,.0001,.0001))))
        overlaps+=int(all(boxes[0][0][i]<boxes[1][1][i] and boxes[1][0][i]<boxes[0][1][i] for i in range(3)))
    check(max_hits==0 and overlaps==0,'source/PowerGate/100_frame_mechanical_clearance',{'fixedIntersections':max_hits,'panelOverlaps':overlaps})
    scene.frame_set(1)

def main():
    s=bpy.data.scenes['StationKit_Workshop']; bpy.context.window.scene=s; s.frame_set(1)
    for l in s.view_layers[0].layer_collection.children['StationKit_Assets'].children: l.exclude=False
    bpy.context.view_layer.update()
    source={aid:measure(bpy.data.objects[aid+'Root'],aid,'source') for aid in IDS}
    interfaces={'sourceSha256':sha(BASE/'StationKit.blend'),'blenderVersion':bpy.app.version_string,'gate':{},'materials':{}}
    for name in ('LowerPanel','UpperPanel'):
        o=find(bpy.data.objects['PowerGateRoot'],name)
        interfaces['gate'][name]={'Closed':json.loads(o['Closed']),'Open':json.loads(o['Open']),'action':o.animation_data.action.name,'frameRange':list(o.animation_data.action.frame_range)}
    used={m for aid in IDS for o in mesh_objects(bpy.data.objects[aid+'Root']) for m in o.data.materials}
    for m in used:
        node=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        srgb=lambda c: 12.92*c if c<=.0031308 else 1.055*c**(1/2.4)-.055
        color='#'+''.join(f'{max(0,min(255,round(srgb(c)*255))):02X}' for c in node.inputs['Base Color'].default_value[:3])
        interfaces['materials'][m.name]={'role':m.get('role'),'sRGB':color,'roughness':node.inputs['Roughness'].default_value,'metallic':node.inputs['Metallic'].default_value,'emissionStrength':node.inputs['Emission Strength'].default_value}
        interfaces['materials'][m.name]['roughnessTextureDriven']=node.inputs['Roughness'].is_linked
        interfaces['materials'][m.name]['metallicTextureDriven']=node.inputs['Metallic'].is_linked
        interfaces['materials'][m.name]['textures']=sorted({Path(bpy.path.abspath(n.image.filepath)).resolve().relative_to(PROJECT).as_posix()
            for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image})
    source_sweeps()
    roundtrip={}
    for aid in IDS:
        temp=bpy.data.scenes.new('Roundtrip_'+aid); bpy.context.window.scene=temp
        existing=set(bpy.data.objects)
        path=PROJECT/'Assets/Art/StationKit/Meshes'/(aid+'.fbx')
        bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False,use_manual_orientation=True,axis_forward='Y',axis_up='Z')
        objects=set(bpy.data.objects)-existing; root=next(o for o in objects if o.parent is None)
        bpy.context.view_layer.update(); data=measure(root,aid,'fbx',True); data['sha256']=sha(path)
        check(data['triangles']==source[aid]['triangles'],'fbx/'+aid+'/triangle_roundtrip')
        check(max(abs(a-b) for a,b in zip(data['bounds']['size'],source[aid]['bounds']['size']))<.0001,'fbx/'+aid+'/bounds_roundtrip')
        roundtrip[aid]=data
        bpy.context.window.scene=s
        for o in objects: bpy.data.objects.remove(o,do_unlink=True)
        bpy.data.scenes.remove(temp)
    result={'protocol':'station-kit-v1','checkedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'blenderVersion':bpy.app.version_string,'sourceSha256':sha(BASE/'StationKit.blend'),
        'passed':all(c['passed'] for c in checks),'source':source,'fbx':roundtrip,'checks':checks,
        'method':'Reopened saved source. Independent per-file raw FBX import with manual Y-forward/Z-up, fixed Z reflection to Unity. Triangle-AABB SAT sweeps and raycast contact grids. Visuals reviewed separately.'}
    (BASE/'validation/blender_fbx_validation.json').write_text(json.dumps(result,indent=2)+'\n')
    (BASE/'validation/source_inventory.json').write_text(json.dumps({'blenderVersion':bpy.app.version_string,'stage':'source','sourceSha256':result['sourceSha256'],'assets':source},indent=2)+'\n')
    (BASE/'validation/source_interfaces.json').write_text(json.dumps(interfaces,indent=2)+'\n')
    print(json.dumps({'passed':result['passed'],'checks':len(checks),'failures':[c for c in checks if not c['passed']]}))
    assert result['passed'],'Asset validation failed'

if __name__=='__main__': main()
