"""Import selected immutable inputs; --check validates without writing any files."""
import argparse
import json
from pathlib import Path
import shutil
import sys

# Check mode must also avoid Python's incidental __pycache__ writes.
sys.dont_write_bytecode = True

from local_art_paths import ARCHIVE, GAME, canonical_path, resolve_source, sha256

TARGET = GAME / 'Assets/_Game/Art/Imported/TerrainStarter_v001'


class ImportPlan:
    def __init__(self, local_only=False):
        self.local_only = local_only
        self.dependencies = {}
        self.files = []

    def require(self, source, expected=None):
        source = canonical_path(source)
        path = resolve_source(source, self.local_only)
        digest = sha256(path)
        if expected and digest != expected:
            raise RuntimeError('Release hash mismatch: ' + source)
        entry = dict(source=source, sha256=digest, bytes=path.stat().st_size,
                     resolved_path=str(path), local=path.is_relative_to(ARCHIVE))
        if entry['local']:
            entry['source_archive'] = path.relative_to(GAME).as_posix()
        self.dependencies[source] = entry
        return path

    def read_json(self, source):
        return json.loads(self.require(source).read_text(encoding='utf-8-sig'))

    def copy(self, source, target, expected=None):
        self.require(source, expected)
        self.files.append(dict(self.dependencies[canonical_path(source)], target=target))


def build_plan(local_only=False):
    plan = ImportPlan(local_only)
    art = 'CozySettlement/'
    package = art + 'LocalHandoffs/TerrainStarter-v002'
    for key in ('house', 'oak', 'well', 'barrel', 'crate', 'fence', 'bench'):
        asset_package = art + 'LocalHandoffs/TerrainStarter-v004' if key == 'house' else package
        plan.read_json(f'{asset_package}/{key}/asset.json')
        for level in range(3):
            plan.copy(f'{asset_package}/{key}/{key}_LOD{level}.fbx', f'Models/{key}_LOD{level}.fbx')
        plan.copy(f'{asset_package}/{key}/{key}_BaseColor.png', f'Textures/{key}_BaseColor.png')

    # The visual slice uses a separate, mip-safe house atlas; fixtures keep v004.
    study_house = art + 'LocalHandoffs/TerrainStarter-v005/house'
    for level in range(3):
        plan.copy(f'{study_house}/house_LOD{level}.fbx', f'Study/Models/house_LOD{level}.fbx')
    plan.copy(f'{study_house}/house_BaseColor.png', 'Study/Textures/house_BaseColor.png')
    plan.copy(f'{study_house}/asset.json', 'Study/house-source.json')

    plant_package = art + 'LocalHandoffs/GardenPlantKit-v005'
    plant_manifest = plan.read_json(plant_package + '/manifest.json')
    for asset in plant_manifest['assets']:
        for lod in asset['lods']:
            source = plant_package + '/' + lod['file']
            plan.copy(source, 'Study/Models/' + Path(source).name, lod['sha256'])
    plan.copy(plant_package + '/GardenPalette.png', 'Study/Textures/GardenPalette.png')
    plan.copy(plant_package + '/manifest.json', 'Study/garden-plants-source.json')
    plan.copy(art + 'LocalHandoffs/GardenGround-v001/Grass_MeadowSoft_BaseColor.png',
              'Study/Textures/Grass_MeadowSoft_BaseColor.png')

    wall_release = 'Little-Castle_Assets/Releases/Wall_Stone_Modular/v005'
    wall_manifest = plan.read_json(wall_release + '/release.json')
    wall_hashes = {entry['path']: entry['sha256'] for entry in wall_manifest['files']}
    # Use the immutable release list, so unrelated files cannot change the import.
    for directory in ('Meshes', 'Textures'):
        for relative, digest in wall_hashes.items():
            if (Path(relative).parent.as_posix() != directory or
                    Path(relative).suffix.lower() not in ('.fbx', '.png', '.json')):
                continue
            target = ('Models/' if directory == 'Meshes' else 'Textures/') + Path(relative).name
            plan.copy(wall_release + '/' + relative, target, digest)
    plan.copy(wall_release + '/Connections.json', 'Connections.json', wall_hashes['Connections.json'])

    for family, relative in {
        'Grass_Base_A': 'Ground/Grass_Base_A',
        'DirtPath_A': 'Roads/DirtPath_A_PBR',
        'DirtGround_A': 'Ground/DirtGround_A_PBR',
    }.items():
        folder = art + 'Mat/Material_terrain/' + relative
        plan.copy(f'{folder}/{family}_BaseColor.png', f'Textures/{family}_BaseColor.png')
        if family != 'DirtGround_A':
            for channel in ('Normal', 'AO', 'Height'):
                plan.copy(f'{folder}/{family}_{channel}.png', f'Study/Textures/{family}_{channel}.png')
    plan.copy(art + 'Mat/T_Foliage/T_Foliage_BaseColor.png', 'Textures/T_Foliage_BaseColor.png')
    plan.copy(art + 'Art/Vegetation/Oak_Kit/Textures/T_Oak_Kit_Palette.png', 'Textures/T_Oak_Kit_Palette.png')
    for name in ('SM_Stone_Small_A', 'SM_Stone_Medium_A', 'SM_FlowerCluster_A',
                 'SM_FlowerCluster_B', 'SM_LeafCluster_Small_A'):
        plan.copy(art + 'Art/Vegetation/Oak_Kit/Exports/' + name + '.fbx', 'Models/' + name + '.fbx')
    return plan


def validate_plan(plan):
    manifest_path = ARCHIVE / 'manifest.json'
    archived = {}
    if manifest_path.is_file():
        archived = {entry['source']: entry for entry in
                    json.loads(manifest_path.read_text(encoding='utf-8'))['files']}
    for entry in plan.dependencies.values():
        if entry['local']:
            recorded = archived.get(entry['source'])
            if not recorded or (recorded['sha256'], recorded['bytes']) != (entry['sha256'], entry['bytes']):
                raise RuntimeError('Archived input differs from archive manifest: ' + entry['source'])
    target_files = []
    for entry in plan.files:
        target = TARGET / entry['target']
        target_files.append(dict(target=entry['target'], exists=target.is_file(),
                                 matches=target.is_file() and sha256(target) == entry['sha256']))
    return dict(status='PASS', mode='CHECK_ONLY', source_dependencies=len(plan.dependencies),
                import_files=len(plan.files), import_bytes=sum(e['bytes'] for e in plan.files),
                archived_dependencies=sum(e['local'] for e in plan.dependencies.values()),
                legacy_fallbacks=[e['source'] for e in plan.dependencies.values() if not e['local']],
                target_matches=sum(e['matches'] for e in target_files),
                target_files=target_files, dependencies=list(plan.dependencies.values()))


def execute(plan):
    # Complete validation precedes the first Unity asset write.
    validate_plan(plan)
    files = []
    for entry in plan.files:
        source, target = Path(entry['resolved_path']), TARGET / entry['target']
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.is_file() or sha256(target) != entry['sha256']:
            shutil.copyfile(source, target)
        # Preserve the historical meaning of source; archive is a separate mapping.
        record = {key: entry[key] for key in ('target', 'sha256', 'bytes')}
        record['source'] = str(Path(entry['source']))
        if entry['local']:
            record['source_archive'] = entry['source_archive']
        files.append(record)
    (TARGET / 'provenance.json').write_text(json.dumps(dict(status='SCENE_TEST', files=files), indent=2), encoding='utf-8')
    print('Imported', len(files), 'files;', sum(e['bytes'] for e in files), 'bytes;', TARGET)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', '--dry-run', action='store_true', dest='check',
                        help='Validate every required input and compare existing targets; never write files.')
    parser.add_argument('--local-only', action='store_true', help='Forbid historical sibling-workspace fallback.')
    args = parser.parse_args()
    try:
        plan = build_plan(args.local_only)
        if args.check:
            print(json.dumps(validate_plan(plan), indent=2))
        else:
            execute(plan)
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        parser.exit(1, str(error) + '\n')


if __name__ == '__main__':
    main()
