from pathlib import Path
import hashlib
import json

root=Path(__file__).resolve().parents[2];data=Path('E:/Games_Develop/Research/SpatialIndirectStage1_20261026')
out=root/'publication-gi-stage1';repo=root/'publication-spatial-gi/repo';prefix='docs/integration-packages/lighting-gi-cache-stage1-20261026'
out.mkdir(exist_ok=True);sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();entries=[]
summary=json.loads((data/'stage1-summary.json').read_text('utf-8'));audit=json.loads((root/'docs/lighting-integration/stage1-audit.json').read_text('utf-8'))
assert summary['pass_'] and audit['pass_'];source=json.loads((root/'docs/lighting-integration/stage1-manifest.json').read_text('utf-8'))

def add(p,rel):
    p=Path(p);target=repo/rel;assert target.resolve().is_relative_to(repo.resolve());target.parent.mkdir(parents=True,exist_ok=True);content=p.read_bytes();target.write_bytes(content)
    entries.append(dict(path=rel,sha256=hashlib.sha256(content).hexdigest(),gitBlobSHA=hashlib.sha1(b'blob '+str(len(content)).encode()+b'\0'+content).hexdigest(),bytes=len(content),source=str(p)))

included=set()
for item in source['modified']+source['newAssets']:
    assert sha(root/item['path'])==item['sha256']
    for rel in (item['path'],item['path']+'.meta') if not item['path'].endswith('.meta') else (item['path'],):
        if rel in included or not (root/rel).exists():continue
        included.add(rel);add(root/rel,prefix+'/payload/'+rel)
base=root/'docs/lighting-integration/stage1-baseline-100c174'
for p in base.rglob('*'):
    if p.is_file():add(p,prefix+'/reference/stage1-baseline-100c174/'+p.relative_to(base).as_posix())
for name in ('stage1-manifest.json','stage1-audit.json','gi-100c174-to-stage1.patch'):
    add(root/'docs/lighting-integration'/name,prefix+'/'+name)
for name in ('audit_stage1.py','run_stage1.ps1','verify_stage1.py','summarize_stage1.py','sample_stage1_memory.ps1','report_stage1.py','prepare_stage1_publication.py'):
    add(root/'Tools/LightingStudy'/name,prefix+'/tools/'+name)

evidence=[]
for i in range(1,4):
    for mode in ('Reference','Cached'):
        folder=data/f'Final{i:02}{mode}'
        for p in folder.iterdir():
            if p.is_file():evidence.append(dict(path=p.relative_to(data).as_posix(),sha256=sha(p),bytes=p.stat().st_size))
        for name in ('updates.json','benchmarks.json','transport-checks.json','result.txt','runtime-errors.txt'):
            add(folder/name,prefix+'/evidence/'+folder.name+'/'+name)
    name=f'Final{i:02}Reference-vs-Final{i:02}Cached.json';add(data/name,prefix+'/evidence/'+name)
for name in ('stage1-summary.json','os-memory-samples-final.json','editmode-final.xml','playmode-final.xml','reference-profile.json','cached-profile.json','build.txt','build-warnings.txt','stage1-game-comparison.png','stage1-performance.png'):
    add(data/name,prefix+'/evidence/'+name)
for name in ('updates.json','benchmarks.json','transport-checks.json','result.txt','runtime-errors.txt'):
    add(data/'AllocationFinal'/name,prefix+'/evidence/AllocationFinal/'+name)
for name in ('build.txt','build-warnings.txt'):add(data/'Development'/name,prefix+'/evidence/Development/'+name)
players=[]
for area in ('Player','Development/Player'):
    for p in (data/area).rglob('*'):
        if p.is_file():players.append(dict(path=p.relative_to(data).as_posix(),sha256=sha(p),bytes=p.stat().st_size))
index=dict(parentCommit=source['parentCommit'],sourceManifestSHA256=sha(root/'docs/lighting-integration/stage1-manifest.json'),localEvidenceRoot=str(data),captureAndProbeArtifacts=evidence,playerFiles=players)
(out/'evidence-index.json').write_text(json.dumps(index,ensure_ascii=False,indent=2),'utf-8');add(out/'evidence-index.json',prefix+'/evidence-index.json')
for rel in ('docs/reports/lighting-gi-cache-stage1-20261026-ru.md','docs/plans/lighting-next-stages-20261026-ru.md'):
    add(root/rel,rel)
add(Path('E:/Games_Develop/Little-Castle/docs/handoffs/lighting-to-map-authoring-20261025-ru.md'),'docs/handoffs/lighting-to-map-authoring-20261025-ru.md')
readme='''# GI cache Stage 1 — measured transport reuse

Parent checkpoint: `100c1745924493d2d8be0f29b5541c695273ed6e`.
This packet preserves the first cache optimization on the unchanged Meadow GI
fixture. Game Assets, Packages and ProjectSettings remain outside this delta.

- [Report, methods, limits and raw results](../../reports/lighting-gi-cache-stage1-20261026-ru.md)
- [Updated next-stage plan](../../plans/lighting-next-stages-20261026-ru.md)
- [Handoff](../../handoffs/lighting-to-map-authoring-20261025-ru.md)
- [Source SHA/GUID manifest](stage1-manifest.json)
- [Before/after C# patch](gi-100c174-to-stage1.patch)
- [Raw summary](evidence/stage1-summary.json)

![Actual Player images](evidence/stage1-game-comparison.png)
![Three paired Release measurements](evidence/stage1-performance.png)

Median albedo convergence 5.33 s -> 31 ms; roof removal 5.62 -> 1.52 s;
roof undo 6.59 -> 1.95 s; light refresh about 13 -> 0.65-0.70 s. Solve budget stays
2 ms. Saved transport payload is about 8.38 MiB plus managed overhead.
193 Edit Mode and 6 Play Mode passed. 24 full-field comparisons of 21,609
probes have maximum absolute coefficient error 5.0664e-7. First pair contains
27 actual reference/cached image pairs. Allocations are diagnosed separately
in a Development Player with a positive GC.Alloc control; Release counters
that failed calibration are explicitly unsupported.

`payload/` contains only three changed research runtime sources and five new
sources, with their GUIDs. Apply as a delta to the existing lighting workspace
and licensed Meadow dependencies, not as a fresh-clone runnable Unity project.
The shared shaders, original scene/materials, reference queue/builder and root
settings are preserved. Source before-images are frozen in `reference/`.

This remains a serial fixed AABB/L1 experiment with flat proxy ground. Roof
updates still take over a second. No production activation, new map editor
migration, canopy, continuous-time injection or temporal blending is claimed.
Next work profiles remaining primary/shadow kernels and prioritizes visible
probes before terrain/object integration. The editor can continue independently.

Raw PNG/bin sets, standalone Players and full logs remain local; SHA-256 and
compiled Player hashes are recorded in evidence-index.json. No third-party
binary source models/textures or large generated probe dumps are uploaded.
'''
(out/'README.md').write_text(readme,'utf-8');add(out/'README.md',prefix+'/README.md')
manifest=dict(repository='DimaFi/Little-Castle',branch='codex/lighting-gi-cache-stage1-2026-10-26',parentCommit=source['parentCommit'],files=entries.copy())
(out/'publication-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),'utf-8');add(out/'publication-manifest.json',prefix+'/publication-manifest.json')
(out/'publication-files.json').write_text(json.dumps(entries,ensure_ascii=False,indent=2),'utf-8')
assert len({v['path'] for v in entries})==len(entries)
print(json.dumps(dict(files=len(entries),bytes=sum(v['bytes'] for v in entries),parent=source['parentCommit'],branch=manifest['branch'],localArtifactsIndexed=len(evidence)),ensure_ascii=False,indent=2))
