"""Package only the lighting work; do not stage MAIN's unrelated dirty files."""
from pathlib import Path
import json,hashlib,shutil,datetime

ROOT=Path(__file__).resolve().parents[2]
MAIN=Path('E:/Games_Develop/Little-Castle')
EVIDENCE=Path('E:/Games_Develop/Research/LightingMeadow_20261025')
OUT=ROOT/'publication'
PREFIX='docs/integration-packages/lighting-meadow-20261025'
OUT.mkdir(exist_ok=True)
entries=[]
def add(path,target):
    data=path.read_bytes();data.decode('utf-8-sig')
    dst=OUT/target;dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(data)
    entries.append({'path':target,'source':str(path),'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})

audit=json.loads((ROOT/'docs/lighting-integration/integration-audit.json').read_text('utf-8'))
assert not audit['mainAssetSettingsDrift'] and not audit['snapshotIntegrityFailures']
modified=[x for x in audit['integrationChanges'] if x['mainSnapshotSHA256']]
for item in modified:
    assert not item['path'].startswith(('ProjectSettings/','Packages/'))
    add(ROOT/item['path'],PREFIX+'/payload/'+item['path'])
for rel in ['Assets/_Game/Editor/LightingMeadowBuild.cs','Assets/_Game/Editor/LightingMeadowIntegration.cs','Assets/_Game/Scripts/Research/LightingMeadowReview.cs','Assets/_Game/Tests/Editor/LightingMeadowPaletteTests.cs','Assets/_Game/Scenes/LightingMeadow_20261025.unity','Assets/_Game/Shaders/Surface/LC_SurfacePaint.hlsl']:
    for suffix in ('','.meta'):add(ROOT/(rel+suffix),PREFIX+'/payload/'+rel+suffix)
for p in sorted((ROOT/'Assets/_Game/Settings/Rendering/LightingMeadow').glob('*')):
    if p.is_file():add(p,PREFIX+'/payload/'+p.relative_to(ROOT).as_posix())
add(ROOT/'Assets/_Game/Settings/Rendering/LightingMeadow.meta',PREFIX+'/payload/Assets/_Game/Settings/Rendering/LightingMeadow.meta')
for name in ['meadow-source-manifest.json','shader-merge-manifest.json','integration-audit.json','main-to-integration.patch','local-cache-invalidation-ru.md','tiny-glade-source-research-20261026-ru.md']:
    add(ROOT/'docs/lighting-integration'/name,PREFIX+'/'+name)
for p in sorted((ROOT/'Tools/LightingStudy').glob('*')):
    if p.is_file() and p.suffix in ('.py','.ps1'):add(p,PREFIX+'/tools/'+p.name)
for name in ['performance-summary.json','capture-validation.json','preflight.txt','measurement-environment.json']:
    add(EVIDENCE/name,PREFIX+'/evidence/'+name)
testSummary={}
import xml.etree.ElementTree as ET
for name in ['editmode.xml','playmode.xml']:
    p=EVIDENCE/name;r=ET.parse(p).getroot();testSummary[name]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),**{k:r.attrib.get(k) for k in ['result','total','passed','failed','skipped','start-time','end-time','duration']}}
testPath=OUT/'test-summary.json';testPath.write_text(json.dumps(testSummary,indent=2),'utf-8');add(testPath,PREFIX+'/evidence/test-summary.json')
basePath=OUT/'baseline-requirements.json';basePath.write_text(json.dumps({'parentGitCommit':'e6a02df6f57461df9cd7cf853e8e9d62b776d71a','requiresUncommittedLocalUrpBaseline':True,'files':modified},indent=2),'utf-8');add(basePath,PREFIX+'/baseline-requirements.json')
add(MAIN/'docs/handoffs/lighting-to-map-authoring-20261025-ru.md','docs/handoffs/lighting-to-map-authoring-20261025-ru.md')
readme=OUT/'README.md'
readme.write_text('''# Meadow lighting integration checkpoint — 25–26 October 2026

This is a reviewable integration packet from the separately validated
Little-Castle_LightingIntegration_20261025 workspace. It does not activate a
rendering migration in the repository checkout. MAIN has an uncommitted local
URP baseline and extensive unrelated work; this commit deliberately preserves
the lighting delta without publishing/staging that other work.

`payload/` stores complete reference versions of the changed lighting sources,
the owned fixture, profiles, shaders and tests at their intended Unity paths.
Use `main-to-integration.patch` and the before/after SHA-256 manifests for a
function-by-function merge. Do not mass-copy payload over the newer map editor:
six runtime/authoring source files have already evolved since the Meadow snapshot.
Source matrices/meshes/textures and third-party binary assets are local-only;
restore their exact GUID dependencies from the licensed source and manifests.
This packet is not a standalone fresh-clone Unity project.

Validation: 178/178 Edit Mode, 6/6 Play Mode, 152 nonempty real Player images,
eight warmed standalone runs of 600 ordinary camera-render intervals each.
Raw measurement distributions and snapshot provenance are in this packet.
Screenshots/Players/full XML remain at the local Research path in the handoff.
No spatial GI is claimed. The Tiny Glade research note links public primary
materials and open reference code by the developers for the next prototype.

Start with [the handoff](../../handoffs/lighting-to-map-authoring-20261025-ru.md),
[Tiny Glade references](tiny-glade-source-research-20261026-ru.md), and
[cache transaction boundary](local-cache-invalidation-ru.md).
''','utf-8')
add(readme,PREFIX+'/README.md')
manifest=OUT/'publication-manifest.json';manifest.write_text(json.dumps({'createdUTC':datetime.datetime.now(datetime.timezone.utc).isoformat(),'files':entries},ensure_ascii=False,indent=2),'utf-8')
add(manifest,PREFIX+'/publication-manifest.json')
transport=[]
chunks=OUT/'transport-chunks';chunks.mkdir(exist_ok=True)
for index,item in enumerate(entries):
    with (OUT/item['path']).open('r',encoding='utf-8',newline='') as handle:
        content=handle.read()
    # Small JSON chunks let the tool orchestrator read without output truncation.
    parts=[content[n:n+16000] for n in range(0,len(content),16000)] or ['']
    for number,part in enumerate(parts):
        filename=f'{index:03d}-{number:03d}.json'
        (chunks/filename).write_text(json.dumps({'path':item['path'],'part':number,'total':len(parts),'content':part},ensure_ascii=False),'utf-8')
        transport.append(filename)
(OUT/'transport-index.json').write_text(json.dumps(transport),'utf-8')
print(json.dumps({'files':len(entries),'bytes':sum(x['bytes'] for x in entries),'chunks':len(transport),'branch':'codex/lighting-meadow-baseline-2026-10-26'},indent=2))
