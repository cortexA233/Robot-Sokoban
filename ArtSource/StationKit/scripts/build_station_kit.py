"""Editable station-kit-v1 geometry. Run through Blender MCP or blender -b -P.

Initial build only. Rebuilding requires --rebuild and first archives the existing
source; normal hand edits should use export_station_kit.py without rebuilding.
All dimensions below are Unity metres (X right, Y up, Z north).
"""
from pathlib import Path
import sys, json, math, shutil, datetime
import bpy, bmesh
from mathutils import Vector, Matrix

BASE = Path(__file__).resolve().parents[1]
PROJECT = BASE.parents[1]
OWNER = 'station-kit-v1'
IDS = ['EnergyCrate','CargoCrate','GoalSocket','UtilitySocket','PowerGate',
       'FloorPlain','FloorService','FloorGrate','WallStraight','WallCorner','WallEnd']
MATERIALS = {
    'Body': ('M_StationBody','#D9DFE5',.12,.48,0),
    'Cargo': ('M_StationCargoBody','#796653',.05,.67,0),
    'Brace': ('M_StationCargoBrace','#C4AC81',.25,.54,0),
    'Dark': ('M_StationDark','#202A35',.18,.62,0),
    'Metal': ('M_StationMetal','#83939F',.65,.38,0),
    'Floor': ('M_StationFloor','#657580',.15,.72,0),
    'LinkAccent': ('M_StationLinkAccent','#FFFFFF',.0,.5,0),
    'StatusEmission': ('M_StationStatusEmission','#273D40',.0,.45,0),
    'Symbol': ('M_StationSymbol','#F2F5EC',.0,.55,0),
    'FixedEnergy': ('M_StationEnergyWindow','#99DAE1',.1,.3,.35),
}

def ub(p): return Vector((p[0],-p[2],p[1]))
def role(key): return {'Cargo':'Body','Brace':'Body','Floor':'Body'}.get(key,key)
def linear(v): return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
def material(key):
    name,h,metal,rough,emission=MATERIALS[key]
    # Preview assembly also uses this factory. Preserve artists' edited source
    # materials instead of resetting them whenever a background or badge is built.
    existing=bpy.data.materials.get(name)
    if existing: return existing
    m=bpy.data.materials.new(name)
    m.use_nodes=True
    n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    rgb=tuple(linear(int(h[i:i+2],16)/255) for i in (1,3,5))+(1,)
    n.inputs['Base Color'].default_value=rgb
    n.inputs['Metallic'].default_value=metal
    n.inputs['Roughness'].default_value=rough
    n.inputs['Emission Color'].default_value=rgb
    n.inputs['Emission Strength'].default_value=emission
    m.diffuse_color=rgb; m['role']=role(key); m['srgb']=h
    m['roughness']=rough; m['metallic']=metal; m['emissionStrength']=emission
    m['owner']=OWNER
    return m

class Mesh:
    """One independently addressable renderer, with disconnected editable parts."""
    def __init__(self): self.v=[]; self.f=[]; self.mi=[]; self.mats=[]
    def add(self,vs,fs,key):
        if key not in self.mats: self.mats.append(key)
        offset=len(self.v); self.v.extend([tuple(ub(v)) for v in vs])
        self.f.extend([tuple(offset+i for i in f) for f in fs])
        self.mi.extend([self.mats.index(key)]*len(fs))
    def box(self,c,s,key='Body',bevel=.008,segments=1,rotation=None):
        bm=bmesh.new()
        bmesh.ops.create_cube(bm,size=1)
        for v in bm.verts: v.co=Vector((v.co.x*s[0],v.co.y*s[1],v.co.z*s[2]))
        if bevel:
            bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(s)*.45),segments=segments,affect='EDGES',profile=.5)
        bm.verts.ensure_lookup_table(); bm.verts.index_update()
        rot=rotation or Matrix.Identity(3)
        vs=[rot@v.co+Vector(c) for v in bm.verts]
        self.add(vs,[[v.index for v in f.verts] for f in bm.faces],key)
        bm.free()
    def bar(self,a,b,width,depth,key='Symbol'):
        direction=Vector(b)-Vector(a)
        # Beam's local X follows a-b; roll is immaterial for square sections.
        rot=Vector((1,0,0)).rotation_difference(direction.normalized()).to_matrix()
        self.box((Vector(a)+Vector(b))*.5,(direction.length,width,depth),key,min(width,depth)*.15,rotation=rot)
    def ring(self,c,r,width,key='Symbol',n=24,normal='top'):
        vs=[]
        for rad in (r,r-width):
            for i in range(n):
                a=math.tau*i/n
                d=(rad*math.cos(a),0,rad*math.sin(a)) if normal=='top' else (rad*math.cos(a),rad*math.sin(a),0)
                vs.append(Vector(c)+Vector(d))
        # Top +Y, front +Z. Reverse top face to keep outward winding.
        fs=[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        if normal=='top': fs=[tuple(reversed(f)) for f in fs]
        self.add(vs,fs,key)
    def polygon(self,points,key='Symbol'):
        self.add(points,[tuple(range(len(points)))],key)
    def object(self,name,parent,col):
        data=bpy.data.meshes.new(name+'_Mesh'); data.from_pydata(self.v,[],self.f); data.update()
        for k in self.mats: data.materials.append(material(k))
        for p,i in zip(data.polygons,self.mi): p.material_index=i
        obj=bpy.data.objects.new(parent.name+'__'+name if parent else name,data); col.objects.link(obj); obj.parent=parent
        obj['owner']=OWNER; obj['nodeName']=name
        return obj

def empty(name,parent,col,pos=(0,0,0)):
    o=bpy.data.objects.new(parent.name+'__'+name if parent else name,None); col.objects.link(o); o.parent=parent; o.location=ub(pos); o['owner']=OWNER; o['nodeName']=name
    return o

def group(root,col,name): return empty(name,root,col)

def rotate_y(p,angle):
    return Matrix.Rotation(angle,3,'Y')@Vector(p)

def rotate_mesh(mesh,angle):
    # Mesh stores Blender vertices, convert to Unity, rotate and convert back.
    rot=Matrix.Rotation(angle,3,'Y')
    mesh.v=[tuple(ub(rot@Vector((p[0],p[2],-p[1])))) for p in mesh.v]
    return mesh

def merge_mesh(dst,src):
    # Preserve each material's independent slot mapping.
    offset=len(dst.v); dst.v.extend(src.v)
    for f,idx in zip(src.f,src.mi):
        k=src.mats[idx]
        if k not in dst.mats: dst.mats.append(k)
        dst.f.append(tuple(offset+i for i in f)); dst.mi.append(dst.mats.index(k))

def crate(root,col,cargo=False):
    geo=group(root,col,'Geometry'); marker=group(root,col,'TypeMarker')
    shell=Mesh(); sign=Mesh(); glass=Mesh()
    shell.box((0,.4,0),(.754,.768,.754),'Dark',.045,2)
    # Eight armour shoes define the exact 0.8 m envelope, without protruding.
    for x in (-.335,.335):
        for z in (-.335,.335):
            for y in (.065,.735): shell.box((x,y,z),(.13,.13,.13),'Metal' if cargo else 'Body',.024,2)
    for side in range(4):
        panel=Mesh(); detail=Mesh(); window=Mesh()
        panel.box((0,.40,.375),(.61,.63,.036),'Cargo' if cargo else 'Body',.021,2)
        # A full, flat robot contact face at Z=.400 and Y=.29-.51.
        panel.box((0,.40,.394),(.50,.22,.012),'Cargo' if cargo else 'Body',.002,1)
        if cargo:
            # X braces end before the flush central contact pad.
            for sx in (-1,1):
                detail.bar((sx*.255,.68,.394),(sx*.075,.505,.394),.036,.010,'Brace')
                detail.bar((sx*.255,.12,.394),(sx*.075,.295,.394),.036,.010,'Brace')
            detail.box((0,.40,.400),(.080,.055,.0008),'Metal',0)
        else:
            panel.box((0,.62,.392),(.47,.105,.012),'Dark',.018,2)
            window.box((0,.62,.399),(.39,.054,.002),'FixedEnergy',.006,2)
            for xx in (-.1,.1): panel.box((xx,.622,.400),(.014,.058,.0005),'Metal',0)
            for xx in (-.19,0,.19): panel.box((xx,.19,.396),(.095,.018,.004),'Dark',.004)
            detail.ring((0,.4,.4001),.039,.008,'Dark',16,normal='front')
        merge_mesh(shell,rotate_mesh(panel,side*math.pi/2))
        merge_mesh(sign,rotate_mesh(detail,side*math.pi/2))
        merge_mesh(glass,rotate_mesh(window,side*math.pi/2))
    shell.box((0,.765,0),(.63,.058,.63),'Cargo' if cargo else 'Body',.021,2)
    if cargo:
        sign.bar((-.25,.797,-.25),(.25,.797,.25),.032,.032,'Brace')
        sign.bar((-.25,.797,.25),(.25,.797,-.25),.032,.032,'Brace')
        # Flatten the top ribs to stay inside the hard envelope.
        sign.v=[(x,y,min(z,.8)) for x,y,z in sign.v]
        sign.box((0,.799,0),(.10,.002,.10),'Metal',0)
    else:
        shell.box((0,.794,0),(.42,.006,.42),'Dark',.002)
        sign.ring((0,.7995,0),.16,.038,'FixedEnergy',32)
        for a in range(4):
            t=Mesh(); t.box((0,.7995,0.095),(.035,.001,.115),'Symbol',0)
            merge_mesh(sign,rotate_mesh(t,a*math.pi/2))
    shell.object('Shell',geo,col); sign.object('CargoBraces' if cargo else 'EnergyGlyph',marker,col)
    if not cargo: glass.object('FixedWindows',group(root,col,'EnergyWindow'),col)
    for i,d in enumerate(((0,.4,.4),(.4,.4,0),(0,.4,-.4),(-.4,.4,0))):
        empty('PushContact_'+['N','E','S','W'][i],root,col,d)

def glyph(mesh,kind,c,size,key='Symbol'):
    x,y,z=c
    if kind=='goal':
        mesh.ring(c,size*.5,size*.11,key,16)
        mesh.ring(c,size*.25,size*.09,key,16)
    elif kind=='plug':
        mesh.box((x,y,z),(size*.55,.0007,size*.4),key,0)
        for dx in (-.17,.17): mesh.box((x+dx*size,y,z+size*.28),(size*.11,.0007,size*.25),key,0)
        mesh.box((x,y,z-size*.28),(size*.12,.0007,size*.25),key,0)
    elif kind=='triangle':
        mesh.polygon([(x-size*.48,y,z-size*.4),(x,y,z+size*.45),(x+size*.48,y,z-size*.4)],key)
    elif kind=='circle': mesh.ring(c,size*.43,size*.16,key,20)
    else: mesh.box(c,(size*.7,.0007,size*.7),key,0)

def socket(root,col,goal=True):
    shell=Mesh(); typ=Mesh(); link=Mesh(); status=Mesh()
    glyph(typ,'goal' if goal else 'plug',(0,.001,0),.36,'Symbol')
    # Only the rim overlays a normal floor. The central 0.8 m remains exactly Y=0.
    for side in range(4):
        rim=Mesh(); rim.box((0,-.027,.438),(.94,.05,.064),'Dark',.006)
        rim.box((0,.0003,.437),(.77,.001,.053),'Metal',.0002)
        merge_mesh(shell,rotate_mesh(rim,side*math.pi/2))
        t=Mesh(); glyph(t,'goal' if goal else 'plug',(0,.0018,.438),.048)
        merge_mesh(typ,rotate_mesh(t,side*math.pi/2))
        l=Mesh(); l.box((-.23,.0015,.438),(.22,.0005,.04),'LinkAccent',0)
        glyph(l,'square',(-.345,.002,.438),.034,'Dark')
        merge_mesh(link,rotate_mesh(l,side*math.pi/2))
        s=Mesh(); s.ring((.16,.002,.438),.018,.005,'Symbol',16)
        s.box((.23,.002,.438),(.085,.0005,.027),'StatusEmission',0)
        merge_mesh(status,rotate_mesh(s,side*math.pi/2))
    # Raised corner pads are outside BOTH 0.8 m orthogonal swept corridors.
    for x in (-.443,.443):
        for z in (-.443,.443): shell.box((x,.0,z),(.054,.01,.054),'Body',.008)
    shell.object('Rim',group(root,col,'Geometry'),col)
    typ.object('TargetRings' if goal else 'PlugSymbols',group(root,col,'TypeMarker'),col)
    link.object('IdentityBands',group(root,col,'LinkMarkers'),col)
    status.object('PowerLamps',group(root,col,'PowerStatus'),col)
    empty('LabelAnchor',root,col,(0,.01,.438))

def badge(root,col,name,pos,shape='square'):
    p=empty(name,root,col,pos); ident=Mesh(); lamp=Mesh()
    ident.box((0,0,0),(.195,.014,.066),'Dark',.004)
    ident.box((-.018,.0075,0),(.147,.001,.052),'LinkAccent',0)
    glyph(ident,shape,(-.025,.0085,0),.043,'Dark')
    lamp.box((.077,.008,0),(.022,.001,.042),'StatusEmission',0)
    ident.object(name+'_Identity',p,col); lamp.object(name+'_SourcePower',p,col)
    return p

def gate(root,col):
    base=Mesh(); upper=Mesh()
    for x in (-.46,.46):
        for z in (-.46,.46):
            base.box((x,.045,z),(.06,.09,.06),'Dark',.006)
            upper.box((x,.76,z),(.06,1.34,.06),'Metal',.009,2)
            upper.box((x,1.52,z),(.06,.16,.06),'Body',.009,2)
    # Top ring and two-track shutter cassette: every fixed cross beam is above 1.04 m.
    for z in (-.445,.445): upper.box((0,1.52,z),(.92,.16,.09),'Body',.016,2)
    for x in (-.465,.465): upper.box((x,1.52,0),(.05,.16,.80),'Body',.010,2)
    for z in (-.16,.16): upper.box((0,1.335,z),(.86,.59,.045),'Body',.018,2)
    upper.box((0,1.62,0),(.86,.06,.28),'Dark',.008)
    for x in (-.30,-.15,0,.15,.30): upper.box((x,1.650,0),(.075,.001,.18),'Metal',0)
    base.object('CornerFeet',group(root,col,'Base'),col)
    upper.object('FrameAndCassette',group(root,col,'UpperStructure'),col)
    moving=group(root,col,'MovingParts')
    for name,y,z in [('LowerPanel',.29,.045),('UpperPanel',.87,-.045)]:
        pivot=empty(name,moving,col,(0,y,z)); panel=Mesh()
        panel.box((0,0,0),(.85,.56,.070),'Dark',.018,2)
        for zz in (-.037,.037):
            panel.box((0,0,zz),(.77,.47,.006),'Body',.002)
            for xx in (-.31,.31): panel.box((xx,0,zz*1.08),(.018,.34,.001),'Metal',0)
            panel.box((0,-.19,zz*1.1),(.36,.025,.001),'Dark',0)
        panel.object(name+'Mesh',pivot,col)
        pivot['Closed']=json.dumps({'position':[0,y,z],'rotation':[0,0,0],'scale':[1,1,1]})
        pivot['Open']=json.dumps({'position':[0,1.305,z],'rotation':[0,0,0],'scale':[1,1,1]})
        for frame,height in [(1,y),(15,y),(39,1.305),(64,1.305),(88,y),(100,y)]:
            pivot.location=ub((0,height,z)); pivot.keyframe_insert('location',frame=frame)
        pivot.location=ub((0,y,z))
    markers=group(root,col,'TopDownMarkers'); closed=Mesh(); opened=Mesh()
    for s in (-1,1):
        closed.box((0,.002,s*.32),(.54,.001,.030),'Symbol',0)
        for x in (-.16,.16):
            opened.bar((x-.045,.001,s*.27),(x,.001,s*.33),.001,.012,'Symbol')
            opened.bar((x,.001,s*.33),(x+.045,.001,s*.27),.001,.012,'Symbol')
    closed.object('ClosedMark',markers,col); opened.object('OpenMark',markers,col)
    badge(root,col,'SourceBadgeTemplate',(-.14,-.0065,.446),'square')
    badge(root,col,'SourceBadge02',(.14,-.0065,.446),'triangle')
    empty('SourceBadgeAnchor',root,col,(-.28,-.0065,-.446))
    empty('PowerModeAnchor',root,col,(0,.01,-.31))
    p=Mesh(); p.box((0,.001,-.446),(.10,.001,.035),'StatusEmission',0)
    p.object('ConditionLamp',group(root,col,'PowerStatus'),col)

def floor(root,col,style):
    m=Mesh()
    m.box((0,-.135,0),(.98,.21,.98),'Floor',.012)
    if style=='Plain':
        m.box((0,-.015,0),(.948,.03,.948),'Floor',.009)
        # Large calm panels, with a shallow service seam.
    elif style=='Service':
        for x in (-.412,.412): m.box((x,-.015,0),(.124,.03,.948),'Floor',.005)
        for z in (-.412,.412): m.box((0,-.015,z),(.69,.03,.124),'Floor',.005)
        m.box((0,-.009,0),(.683,.018,.683),'Floor',.010)
        for x in (-.295,.295):
            for z in (-.295,.295): m.box((x,-.0005,z),(.028,.001,.028),'Floor',0)
    else:
        for x in (-.423,.423): m.box((x,-.012,0),(.102,.024,.948),'Floor',.004)
        for z in (-.423,.423): m.box((0,-.012,z),(.74,.024,.102),'Floor',.004)
        # 5x5 shallow grid: no rail-like directional language.
        for i in range(6):
            q=-.345+i*.138
            m.box((q,-.010,0),(.049,.02,.737),'Floor',0)
            m.box((0,-.014,q),(.737,.028,.049),'Floor',0)
    m.object('Deck',root,col)

def wall(root,col,style):
    base=Mesh(); upper=Mesh()
    base.box((0,.12,0),(.98,.24,.98),'Dark',.025)
    # Full solid cell: appearance modules never leave a false L-shaped walkable gap.
    upper.box((0,.865,0),(.92,1.25,.92),'Body',.035)
    upper.box((0,1.515,0),(.98,.05,.98),'Metal',.012)
    exposed={'Straight':(0,2),'Corner':(0,1),'End':(0,1,3)}[style]
    for side in range(4):
        f=Mesh(); f.box((0,.88,.468),(.83,1.12,.025),'Body',.018)
        f.box((0,.295,.481),(.83,.035,.014),'Dark',.004)
        if side in exposed:
            f.box((0,.92,.474),(.66,.77,.014),'Dark',.006)
            f.box((0,.95,.485),(.61,.69,.006),'Body',.002)
            for yy in (.58,.625,.67): f.box((0,yy,.489),(.34,.012,.002),'Metal',0)
        merge_mesh(upper,rotate_mesh(f,side*math.pi/2))
    base.object('Plinth',group(root,col,'Base'),col)
    upper.object('WallBody',group(root,col,'Upper'),col)

def build():
    old=bpy.data.scenes.get('StationKit_Workshop')
    if old and '--rebuild' not in sys.argv:
        raise RuntimeError('Source already exists: export hand edits, or explicitly use --rebuild after reviewing changes.')
    path=BASE/'StationKit.blend'
    if path.exists() and '--rebuild' not in sys.argv: raise RuntimeError('Existing source protected; open it to edit/export.')
    if path.exists():
        backup=PROJECT/'Logs/StationKitBackups'/datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
        backup.mkdir(parents=True,exist_ok=True); shutil.copy2(path,backup/path.name)
    if old:
        # Only remove owned objects, never other scenes or user collections.
        for obj in list(old.objects):
            if obj.get('owner')==OWNER: bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(old)
        for c in list(bpy.data.collections):
            if c.get('owner')==OWNER and not c.all_objects: bpy.data.collections.remove(c)
    scene=bpy.data.scenes.new('StationKit_Workshop'); scene['owner']=OWNER
    scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1; scene.render.fps=30
    scene.frame_start=1; scene.frame_end=100
    bpy.context.window.scene=scene
    master=bpy.data.collections.new('StationKit_Assets'); master['owner']=OWNER; scene.collection.children.link(master)
    for name in ('StationKit_Reference','StationKit_Preview'):
        c=bpy.data.collections.new(name); c['owner']=OWNER; scene.collection.children.link(c)
    for aid in IDS:
        c=bpy.data.collections.new(aid); c['owner']=OWNER; master.children.link(c)
        root=empty(aid+'Root',None,c); root['assetId']=aid
        if aid.endswith('Crate'): crate(root,c,aid=='CargoCrate')
        elif aid.endswith('Socket'): socket(root,c,aid=='GoalSocket')
        elif aid=='PowerGate': gate(root,c)
        elif aid.startswith('Floor'): floor(root,c,aid[5:])
        elif aid.startswith('Wall'): wall(root,c,aid[4:])
    scene.frame_set(1); bpy.context.view_layer.update()
    BASE.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    print('Built 11 assets:',[(a,len(bpy.data.objects[a+'Root'].children_recursive)) for a in IDS])
    return scene

if __name__=='__main__': build()
