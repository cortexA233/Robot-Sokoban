"""Package measured evidence; stale or failed validations never become a green manifest."""
from pathlib import Path
import ast
import hashlib
import json

BASE=Path(__file__).resolve().parents[1]
PROJECT=BASE.parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source=json.loads((BASE/'blender_validation.json').read_text())
    fbx=json.loads((BASE/'fbx_validation.json').read_text())
    unity=json.loads((BASE/'unity_validation.json').read_text())
    fbx_path=PROJECT/'Assets/Art/Robot/Meshes/Robot.fbx'
    assert source['passed'] and fbx['passed'] and unity['passed']
    assert source['artifactSha256']==sha(BASE/'Robot.blend'), 'Source changed after validation'
    assert fbx['artifactSha256']==unity['fbxSha256']==sha(fbx_path), 'FBX changed after validation'
    assert source['metrics']['triangles']==fbx['metrics']['triangles']==unity['triangles']
    tree=ast.parse((BASE/'scripts/build_robot.py').read_text())
    specs=next(ast.literal_eval(n.value) for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='COLORS' for t in n.targets))
    materials=[{'name':name,'baseColorSrgb':'#'+spec[0],'metallic':spec[1],'roughness':spec[2],
                'blenderEmissionStrength':2 if name=='M_RobotEmission' else 0,
                'unityEmission':'linear(#63DCEB) * 0.6' if name=='M_RobotEmission' else 'none'} for name,spec in specs.items()]
    manifest={
        'schemaVersion':'robot-asset-v1','assetName':'Robot','targetUnityVersion':'2022.3.51f1',
        'targetRenderPipeline':'URP 14.0.11','blenderVersion':source['blenderVersion'],'unitMeters':1,
        'unityAxes':{'up':'+Y','forward':'+Z','right':'+X'},'blenderAxes':{'up':'+Z','forward':'-Y'},
        'rootNode':'RobotRoot','rootMotion':False,'fps':30,
        'clips':[{'name':name,'startFrame':a,'endFrame':b,'durationSeconds':(b-a)/30,'loop':loop}
                 for name,a,b,loop in [('Idle',0,60,True),('Move',70,100,True),('Push',110,128,False)]],
        'push':{'contactFrame':113,'releaseFrame':125,'contactNormalized':1/6,'releaseNormalized':5/6,
                'strokeMeters':.24,'retractedFaceZ':.36,'extendedFaceZ':.60,'contactHeightY':.4},
        'anchors':{'emotion':{'path':'EmotionAnchor','position':[0,1.05,0]},'pushContact':{'path':'PushSlide/PushContact'}},
        'geometry':source['metrics'],'materials':materials,'textures':[],
        'exportSettings':json.loads((BASE/'export_settings.json').read_text()),
        'unityImport':{'modelRootWrapper':'Robot','robotRootPath':unity['rootPath'],'bakeAxisConversion':True,
                       'preserveHierarchy':True,'animationType':'Generic','avatar':'NoAvatar',
                       'animationCompression':'Off','optimizeGameObjects':False,'applyRootMotion':False,
                       'clips':unity['clips'],'rootEuler':unity['rootEuler'],'rootScale':unity['rootScale']},
        'validation':{'blenderChecked':True,'fbxRoundTripChecked':True,'unityChecked':True,
                      'sourceChecks':len(source['checks']),'fbxChecks':len(fbx['checks']),
                      'unityChecks':len(unity['checks']),'maxUnityContactErrorMeters':unity['maxContactError'],
                      'reports':['ArtSource/Robot/'+n for n in ('blender_validation.json','fbx_validation.json','unity_validation.json')],
                      'scope':'Asset import, local transforms, sampled clips, physical contact face, synchronized one-metre displacement and URP preview.',
                      'gameplayIntegrationChecked':False},
        'previewNotes':{'idle':'Three seamless cycles at 30 fps (6 seconds).',
                        'move':'Three seamless cycles at 30 fps (3 seconds).',
                        'push':'Frames 110-128 inclusive, followed by a 0.6 s neutral hold for viewing; runtime clip duration is 0.6 s.'},
        'knownIssues':[],
        'remainingGameIntegration':['RobotPresenter sampling and grid movement','Blocked/victory emotion bubbles','Undo/restart cancellation and camera modes in complete levels'],
    }
    deliverables=[BASE/'Robot.blend',BASE/'Robot_blockout.blend',fbx_path]
    deliverables+=list((BASE/'scripts').glob('*.py'))
    deliverables+=list((BASE/'previews').glob('*.png'))+list((BASE/'previews').glob('*.mp4'))
    deliverables+=list((BASE/'previews/blockout').glob('*.png'))
    deliverables+=list((PROJECT/'Assets/Resources/prefabs/gameplay/player').glob('Robot.prefab'))
    deliverables+=list((PROJECT/'Assets/Art/Robot/Animations').glob('Robot.controller'))
    deliverables+=list((PROJECT/'Assets/Art/Robot/Materials').glob('*.mat'))
    manifest['files']=[{'path':p.relative_to(PROJECT).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(set(deliverables))]
    (BASE/'robot_asset_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Manifest packaged:',len(deliverables),'files; all three validation stages passed.')


if __name__=='__main__':
    main()
