"""Render the current saved robot source from front/rear without resaving it."""
from pathlib import Path
import bpy
from mathutils import Vector

BASE=Path(__file__).resolve().parents[1]
scene=bpy.data.scenes['Robot_Workshop']; bpy.context.window.scene=scene
scene.frame_set(0)
scene.render.resolution_x=900; scene.render.resolution_y=900
scene.render.resolution_percentage=100
formats=[v.identifier for v in scene.render.image_settings.bl_rna.properties['file_format'].enum_items]
scene.render.image_settings.file_format=next(v for v in formats if v=='PNG')
camera=scene.camera; camera.data.ortho_scale=1.20
for label,location in [('refined_front',(1.3,-1.9,1.25)),('refined_back',(1.4,1.9,1.2))]:
    camera.location=location
    camera.rotation_euler=(Vector((0,0,.4))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(BASE/'previews'/(label+'.png'))
    bpy.ops.render.render(write_still=True)
