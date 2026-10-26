"""Prepare a LOCAL, reviewable delta packet after audits; never publishes/applies it."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / 'docs/lighting-integration'
OUT = Path('E:/Games_Develop/Research/SpatialIndirect_20261026')
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
audit = json.loads((DOCS / 'spatial-indirect-audit.json').read_text('utf-8'))
evidence = json.loads((OUT / 'evidence-validation.json').read_text('utf-8'))
assert audit['pass'] and evidence['pass_'] and evidence['run'] == 'FinalPrecision'
source = json.loads((DOCS / 'spatial-indirect-manifest.json').read_text('utf-8'))
files = {}
for item in source['modifiedShaders'] + source['newAssets']:
    rel = item['path']
    p = ROOT / rel
    assert sha(p) == item['sha256'], rel
    files['payload/' + rel] = p
    if not rel.endswith('.meta') and (ROOT / (rel + '.meta')).exists():
        files['payload/' + rel + '.meta'] = ROOT / (rel + '.meta')
for rel in (
    'docs/reports/spatial-indirect-prototype-20261026-ru.md',
    'docs/decisions/ADR-lighting-spatial-indirect-experiment-20261026-ru.md',
    'docs/lighting-integration/local-cache-invalidation-ru.md',
    'docs/lighting-integration/spatial-indirect-manifest.json',
    'docs/lighting-integration/spatial-indirect-audit.json',
    'docs/lighting-integration/meadow-baseline-to-spatial-indirect.patch',
):
    files[rel] = ROOT / rel
for name in ('audit_spatial_indirect.py', 'verify_spatial_indirect_evidence.py', 'present_spatial_indirect.py', 'prepare_spatial_indirect_packet.py'):
    files['Tools/LightingStudy/' + name] = ROOT / 'Tools/LightingStudy' / name
for name in ('meadow-source-manifest.json', 'shader-merge-manifest.json'):
    files['reference/' + name] = DOCS / name
files['reference/lighting-to-map-authoring-20261025-ru.md'] = Path('E:/Games_Develop/Little-Castle/docs/handoffs/lighting-to-map-authoring-20261025-ru.md')
files['reference/publication-receipt.json'] = ROOT / 'publication/publication-receipt.json'
for p in (DOCS / 'gi-baseline-9e8c126').rglob('*'):
    if p.is_file():
        files['reference/gi-baseline-9e8c126/' + p.relative_to(DOCS / 'gi-baseline-9e8c126').as_posix()] = p
for name in ('editmode-final.xml', 'playmode-final.xml', 'build.txt', 'build-warnings.txt', 'evidence-validation.json', 'evidence-validation-initial-strict.json', 'evidence-validation-HDR-strict.json'):
    files['evidence/' + name] = OUT / name
for name in ('benchmarks.json', 'updates.json', 'transport-checks.json', 'result.txt', 'runtime-errors.txt'):
    files['evidence/FinalPrecision/' + name] = OUT / 'FinalPrecision' / name
for name in ('spatial-indirect-comparison.png', 'spatial-indirect-time-of-day.png', 'spatial-indirect-transport.png'):
    files['evidence/' + name] = OUT / name

player = []
for p in sorted((OUT / 'Player').rglob('*')):
    if p.is_file():
        player.append(dict(path=p.relative_to(OUT).as_posix(), sha256=sha(p), bytes=p.stat().st_size))
(OUT / 'player-source-receipt.json').write_text(json.dumps(dict(parentCommit=source['parentCommit'], sourceManifestSHA256=sha(DOCS / 'spatial-indirect-manifest.json'), run='FinalPrecision', playerFiles=player), ensure_ascii=False, indent=2), 'utf-8')
files['evidence/player-source-receipt.json'] = OUT / 'player-source-receipt.json'
manifest = [dict(path=rel, sha256=sha(p), bytes=p.stat().st_size) for rel, p in sorted(files.items())]
(OUT / 'local-packet-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), 'utf-8')
readme = '''# SpatialIndirectStudy: локальный эксперимент 26.10.2026

Не опубликован автоматически. Родительский Meadow checkpoint: 9e8c126abd2bf34a15df3bcbf01f7285cfe90843.
Это delta к lighting integration copy с уже импортированным Meadow snapshot,
не самостоятельный Unity project и не готовая миграция нового редактора карты.

payload содержит собственные новые/изменённые source assets с GUID. Общие shaders
переносить по функциям из patch; не заменять новые ветки целиком. Original before
sources сохранены под reference. ProjectSettings там служат только контрольными
снимками, их не применять к игровому проекту. Сторонние модели/текстуры не включены;
GUID и source SHA — reference/meadow-source-manifest.json. Старые map tool files
не входят в payload. Полный согласованный baseline пакет ранее опубликован в GitHub.

Начать с docs/reports/spatial-indirect-prototype-20261026-ru.md и экспериментального
ADR. Сходимость кеша занимает секунды, геометрия ограничена шестью AABB и плоской
землёй. Не включать в основной игре/редакторе по умолчанию. Baseline без keyword
сохраняет исходное выражение ambient. Новая Scene служит только стендом.

evidence включает сырые финальные измерения и labelled contact sheets реальных
Player PNG. Полные27 PNG и76 контрольных PNG остаются в Research/SpatialIndirect_20261026.
Локальный player-source-receipt связывает текущий собранный Player с manifest.
Точные хеши файлов packet — local-packet-manifest.json; zip не меняет источники.
'''
archive = OUT / 'spatial-indirect-integration-20261026.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
    for rel, p in sorted(files.items()):
        z.write(p, rel)
    z.writestr('README-ru.md', readme)
    z.write(OUT / 'local-packet-manifest.json', 'local-packet-manifest.json')
with zipfile.ZipFile(archive) as z:
    for item in manifest:
        assert hashlib.sha256(z.read(item['path'])).hexdigest() == item['sha256']
print(json.dumps(dict(archive=str(archive), sha256=sha(archive), files=len(manifest)+2, bytes=archive.stat().st_size), ensure_ascii=False, indent=2))
