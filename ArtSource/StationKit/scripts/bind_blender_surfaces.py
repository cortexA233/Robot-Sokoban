"""Use the same packed surface masks in editable Blender and Unity materials."""
from pathlib import Path
import bpy

PROJECT=Path(__file__).resolve().parents[3]
PROFILES={'M_RobotBody':'Coating','M_RobotDark':'Rubber','M_StationBody':'Coating',
          'M_StationDark':'Polymer','M_StationMetal':'Metal','M_StationCargoBody':'Polymer',
          'M_StationCargoBrace':'Brace'}


def data_image(path):
    image=bpy.data.images.load(str(path),check_existing=True)
    spaces=[e.identifier for e in image.colorspace_settings.bl_rna.properties['name'].enum_items]
    image.colorspace_settings.name=next(s for s in spaces if s.lower() in ('non-color','raw'))
    image.filepath=bpy.path.relpath(str(path))
    return image


def bind():
    prefix='M_Robot' if Path(bpy.data.filepath).stem=='Robot' else 'M_Station'
    for name,profile in PROFILES.items():
        if not name.startswith(prefix) or name not in bpy.data.materials: continue
        material=bpy.data.materials[name]; nodes=material.node_tree.nodes; links=material.node_tree.links
        bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
        tint={'M_StationCargoBrace':'A3A69A','M_StationCargoBody':'74675B'}.get(name)
        if tint:
            rgb=[int(tint[i:i+2],16)/255 for i in (0,2,4)]
            linear=tuple(c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in rgb)+(1,)
            bsdf.inputs['Base Color'].default_value=linear; material.diffuse_color=linear
            material['srgb']='#'+tint
        for node in list(nodes):
            if node.label.startswith('Art polish /'): nodes.remove(node)
        texture=nodes.new('ShaderNodeTexImage'); texture.label='Art polish / packed surface'
        texture.image=data_image(PROJECT/'Assets/Art/StationKit/Textures'/(profile+'.png'))
        rough=nodes.new('ShaderNodeMath'); rough.label='Art polish / roughness'
        operations=[e.identifier for e in rough.bl_rna.properties['operation'].enum_items]
        rough.operation=next(op for op in operations if op=='SUBTRACT'); rough.inputs[0].default_value=1
        links.new(texture.outputs['Alpha'],rough.inputs[1]); links.new(rough.outputs[0],bsdf.inputs['Roughness'])
        separate=nodes.new('ShaderNodeSeparateColor'); separate.label='Art polish / metallic'
        links.new(texture.outputs['Color'],separate.inputs[0]); links.new(separate.outputs[0],bsdf.inputs['Metallic'])
        normal_texture=nodes.new('ShaderNodeTexImage'); normal_texture.label='Art polish / micro normal'
        normal_texture.image=data_image(PROJECT/'Assets/Art/StationKit/Textures/MicroNormal.png')
        normal=nodes.new('ShaderNodeNormalMap'); normal.label='Art polish / normal strength'
        normal.inputs['Strength'].default_value=.25; normal.uv_map='SurfaceUV'
        links.new(normal_texture.outputs['Color'],normal.inputs['Color']); links.new(normal.outputs[0],bsdf.inputs['Normal'])
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
    print('Bound shared packed masks in '+prefix+' source materials')


if __name__=='__main__': bind()
