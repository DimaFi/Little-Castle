from pathlib import Path
import hashlib,json

root=Path(__file__).resolve().parents[2];data=Path('E:/Games_Develop/Research/SpatialIndirectStage1B_20261026');out=root/'publication-gi-stage1b';repo=root/'publication-spatial-gi/repo';prefix='docs/integration-packages/lighting-gi-kernels-stage1b-20261026';out.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();entries=[]
j=json.loads((data/'stage1b-summary.json').read_text('utf-8'));audit=json.loads((root/'docs/lighting-integration/stage1b-audit.json').read_text('utf-8'));source=json.loads((root/'docs/lighting-integration/stage1b-manifest.json').read_text('utf-8'))
assert j['pass_'] and audit['pass_'] and j['frozenBeforePlayerMatchesPublishedHashes']
def add(p,rel):
    p=Path(p);target=repo/rel;assert target.resolve().is_relative_to(repo.resolve());target.parent.mkdir(parents=True,exist_ok=True);content=p.read_bytes();target.write_bytes(content)
    entries.append(dict(path=rel,sha256=hashlib.sha256(content).hexdigest(),gitBlobSHA=hashlib.sha1(b'blob '+str(len(content)).encode()+b'\0'+content).hexdigest(),bytes=len(content),source=str(p)))
for item in source['modified']:
    assert sha(root/item['path'])==item['sha256']
    for suffix in ('','.meta'):add(root/(item['path']+suffix),prefix+'/payload/'+item['path']+suffix)
base=root/'docs/lighting-integration/stage1b-baseline-98da069'
for p in base.rglob('*'):
    if p.is_file():add(p,prefix+'/reference/stage1b-baseline-98da069/'+p.relative_to(base).as_posix())
for name in ('stage1b-manifest.json','stage1b-audit.json','gi-98da069-to-stage1b.patch'):add(root/'docs/lighting-integration'/name,prefix+'/'+name)
for name in ('run_stage1b.ps1','audit_stage1b.py','summarize_stage1b.py','report_stage1b.py','prepare_stage1b_publication.py'):add(root/'Tools/LightingStudy'/name,prefix+'/tools/'+name)
artifacts=[]
for i in range(1,4):
    for mode in ('Before','After'):
        directory=data/f'Final{i:02}{mode}'
        for p in directory.iterdir():
            if p.is_file():artifacts.append(dict(path=p.relative_to(data).as_posix(),sha256=sha(p),bytes=p.stat().st_size))
        for name in ('updates.json','benchmarks.json','transport-checks.json','result.txt','runtime-errors.txt'):add(directory/name,prefix+'/evidence/'+directory.name+'/'+name)
    add(data/f'Final{i:02}-comparison.json',prefix+f'/evidence/Final{i:02}-comparison.json')
for name in ('stage1b-summary.json','editmode.xml','playmode.xml','reference-profile.json','cached-profile.json','build.txt','build-warnings.txt','stage1b-game-comparison.png','stage1b-time-of-day.png','stage1b-performance.png'):add(data/name,prefix+'/evidence/'+name)
logs=[dict(path=p.name,sha256=sha(p),bytes=p.stat().st_size) for p in data.glob('*.log')]
index=dict(parentCommit=source['parentCommit'],localEvidenceRoot=str(data),sourceManifestSHA256=sha(root/'docs/lighting-integration/stage1b-manifest.json'),rawArtifacts=artifacts,logs=logs,playerFiles=j['players'])
(out/'evidence-index.json').write_text(json.dumps(index,indent=2),'utf-8');add(out/'evidence-index.json',prefix+'/evidence-index.json')
for rel in ('docs/reports/lighting-gi-kernels-stage1b-20261026-ru.md','docs/plans/lighting-next-stages-20261026-ru.md'):add(root/rel,rel)
add(Path('E:/Games_Develop/Little-Castle/docs/handoffs/lighting-to-map-authoring-20261025-ru.md'),'docs/handoffs/lighting-to-map-authoring-20261025-ru.md')
readme='''# GI Stage 1B — faster exact candidate/visibility kernels

Delta over `98da06954c1d8c027d275ea80f8300c8b65ff7e9`; the existing Stage1
transport packet and licensed Meadow dependencies are required. Root game Assets,
Packages and ProjectSettings are unchanged. This is an integration packet, not
an automatic production-GI activation or a fresh-clone runnable game.

- [Russian report and measured limits](../../reports/lighting-gi-kernels-stage1b-20261026-ru.md)
- [Next steps, including ready-model visual fixture](../../plans/lighting-next-stages-20261026-ru.md)
- [Map-authoring handoff](../../handoffs/lighting-to-map-authoring-20261025-ru.md)
- [Source hashes](stage1b-manifest.json), [audit](stage1b-audit.json), [patch](gi-98da069-to-stage1b.patch)
- [Raw summary](evidence/stage1b-summary.json), [local binary/capture/log hashes](evidence-index.json)

Three paired Release runs: median initial convergence 4.701 -> 2.765 s,
roof removal 1.532 -> 0.831 s, undo 1.955 -> 0.934 s, light refresh about
0.65-0.70 -> 0.30-0.34 s. Solve budget remains 2 ms, same rays and workload.
197 Edit Mode and 6 Play Mode tests passed. All 24 full probe fields are
bit-identical to the published cached Stage1 Player; serial reference error
is at most 5.0664e-7. Actual PNGs have ordinary SSAO noise.

![Real Player before and after](evidence/stage1b-game-comparison.png)
![Day, sunset, night in the new Player](evidence/stage1b-time-of-day.png)
![Median and min/max of three paired Release runs](evidence/stage1b-performance.png)

Candidate slabs reject disjoint axes early. Any-hit stops visibility at the
first blocker. Exactly zero sun/moon sources skip visibility with a correctly
invalidated zero-to-nonzero epoch. The original 72 sky directions/inverses are
precomputed without sampling changes. Independent serial reference is retained.
Four C# files and their existing GUIDs are in payload; before-images in reference.
Shared shaders, original fixture, palette and volume/queue/review are unchanged.

Remaining limits: fixed L1 field, flat ground and six AABB proxies, no canopy,
real terrain, local emitters, continuous-time blending or visible priority.
Geometry still takes 0.83-0.93 s. Next: visible-priority queue plus a standard
small-edit latency case, then versioned continuous lighting and current terrain.
Ready models will have a separate visual zone; the cube/walls/roof experiment
stays available. No new production model was added to this performance A/B.

Raw full-size PNG/bin sets and compiled Players remain local with hashes.
Release allocation bytes remain unsupported; previous Stage1 Development
allocation evidence is not claimed as a new Player-wide zero-GC measurement.
'''
(out/'README.md').write_text(readme,'utf-8');add(out/'README.md',prefix+'/README.md')
manifest=dict(repository='DimaFi/Little-Castle',branch='codex/lighting-gi-kernels-stage1b-2026-10-26',parentCommit=source['parentCommit'],files=entries.copy())
(out/'publication-manifest.json').write_text(json.dumps(manifest,indent=2),'utf-8');add(out/'publication-manifest.json',prefix+'/publication-manifest.json');(out/'publication-files.json').write_text(json.dumps(entries,indent=2),'utf-8')
assert len({v['path'] for v in entries})==len(entries)
print(json.dumps(dict(files=len(entries),bytes=sum(v['bytes'] for v in entries),parent=source['parentCommit'],branch=manifest['branch'],rawArtifactsIndexed=len(artifacts)),indent=2))
