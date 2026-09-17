"""Write a delivery manifest only when saved files match measured report hashes."""
from pathlib import Path
import json, hashlib, datetime, re

BASE=Path(__file__).resolve().parents[1]; PROJECT=BASE.parents[1]
def read(name): return json.loads((BASE/'validation'/name).read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def rel(path): return path.relative_to(PROJECT).as_posix()
def phase(metrics): return {'status':'passed',**metrics}

def write():
    bf=read('blender_fbx_validation.json'); uv=read('unity_validation.json'); presentation=read('unity_presentation.json')
    occupancy=read('unity_occupancy.json'); visual=read('visual_review.json'); video=read('video_validation.json'); interfaces=read('source_interfaces.json')
    assert all(r['passed'] for r in (bf,uv,presentation,occupancy,visual,video)), 'A report has failed'
    assert bf['sourceSha256']==sha(BASE/'StationKit.blend')==interfaces['sourceSha256'],'Source changed after inspection; reopen and revalidate.'
    assets=[]
    for record in uv['assets']:
        aid=record['assetId']; fbx=PROJECT/'Assets/Art/StationKit/Meshes'/(aid+'.fbx'); prefab=PROJECT/'Assets/Resources/prefabs/gameplay/StationKit'/(aid+'.prefab')
        assert record['fbxSha256']==bf['fbx'][aid]['sha256']==sha(fbx),aid+' changed after FBX/Unity checks'
        assert prefab.exists(),prefab
        renderer_paths=[r['path'] for r in record['renderers']]
        hidden=[p for p in renderer_paths if p.startswith(('Upper/','UpperStructure/','MovingParts/'))]
        item={'assetId':aid,'kind':{'EnergyCrate':'Energy','CargoCrate':'Cargo'}.get(aid),
            'source':rel(BASE/'StationKit.blend'),'sourceScene':'StationKit_Workshop','sourceCollection':'StationKit_Assets/'+aid,
            'sourceObjectNaming':'<AssetId>Root__<group>__<mesh>; nodeName property defines exported semantic name',
            'fbx':rel(fbx),'prefab':rel(prefab),'wrapperRoot':aid,'rootPath':record['rootPath'],
            'coordinates':{'unit':'metre','rootPosition':[0,0,0],'rootRotation':[0,0,0],'rootScale':[1,1,1],'up':'+Y','front':'+Z','right':'+X','sourceBlenderFromUnity':'(x,-z,y)'},
            'stages':{'source':phase(bf['source'][aid]),'fbxRoundtrip':phase(bf['fbx'][aid]),'unity':phase(record)},
            'materialBindings':record['renderers'],'topDown':{'hideRenderers':hidden,'keepRenderers':[p for p in renderer_paths if p not in hidden]},
            'sha256':{'fbx':sha(fbx),'prefab':sha(prefab)},'textureDependencies':[]}
        if aid.endswith('Crate'):
            item['pushContacts']={d:'PushContact_'+d for d in ('N','E','S','W')}
            item['appearance']='Fixed appearance, no LinkAccent or StatusEmission; type selected only from explicit kind.'
        if aid.endswith('Socket'):
            item['interfaces']={'identity':'LinkMarkers/IdentityBands','type':'TypeMarker','power':'PowerStatus/PowerLamps','label':'LabelAnchor',
                'powerRule':'Energy occupancy only; empty, Robot and Cargo remain off.', 'overlayToleranceMetres':.003}
        if aid=='PowerGate':
            item['movingParts']={f'MovingParts/{n}':p for n,p in interfaces['gate'].items()}
            item['animation']={'blenderFPS':30,'frameRange':[1,100],'rootAnimated':False,'fbxClips':[],
                'note':'Authored mechanical keys in Blender; Unity restores explicit poses and implements interruptible playback during later integration.'}
            item['sourceBadges']={'template':'SourceBadgeTemplate','defaultVisibleInstances':['SourceBadgeTemplate','SourceBadge02'],
                'renderersPerBadge':['<badgeName>_Identity','<badgeName>_SourcePower'],'anchor':'SourceBadgeAnchor','anchorLocalPosition':[-.28,-.0065,-.446],
                'repeatDirection':[1,0,0],'spacing':.28,'inCellBackRowCapacity':3,'moreInputs':'External expansion display; do not overlap badges.',
                'orientation':'Upward surface, visible from elevated front and top views','modeAnchor':'PowerModeAnchor'}
            item['stateInterfaces']={'conditionPower':'PowerStatus/ConditionLamp','closed':'TopDownMarkers/ClosedMark','open':'TopDownMarkers/OpenMark',
                'defaultActiveMarker':'ClosedMark','semantics':'Source power, condition power and effective passage are three independent inputs.'}
            item['clearance']={'minimumWidth':.86,'minimumHeight':1.025,'aboveGroundHeight':1.6505,'badgeEmbeddingDepth':.0135}
        assets.append(item)

    evidence={
        'A01':('Reopen source and independently import each of 11 FBX files; export contains MESH/EMPTY hierarchy only.',['validation/blender_fbx_validation.json']),
        'A02':('Separate source, raw FBX and Unity measurements; 36 raycasts per crate; source contact anchors and Unity N/E axes.',['validation/blender_fbx_validation.json','validation/unity_validation.json']),
        'A03':('Triangle/AABB SAT against actual asset triangles, both orthogonal swept corridors for each crate and measured robot envelope; 3 mm overlay tolerance.',['validation/blender_fbx_validation.json','validation/unity_validation.json','previews/blender_crates.png']),
        'A04':('Six explicit socket occupancy fixtures; only Energy illuminates independent lamps; visual inspection of covered perimeter.',['previews/blender_sockets.png','previews/unity_sockets_1080.png','validation/unity_presentation.json']),
        'A05':('Blue square / purple triangle / lime circle; change only A materials, verify B and fixed body material references unchanged; crate roles have no variable slots.',['previews/unity_colors_1080.png','validation/unity_validation.json']),
        'A06':('Explicit A=Energy/on, B=Cargo/off fixtures for Any, All and shared A on another gate; independent third badge.',['previews/unity_connections_1080.png','validation/unity_presentation.json']),
        'A07':('100 source mechanical frames tested; closed/powered-open/unpowered-held fixtures; 72-frame Cargo -> Robot -> empty demo with every source/condition lamp off.',['previews/blender_mechanical.mp4','previews/blender_occupancy.mp4','previews/unity_occupancy.mp4','previews/unity_gates_front_1080.png','previews/unity_gates_top_1080.png','validation/unity_occupancy.json']),
        'A08':('Hide all upper wall, gate post/cassette and moving-panel renderers; retain bases and low passage/source markers.',['previews/unity_corridor_gray_720.png','validation/unity_presentation.json']),
        'A09':('Mixed 3x3 floors with quarter turns; whole-cell straight/corner/end, solitary wall, T and cross assemblies.',['previews/blender_joins.png','previews/unity_joins_1080.png','validation/blender_fbx_validation.json']),
        'A10':('Actual 1920x1080/1280x720 Unity renders, 55-degree FOV at 3.2 m and north-up orthographic views; no postprocessing; grayscale inspection.',['previews/unity_overview_1080.png','previews/unity_overview_top_1080.png','previews/unity_third_person_1080.png','previews/unity_third_person_gray_720.png','validation/visual_review.json']),
        'A11':('Relative file paths, full material/path mapping, independent stage statistics, export settings, no image textures/private dependencies, measured-file hash guards.',['README.md','validation/export_settings.json','validation/source_interfaces.json']),
        'A12':('Explicit Energy/Cargo kind mapping, radial energy window design vs closed cargo/X bracing; narrow adjacent/front-back corridor and four-angle grayscale views.',['previews/blender_crates.png','previews/unity_corridor_gray_720.png','previews/qa_unity_views.jpg','validation/visual_review.json']),
        'U01':('11 imported models, URP/Lit remapping, measured identity roots/coordinates, reusable prefabs, Console inspection.',['validation/unity_validation.json','validation/unity_console.json']),
        'U02':('Isolated saved editor showcase, sweep tests, presentation assertions and actual captures; explicit fixture states, not runtime rule events.',['validation/unity_presentation.json','validation/unity_occupancy.json','validation/visual_review.json'])}
    acceptance={k:{'status':'passed','method':v[0],'evidence':[rel(BASE/p) for p in v[1]],'limitation':'Presentation fixtures only; formal gameplay integration not performed.' if k in ('A06','A07','U02') else None} for k,v in evidence.items()}
    folders=[BASE,PROJECT/'Assets/Art/StationKit',PROJECT/'Assets/Material/Station',PROJECT/'Assets/Resources/prefabs/gameplay/StationKit',PROJECT/'Assets/Scripts/Editor/StationKit']
    files=[]
    for folder in folders:
        files.extend(p for p in folder.rglob('*') if p.is_file() and p.name!='station_kit_asset_manifest.json' and p.suffix not in ('.blend1','.pyc') and '__pycache__' not in p.parts)
    files.extend([PROJECT/'Assets/Scenes/StationKitPreview.unity',PROJECT/'Assets/Scenes/StationKitPreview.unity.meta',PROJECT/'Docs/StationArtProduction.md'])
    for asset in ('Assets/Art/StationKit.meta','Assets/Material.meta','Assets/Material/Station.meta','Assets/Resources/prefabs/gameplay/StationKit.meta','Assets/Scripts/Editor/StationKit.meta'):
        p=PROJECT/asset
        if p.exists(): files.append(p)
    for check in acceptance.values():
        for path in check['evidence']: assert (PROJECT/path).exists(),path
    integration_path=PROJECT/'Docs/Validation/StationKitIntegration.json'
    integration=json.loads(integration_path.read_text(encoding='utf-8-sig')) if integration_path.exists() else None
    integrated=bool(integration and integration.get('passed') and integration.get('artifactHashes') and
        all((PROJECT/p).exists() and sha(PROJECT/p)==digest for p,digest in integration['artifactHashes'].items()))
    result={'protocol':'station-kit-v1','specification':'Docs/StationArtProduction.md v1.1','checkedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'tools':{'blender':bf['blenderVersion'],'unity':uv['unityVersion'],'urp':'14.0.11','cinemachine':'2.10.7','unityMCP':'10.2.0'},
        'completion':{'blenderFbxProduction':'passed','unityAssetAcceptance':'passed','formalGameplayIntegration':'passed' if integrated else 'not_run','gameRegression':'passed' if integrated else 'not_run'},
        'gameplayIntegrationEvidence':rel(integration_path) if integrated else None,
        'sourceSha256':sha(BASE/'StationKit.blend'),'assets':assets,'materials':interfaces['materials'],'unityMaterials':uv['materials'],
        'materialConvention':{'colorSpace':'sRGB','unityShader':'Universal Render Pipeline/Lit','smoothness':'1 - roughness','emission':'sRGB converted to linear, multiplied by emissionStrength; _EMISSION enabled for status lamps',
            'textureDependencies':[],'runtimeMutation':'Finite cached demonstration material variants; no mutation of shared neutral bases and no per-frame material allocation.'},
        'acceptance':acceptance,'limitations':visual['limits'],
        'files':[{'path':rel(p),'sha256':sha(p),'bytes':p.stat().st_size} for p in sorted(set(files))]}
    path=BASE/'station_kit_asset_manifest.json'; path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    # Links, metadata and hash closure are part of the delivery check.
    for f in result['files']: assert sha(PROJECT/f['path'])==f['sha256']
    readme=(BASE/'README.md').read_text(encoding='utf-8')
    for target in re.findall(r'\]\(([^)]+)\)',readme):
        if not target.startswith(('http:','https:','#')): assert (BASE/target).exists(),target
    print(json.dumps({'assets':len(assets),'files':len(result['files']),'acceptance':{k:v['status'] for k,v in acceptance.items()}},ensure_ascii=False))

if __name__=='__main__': write()
