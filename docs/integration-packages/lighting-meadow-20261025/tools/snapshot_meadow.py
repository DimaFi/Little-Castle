"""Read-only source snapshot. Never writes to MAIN or the map-authoring copy."""
from pathlib import Path
import hashlib, json, re, shutil, datetime, difflib

MAIN=Path('E:/Games_Develop/Little-Castle')
SOURCE=Path('E:/Games_Develop/Little-Castle_URP_20261024')
DEST=Path('E:/Games_Develop/Little-Castle_LightingIntegration_20261025')
OUT=DEST/'docs/lighting-integration'
OUT.mkdir(parents=True,exist_ok=True)
if (OUT/'meadow-source-manifest.json').exists():
    raise SystemExit('Snapshot already exists. Preserve it; use a drift audit instead of re-snapshotting live authoring work.')
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
index={}
for p in (SOURCE/'Assets').rglob('*.meta'):
    m=re.search(r'^guid: ([a-f0-9]{32})',p.read_text('utf-8-sig',errors='ignore'),re.M)
    if m:index[m[1]]=p.with_suffix('').relative_to(SOURCE).as_posix()
queue=['Assets/_Game/Scenes/Meadow_20261025.unity']
for pat in ('Meadow*.cs','SurfacePaint*.cs','GrassInteraction*.cs','InnerverseGrassStudy.cs'):
    queue += [p.relative_to(SOURCE).as_posix() for p in (SOURCE/'Assets/_Game/Scripts').rglob(pat)]
queue += ['Assets/_Game/Shaders/Surface/LC_SurfacePaint.hlsl']
seen=set(); entries=[]; collisions=[]; unresolved=set()
while queue:
    rel=queue.pop()
    if rel in seen:continue
    seen.add(rel); p=SOURCE/rel
    if not p.is_file():continue
    if p.suffix in ('.unity','.prefab','.asset','.mat','.controller','.cs','.hlsl','.shader'):
        data=p.read_text('utf-8-sig',errors='ignore')
        for guid in re.findall(r'guid: ([a-f0-9]{32})',data):
            if guid in index:queue.append(index[guid])
            elif not guid.startswith('0000000'):unresolved.add(guid)
    # Follow FBX material remaps and texture references in importer metadata, too.
    meta=Path(str(p)+'.meta')
    if meta.exists():
        for guid in re.findall(r'guid: ([a-f0-9]{32})',meta.read_text('utf-8-sig',errors='ignore')):
            if guid in index and index[guid]!=rel:queue.append(index[guid])
    for src in (p,meta):
        if not src.is_file():continue
        dst=DEST/src.relative_to(SOURCE); original=MAIN/src.relative_to(SOURCE)
        digest=sha(src)
        same=dst.exists() and sha(dst)==digest
        # Existing shader implementations are merged by function in a separate step.
        is_shader=('/Shaders/' in '/'+src.relative_to(SOURCE).as_posix() or src.name=='StylizedLightingGlobals.cs') and dst.exists()
        action='identical' if same else 'merge-required' if is_shader else 'snapshot'
        if not same and not is_shader:
            dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
        if original.is_file() and sha(original)!=digest:
            collisions.append({'path':src.relative_to(SOURCE).as_posix(),'mainSHA256':sha(original),'meadowSHA256':digest,'action':action})
        entries.append({'path':src.relative_to(SOURCE).as_posix(),'sha256':digest,'bytes':src.stat().st_size,'action':action})
    parent=p.parent
    while parent!=SOURCE/'Assets' and parent!=SOURCE:
        sm=Path(str(parent)+'.meta');dm=DEST/sm.relative_to(SOURCE)
        if sm.exists() and not dm.exists():dm.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(sm,dm)
        parent=parent.parent
manifest={'capturedUTC':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':str(SOURCE),'base':str(MAIN),'destination':str(DEST),'files':sorted(entries,key=lambda e:e['path']),'collisions':collisions,'externalPackageGUIDs':sorted(unresolved)}
(OUT/'meadow-source-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),'utf-8')
base=[]
for area in ('Assets','Packages','ProjectSettings'):
    for p in (MAIN/area).rglob('*'):
        if p.is_file():base.append({'path':p.relative_to(MAIN).as_posix(),'sha256':sha(p)})
(OUT/'main-source-manifest.json').write_text(json.dumps(base,indent=2),'utf-8')
print(json.dumps({'files':len(entries),'collisions':collisions,'externalPackageGUIDs':sorted(unresolved)},ensure_ascii=False,indent=2))
