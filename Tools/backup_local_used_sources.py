"""Archive only importer dependencies and a bounded set of directly used sources.

Default is read-only. --copy creates independent copies under SourceAssets only;
existing differing files are refused, never overwritten. --check verifies the
archive manifest without requiring old sibling workspaces.
"""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import sys

sys.dont_write_bytecode = True

from import_terrain_starter import build_plan
from local_art_paths import ARCHIVE, GAME, ROOT, canonical_path, sha256


def selection():
    plan = build_plan()
    selected = {source: dict(role='import_dependency') for source in plan.dependencies}
    # Direct .blend sources named by used asset reports, without prior versions.
    for source in list(selected):
        if not source.endswith('/asset.json'):
            continue
        report = json.loads(Path(plan.dependencies[source]['resolved_path']).read_text(encoding='utf-8-sig'))
        named = report.get('source', '').replace('\\', '/')
        if Path(named).suffix.lower() == '.blend':
            # Keep historical source identity even after moving this project to
            # another drive or removing its old sibling source workspace.
            parts = named.split('/')
            index = next(i for i, part in enumerate(parts)
                         if part in ('CozySettlement', 'Little-Castle_Assets'))
            relative = canonical_path('/'.join(parts[index:]))
            selected[relative] = dict(role='direct_authoring_snapshot',
                                      referenced_by=source, source_expected_sha256=report.get('sourceSha256'))
    # A directory reference for the used oak: current main files, no backup files,
    # variant history, renders, or unrelated exports. These are snapshots only;
    # external Blender library/image dependencies have not been resolved.
    oak = 'CozySettlement/Art/Vegetation/Oak_Kit/'
    for name in ('Cozy_Oak_Kit.blend', 'Oak_Canopy.blend', 'Oak_Structure.blend',
                 'build_oak_kit.py', 'validate_exports.py', 'README.md',
                 'QA/asset_manifest.json', 'QA/material_spec.json', 'QA/export_validation.json'):
        selected[oak + name] = dict(role='direct_authoring_snapshot')
    for source in ('CozySettlement/LocalHandoffs/GardenPlantKit-v005/GardenPlantKit.blend',
                   'CozySettlement/LocalHandoffs/GardenGround-v001/README.md',
                   'Little-Castle_Assets/Releases/Wall_Stone_Modular/v005/README_RU.md'):
        selected[source] = dict(role='direct_authoring_snapshot' if source.endswith('.blend') else 'release_context')
    return selected


def check_archive():
    manifest = json.loads((ARCHIVE / 'manifest.json').read_text(encoding='utf-8'))
    failures = []
    for entry in manifest['files']:
        relative = canonical_path(entry['source'])
        path = ARCHIVE / relative
        if not path.is_file() or sha256(path) != entry['sha256'] or path.stat().st_size != entry['bytes']:
            failures.append(relative)
        elif path.is_symlink() or path.stat().st_nlink != 1:
            failures.append(relative + ': not an independent regular file')
    return dict(status='PASS' if not failures else 'FAIL', files=len(manifest['files']),
                bytes=sum(e['bytes'] for e in manifest['files']), failures=failures,
                independent_regular_files=not failures)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--copy', action='store_true')
    mode.add_argument('--check', action='store_true')
    args = parser.parse_args()
    if args.check:
        result = check_archive()
        print(json.dumps(result, indent=2))
        raise SystemExit(0 if result['status'] == 'PASS' else 1)
    selected = selection()
    entries = []
    for relative, metadata in sorted(selected.items()):
        original, target = ROOT / relative, ARCHIVE / relative
        # Existing archives remain checkable if an original has since been moved.
        source = original if original.is_file() else target
        digest = sha256(source)
        if args.copy:
            if target.exists() and (target.is_symlink() or sha256(target) != digest):
                raise RuntimeError('Refusing to replace different archive: ' + str(target))
            if not target.exists():
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, target)
            if sha256(target) != digest or target.stat().st_nlink != 1:
                raise RuntimeError('Archive verification failed: ' + str(target))
            if original.is_file() and os.path.samefile(original, target):
                raise RuntimeError('Archive must be an independent file: ' + str(target))
        entry = dict(source=relative, source_archive=target.relative_to(GAME).as_posix(),
                     sha256=digest, bytes=source.stat().st_size, **metadata)
        if entry.get('source_expected_sha256'):
            entry['matches_referenced_source_hash'] = digest == entry['source_expected_sha256']
        entries.append(entry)
    manifest = dict(schema_version=1, created_utc=datetime.now(timezone.utc).isoformat(),
                    canonical_source_root='Historical parent of Little-Castle; paths retain workspace names.',
                    archive_root='SourceAssets/Dependencies',
                    scope='Exact terrain/garden importer inputs plus bounded direct CozySettlement authoring snapshots.',
                    limits=['Not a complete art library or complete Blender linked-data closure.',
                            'Canonical Little-Castle_Assets authoring/history remains in its existing workspace.',
                            'Source manifests are byte-preserved and may name historical absolute paths.',
                            'Authoring scripts outside the importer retain their historical output/root conventions.'],
                    file_count=len(entries), total_bytes=sum(e['bytes'] for e in entries), files=entries)
    if args.copy:
        (ARCHIVE / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
        print(json.dumps(check_archive(), indent=2))
    else:
        print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    main()
