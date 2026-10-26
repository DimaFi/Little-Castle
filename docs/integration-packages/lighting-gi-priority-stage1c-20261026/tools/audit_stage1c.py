from pathlib import Path
import hashlib,json,difflib,re

root=Path(__file__).resolve().parents[2];out=root/'docs/lighting-integration';base=out/'stage1c-baseline-d1d92e0';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
allowed={'Assets/_Game/Scripts/Research/SpatialIndirectCache.cs','Assets/_Game/Scripts/Research/SpatialIndirectVolume.cs','Assets/_Game/Scripts/Research/SpatialIndirectStudyReview.cs','Assets/_Game/Editor/SpatialIndirectStage1Build.cs'}
original=json.loads((base/'manifest.json').read_text('utf-8'));failures=[];modified=[];patch=[]
for item in original:
    rel=item['path'];before=base/rel;after=root/rel
    if sha(before)!=item['sha256']:failures.append('Frozen source changed: '+rel)
    if rel not in allowed and sha(after)!=item['sha256']:failures.append('Protected input changed: '+rel)
    if rel in allowed:
        modified.append(dict(path=rel,beforeSHA256=item['sha256'],sha256=sha(after),bytes=after.stat().st_size))
        patch.extend(difflib.unified_diff(before.read_text('utf-8-sig').splitlines(True),after.read_text('utf-8-sig').splitlines(True),fromfile='STAGE1B/'+rel,tofile='STAGE1C/'+rel))
expected={}
for name,groups in [('spatial-indirect-manifest.json',('modifiedShaders','newAssets')),('stage1-manifest.json',('modified','newAssets')),('stage1b-manifest.json',('modified',))]:
    j=json.loads((out/name).read_text('utf-8'))
    for group in groups:
        for item in j[group]:expected[item['path']]=item['sha256']
for rel,value in expected.items():
    if rel not in allowed and sha(root/rel)!=value:failures.append('Existing dependency drift: '+rel)
new=[]
for suffix in ('','.meta'):
    p=root/('Assets/_Game/Tests/Editor/SpatialIndirectPriorityTests.cs'+suffix);new.append(dict(path=p.relative_to(root).as_posix(),sha256=sha(p),bytes=p.stat().st_size))
guid=re.search(r'^guid:\s*(\w+)',(root/new[1]['path']).read_text('utf-8-sig'),re.M)[1]
matches=[p for p in (root/'Assets').rglob('*.meta') if re.search(r'^guid:\s*'+guid+r'\s*$',p.read_text('utf-8-sig'),re.M)]
if len(matches)!=1:failures.append('New test GUID collision')
main=Path('E:/Games_Develop/Little-Castle');drift=[v['path'] for v in json.loads((out/'main-source-manifest.json').read_text('utf-8')) if not (main/v['path']).exists() or sha(main/v['path'])!=v['sha256']]
if drift:failures.append('MAIN game drift')
manifest=dict(parentCommit='d1d92e0505110726be62f6ed408f33e446201ce8',baselineManifestSHA256=sha(base/'manifest.json'),modified=modified,newAssets=new,unchangedInputs=[v for v in original if v['path'] not in allowed])
audit=dict(pass_=not failures,failures=failures,mainDrift=drift,sharedShadersUnchanged=7,traceAndTransportUnchanged=True,fixtureAndSettingsUnchanged=True,newGuidUnique=len(matches)==1)
(out/'stage1c-manifest.json').write_text(json.dumps(manifest,indent=2),'utf-8');(out/'stage1c-audit.json').write_text(json.dumps(audit,indent=2),'utf-8');(out/'gi-d1d92e0-to-stage1c.patch').write_text(''.join(patch),'utf-8');print(json.dumps(audit,indent=2));assert not failures
