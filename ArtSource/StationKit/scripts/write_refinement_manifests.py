"""Package current measured v0.11.0 assets without promoting old preview reports."""
from pathlib import Path
import datetime, hashlib, json
import xml.etree.ElementTree as ET

PROJECT=Path(__file__).resolve().parents[3]
KIT=PROJECT/'ArtSource/StationKit'
ROBOT=PROJECT/'ArtSource/Robot'
EVIDENCE=PROJECT/'Docs/Versions/V0.11.0Validation'


def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def relative(path): return path.relative_to(PROJECT).as_posix()


def package():
    unity=read(EVIDENCE/'UnityAssets.json')
    station=read(KIT/'validation/blender_fbx_validation.json')
    robot=read(ROBOT/'blender_validation.json')
    robot_fbx=read(ROBOT/'fbx_validation.json')
    assert all(r['passed'] for r in (unity,station,robot,robot_fbx))
    assert station['sourceSha256']==sha(KIT/'StationKit.blend')
    assert robot['artifactSha256']==sha(ROBOT/'Robot.blend')
    assert robot_fbx['artifactSha256']==sha(PROJECT/'Assets/Art/Robot/Meshes/Robot.fbx')
    outcomes={}
    for filename in ('PlayModeInitial.xml','PlayModeRetest.xml'):
        for test in ET.parse(EVIDENCE/filename).iter('test-case'):
            outcomes[test.attrib['fullname']]=test.attrib['result']
    assert len(outcomes)==38 and all(v=='Passed' for v in outcomes.values()), outcomes
    for asset in unity['assets']:
        assert sha(PROJECT/asset['fbx'])==asset['fbxSha256']
        assert sha(PROJECT/asset['prefab'])==asset['prefabSha256']
        metrics=robot['metrics'] if asset['assetId']=='Robot' else station['fbx'][asset['assetId']]
        assert metrics['triangles']==asset['triangles']
    textures=[relative(p) for p in (PROJECT/'Assets/Art/StationKit/Textures').glob('*.png')]
    shared={
        'developmentVersion':'v0.11.0','checkedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'tools':{'blender':'5.2.2 LTS','unity':unity['unityVersion'],'urp':'14.0.11','cinemachine':'2.10.7'},
        'textureDependencies':sorted(textures),
        'surfaceConvention':'Linear packed PNG: R=metallic, A=smoothness; shared tangent-space normal. Repeat + mipmaps. Existing URP/Lit materials and GUIDs.',
        'verification':{'sourceAndFbx':'passed','unityAssetChecks':len(unity['checks']),
            'relevantPlayModeUniquePassed':len(outcomes),'reports':[relative(EVIDENCE/n) for n in ('UnityAssets.json','PlayModeInitial.xml','PlayModeRetest.xml')],
            'scope':'Measured asset geometry/material references, robot clips/contact, imported transport surfaces, power/gate/movement/camera regressions. See version plan for visual and Player checks.'},
        'historicalEvidence':'Pre-v0.11.0 standalone Unity showcase reports and older preview images describe the initial production baseline, not the current exported files.'
    }
    for label,base,source_file,manifest_name in [('StationKit',KIT,'StationKit.blend','station_kit_asset_manifest.json'),('Robot',ROBOT,'Robot.blend','robot_asset_manifest.json')]:
        records=[a for a in unity['assets'] if (a['assetId']=='Robot')==(label=='Robot')]
        result={**shared,'protocol':'robot-asset-v1' if label=='Robot' else 'station-kit-v1',
            'assetName':label,'source':relative(base/source_file),'sourceSha256':sha(base/source_file),
            'assets':records,'unitMeters':1,'unityAxes':{'up':'+Y','forward':'+Z'},'sourceBlenderFromUnity':'(x,-z,y)'}
        if label=='Robot':
            result.update(schemaVersion='robot-asset-v1',geometry=robot['metrics'],rootNode='RobotRoot',rootMotion=False,
                clips=[{'name':n,'startFrame':a,'endFrame':b,'durationSeconds':(b-a)/30,'loop':loop}
                    for n,a,b,loop in [('Idle',0,60,True),('Move',70,100,True),('Push',110,128,False)]],
                anchors={'emotion':'RobotRoot/EmotionAnchor','contact':'RobotRoot/PushSlide/PushContact'},
                push={'retractedFaceZ':.36,'extendedFaceZ':.60,'contactHeightY':.4,'strokeMeters':.24},
                currentPreviews=['ArtSource/Robot/previews/refined_front.png','ArtSource/Robot/previews/refined_back.png'])
        else:
            result['sourceAndFbx']=station
            result['materials']=read(KIT/'validation/source_interfaces.json')['materials']
            result['runtimeSocketRoles']={'geometry':'Imported Geometry/Rim remains visible.',
                'identityAndPower':'Board-owned Circuit socket marks; old imported symbols stay hidden. A Cargo crate does not power the socket.'}
        files={base/source_file}
        files.update(p for p in (base/'scripts').glob('*') if p.suffix in ('.py','.cs'))
        for record in records:
            files.update(PROJECT/record[k] for k in ('fbx','prefab'))
            for renderer in record['renderers']:
                files.update(PROJECT/m['path'] for m in renderer['materials'])
        files.update(PROJECT/p for p in textures)
        result['files']=[{'path':relative(p),'sha256':sha(p),'bytes':p.stat().st_size} for p in sorted(files)]
        (base/manifest_name).write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'assets':len(unity['assets']),'sourceChecks':len(station['checks'])+len(robot['checks'])+len(robot_fbx['checks']),'unityChecks':len(unity['checks']),'playModeUniquePassed':len(outcomes)}))


if __name__=='__main__': package()
