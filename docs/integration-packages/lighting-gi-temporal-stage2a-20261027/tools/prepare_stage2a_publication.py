from pathlib import Path
import hashlib,json

r=Path(__file__).resolve().parents[2];out=r/'publication-gi-stage2a';out.mkdir(exist_ok=True)
repo=r/'publication-spatial-gi/repo';prefix='docs/integration-packages/lighting-gi-temporal-stage2a-20261027'
core=Path('E:/Games_Develop/Research/SpatialIndirectStage2A_20261026');game=Path('E:/Games_Develop/Research/LightingGameViews_20261026')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
load=lambda p:json.loads(p.read_text('utf-8'))
source=load(r/'docs/lighting-integration/stage2a-manifest.json');audit=load(r/'docs/lighting-integration/stage2a-audit.json')
assert audit['pass_'] and load(core/'stage2a-summary.json')['pass_'] and load(game/'game-views-summary.json')['pass_']
entries=[]
def add(p,rel):
    p=Path(p);target=repo/rel;assert target.resolve().is_relative_to(repo.resolve())
    target.parent.mkdir(parents=True,exist_ok=True);b=p.read_bytes();target.write_bytes(b)
    entries.append(dict(path=rel,sha256=hashlib.sha256(b).hexdigest(),gitBlobSHA=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest(),bytes=len(b),source=str(p)))
included=set()
for item in source['modified']+source['newAssets']:
    assert sha(r/item['path'])==item['sha256']
    for rel in (item['path'],item['path']+'.meta') if not item['path'].endswith('.meta') else (item['path'],):
        if rel not in included:included.add(rel);add(r/rel,prefix+'/payload/'+rel)
base=r/'docs/lighting-integration/stage2a-baseline-f4ae7b1'
for item in source['modified']:add(base/item['path'],prefix+'/reference/'+item['path'])
add(base/'manifest.json',prefix+'/reference/manifest.json')
for name in ('stage2a-manifest.json','stage2a-audit.json','gi-f4ae7b1-to-stage2a.patch'):add(r/'docs/lighting-integration'/name,prefix+'/'+name)
for name in ('run_stage2a.ps1','run_game_views.ps1','audit_stage2a.py','summarize_stage2a.py','summarize_game_views.py','report_stage2a.py','prepare_stage2a_publication.py'):add(r/'Tools/LightingStudy'/name,prefix+'/tools/'+name)
raw=[];logs=[];players={}
coreFolders=[f'Final{i:02}{m}' for i in range(1,4) for m in ('Immediate','Temporal')]+['FinalCycle1x','FinalCycle8x','FinalProgress','FinalCycle8xImmediate']
gameFolders=[f'{p}{i:02}{m}' for i,p in ((1,'Final'),(2,'Verified'),(3,'Verified')) for m in ('Base','GroundFog')]
for name,root,folders in (('core',core,coreFolders),('game',game,gameFolders)):
    for folder in folders:
        directory=root/folder
        assert (directory/'result.txt').read_text('utf-8').startswith('PASS') and not (directory/'runtime-errors.txt').read_text('utf-8').strip()
        for p in directory.iterdir():
            if p.is_file():raw.append(dict(group=name,path=p.relative_to(root).as_posix(),sha256=sha(p),bytes=p.stat().st_size))
        for file in ('result.txt','runtime-errors.txt','temporal-steps.json','benchmarks.json','game-views.json'):
            if (directory/file).exists():add(directory/file,prefix+'/evidence/'+name+'/'+folder+'/'+file)
        log=root/(folder+'.log')
        if log.exists():logs.append(dict(group=name,path=log.name,sha256=sha(log),bytes=log.stat().st_size))
    players[name]=[dict(path=p.relative_to(root).as_posix(),sha256=sha(p),bytes=p.stat().st_size) for p in (root/'Player').rglob('*') if p.is_file()]
for file in ('stage2a-summary.json','Final01-comparison.json','Final02-comparison.json','Final03-comparison.json','editmode.xml','playmode.xml','build.txt','build-warnings.txt','core-build-source-and-binary-index.json','stage2a-game-comparison.png','stage2a-continuous.png','stage2a-transition.png'):add(core/file,prefix+'/evidence/core/'+file)
for file in ('game-views-summary.json','editmode-final.xml','playmode-final.xml','build.txt','build-warnings.txt','strategy-camera-day-night.png','strategy-camera-fog-ab.png','strategy-camera-gi-ab.png','strategy-camera-oak-and-motion.png'):add(game/file,prefix+'/evidence/game/'+file)
for name,root,files in (('core',core,('build.log','editmode.log','playmode.log')),('game',game,('build-final.log','editmode-final.log','playmode-final.log'))):
    for file in files:
        p=root/file
        if p.exists():logs.append(dict(group=name,path=file,sha256=sha(p),bytes=p.stat().st_size))
index=dict(parentCommit=source['parentCommit'],localRoots=dict(core=str(core),game=str(game)),sourceManifestSHA256=sha(r/'docs/lighting-integration/stage2a-manifest.json'),rawArtifacts=raw,logs=logs,playerFiles=players,excluded=['core PilotTemporal','game Final01 pilot','game Final02Base/GroundFog and Final03Base/GroundFog incomplete','game DiagnosticNoCapture standalone diagnostic'],coreSourceNote='Core Player preserved before camera harness; core-build-source-and-binary-index.json records its exact sources. Rendering core unchanged; separate final camera Player includes the new adapter and build output selector.')
(out/'evidence-index.json').write_text(json.dumps(index,indent=2),'utf-8');add(out/'evidence-index.json',prefix+'/evidence-index.json')
for rel in ('docs/reports/lighting-gi-temporal-stage2a-20261027-ru.md','docs/plans/lighting-next-stages-20261026-ru.md','docs/decisions/ADR-lighting-temporal-presentation-stage2a-20261027-ru.md'):add(r/rel,rel)
add(Path('E:/Games_Develop/Little-Castle/docs/handoffs/lighting-to-map-authoring-20261025-ru.md'),'docs/handoffs/lighting-to-map-authoring-20261025-ru.md')
readme='''# GI Stage2A — temporal presentation and strategy-camera evidence

Experimental delta over published `f4ae7b19e70bcd9a53f8ce7eea84d97ef195d542`.
Root game Assets, Packages and ProjectSettings remain unchanged. Previous packets
and licensed Meadow/Oak inputs are prerequisites; no new third-party assets copied.

- [Report, measurements and limits](../../reports/lighting-gi-temporal-stage2a-20261027-ru.md)
- [Current plan](../../plans/lighting-next-stages-20261026-ru.md)
- [ADR](../../decisions/ADR-lighting-temporal-presentation-stage2a-20261027-ru.md)
- [Map handoff](../../handoffs/lighting-to-map-authoring-20261025-ru.md)
- [Source/GUID hashes](stage2a-manifest.json), [audit](stage2a-audit.json), [patch](gi-f4ae7b1-to-stage2a.patch)
- [Local raw evidence and both Player hashes](evidence-index.json)

Two coherent published snapshots fade over0.2s; computation remains separate.
Geometry edits forbid old radiance in both histories. Known-invalid does not use
sky fallback. Continuous WorldTime drives presentation:360s day+90s night, no
primary geometry rebuild. 1x/8x cycles, rapid undo/remote move and exact restoration
pass. 210 Edit+6Play tests;12fields match Stage1C and Immediate exactly, serial
error <=5.0664e-7. This trades global publication latency/memory for coherence;
dark pauses after edits remain an explicit limitation. Not production adoption.

Three strategy-camera pairs use the unchanged real rig, FOV50,11..190m, native
grass LOD and Oak LODGroups, day/sunset/night.180measurements,108000 ordinary
frames. Optional ground-relative fog is a runtime A/B only. Long-range shadows
and fog/palette matter more to this top-down game than roof-interior refinement.

![Actual Player camera views](evidence/game/strategy-camera-day-night.png)
![Continuous clock diagnostic](evidence/core/stage2a-continuous.png)

Apply payload by function in the isolated integration copy, preserving GUIDs.
Do not replace divergent map-authoring shaders or auto-activate main settings.
Build core with SpatialIndirectStage1Build.BuildLinear and -giStage2A; build the
separate camera Player with -giGameViewsBuild. Unity6000.5.5f1, Linear Release,
frame timings enabled temporarily; settings are restored by the builder.
run_stage2a.ps1 generates core evidence without overwriting existing output.
run_game_views.ps1 defaults to Verified prefix; use Final for the first captured
pair, Verified FirstRound2 Repeats2 for the recorded later pairs. The summarizers
expect the canonical folders listed in the report. Prior Stage1C serial/reference
local evidence is needed for numerical comparison. Large bin/PNG/frame JSON and
Player binaries are indexed locally, not bundled. Preserve them before rebuilding.

No actual terrain/height-mesh transport, canopy/local emitters, moving population,
streaming or map save/load adapter. Fixed49x9x49 field and sparse test map remain.
Next: selective current terrain/object snapshot, ready-model visual zone while
retaining cube/walls/roof, strategic near/mid/far shadows/fog/AO and joint clock/
camera/edit checks. Map editor development remains independent.
'''
(out/'README.md').write_text(readme,'utf-8');add(out/'README.md',prefix+'/README.md')
manifest=dict(repository='DimaFi/Little-Castle',branch='codex/lighting-gi-temporal-stage2a-2026-10-27',parentCommit=source['parentCommit'],files=entries.copy())
(out/'publication-manifest.json').write_text(json.dumps(manifest,indent=2),'utf-8');add(out/'publication-manifest.json',prefix+'/publication-manifest.json')
(out/'publication-files.json').write_text(json.dumps(entries,indent=2),'utf-8')
assert len({v['path'] for v in entries})==len(entries)
print(json.dumps(dict(files=len(entries),bytes=sum(v['bytes'] for v in entries),branch=manifest['branch'],rawArtifactsIndexed=len(raw)),indent=2))
