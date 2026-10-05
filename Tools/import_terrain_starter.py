"""Copy only the selected local test package and record provenance; no source edits."""
import hashlib
import json
from pathlib import Path
import shutil

GAME = Path(__file__).resolve().parents[1]
ROOT = GAME.parent
ART = ROOT / 'CozySettlement'
PACKAGE = ART / 'LocalHandoffs/TerrainStarter-v002'
TARGET = GAME / 'Assets/_Game/Art/Imported/TerrainStarter_v001'
files = []


def copy(source, relative):
    target = TARGET / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if not target.exists() or hashlib.sha256(target.read_bytes()).hexdigest() != digest:
        shutil.copyfile(source, target)
    files.append(dict(source=str(source.relative_to(ROOT)), target=relative,
                      sha256=digest, bytes=source.stat().st_size))


for key in ('house', 'oak', 'well', 'barrel', 'crate', 'fence', 'bench'):
    asset_package = ART / 'LocalHandoffs/TerrainStarter-v004' if key == 'house' else PACKAGE
    report = json.loads((asset_package / key / 'asset.json').read_text())
    for level in range(3):
        copy(asset_package / key / f'{key}_LOD{level}.fbx', f'Models/{key}_LOD{level}.fbx')
    copy(asset_package / key / f'{key}_BaseColor.png', f'Textures/{key}_BaseColor.png')

# The visual slice uses a separate, mip-safe house atlas; older fixtures keep v004.
study_house = ART / 'LocalHandoffs/TerrainStarter-v005/house'
for level in range(3):
    copy(study_house / f'house_LOD{level}.fbx', f'Study/Models/house_LOD{level}.fbx')
copy(study_house / 'house_BaseColor.png', 'Study/Textures/house_BaseColor.png')
copy(study_house / 'asset.json', 'Study/house-source.json')

# New decorative models remain isolated to the visual slice, not WorldSpawnCatalog.
plant_package = ART / 'LocalHandoffs/GardenPlantKit-v005'
plant_manifest = json.loads((plant_package / 'manifest.json').read_text())
for asset in plant_manifest['assets']:
    for lod in asset['lods']:
        source = plant_package / lod['file']
        if hashlib.sha256(source.read_bytes()).hexdigest() != lod['sha256']:
            raise RuntimeError('Plant release hash mismatch: ' + str(source))
        copy(source, 'Study/Models/' + source.name)
copy(plant_package / 'GardenPalette.png', 'Study/Textures/GardenPalette.png')
copy(plant_package / 'manifest.json', 'Study/garden-plants-source.json')
ground_package = ART / 'LocalHandoffs/GardenGround-v001'
copy(ground_package / 'Grass_MeadowSoft_BaseColor.png', 'Study/Textures/Grass_MeadowSoft_BaseColor.png')

wall_source = ROOT / 'Little-Castle_Assets/Source/Architecture/Wall_Stone_Modular/v004'
wall_release = ROOT / 'Little-Castle_Assets/Releases/TerrainStarter-v001'
for directory in ('Meshes', 'Textures'):
    for source in (wall_source / directory).iterdir():
        if source.suffix.lower() not in ('.fbx', '.png', '.json'):
            continue
        release = wall_release / directory / source.name
        release.parent.mkdir(parents=True, exist_ok=True)
        if release.exists() and release.read_bytes() != source.read_bytes():
            raise RuntimeError('Refusing to overwrite wall release: ' + str(release))
        if not release.exists(): shutil.copyfile(source, release)
        copy(release, ('Models/' if directory == 'Meshes' else 'Textures/') + source.name)
for name in ('Connections.json',):
    release = wall_release / name
    if not release.exists(): shutil.copyfile(wall_source / name, release)
    copy(release, name)

for family, relative in {
    'Grass_Base_A': 'Ground/Grass_Base_A',
    'DirtPath_A': 'Roads/DirtPath_A_PBR',
    'DirtGround_A': 'Ground/DirtGround_A_PBR',
}.items():
    copy(ART / 'Mat/Material_terrain' / relative / f'{family}_BaseColor.png',
         f'Textures/{family}_BaseColor.png')
    if family != 'DirtGround_A':
        for channel in ('Normal','AO','Height'):
            copy(ART / 'Mat/Material_terrain' / relative / f'{family}_{channel}.png',
                 f'Study/Textures/{family}_{channel}.png')
copy(ART / 'Mat/T_Foliage/T_Foliage_BaseColor.png', 'Textures/T_Foliage_BaseColor.png')
copy(ART / 'Art/Vegetation/Oak_Kit/Textures/T_Oak_Kit_Palette.png', 'Textures/T_Oak_Kit_Palette.png')
for name in ('SM_Stone_Small_A', 'SM_Stone_Medium_A', 'SM_FlowerCluster_A', 'SM_FlowerCluster_B', 'SM_LeafCluster_Small_A'):
    copy(ART / 'Art/Vegetation/Oak_Kit/Exports' / (name + '.fbx'), 'Models/' + name + '.fbx')

(TARGET / 'provenance.json').write_text(json.dumps(dict(status='SCENE_TEST',files=files), indent=2))
(wall_release / 'release.json').write_text(json.dumps(dict(status='SCENE_TEST',
    source=str(wall_source), note='Existing geometry QA; runtime validation reported separately',
    files=[f for f in files if 'Little-Castle_Assets' in f['source']]), indent=2))
print('Imported', len(files), 'files;', sum(f['bytes'] for f in files), 'bytes;', TARGET)
