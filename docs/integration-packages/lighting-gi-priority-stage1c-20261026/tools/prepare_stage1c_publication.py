from pathlib import Path
import hashlib,json

r=Path(__file__).resolve().parents[2];data=Path('E:/Games_Develop/Research/SpatialIndirectStage1C_20261026');out=r/'publication-gi-stage1c';repo=r/'publication-spatial-gi/repo';prefix='docs/integration-packages/lighting-gi-priority-stage1c-20261026';out.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();entries=[]
j=json.loads((data/'stage1c-summary.json').read_text('utf-8'));audit=json.loads((r/'docs/lighting-integration/stage1c-audit.json').read_text('utf-8'));source=json.loads((r/'docs/lighting-integration/stage1c-manifest.json').read_text('utf-8'))
assert j['pass_'] and audit['pass_'] and j['runPrefix']=='Guarded' and j['movingCameraCheck']['pass']
def add(p,rel):
    p=Path(p);target=repo/rel;assert target.resolve().is_relative_to(repo.resolve());target.parent.mkdir(parents=True,exist_ok=True);content=p.read_bytes();target.write_bytes(content);entries.append(dict(path=rel,sha256=hashlib.sha256(content).hexdigest(),gitBlobSHA=hashlib.sha1(b'blob '+str(len(content)).encode()+b'\0'+content).hexdigest(),bytes=len(content),source=str(p)))
included=set()
for item in source['modified']+source['newAssets']:
    assert sha(r/item['path'])==item['sha256']
    for rel in (item['path'],item['path']+'.meta') if not item['path'].endswith('.meta') else (item['path'],):
        if rel not in included:included.add(rel);add(r/rel,prefix+'/payload/'+rel)
base=r/'docs/lighting-integration/stage1c-baseline-d1d92e0'
for p in base.rglob('*'):
    if p.is_file():add(p,prefix+'/reference/stage1c-baseline-d1d92e0/'+p.relative_to(base).as_posix())
for name in ('stage1c-manifest.json','stage1c-audit.json','gi-d1d92e0-to-stage1c.patch'):add(r/'docs/lighting-integration'/name,prefix+'/'+name)
for name in ('run_stage1c.ps1','audit_stage1c.py','summarize_stage1c.py','report_stage1c.py','prepare_stage1c_publication.py'):add(r/'Tools/LightingStudy'/name,prefix+'/tools/'+name)
artifacts=[]
folders=[data/f'Guarded{i:02}{m}' for i in range(1,4) for m in ('Fifo','Priority')]+[data/'GuardedSerial',data/'GuardedMotion']
for directory in folders:
    for p in directory.iterdir():
        if p.is_file():artifacts.append(dict(path=p.relative_to(data).as_posix(),sha256=sha(p),bytes=p.stat().st_size))
    for name in ('updates.json','benchmarks.json','transport-checks.json','result.txt','runtime-errors.txt','motion-checks.json'):
        if (directory/name).exists():add(directory/name,prefix+'/evidence/'+directory.name+'/'+name)
for i in range(1,4):add(data/f'Guarded{i:02}-comparison.json',prefix+f'/evidence/Guarded{i:02}-comparison.json')
for name in ('stage1c-summary.json','editmode-final.xml','playmode-final.xml','build.txt','build-warnings.txt','stage1c-game-comparison.png','stage1c-latency.png'):add(data/name,prefix+'/evidence/'+name)
logs=[dict(path=p.name,sha256=sha(p),bytes=p.stat().st_size) for p in data.glob('*.log') if p.name.startswith('Guarded') or p.name in ('build-guarded.log','editmode-final.log','playmode-final.log')]
index=dict(parentCommit=source['parentCommit'],localEvidenceRoot=str(data),sourceManifestSHA256=sha(r/'docs/lighting-integration/stage1c-manifest.json'),rawArtifacts=artifacts,logs=logs,playerFiles=j['playerFiles'],excludedPreliminaryPrefix='Final',excludedCompileFailure='build-final.log')
(out/'evidence-index.json').write_text(json.dumps(index,indent=2),'utf-8');add(out/'evidence-index.json',prefix+'/evidence-index.json')
for rel in ('docs/reports/lighting-gi-priority-stage1c-20261026-ru.md','docs/plans/lighting-next-stages-20261026-ru.md'):add(r/rel,rel)
add(Path('E:/Games_Develop/Little-Castle/docs/handoffs/lighting-to-map-authoring-20261025-ru.md'),'docs/handoffs/lighting-to-map-authoring-20261025-ru.md')
readme='''# GI Stage 1C — frustum-support priority with bounded background progress

Delta over `d1d92e0505110726be62f6ed408f33e446201ce8`. Existing Stage1/1B packets
and licensed Meadow dependencies are required. Root game Assets, Packages and
ProjectSettings are unchanged. No production GI activation or map-tool migration.

- [Report: definitions, measured results and limits](../../reports/lighting-gi-priority-stage1c-20261026-ru.md)
- [Next-stage plan](../../plans/lighting-next-stages-20261026-ru.md)
- [Map-authoring handoff](../../handoffs/lighting-to-map-authoring-20261025-ru.md)
- [Source/GUID hashes](stage1c-manifest.json), [audit](stage1c-audit.json), [patch](gi-d1d92e0-to-stage1c.patch)
- [Raw summary](evidence/stage1c-summary.json), [local artifacts/Player SHA index](evidence-index.json)

Three preferred requests are followed by one global FIFO request. Intrusive lists
avoid stale/duplicate queue entries. Camera reclassification retains fairness and
is limited to once per four frames; its cost is inside the same cooperative 2 ms
budget. Camera queue preference may lag three frames; geometry correctness does not.
Priority is conservative frustum support, including interpolation neighbours,
not depth-based pixel visibility. Keep this distinction when reading latency.

203 Edit Mode and 6 Play Mode tests passed. Final Guarded Release evidence has
three FIFO/Priority pairs on one Player, a serial reference run and a separate
moving-camera progress check. 36 complete probe fields compare exactly between
FIFO and Priority; serial error <=5.0664e-7. The first eight scenarios also match
the published Stage1B fields exactly. Raw metrics and min/max are in the report.

![Actual Player final-state images](evidence/stage1c-game-comparison.png)
![Three paired support/full convergence measurements](evidence/stage1c-latency.png)

The optional automated small-edit case adds a 0.8 m diagnostic cube, commits a
0.5 m move, undoes it and removes it, using the existing shared neutral material.
The original cube/walls/roof experiment remains. Ready production models have a
separate planned visual zone; no new production models were added in this pass.

Four existing C# files and one new test source plus existing/new GUID metadata
are in payload; before-images in reference. Trace/transport kernels, all shared
shaders, original saved scene/materials and root settings stay fixed. Changed
buffer probes are still published in place: temporal age boundaries are possible.
Next work is versioned/paced publication and continuous-time lighting, followed
by current terrain/object snapshots and the ready-model visual fixture.

Limits remain fixed L1 field, flat proxy ground, max eight AABBs, no canopy or
local emitters. Queue memory grows about 281 KB; full work does not disappear.
Support rendering metrics are not pixel-change or input-to-display latency.
Whole-Player allocations/OS/VRAM peak are not newly measured. Large PNG/bin sets,
Players and logs remain local with hashes. Earlier Final* pilot evidence is
excluded; Guarded* and final XML/build are authoritative.
'''
(out/'README.md').write_text(readme,'utf-8');add(out/'README.md',prefix+'/README.md')
manifest=dict(repository='DimaFi/Little-Castle',branch='codex/lighting-gi-priority-stage1c-2026-10-26',parentCommit=source['parentCommit'],files=entries.copy());(out/'publication-manifest.json').write_text(json.dumps(manifest,indent=2),'utf-8');add(out/'publication-manifest.json',prefix+'/publication-manifest.json');(out/'publication-files.json').write_text(json.dumps(entries,indent=2),'utf-8');assert len({v['path'] for v in entries})==len(entries)
print(json.dumps(dict(files=len(entries),bytes=sum(v['bytes'] for v in entries),branch=manifest['branch'],parent=source['parentCommit'],rawArtifactsIndexed=len(artifacts)),indent=2))
