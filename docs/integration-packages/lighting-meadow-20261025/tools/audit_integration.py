from pathlib import Path
import hashlib,json,difflib,zipfile

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'docs/lighting-integration'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
main=Path('E:/Games_Develop/Little-Castle')
source=Path('E:/Games_Develop/Little-Castle_URP_20261024')
base={x['path']:x['sha256'] for x in json.loads((OUT/'main-source-manifest.json').read_text('utf-8'))}
snapshot=json.loads((OUT/'meadow-source-manifest.json').read_text('utf-8'))
for item in snapshot['files']:
    if item['path'].endswith('/StylizedLightingGlobals.cs'):item['action']='keep-main-linear-plus-meadow-wind-api'
(OUT/'meadow-source-manifest.json').write_text(json.dumps(snapshot,ensure_ascii=False,indent=2),'utf-8')
drift=[];snapshotIntegrity=[]
for item in snapshot['files']:
    p=source/item['path']
    if not p.exists() or sha(p)!=item['sha256']:drift.append({'path':item['path'],'snapshot':item['sha256'],'current':sha(p) if p.exists() else None})
    saved=ROOT/item['path']
    if item['action']=='snapshot' and (not saved.exists() or sha(saved)!=item['sha256']):snapshotIntegrity.append(item['path'])
for item in json.loads((OUT/'shader-merge-manifest.json').read_text('utf-8')):
    p=source/item['path']
    if p.exists() and sha(p)!=item['sourceSHA256']:drift.append({'path':item['path'],'snapshot':item['sourceSHA256'],'current':sha(p)})
mainDrift=[rel for rel,digest in base.items() if not (main/rel).exists() or sha(main/rel)!=digest]
changed=[]
for area in ('Assets','Packages','ProjectSettings'):
    for p in (ROOT/area).rglob('*'):
        if not p.is_file():continue
        rel=p.relative_to(ROOT).as_posix();digest=sha(p)
        if digest!=base.get(rel):changed.append({'path':rel,'sha256':digest,'mainSnapshotSHA256':base.get(rel),'bytes':p.stat().st_size})
audit={'sourceDrift':drift,'snapshotIntegrityFailures':snapshotIntegrity,'mainAssetSettingsDrift':mainDrift,'integrationChanges':changed}
(OUT/'integration-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),'utf-8')
patch=[]
for item in changed:
    rel=item['path'];p=ROOT/rel
    if item['mainSnapshotSHA256'] and p.suffix in ('.cs','.shader','.hlsl','.cginc','.asset','.meta'):
        a=(main/rel).read_text('utf-8-sig',errors='replace').splitlines(True);b=p.read_text('utf-8-sig',errors='replace').splitlines(True)
        patch.extend(difflib.unified_diff(a,b,fromfile='MAIN/'+rel,tofile='INTEGRATION/'+rel))
(OUT/'main-to-integration.patch').write_text(''.join(patch),'utf-8')
print(json.dumps({'sourceDrift':drift,'mainDrift':mainDrift,'modifiedBase':[x['path'] for x in changed if x['mainSnapshotSHA256']],'addedCount':sum(x['mainSnapshotSHA256'] is None for x in changed)},ensure_ascii=False,indent=2))
