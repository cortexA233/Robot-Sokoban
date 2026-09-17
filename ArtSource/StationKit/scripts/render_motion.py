"""Render Blender mechanical animation and an explicitly staged occupancy demo.
Frames go to ignored Logs; encode with package_evidence.py after rendering.
"""
from pathlib import Path
import runpy, bpy, json, math
BASE=Path(__file__).resolve().parents[1]; PROJECT=BASE.parents[1]
p=runpy.run_path(str(BASE/'scripts/preview_station_kit.py'))
ub=p['ub']; get=p['get']; scene=p['scene']; clone=p['clone']; state=p['state']

def output(s,name,frame):
    target=PROJECT/'Logs/StationKitFrames'/name; target.mkdir(parents=True,exist_ok=True)
    s.render.resolution_x=960; s.render.resolution_y=540
    s.render.filepath=str(target/f'{frame:04d}.png'); bpy.ops.render.render(write_still=True)

def main():
    source=bpy.data.scenes['StationKit_Workshop']; source.frame_set(1)
    s,c=scene('StationKit_MechanicalEvidence'); g=clone('PowerGate',c)
    p['floor_at'](c,0,0); p['camera'](s,(2.5,2.5,4),(0,.7,0),3.6)
    p['label'](c,'TWO-TRACK SHUTTER / BLENDER',(0,-.24,.9),.10)
    for i,frame in enumerate(range(1,101,3)):
        source.frame_set(frame); bpy.context.window.scene=source; bpy.context.view_layer.update()
        matrices={name:bpy.data.objects['PowerGateRoot__MovingParts__'+name].matrix_basis.copy() for name in ('LowerPanel','UpperPanel')}
        bpy.context.window.scene=s
        for name,matrix in matrices.items(): get(g,name).matrix_basis=matrix
        op=frame>=28 and frame<=76; get(g,'OpenMark').hide_render=not op; get(g,'ClosedMark').hide_render=op
        output(s,'blender_mechanical',i)
    source.frame_set(1)
    s,c=scene('StationKit_OccupancyEvidence'); gate=clone('PowerGate',c); state(gate,True,False)
    for x in range(-1,2):
        for z in range(-1,3): p['floor_at'](c,x,z)
    cargo=clone('CargoCrate',c); robot=p['robot'](c,(0,0,-1),push=True)
    title=p['label'](c,'CARGO HOLDS OPEN / POWER OFF',(0,-.23,3.0),.12)
    p['camera'](s,(3.4,4.3,5.4),(0,.5,.45),5.8)
    samples=[]
    for i in range(72):
        if i<16: z=0; x=0; name='CARGO HOLDS OPEN / POWER OFF'; openness=1
        elif i<36:
            z=(i-16)/19; z=z*z*(3-2*z); x=0; name='CARGO TO ROBOT / POWER OFF'; openness=1
        elif i<48: z=1; x=0; name='ROBOT HOLDS OPEN / POWER OFF'; openness=1
        elif i<60:
            z=1; x=(i-48)/11; x=x*x*(3-2*x); name='ROBOT LEAVES / POWER OFF'; openness=1
        else:
            z=1; x=1; t=(i-60)/11; openness=1-t*t*(3-2*t); name='EMPTY - CLOSE / POWER OFF'
        cargo.location=ub((0,0,z)); robot.location=ub((x,0,z-1)); title.data.body=name
        if i>=48:
            robot.rotation_euler.z=-math.pi/2; get(robot,'PushSlide').location.y=-.34
        for part,closed in [('LowerPanel',.29),('UpperPanel',.87)]:
            o=get(gate,part); o.location.z=closed+(1.305-closed)*openness
        get(gate,'OpenMark').hide_render=openness<.5; get(gate,'ClosedMark').hide_render=openness>=.5
        output(s,'blender_occupancy',i)
        samples.append({'frame':i,'phase':name,'powered':False,'cargoPosition':[0,0,z],'robotPosition':[x,0,z-1],'openFraction':openness})
    (BASE/'validation/blender_motion.json').write_text(json.dumps({'mechanicalSourceFrames':[1,100],'mechanicalSampleStep':3,'mechanicalFPS':10,'occupancyFPS':12,'driver':'Explicit presentation fixture, all occupancy-sequence source and condition lamps stay off.','samples':samples},indent=2)+'\n')

if __name__=='__main__': main()
