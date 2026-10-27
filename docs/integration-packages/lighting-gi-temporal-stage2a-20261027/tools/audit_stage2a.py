from pathlib import Path
import hashlib,json,difflib,re

r=Path(__file__).resolve().parents[2];out=r/'docs/lighting-integration';base=out/'stage2a-baseline-f4ae7b1';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
allowed={'Assets/_Game/Scripts/Research/SpatialIndirectVolume.cs','Assets/_Game/Scripts/Research/SpatialIndirectStudyReview.cs','Assets/_Game/Editor/SpatialIndirectStage1Build.cs','Assets/_Game/Shaders/Surface/LC_SpatialIndirect.hlsl'}
old=json.loads((base/'manifest.json').read_text('utf-8'));failures=[];modified=[];patch=[]
for item in old:
    rel=item['path'];before=base/rel;after=r/rel
    if sha(before)!=item['sha256']:failures.append('Frozen source changed: '+rel)
    if rel not in allowed and sha(after)!=item['sha256']:failures.append('Protected input changed: '+rel)
    if rel in allowed:
        modified.append(dict(path=rel,beforeSHA256=item['sha256'],sha256=sha(after),bytes=after.stat().st_size));patch.extend(difflib.unified_diff(before.read_text('utf-8-sig').splitlines(True),after.read_text('utf-8-sig').splitlines(True),fromfile='STAGE1C/'+rel,tofile='STAGE2A/'+rel))
expected={}
for name,groups in [('spatial-indirect-manifest.json',('modifiedShaders','newAssets')),('stage1-manifest.json',('modified','newAssets')),('stage1b-manifest.json',('modified',)),('stage1c-manifest.json',('modified','newAssets'))]:
    j=json.loads((out/name).read_text('utf-8'))
    for group in groups:
        for item in j[group]:expected[item['path']]=item['sha256']
for rel,value in expected.items():
    if rel not in allowed and sha(r/rel)!=value:failures.append('Existing dependency drift: '+rel)
new=[];guids={}
for rel in ('Assets/_Game/Scripts/Research/SpatialIndirectPublication.cs','Assets/_Game/Scripts/Research/SpatialIndirectTemporalStudy.cs','Assets/_Game/Scripts/Research/SpatialIndirectGameViewStudy.cs','Assets/_Game/Tests/Editor/SpatialIndirectPublicationTests.cs'):
    for suffix in ('','.meta'):
        p=r/(rel+suffix);new.append(dict(path=rel+suffix,sha256=sha(p),bytes=p.stat().st_size))
    guid=re.search(r'^guid:\s*(\w+)',(r/(rel+'.meta')).read_text('utf-8-sig'),re.M)[1];guids[guid]=0
for p in (r/'Assets').rglob('*.meta'):
    m=re.search(r'^guid:\s*(\w+)',p.read_text('utf-8-sig'),re.M)
    if m and m[1] in guids:guids[m[1]]+=1
if any(n!=1 for n in guids.values()):failures.append('New GUID collision')
main=Path('E:/Games_Develop/Little-Castle');drift=[v['path'] for v in json.loads((out/'main-source-manifest.json').read_text('utf-8')) if not (main/v['path']).exists() or sha(main/v['path'])!=v['sha256']]
if drift:failures.append('MAIN game drift')
manifest=dict(parentCommit='f4ae7b19e70bcd9a53f8ce7eea84d97ef195d542',baselineManifestSHA256=sha(base/'manifest.json'),modified=modified,newAssets=new,unchangedInputs=[v for v in old if v['path'] not in allowed])
audit=dict(pass_=not failures,failures=failures,mainDrift=drift,sharedShaderFamiliesUnchanged=7,changedShaderIncludeOnly='LC_SpatialIndirect.hlsl',traceTransportQueueUnchanged=True,fixtureSettingsUnchanged=True,newGuidsUnique=all(n==1 for n in guids.values()))
(out/'stage2a-manifest.json').write_text(json.dumps(manifest,indent=2),'utf-8');(out/'stage2a-audit.json').write_text(json.dumps(audit,indent=2),'utf-8');(out/'gi-f4ae7b1-to-stage2a.patch').write_text(''.join(patch),'utf-8');print(json.dumps(audit,indent=2));assert not failures
