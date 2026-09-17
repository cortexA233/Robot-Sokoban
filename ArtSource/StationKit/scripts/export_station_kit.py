"""Non-destructive per-asset FBX export and measured source inventory."""
from pathlib import Path
import json, math, hashlib, shutil, datetime
import bpy, bmesh
from mathutils import Matrix, Vector

BASE=Path(__file__).resolve().parents[1]; PROJECT=BASE.parents[1]
IDS=['EnergyCrate','CargoCrate','GoalSocket','UtilitySocket','PowerGate','FloorPlain','FloorService','FloorGrate','WallStraight','WallCorner','WallEnd']
BASIS=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))

def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def uname(obj): return obj.get('nodeName',obj.name.split('__')[-1])
def nodepath(obj,root):
    names=[]
    while obj!=root: names.append(uname(obj)); obj=obj.parent
    return '/'.join(reversed(names))
def vec(v): return [round(float(x),7) for x in v]
def unity(v): return Vector((v[0],v[2],-v[1]))
def metrics(root,raw=False):
    inverse=root.matrix_world.inverted(); meshes=[o for o in root.children_recursive if o.type=='MESH']
    pts=[]; nodes=[]; triangles=0; degenerate=0
    for obj in meshes:
        # Raw Y-up FBX coordinates are reflected on Z by Unity's reader.
        points=[inverse@obj.matrix_world@v.co for v in obj.data.vertices]
        points=[Vector((v.x,v.y,-v.z)) if raw else unity(v) for v in points]
        pts.extend(points); obj.data.calc_loop_triangles(); triangles+=len(obj.data.loop_triangles)
        degenerate+=sum(t.area<1e-12 for t in obj.data.loop_triangles)
        nodes.append({'path':nodepath(obj,root),'triangles':len(obj.data.loop_triangles),
            'bounds':{'min':vec([min(p[i] for p in points) for i in range(3)]),'max':vec([max(p[i] for p in points) for i in range(3)])},
            'materials':[{'slot':i,'material':m.name,'role':m.get('role','unknown')} for i,m in enumerate(obj.data.materials)]})
    low=Vector([min(p[i] for p in pts) for i in range(3)]); high=Vector([max(p[i] for p in pts) for i in range(3)])
    return {'bounds':{'min':vec(low),'max':vec(high),'size':vec(high-low)},'triangles':triangles,'meshCount':len(meshes),'rendererCount':len(meshes),'degenerateTriangles':degenerate,'renderers':nodes}

def protect(path):
    receipt=BASE/'validation/export_receipt.json'
    prior=json.loads(receipt.read_text()) if receipt.exists() else {}
    relative=path.relative_to(PROJECT).as_posix()
    if path.exists() and prior.get(relative)!=sha(path):
        backup=PROJECT/'Logs/StationKitBackups'/datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
        backup.mkdir(parents=True,exist_ok=True); shutil.copy2(path,backup/path.name)

def export_all():
    source=bpy.data.scenes['StationKit_Workshop']; bpy.context.window.scene=source; source.frame_set(1)
    report={'blenderVersion':bpy.app.version_string,'stage':'source','assets':{}}
    settings=dict(use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        use_space_transform=False,bake_space_transform=False,use_mesh_modifiers=True,use_triangles=True,
        add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False,use_custom_props=True)
    props=bpy.ops.export_scene.fbx.get_rna_type().properties
    for k,v in settings.items():
        if props[k].type=='ENUM':
            valid={i.identifier for i in props[k].enum_items}
            assert (v if isinstance(v,set) else {v})<=valid,k
    receipt={}
    for aid in IDS:
        root=bpy.data.objects[aid+'Root']; objects=[root]+list(root.children_recursive)
        report['assets'][aid]=metrics(root)
        names={o:o.name for o in objects}; copies={}
        temp=bpy.data.scenes.new('StationKit_Export_Temporary'); temp.unit_settings.scale_length=1
        try:
            for o in objects: o.name='__Source__'+names[o]
            for o in objects:
                clone=o.copy(); clone.name=uname(o); clone.animation_data_clear()
                if o.type=='MESH':
                    clone.data=o.data.copy(); clone.data.transform(BASIS)
                    bm=bmesh.new(); bm.from_mesh(clone.data); bmesh.ops.reverse_faces(bm,faces=list(bm.faces)); bm.to_mesh(clone.data); bm.free()
                temp.collection.objects.link(clone); copies[o]=clone
            for o,c in copies.items():
                c.parent=copies.get(o.parent); c.matrix_parent_inverse=Matrix.Identity(4)
                c.matrix_local=BASIS@o.matrix_local@BASIS.inverted()
            bpy.context.window.scene=temp
            for c in copies.values(): c.select_set(True)
            bpy.context.view_layer.objects.active=copies[root]
            path=PROJECT/'Assets/Art/StationKit/Meshes'/(aid+'.fbx'); path.parent.mkdir(parents=True,exist_ok=True)
            protect(path); bpy.ops.export_scene.fbx(filepath=str(path),**settings)
            receipt[path.relative_to(PROJECT).as_posix()]=sha(path)
        finally:
            bpy.context.window.scene=source
            for c in copies.values():
                data=c.data if c.type=='MESH' else None; bpy.data.objects.remove(c,do_unlink=True)
                if data and data.users==0: bpy.data.meshes.remove(data)
            bpy.data.scenes.remove(temp)
            for o,n in names.items(): o.name=n
    report['sourceSha256']=sha(BASE/'StationKit.blend')
    (BASE/'validation/source_inventory.json').write_text(json.dumps(report,indent=2)+'\n')
    (BASE/'validation/export_receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    (BASE/'validation/export_settings.json').write_text(json.dumps({**{k:sorted(v) if isinstance(v,set) else v for k,v in settings.items()},'rawFbxFromBlender':[list(r) for r in BASIS]},indent=2)+'\n')
    print(json.dumps({a:{k:v for k,v in m.items() if k not in ('renderers',)} for a,m in report['assets'].items()}))
    return report

if __name__=='__main__': export_all()
