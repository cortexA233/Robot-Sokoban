"""Render clean orthographic views and actual keyed animation; references never export.

blender -b Robot.blend -P scripts/render_previews.py
Pass -- --blockout for the saved white model's four views only.
"""
from pathlib import Path
import sys
import runpy
import bpy

BASE=Path(__file__).resolve().parents[1]
helpers=runpy.run_path(str(BASE/'scripts/build_robot.py'))
set_camera=helpers['set_camera']
scene=bpy.data.scenes['Robot_Workshop']
bpy.context.window.scene=scene
blockout='--blockout' in sys.argv
out=BASE/'previews'/('blockout' if blockout else '')
out.mkdir(parents=True,exist_ok=True)
scene.render.resolution_x=720; scene.render.resolution_y=720
scene.render.resolution_percentage=100
if hasattr(scene,'eevee'):
    scene.eevee.taa_render_samples=32
views={'front':((0,-3,.40),(0,0,.4),1.14),
       'side':((3,0,.40),(0,0,.4),1.14),
       'back':((0,3,.40),(0,0,.4),1.14),
       'top':((0,0,3),(0,0,0),1.14),
       'third_person':((1.4,2,1.7),(0,0,.38),1.35),
       'hero':((1.4,-2,1.3),(0,0,.40),1.35)}
scene.frame_set(0)
for name,(loc,target,scale) in views.items():
    if blockout and name in ('hero','third_person'):
        continue
    set_camera(scene,loc,target,scale)
    scene.render.filepath=str(out/(name+'.png'))
    bpy.ops.render.render(write_still=True)
if not blockout:
    scene.render.resolution_x=560; scene.render.resolution_y=560
    for clip,start,end,loc,target,scale in [
        ('idle',0,60,(1.4,-2,1.3),(0,0,.4),1.35),
        ('move',70,100,(2,-1.2,1.0),(0,0,.4),1.35),
        ('push',110,128,(2,-1.3,1.1),(0,-.12,.4),1.6)]:
        frames=out/'frames'/clip; frames.mkdir(parents=True,exist_ok=True)
        set_camera(scene,loc,target,scale)
        for frame in range(start,end+1):
            scene.frame_set(frame)
            scene.render.filepath=str(frames/f'{frame-start:04d}.png')
            bpy.ops.render.render(write_still=True)
    # A stationary root and the 0.8 m contact box, seen from the side.
    cube=bpy.data.objects['Reference_EnergyBox']; cube.hide_render=False
    scene.frame_set(113)
    set_camera(scene,(3,-.5,1.3),(0,-.45,.4),1.95)
    scene.render.resolution_x=900; scene.render.resolution_y=640
    scene.render.filepath=str(out/'push_contact.png')
    bpy.ops.render.render(write_still=True)
print('ROBOT_PREVIEWS_COMPLETE',out)
