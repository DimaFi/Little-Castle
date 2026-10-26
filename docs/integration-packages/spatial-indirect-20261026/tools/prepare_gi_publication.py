"""Freeze the already-validated GI ZIP and the next-stage plan for Git publication.

Writes only the isolated publication checkout and local review manifests.
Does not publish, stage, or alter any game project settings.
"""
from pathlib import Path, PurePosixPath
import hashlib
import json
import re
import zipfile

ROOT = Path(__file__).resolve().parents[2]
MAIN = Path('E:/Games_Develop/Little-Castle')
RESEARCH = Path('E:/Games_Develop/Research/SpatialIndirect_20261026')
OUT = ROOT / 'publication-spatial-gi'
REPO = OUT / 'repo'
PREFIX = 'docs/integration-packages/spatial-indirect-20261026'
PARENT = '9e8c126abd2bf34a15df3bcbf01f7285cfe90843'
ARCHIVE_SHA = 'b19f88a43334f97d92a42029ff1d5c9b5ab824268b86ef3edc3f65b577d18980'
entries = []
sha = lambda data: hashlib.sha256(data).hexdigest()


def add(rel, data, source):
    target = REPO / rel
    assert target.resolve().is_relative_to(REPO.resolve()), rel
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode('ascii') + b'\0' + data).hexdigest()
    entries.append(dict(path=rel, sha256=sha(data), gitBlobSHA=blob, bytes=len(data), source=source))


archive = RESEARCH / 'spatial-indirect-integration-20261026.zip'
assert sha(archive.read_bytes()) == ARCHIVE_SHA, 'Frozen archive changed'
assert (REPO / '.git').is_dir(), 'Expected isolated publication clone'
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    names = z.namelist()
    assert len(names) == 82 and len(set(names)) == 82
    frozen = json.loads(z.read('local-packet-manifest.json'))
    assert set(names) == {v['path'] for v in frozen} | {'README-ru.md', 'local-packet-manifest.json'}
    for v in frozen:
        assert sha(z.read(v['path'])) == v['sha256'], v['path']
        if v['path'].startswith('payload/'):
            current = ROOT / v['path'][len('payload/'):]
            assert current.is_file() and sha(current.read_bytes()) == v['sha256'], str(current)
    evidence = json.loads(z.read('evidence/evidence-validation.json'))
    audit = json.loads(z.read('docs/lighting-integration/spatial-indirect-audit.json'))
    assert evidence['pass_'] and audit['pass'] and evidence['run'] == 'FinalPrecision'
    for name in names:
        path = PurePosixPath(name)
        assert not path.is_absolute() and '..' not in path.parts
        add(PREFIX + '/packet/' + name, z.read(name), 'frozen-zip:' + name)

plan = 'docs/plans/lighting-next-stages-20261026-ru.md'
handoff = 'docs/handoffs/lighting-to-map-authoring-20261025-ru.md'
add(plan, (ROOT / plan).read_bytes(), str(ROOT / plan))
add(handoff, (MAIN / handoff).read_bytes(), str(MAIN / handoff))
add(PREFIX + '/tools/prepare_gi_publication.py', Path(__file__).read_bytes(), str(Path(__file__)))

readme = '''# Spatial indirect lighting checkpoint — 26 October 2026

This commit publishes the complete frozen 82-file GI experiment packet, real
Player contact sheets, raw validation and measurements, and the next-stage plan.
It builds on Meadow checkpoint `9e8c126abd2bf34a15df3bcbf01f7285cfe90843`.
Unity source paths are under `packet/payload/` for reviewable integration;
this publication does not activate GI or migrate the live game project.

- [Next stages and acceptance criteria](../../plans/lighting-next-stages-20261026-ru.md)
- [Current handoff](../../handoffs/lighting-to-map-authoring-20261025-ru.md)
- [Experiment report](packet/docs/reports/spatial-indirect-prototype-20261026-ru.md)
- [Source/GUID manifest](packet/docs/lighting-integration/spatial-indirect-manifest.json)
- [Function-scoped shader patch](packet/docs/lighting-integration/meadow-baseline-to-spatial-indirect.patch)
- [CPU update latency](packet/evidence/FinalPrecision/updates.json)
- [Ordinary-frame GPU/CPU timings](packet/evidence/FinalPrecision/benchmarks.json)

![Real Player baseline / GI](packet/evidence/spatial-indirect-comparison.png)
![Real Player day / sunset / night](packet/evidence/spatial-indirect-time-of-day.png)
![Offscreen reflector and roof removal](packet/evidence/spatial-indirect-transport.png)

186 Edit Mode and 6 Play Mode tests passed. The bounded field demonstrates sky
occlusion, one diffuse bounce, an offscreen contributor, local invalidation,
revision rejection and deterministic undo. The final build had no errors/warnings.
GPU mean was 0.840 / 1.184 ms off/on at 1920x1080 on RTX 4070 Ti SUPER with Linear,
HDR, MSAA4 and SSAO. Local updates still take 5-7 seconds at the chosen CPU budget.
The prototype has six proxy boxes, approximate flat ground, a fixed L1 probe
field, no production map adapter and no continuous-time injection strategy.
It is not production GI or a reproduction of the complete Tiny Glade renderer.

The first planned implementation profiles and accelerates geometry transport and
lighting injection on the unchanged fixture. Later stages cover smooth updates,
current terrain/object snapshots, artistic lighting and scalable integration.

The original ZIP is preserved byte-for-byte as expanded files under `packet/`.
Its README and reference handoff retain the historical local-only status at
packaging time; this outer README and current handoff describe this publication.
`publication-manifest.json` maps every published input to SHA-256 and Git blob SHA.
The three PNGs are labelled contact sheets of actual game frames; no generated
or repainted images are used. Complete capture sets and standalone Players remain
at the local Research path. Third-party binary model/texture sources are not
included: restore licensed dependencies using the frozen GUID/hash manifests.
Do not copy old map-tool snapshots over the independently evolving editor;
merge shader changes by function and preserve wind/masks/LOD/depth/shadow/normals.
'''
add(PREFIX + '/README.md', readme.encode('utf-8'), 'generated-publication-readme')
provenance = dict(parentCommit=PARENT, originalArchiveSHA256=ARCHIVE_SHA, originalFiles=82,
                  layout='all 82 original ZIP entries preserved under packet/',
                  branch='codex/lighting-spatial-gi-2026-10-26', validationRun='FinalPrecision')
add(PREFIX + '/source-archive-receipt.json', (json.dumps(provenance, ensure_ascii=False, indent=2) + '\n').encode('utf-8'), 'frozen-archive-provenance')
manifest = dict(repository='DimaFi/Little-Castle', branch=provenance['branch'], parentCommit=PARENT,
                archiveSHA256=ARCHIVE_SHA, originalPacketFiles=82, files=entries.copy())
add(PREFIX + '/publication-manifest.json', (json.dumps(manifest, ensure_ascii=False, indent=2) + '\n').encode('utf-8'), 'publication-manifest-excludes-own-hash')
(OUT / 'publication-files.json').write_text(json.dumps(entries, ensure_ascii=False, indent=2), 'utf-8')

# Validate repository-relative links in the new navigational documents before commit.
for rel in (plan, PREFIX + '/README.md'):
    p = REPO / rel
    for link in re.findall(r'\]\(([^)]+)\)', p.read_text('utf-8')):
        if '://' in link or link.startswith('#'):
            continue
        target = (p.parent / link.split('#')[0]).resolve()
        # One research link belongs to the unchanged parent packet; its Git tree
        # presence is checked while staging without fetching unrelated Unity assets.
        if 'lighting-meadow-20261025' not in target.as_posix():
            assert target.is_file(), f'Broken link {rel}: {link}'
assert len({v['path'] for v in entries}) == len(entries)
print(json.dumps(dict(files=len(entries), frozenPacketFiles=82, bytes=sum(v['bytes'] for v in entries), sourceArchiveSHA256=ARCHIVE_SHA, branch=provenance['branch']), ensure_ascii=False, indent=2))
