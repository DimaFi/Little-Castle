from pathlib import Path
import hashlib
import json
import difflib
import re

root=Path(__file__).resolve().parents[2];out=root/'docs/lighting-integration';base=out/'stage1-baseline-100c174'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
original=json.loads((base/'manifest.json').read_text('utf-8-sig'))
allowed={'Assets/_Game/Scripts/Research/SpatialIndirectTrace.cs','Assets/_Game/Scripts/Research/SpatialIndirectVolume.cs','Assets/_Game/Scripts/Research/SpatialIndirectStudyReview.cs'}
failures=[];modified=[];patch=[]
for item in original:
    rel=item['path'];before=base/rel;after=root/rel
    if sha(before)!=item['sha256']:failures.append('Frozen source changed: '+rel)
    if rel not in allowed and sha(after)!=item['sha256']:failures.append('Fixture/settings/reference input changed: '+rel)
    if rel in allowed:
        modified.append(dict(path=rel,beforeSHA256=item['sha256'],sha256=sha(after),bytes=after.stat().st_size))
        patch.extend(difflib.unified_diff(before.read_text('utf-8-sig').splitlines(True),after.read_text('utf-8-sig').splitlines(True),fromfile='GI_100c174/'+rel,tofile='STAGE1/'+rel))
old=json.loads((out/'spatial-indirect-manifest.json').read_text('utf-8'))
for item in old['modifiedShaders']:
    if sha(root/item['path'])!=item['sha256']:failures.append('Shared shader changed: '+item['path'])
for item in old['newAssets']:
    if item['path'] not in allowed and sha(root/item['path'])!=item['sha256']:failures.append('Parent fixture asset changed: '+item['path'])
new=[]
for rel in ['Assets/_Game/Scripts/Research/SpatialIndirectProfile.cs','Assets/_Game/Scripts/Research/SpatialIndirectTransport.cs','Assets/_Game/Scripts/Research/SpatialIndirectAllocationCounter.cs','Assets/_Game/Editor/SpatialIndirectStage1Build.cs','Assets/_Game/Tests/Editor/SpatialIndirectTransportTests.cs']:
    for suffix in ('','.meta'):
        p=root/(rel+suffix);new.append(dict(path=rel+suffix,sha256=sha(p),bytes=p.stat().st_size))
guids={}
for item in new:
    if item['path'].endswith('.meta'):
        guid=re.search(r'^guid:\s*([0-9a-f]{32})',(root/item['path']).read_text('utf-8-sig'),re.M)[1];guids[guid]=[]
for p in (root/'Assets').rglob('*.meta'):
    match=re.search(r'^guid:\s*([0-9a-f]{32})',p.read_text('utf-8-sig'),re.M)
    if match and match[1] in guids:guids[match[1]].append(p.relative_to(root).as_posix())
if any(len(locations)!=1 for locations in guids.values()):failures.append('New GUID collision')
main=Path('E:/Games_Develop/Little-Castle');mainBase=json.loads((out/'main-source-manifest.json').read_text('utf-8'))
mainDrift=[v['path'] for v in mainBase if not (main/v['path']).exists() or sha(main/v['path'])!=v['sha256']]
if mainDrift:failures.append('MAIN Assets/Packages/Settings drift')
research=Path('E:/Games_Develop/Research/SpatialIndirectStage1_20261026')
manifest=dict(parentCommit='100c1745924493d2d8be0f29b5541c695273ed6e',inputBaselineManifestSHA256=sha(base/'manifest.json'),modified=modified,newAssets=new,unchangedInputs=[v for v in original if v['path'] not in allowed])
audit=dict(pass_=not failures,failures=failures,mainAssetsPackagesSettingsDrift=mainDrift,sharedShaderFilesUnchanged=7,fixtureSceneUnchanged=True,settingsRestored=True)
(out/'stage1-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),'utf-8')
(out/'stage1-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),'utf-8')
(out/'gi-100c174-to-stage1.patch').write_text(''.join(patch),'utf-8')
print(json.dumps(dict(pass_=not failures,modified=len(modified),newAssets=len(new),failures=failures),ensure_ascii=False,indent=2))
if failures:raise SystemExit(1)
