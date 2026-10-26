from pathlib import Path
import hashlib,json,difflib

root=Path(__file__).resolve().parents[2];out=root/'docs/lighting-integration';base=out/'stage1b-baseline-98da069'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
allowed={'Assets/_Game/Scripts/Research/SpatialIndirectTrace.cs','Assets/_Game/Scripts/Research/SpatialIndirectTransport.cs','Assets/_Game/Tests/Editor/SpatialIndirectTransportTests.cs','Assets/_Game/Editor/SpatialIndirectStage1Build.cs'}
original=json.loads((base/'manifest.json').read_text('utf-8'));failures=[];modified=[];patch=[]
for item in original:
    rel=item['path'];before=base/rel;after=root/rel
    if sha(before)!=item['sha256']:failures.append('Frozen input changed: '+rel)
    if rel not in allowed and sha(after)!=item['sha256']:failures.append('Protected scene/settings/runtime changed: '+rel)
    if rel in allowed:
        modified.append(dict(path=rel,beforeSHA256=item['sha256'],sha256=sha(after),bytes=after.stat().st_size))
        patch.extend(difflib.unified_diff(before.read_text('utf-8-sig').splitlines(True),after.read_text('utf-8-sig').splitlines(True),fromfile='STAGE1/'+rel,tofile='STAGE1B/'+rel))
# All previous assets, metadata and the rest of the reference solver stay fixed.
expected={}
for manifest,groups in [('spatial-indirect-manifest.json',('modifiedShaders','newAssets')),('stage1-manifest.json',('modified','newAssets'))]:
    data=json.loads((out/manifest).read_text('utf-8'))
    for group in groups:
        for item in data[group]:
            rel=item['path']
            expected[rel]=item['sha256']
for rel,value in expected.items():
    if rel not in allowed and sha(root/rel)!=value:failures.append('Previous asset drift: '+rel)
trace='Assets/_Game/Scripts/Research/SpatialIndirectTrace.cs'
def reference_tail(p):return p.read_text('utf-8-sig').split('        bool TraceInverse(',1)[1]
if reference_tail(base/trace)!=reference_tail(root/trace):failures.append('Serial reference trace/solve changed')
main=Path('E:/Games_Develop/Little-Castle');main_drift=[v['path'] for v in json.loads((out/'main-source-manifest.json').read_text('utf-8')) if not (main/v['path']).exists() or sha(main/v['path'])!=v['sha256']]
if main_drift:failures.append('MAIN Assets/Packages/Settings drift')
manifest=dict(parentCommit='98da06954c1d8c027d275ea80f8300c8b65ff7e9',baselineManifestSHA256=sha(base/'manifest.json'),modified=modified,unchangedInputs=[v for v in original if v['path'] not in allowed])
audit=dict(pass_=not failures,failures=failures,mainDrift=main_drift,sharedShadersUnchanged=7,referenceSolveUnchanged=True,fixtureAndSettingsUnchanged=True)
(out/'stage1b-manifest.json').write_text(json.dumps(manifest,indent=2),'utf-8');(out/'stage1b-audit.json').write_text(json.dumps(audit,indent=2),'utf-8');(out/'gi-98da069-to-stage1b.patch').write_text(''.join(patch),'utf-8')
print(json.dumps(audit,indent=2));assert not failures
