"""Small seamless, deterministic metallic/smoothness masks authored in Blender.

R = metallic; A = smoothness (URP metallic workflow). The normal map is a subtle
machined surface, not a painted colour texture. All patterns are periodic.
"""
from pathlib import Path
import math
import bpy

PROJECT = Path(__file__).resolve().parents[3]
OUTPUT = PROJECT/'Assets/Art/StationKit/Textures'
PROFILES = {'Coating':(.18,.59), 'Metal':(.78,.71), 'Polymer':(.10,.37),
            'Rubber':(.025,.23), 'Brace':(.48,.57)}


def create():
    OUTPUT.mkdir(parents=True,exist_ok=True)
    size=256
    for name,(metal,smooth) in list(PROFILES.items())+[('MicroNormal',(0,0))]:
        image=bpy.data.images.new('ArtSurface_'+name,width=size,height=size,alpha=True)
        spaces=[e.identifier for e in image.colorspace_settings.bl_rna.properties['name'].enum_items]
        data_space=next((s for s in spaces if s.lower() in ('non-color','raw')),None)
        if data_space: image.colorspace_settings.name=data_space
        values=[]
        for y in range(size):
            v=y/size*math.tau
            for x in range(size):
                u=x/size*math.tau
                fine=math.sin(43*u+17*v)*math.sin(23*u-37*v)
                broad=math.sin(3*u+2*v)*math.sin(5*v-u)
                brush=math.sin(79*v+math.sin(4*u))
                if name=='MicroNormal':
                    nx=.018*math.sin(43*u+17*v)
                    ny=.018*math.cos(23*u-37*v)
                    values.extend((.5+nx,.5+ny,math.sqrt(1-4*nx*nx-4*ny*ny)*.5+.5,1))
                else:
                    variation=.025*broad+.015*fine+(.018*brush if name in ('Metal','Brace') else 0)
                    values.extend((metal,0,0,smooth+variation))
        image.pixels.foreach_set(values)
        image.filepath_raw=str(OUTPUT/(name+'.png'))
        formats=[e.identifier for e in image.bl_rna.properties['file_format'].enum_items]
        image.file_format=next(f for f in formats if f=='PNG')
        image.save()
        bpy.data.images.remove(image)
    print('Created 5 linear packed surface masks and 1 tangent-space normal map')


if __name__=='__main__': create()
