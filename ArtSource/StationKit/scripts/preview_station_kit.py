"""Isolated Blender evidence scenes; no source mesh or robot source is changed."""
from pathlib import Path
import bpy, math, json, runpy, sys
from mathutils import Vector, Matrix
BASE=Path(__file__).resolve().parents[1]; PROJECT=BASE.parents[1]
helpers=runpy.run_path(str(BASE/'scripts/build_station_kit.py'))
ub=helpers['ub']; Mesh=helpers['Mesh']; glyph=helpers['glyph']; linear=helpers['linear']
OWNER='station-kit-preview-v1'

def mat(name,color,emission=0):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    rgb=tuple(linear(int(color[i:i+2],16)/255) for i in (1,3,5))+(1,)
    p.inputs['Base Color'].default_value=rgb; p.inputs['Roughness'].default_value=.58
    p.inputs['Emission Color'].default_value=rgb; p.inputs['Emission Strength'].default_value=emission
    m.diffuse_color=rgb
    if name.startswith('Preview_#'): m['role']='LinkAccent'
    if name in ('Preview_PowerOn','Preview_PowerOff'): m['role']='StatusEmission'
    return m

def get(root,part): return next(o for o in [root]+list(root.children_recursive) if o.get('nodeName')==part)
def all_mesh(root): return [o for o in root.children_recursive if o.type=='MESH']

def clone(aid,col,pos=(0,0,0),yaw=0):
    src=bpy.data.objects[aid+'Root']; mapping={}
    for o in [src]+list(src.children_recursive):
        c=o.copy(); c.name='Preview_'+o.name; c.animation_data_clear(); col.objects.link(c); mapping[o]=c; c['owner']=OWNER
    for o,c in mapping.items():
        c.parent=mapping.get(o.parent); c.matrix_parent_inverse=o.matrix_parent_inverse.copy(); c.matrix_basis=o.matrix_basis.copy()
    root=mapping[src]; root.location=ub(pos); root.rotation_euler.z=-yaw
    if aid=='PowerGate': state(root,False,False)
    return root

def recolor(root,color='#559CE8',powered=False):
    for o in all_mesh(root):
        for i,slot in enumerate(o.material_slots):
            role=slot.material.get('role')
            if role in ('LinkAccent','StatusEmission'):
                slot.link='OBJECT'
                slot.material=mat('Preview_'+(color if role=='LinkAccent' else ('PowerOn' if powered else 'PowerOff')),
                    color if role=='LinkAccent' else ('#BDF0D8' if powered else '#273D40'),.7 if powered and role=='StatusEmission' else 0)

def state(root,opened,powered,sources=(False,False)):
    for name in ('LowerPanel','UpperPanel'):
        o=get(root,name); o.location=ub(json.loads(o['Open' if opened else 'Closed'])['position'])
    get(root,'OpenMark').hide_render=not opened; get(root,'ClosedMark').hide_render=opened
    recolor(get(root,'PowerStatus'),'#FFFFFF',powered)
    for i,name in enumerate(('SourceBadgeTemplate','SourceBadge02')):
        recolor(get(root,name),('#559CE8','#B88CDF')[i],sources[i])

def hide_upper(root,hide=True):
    for o in root.children_recursive:
        p=o
        while p!=root:
            if p.get('nodeName') in ('Upper','UpperStructure','MovingParts'):
                if o.type=='MESH': o.hide_render=hide
                break
            p=p.parent

def label(col,text,pos,size=.15):
    curve=bpy.data.curves.new('PreviewLabel','FONT'); curve.body=text; curve.size=size; curve.align_x='CENTER'
    o=bpy.data.objects.new('Label_'+text,curve); col.objects.link(o); o.location=ub(pos)
    curve.materials.append(mat('PreviewText','#CFDBE1',.2)); o['owner']=OWNER; return o

def scene(name):
    previous=bpy.data.scenes.get(name)
    if previous:
        for o in list(previous.objects):
            if o.get('owner')==OWNER: bpy.data.objects.remove(o,do_unlink=True)
        bpy.data.scenes.remove(previous)
    s=bpy.data.scenes.new(name); s['owner']=OWNER; bpy.context.window.scene=s
    s.render.engine='BLENDER_EEVEE'; s.render.resolution_x=1920; s.render.resolution_y=1080; s.render.resolution_percentage=100
    s.render.image_settings.file_format='PNG'; s.render.fps=30
    s.world=bpy.data.worlds.new(name+'_World'); s.world.use_nodes=True
    n=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND'); n.inputs['Color'].default_value=(.18,.23,.30,1); n.inputs['Strength'].default_value=.5
    c=bpy.data.collections.new(name+'_Content'); s.collection.children.link(c)
    m=Mesh(); m.box((0,-.30,0),(200,.1,200),'Dark',0)
    background=m.object('PreviewBackground',None,c); background['owner']=OWNER
    for name,pos,energy,size in [('Key',(1,8,4),1800,7),('Fill',(-5,4,-3),1200,6),('Rim',(5,5,-5),1400,5)]:
        data=bpy.data.lights.new(name,'AREA'); data.energy=energy; data.shape='DISK'; data.size=size
        o=bpy.data.objects.new(name,data); c.objects.link(o); o.location=ub(pos); o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler(); o['owner']=OWNER
    camdata=bpy.data.cameras.new('EvidenceCamera'); cam=bpy.data.objects.new('EvidenceCamera',camdata); c.objects.link(cam); cam['owner']=OWNER; s.camera=cam
    camera(s,(6,9,9),(0,.3,0),7)
    return s,c

def camera(s,pos,target,ortho=None):
    cam=s.camera; cam.location=ub(pos); cam.rotation_euler=(ub(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.type='ORTHO' if ortho else 'PERSP'
    if ortho: cam.data.ortho_scale=ortho
    else: cam.data.lens_unit='FOV'; cam.data.angle=math.radians(55)

def import_robot():
    existing=bpy.data.objects.get('StationReferenceRobotRoot')
    if existing: return existing
    s=bpy.data.scenes['StationKit_Workshop']; bpy.context.window.scene=s
    old=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(PROJECT/'Assets/Art/Robot/Meshes/Robot.fbx'),use_anim=False,use_manual_orientation=True,axis_forward='Y',axis_up='Z')
    objects=set(bpy.data.objects)-old; basis=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
    import bmesh
    ref=bpy.data.collections['StationKit_Reference']
    for o in objects:
        o.animation_data_clear(); o.matrix_local=basis@o.matrix_local@basis
        if o.type=='MESH':
            o.data.transform(basis); bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.reverse_faces(bm,faces=list(bm.faces)); bm.to_mesh(o.data); bm.free()
        for c in list(o.users_collection): c.objects.unlink(o)
        ref.objects.link(o); o['nodeName']=o.name; o['owner']=OWNER
    root=next(o for o in objects if o.parent is None); root.name='StationReferenceRobotRoot'
    bpy.context.view_layer.update()
    return root

def robot(col,pos,yaw=0,push=False):
    root=clone('StationReferenceRobot',col,pos,yaw)
    if push:
        slide=next(o for o in root.children_recursive if o.get('nodeName')=='PushSlide'); slide.location.y-=.24
    return root

def floor_at(col,x,z,style='FloorPlain',yaw=0): return clone(style,col,(x,0,z),yaw)

def make_scenes():
    bpy.data.scenes['StationKit_Workshop'].frame_set(1)
    for l in bpy.data.scenes['StationKit_Workshop'].view_layers[0].layer_collection.children['StationKit_Assets'].children: l.exclude=False
    import_robot()
    scenes=[]
    s,c=scene('StationKit_Overview'); scenes.append(s)
    lineup=[('EnergyCrate',-2.5,1.1),('CargoCrate',-1.15,1.1),('GoalSocket',.2,1.1),('UtilitySocket',1.55,1.1),('PowerGate',2.9,1.1),
        ('FloorPlain',-2.5,-.5),('FloorService',-1.15,-.5),('FloorGrate',.2,-.5),('WallStraight',1.55,-.5),('WallCorner',2.9,-.5),('WallEnd',2.9,-2.0)]
    for aid,x,z in lineup:
        if not aid.startswith('Floor'): floor_at(c,x,z)
        r=clone(aid,c,(x,0,z))
        if aid.endswith('Socket'): recolor(r,'#559CE8' if aid=='GoalSocket' else '#B88CDF',False)
        if aid=='PowerGate': state(r,True,True,(True,False))
        label(c,aid,(x,-.22,z+.68),.12)
    robot(c,(-1.5,0,-2.0)); label(c,'STATION / 11 MODULES',(-.8,-.22,-3.2),.24)
    camera(s,(7,10,11),(0,.30,-.2),9.5)
    s,c=scene('StationKit_Crates'); scenes.append(s)
    for x,aid in [(-.9,'EnergyCrate'),(.9,'CargoCrate')]:
        floor_at(c,x,0); floor_at(c,x,-1); clone(aid,c,(x,0,0)); robot(c,(x,0,-1),push=True)
        label(c,aid+' / 0.80 m',(x,-.20,.74),.12)
    camera(s,(4,4,6),(0,.32,-.25),5.1)
    s,c=scene('StationKit_Sockets'); scenes.append(s)
    for row,aid in enumerate(('GoalSocket','UtilitySocket')):
        for x,crateid in [(-1.4,None),(0,'EnergyCrate'),(1.4,'CargoCrate')]:
            z=-row*1.7+.85; floor_at(c,x,z); r=clone(aid,c,(x,0,z)); recolor(r,'#559CE8' if row==0 else '#B88CDF',crateid=='EnergyCrate')
            if crateid: clone(crateid,c,(x,0,z))
            label(c,('EMPTY / OFF' if not crateid else ('ENERGY / ON' if crateid=='EnergyCrate' else 'CARGO / OFF')),(x,-.20,z+.66),.105)
    camera(s,(1,8,5),(0,0,0),6.3)
    s,c=scene('StationKit_Colors'); scenes.append(s)
    for x,color,shape in [(-1.45,'#559CE8','square'),(0,'#B88CDF','triangle'),(1.45,'#C2D969','circle')]:
        floor_at(c,x,0); r=clone('GoalSocket',c,(x,0,0)); recolor(r,color,False)
        # Replace only the identity geometry in this preview instance.
        old=get(r,'IdentityBands'); custom=Mesh()
        for side in range(4):
            m=Mesh(); m.box((-.23,.0015,.438),(.22,.0005,.04),'LinkAccent',0); glyph(m,shape,(-.345,.002,.438),.034,'Dark')
            helpers['merge_mesh'](custom,helpers['rotate_mesh'](m,side*math.pi/2))
        new=custom.object('ColorShape',old.parent,c); new['owner']=OWNER; old.hide_render=True; recolor(r,color,False)
        label(c,shape.upper()+' / IDENTITY',(x,-.20,.66),.11)
    camera(s,(0,6,2.8),(0,0,0),5)
    s,c=scene('StationKit_Connections'); scenes.append(s)
    for x,aid,color in [(-1.1,'GoalSocket','#559CE8'),(.1,'UtilitySocket','#B88CDF')]:
        floor_at(c,x,1.6); r=clone(aid,c,(x,0,1.6)); recolor(r,color,x<0)
        clone('EnergyCrate' if x<0 else 'CargoCrate',c,(x,0,1.6))
    for x,mode,opened in [(-1.6,'ANY',True),(0,'ALL',False),(1.6,'ANY / SHARED A',True)]:
        floor_at(c,x,-.5); g=clone('PowerGate',c,(x,0,-.5)); state(g,opened,opened,(True,False))
        label(c,mode,(x,-.20,.15),.12)
        if x>1:
            p=helpers['badge'](g,c,'SourceBadge03',(-.28,-.0065,-.446),'circle'); p['owner']=OWNER
            for o in p.children_recursive: o['owner']=OWNER
            recolor(p,'#C2D969',False)
    camera(s,(4,8,8),(0,.3,.5),6.2)
    s,c=scene('StationKit_Gates'); scenes.append(s)
    for x,op,pw,occupant,txt in [(-1.7,False,False,None,'CLOSED / NO POWER'),(0,True,True,None,'OPEN / POWERED'),(1.7,True,False,'CargoCrate','OPEN / OCCUPIED')]:
        floor_at(c,x,0); r=clone('PowerGate',c,(x,0,0)); state(r,op,pw,(pw,False))
        if occupant: clone(occupant,c,(x,0,0))
        label(c,txt,(x,-.20,.73),.105)
    camera(s,(4,5,8),(0,.65,0),6.5)
    s,c=scene('StationKit_Joins'); scenes.append(s)
    for x in range(3):
        for z in range(3): floor_at(c,x-3,z-1,('FloorPlain','FloorService','FloorGrate')[(x+z)%3],math.pi/2*((x+z)%4))
    for x,z,aid,yaw in [(1,0,'WallCorner',0),(1,1,'WallStraight',0),(1,2,'WallEnd',0),(2,0,'WallStraight',math.pi/2),(3,0,'WallEnd',math.pi/2),
                          (4,2,'WallStraight',0),(4,3,'WallEnd',0),(3,2,'WallEnd',math.pi/2),(5,2,'WallEnd',-math.pi/2),(4,1,'WallEnd',math.pi),
                          (0,4,'WallStraight',0),(-1,4,'WallEnd',math.pi/2),(1,4,'WallEnd',-math.pi/2),(0,3,'WallEnd',math.pi)]:
        floor_at(c,x,z); clone(aid,c,(x,0,z),yaw)
    camera(s,(9,12,12),(1,.2,1),11)
    s,c=scene('StationKit_Corridor'); scenes.append(s)
    for x in range(-1,3):
        for z in range(-2,3):
            floor_at(c,x,z,('FloorPlain','FloorService','FloorGrate')[(x+z)%3])
            if x in (-1,2): clone('WallStraight',c,(x,0,z))
    for aid,x,z in [('CargoCrate',0,0),('EnergyCrate',0,1),('CargoCrate',1,1),('EnergyCrate',1,-1)]: clone(aid,c,(x,0,z))
    robot(c,(0,0,-1)); g=clone('PowerGate',c,(1,0,0)); state(g,True,False)
    for o in list(c.objects):
        if o.parent is None and ('Wall' in o.name or 'PowerGate' in o.name): hide_upper(o)
    camera(s,(.5,10,0),(.5,0,0),6)
    bpy.context.window.scene=bpy.data.scenes['StationKit_Overview']
    bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'StationKit.blend'))
    print('Preview scenes:',[s.name for s in scenes])
    return scenes

def render(name,filename=None,width=1920,top=False):
    s=bpy.data.scenes[name]; bpy.context.window.scene=s
    if top:
        for o in list(s.objects):
            if o.parent is None and o.type=='EMPTY': hide_upper(o)
        camera(s,(0,14,-.2),(0,0,-.2),8.4)
    s.render.resolution_x=width; s.render.resolution_y=round(width*9/16)
    s.render.filepath=str(BASE/'previews'/((filename or name.replace('StationKit_','blender_').lower())+'.png'))
    bpy.ops.render.render(write_still=True)

def gallery_layout():
    """Data-only assembly recipes. Unity instantiates the actual imported prefabs."""
    sheets=[]
    for s in bpy.data.scenes:
        if not s.name.startswith('StationKit_') or s.name=='StationKit_Workshop': continue
        objects=[]; labels=[]
        for o in s.objects:
            if o.type=='FONT':
                p=o.location; labels.append({'text':o.data.body,'position':{'x':p.x,'y':p.z,'z':-p.y},'size':o.data.size})
            if o.parent or o.type!='EMPTY': continue
            aid=o.get('assetId')
            if not aid and o.name.startswith('Preview_StationReferenceRobotRoot'): aid='Robot'
            if not aid: continue
            p=o.location; item={'assetId':aid,'position':{'x':p.x,'y':p.z,'z':-p.y},'yaw':-math.degrees(o.rotation_euler.z),
                'color':'Blue','powered':False,'open':False,'a':False,'b':False,'top':False,'extraBadge':False,'shape':0,'push':s.name=='StationKit_Crates'}
            lamps=[slot.material.name for m in all_mesh(o) for slot in m.material_slots if slot.material and ('PowerOn' in slot.material.name or 'PowerOff' in slot.material.name)]
            item['powered']=bool(lamps and all('PowerOn' in n for n in lamps))
            if aid.endswith('Socket'):
                bands=get(o,'IdentityBands'); names=[sl.material.name for sl in bands.material_slots]
                item['color']='Purple' if any('B88CDF' in n for n in names) else 'Lime' if any('C2D969' in n for n in names) else 'Blue'
                if s.name=='StationKit_Colors': item['shape']={'Blue':0,'Purple':1,'Lime':2}[item['color']]
            if aid=='PowerGate':
                item['open']=get(o,'LowerPanel').location.z>1
                for key,name in [('powered','ConditionLamp'),('a','SourceBadgeTemplate_SourcePower'),('b','SourceBadge02_SourcePower')]:
                    item[key]=any('PowerOn' in slot.material.name for slot in get(o,name).material_slots)
                item['extraBadge']=any(x.get('nodeName')=='SourceBadge03' for x in o.children_recursive)
            item['top']=any(x.hide_render for x in all_mesh(o) if 'FrameAndCassette' in x.name or 'WallBody' in x.name)
            objects.append(item)
        if objects: sheets.append({'name':s.name.replace('StationKit_',''),'objects':objects,'labels':labels})
    (BASE/'gallery_layout.json').write_text(json.dumps({'sheets':sheets},indent=2)+'\n')
    return sheets

if __name__=='__main__':
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    if args and args[0]=='render':
        for name in args[1:]: render(name)
    else:
        make_scenes()
        gallery_layout()
