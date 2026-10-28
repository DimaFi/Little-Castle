"""Selective, non-destructive import. Sources are read-only; existing MAIN is backed up first.
Run without --apply to inspect the exact proposed file list. Shader merge is a separate reviewed step.
"""
from pathlib import Path
import argparse, hashlib, json, re, shutil, subprocess, sys
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1]
MAP=ROOT.parent/'Little-Castle_BrushFoundation_20261027'
LIGHT=ROOT.parent/'Little-Castle_LightingIntegration_20261025'
OUT=ROOT.parent/'Research/WorldIntegration_20261027'
BACKUP=OUT/'BeforeMain'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def text(p):return p.read_text(encoding='utf-8-sig',errors='replace')
def guid(p):
    if not p.exists():return None
    m=re.search(r'^guid:\s*([0-9a-f]{32})',text(p),re.M);return m.group(1) if m else None
plan={};conflicts=[];decisions=[];unresolved=[]
def propose(src,rel,kind):
    rel=str(rel).replace('\\','/');p=src/rel;dst=ROOT/rel
    if not p.is_file():return
    if rel in plan:
        if sha(plan[rel]['source'])!=sha(p):decisions.append({'path':rel,'kept':plan[rel]['kind'],'ignored':kind})
        return
    if dst.exists():
        if sha(dst)==sha(p):return
        if rel.endswith('.meta'):
            if guid(dst)!=guid(p):conflicts.append({'path':rel,'reason':'GUID mismatch','main':guid(dst),'source':guid(p)})
            return # preserve existing importer/folder settings when GUID agrees
        if kind=='asset' and not rel.startswith(('Assets/_Game/Settings/Rendering/LightingMeadow/','Assets/_Game/Settings/Rendering/SpatialIndirectStudy/')):
            decisions.append({'path':rel,'kept':'existing MAIN asset','ignored':str(src)})
            return
    plan[rel]={'source':p,'kind':kind,'before':sha(dst) if dst.exists() else None,'after':sha(p)}

# Existing map source checkpoint plus the actual unified tool implementation; no checkout/reset.
paths=subprocess.check_output(['git','diff','--name-only','e6a02df','4cc6382','--','Assets/_Game/Scripts','Assets/_Game/Editor','Assets/_Game/Tests'],cwd=ROOT,text=True).splitlines()
lighting_shared={'Assets/_Game/Scripts/World/Rendering/DayNightLightingController.cs','Assets/_Game/Scripts/World/Rendering/StylizedLightingGlobals.cs'}
for rel in paths:
    if rel.removesuffix('.meta') not in lighting_shared:propose(MAP,rel,'map source')
# Only owned new research/build/test modules, never old Meadow snapshot adapters.
for folder in ['Scripts/Research','Editor','Tests/Editor']:
    for p in (LIGHT/'Assets/_Game'/folder).glob('*'):
        if p.is_file() and p.name.startswith(('LightingMeadow','SpatialIndirect')):propose(LIGHT,p.relative_to(LIGHT),'lighting module')

# GUID closure of existing authored fixtures, catalogs, and private lighting profiles.
indexes={}
for src in [MAP,LIGHT,ROOT]:
    index={}
    for meta in (src/'Assets').rglob('*.meta'):
        g=guid(meta)
        if g:index[g]=meta.relative_to(src).as_posix()[:-5]
    indexes[src]=index
queue=[]
def enqueue(src,rel):queue.append((src,str(rel).replace('\\','/')))
enqueue(MAP,'Assets/_Game/Scenes/Meadow_20261025.unity')
for p in (MAP/'Assets/_Game/Resources/MapAuthoring').rglob('*'):
    if p.is_file() and p.suffix!='.meta':enqueue(MAP,p.relative_to(MAP))
for scene in ['LightingMeadow_20261025.unity','SpatialIndirectStudy_20261026.unity']:enqueue(LIGHT,'Assets/_Game/Scenes/'+scene)
for folder in ['LightingMeadow','SpatialIndirectStudy']:
    for p in (LIGHT/'Assets/_Game/Settings/Rendering'/folder).rglob('*'):
        if p.is_file() and p.suffix!='.meta':enqueue(LIGHT,p.relative_to(LIGHT))
visited=set()
while queue:
    src,rel=queue.pop();key=(str(src),rel)
    if key in visited:continue
    visited.add(key);p=src/rel
    if not p.is_file():continue
    if p.suffix in ['.shader','.hlsl','.cginc','.compute']:
        continue # functional shader merge reviewed separately
    if p.suffix not in ['.cs','.asmdef']:propose(src,rel,'asset')
    meta=src/(rel+'.meta');propose(src,rel+'.meta','asset')
    # Folder GUIDs only when absent in MAIN; keep existing folder metadata.
    for parent in Path(rel).parents:
        if parent.as_posix() in ['.','Assets']:continue
        m=src/(parent.as_posix()+'.meta')
        if m.exists():propose(src,parent.as_posix()+'.meta','asset')
    for scan in [p,meta]:
        if not scan.is_file() or scan.stat().st_size>12_000_000:continue
        if scan.suffix not in ['.meta','.unity','.prefab','.asset','.mat','.controller','.cs','.asmdef']:continue
        for g in set(re.findall(r'guid:\s*([0-9a-f]{32})',text(scan))):
            if g in indexes[src]:enqueue(src,indexes[src][g])
            elif g in indexes[ROOT]:pass
            elif g.strip('0') and not g.startswith('0000000000000000'):unresolved.append(g) # may be package GUID

report={'mapCommit':'4cc6382','lightingCommit':'42f317c','files':[dict(path=k,source=str(v['source']),kind=v['kind'],before=v['before'],after=v['after']) for k,v in sorted(plan.items())],
        'guidConflicts':conflicts,'preservedExistingDecisions':decisions,'packageGuidsToCheck':sorted(set(unresolved))}
OUT.mkdir(parents=True,exist_ok=True);(OUT/'selective-transfer-plan.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'proposedFiles':len(plan),'bytes':sum(v['source'].stat().st_size for v in plan.values()),'GUIDconflicts':conflicts,'preservedDecisions':len(decisions),'packageGUIDs':len(set(unresolved))},ensure_ascii=False))
if conflicts:raise SystemExit('GUID conflicts must be resolved before import')
if '--apply' in sys.argv:
    for rel,v in plan.items():
        dst=ROOT/rel
        if dst.exists():
            backup=BACKUP/rel
            if not backup.exists():backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(dst,backup)
            if sha(dst)!=v['before']:raise RuntimeError('MAIN changed after planning: '+rel)
        dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(v['source'],dst)
    (OUT/'applied-code-assets.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Applied exact planned code/assets. Shared shaders and ProjectSettings untouched.')
