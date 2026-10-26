"""Read-only source audit; writes only this experiment's new manifest and patch."""
from pathlib import Path
import difflib
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/lighting-integration'
BASE = OUT / 'gi-baseline-9e8c126'
MAIN = Path('E:/Games_Develop/Little-Castle')
MAP = Path('E:/Games_Develop/Little-Castle_URP_20261024')


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def block(text, start):
    opening = text.index('{', start)
    depth = 0
    for i in range(opening, len(text)):
        if text[i] == '{':
            depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0:
                return text[start:i + 1]
    raise ValueError('Unbalanced source block')


def passes(text):
    result = {}
    for m in re.finditer(r'\bPass\s*\{', text):
        body = block(text, m.start())
        name = re.search(r'\bName\s+"([^"]+)"', body)
        if name:
            result[name[1]] = body
    return result


def function(text, name):
    m = re.search(r'\b' + re.escape(name) + r'\s*\([^;{}]*\)\s*(?::[^{};]+)?\s*\{', text)
    if m is None:
        raise ValueError('Missing ' + name)
    return block(text, m.start())


records = json.loads((BASE / 'manifest.json').read_text('utf-8'))
frozen_failures, settings_failures, changed, protected_failures, patch = [], [], [], [], []
for item in records:
    rel = item['path']
    saved, current = BASE / rel, ROOT / rel
    if sha(saved) != item['sha256']:
        frozen_failures.append(rel)
    if rel.startswith('ProjectSettings/'):
        if sha(current) != item['sha256']:
            settings_failures.append(rel)
        continue
    before, after = saved.read_text('utf-8-sig'), current.read_text('utf-8-sig')
    changed.append(dict(path=rel, beforeSHA256=item['sha256'], sha256=sha(current), bytes=current.stat().st_size))
    patch.extend(difflib.unified_diff(before.splitlines(True), after.splitlines(True), fromfile='MEADOW_BASELINE/' + rel, tofile='SPATIAL_INDIRECT/' + rel))
    if rel.endswith('.shader'):
        old, new = passes(before), passes(after)
        for name in ('ShadowCaster', 'DepthOnly', 'DepthNormals'):
            if name in old and old.get(name) != new.get(name):
                protected_failures.append(rel + ':' + name)
    if rel.endswith('URP.hlsl'):
        for name in ('LCDeformVertex', 'vert', 'vertShadow', 'fragDepth', 'fragDepthNormals', 'LCVegetationAlpha', 'LCVegetationClip', 'EvaluateNormal'):
            if function(before, name) != function(after, name):
                protected_failures.append(rel + ':' + name)
        wind = 'ApplyGrassWind' if 'Grass' in rel else 'ApplyWind'
        if function(before, wind) != function(after, wind):
            protected_failures.append(rel + ':' + wind)

new_files = []
for p in (ROOT / 'Assets/_Game').rglob('*'):
    if p.is_file() and 'SpatialIndirect' in p.as_posix():
        new_files.append(dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p), bytes=p.stat().st_size))
new_guids = {}
for v in new_files:
    if v['path'].endswith('.meta'):
        match = re.search(r'^guid:\s*([0-9a-f]{32})', (ROOT / v['path']).read_text('utf-8-sig'), re.M)
        if match:
            new_guids[v['path']] = match[1]
guid_locations = {}
for p in (ROOT / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid:\s*([0-9a-f]{32})', p.read_text('utf-8-sig'), re.M)
    if match and match[1] in new_guids.values():
        guid_locations.setdefault(match[1], []).append(p.relative_to(ROOT).as_posix())
guid_failures = {guid: files for guid, files in guid_locations.items() if len(files) != 1}

main_base = json.loads((OUT / 'main-source-manifest.json').read_text('utf-8'))
main_drift = [v['path'] for v in main_base if not (MAIN / v['path']).exists() or sha(MAIN / v['path']) != v['sha256']]
meadow = json.loads((OUT / 'meadow-source-manifest.json').read_text('utf-8'))
source_drift = []
for v in meadow['files']:
    p = MAP / v['path']
    digest = sha(p) if p.exists() else None
    if digest != v['sha256']:
        source_drift.append(dict(path=v['path'], snapshotSHA256=v['sha256'], currentSHA256=digest))

map_contract = []
for p in (MAP / 'Assets/_Game/Scripts/World/Runtime').glob('MapEditSession*.cs'):
    map_contract.append(dict(path=p.relative_to(MAP).as_posix(), sha256=sha(p)))
map_handoff = MAP / 'docs/handoffs/map-authoring-coordination-20261025-ru.md'
map_contract.append(dict(path=map_handoff.relative_to(MAP).as_posix(), sha256=sha(map_handoff)))
map_shaders = []
for v in json.loads((OUT / 'shader-merge-manifest.json').read_text('utf-8')):
    p = MAP / v['path']
    map_shaders.append(dict(path=v['path'], snapshotSHA256=v['sourceSHA256'], currentSHA256=sha(p) if p.exists() else None))
snapshot_failures = []
for v in meadow['files']:
    p = ROOT / v['path']
    if v['action'] == 'snapshot' and (not p.exists() or sha(p) != v['sha256']):
        snapshot_failures.append(v['path'])

manifest = dict(parentCommit='9e8c126abd2bf34a15df3bcbf01f7285cfe90843',
                frozenBaselineManifestSHA256=sha(BASE / 'manifest.json'),
                meadowSourceManifestSHA256=sha(OUT / 'meadow-source-manifest.json'),
                modifiedShaders=changed, newAssets=sorted(new_files, key=lambda x: x['path']),
                newGUIDs=new_guids, currentMapContractReadOnly=map_contract, currentMapSharedShadersReadOnly=map_shaders)
audit = dict(frozenBaselineFailures=frozen_failures, integrationProjectSettingsDrift=settings_failures,
             protectedShaderFunctionOrPassDrift=protected_failures, mainAssetsPackagesSettingsDrift=main_drift,
             meadowSnapshotSourceDrift=source_drift, meadowSnapshotIntegrityFailures=snapshot_failures,
             newGUIDCollisionFailures=guid_failures)
audit['pass'] = not any((frozen_failures, settings_failures, protected_failures, main_drift, snapshot_failures, guid_failures))
(OUT / 'spatial-indirect-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), 'utf-8')
(OUT / 'spatial-indirect-audit.json').write_text(json.dumps(audit, ensure_ascii=False, indent=2), 'utf-8')
(OUT / 'meadow-baseline-to-spatial-indirect.patch').write_text(''.join(patch), 'utf-8')
print(json.dumps(dict(pass_=audit['pass'], modifiedShaders=len(changed), newAssets=len(new_files), sourceDriftCount=len(source_drift), failures={k: v for k, v in audit.items() if 'Drift' in k and k != 'meadowSnapshotSourceDrift'}), ensure_ascii=False, indent=2))
if not audit['pass']:
    raise SystemExit(1)
