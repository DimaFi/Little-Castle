"""Function-scoped Meadow merge on MAIN lighting, with reproducible input hashes."""
from pathlib import Path
import hashlib,json,difflib,shutil
ROOT=Path(__file__).resolve().parents[2]
MAIN=Path('E:/Games_Develop/Little-Castle')
SOURCE=Path('E:/Games_Develop/Little-Castle_URP_20261024')
OUT=ROOT/'docs/lighting-integration'
FROZEN=OUT/'source-shaders'
files=['Foliage/LC_Grass.shader','Foliage/LC_GrassURP.hlsl','Foliage/LC_VegetationURPCommon.hlsl','Foliage/LC_Foliage.shader','Foliage/LC_FoliageURP.hlsl','Surface/LC_StylizedLit.shader','Surface/LC_SurfacePaint.hlsl','Terrain/LC_Terrain.shader']
manifest=[]
for rel in files:
    src=SOURCE/'Assets/_Game/Shaders'/rel
    frozen=FROZEN/rel
    if not frozen.exists():frozen.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,frozen)
    manifest.append({'path':'Assets/_Game/Shaders/'+rel,'sourceSHA256':hashlib.sha256(frozen.read_bytes()).hexdigest(),'mainSHA256':hashlib.sha256((MAIN/'Assets/_Game/Shaders'/rel).read_bytes()).hexdigest() if (MAIN/'Assets/_Game/Shaders'/rel).exists() else None})

def read(root,rel):return (root/rel).read_text('utf-8-sig')
def replace_once(s,a,b):
    assert s.count(a)==1,(a,s.count(a))
    return s.replace(a,b,1)

for rel in ['Foliage/LC_Grass.shader','Foliage/LC_GrassURP.hlsl']:
    base=read(MAIN/'Assets/_Game/Shaders',rel).splitlines(True)
    meadow=read(FROZEN,rel).splitlines(True)
    merged=[]
    for tag,a,b,c,d in difflib.SequenceMatcher(None,base,meadow,autojunk=False).get_opcodes():
        if tag=='equal':merged.extend(base[a:b]);continue
        deleted=base[a:b]
        # Lighting-only differences are retained, not replaced with the older branch.
        protected=[line for line in deleted if '_SCREEN_SPACE_OCCLUSION' in line or 'LCIndirectOcclusion' in line]
        merged.extend(protected);merged.extend(meadow[c:d])
    text=''.join(merged)
    if rel.endswith('.hlsl'):
        text=replace_once(text,'    half3 ambient =','    half materialOcclusion = 1.0h;\n#if defined(_LC_MEADOW)\n    materialOcclusion = 1-_MeadowRootOcclusion*(1-smoothstep(0,.65h,i.bladeData.x));\n#endif\n    half3 ambient =')
        text=replace_once(text,'LCIndirectOcclusion(i.pos, 1.0h)','LCIndirectOcclusion(i.pos, materialOcclusion)')
        text=replace_once(text,'#if defined(_LC_MEADOW)\n    ambient *= 1-_MeadowRootOcclusion*(1-smoothstep(0,.65h,i.bladeData.x));\n#endif','// Material root AO and screen AO were combined once above (minimum).')
    (ROOT/'Assets/_Game/Shaders'/rel).write_text(text,'utf-8')

rel='Terrain/LC_Terrain.shader'
s=read(MAIN/'Assets/_Game/Shaders',rel)
controls='        _GrassNormalScale ("Meadow Normal Multiplier", Range(0,1)) = .45\n        _SurfaceAOStrength ("Surface AO Multiplier", Range(0,1)) = .35\n        _DetailFadeStart ("Surface Detail Fade Start", Float) = 28\n        _DetailFadeEnd ("Surface Detail Fade End", Float) = 65\n'
s=replace_once(s,'        _LightResponse (',controls+'        _LightResponse (')
s=replace_once(s,'        _PathMaskStrength (','        [Toggle(_LC_SURFACE_PAINT)] _UseSurfacePaint ("Authored / Player Road Mask", Float) = 0\n        _SurfacePaint ("Derived Road Coverage", 2D) = "black" {}\n        _SurfacePaintBounds ("Road Mask Origin XZ / Inverse Size", Vector) = (0,0,.01,.01)\n        _PathMaskStrength (')
s=replace_once(s,'        #include "../Surface/LC_GroundPalette.cginc"','        #include "../Surface/LC_GroundPalette.cginc"\n        #include "../Surface/LC_SurfacePaint.hlsl"')
s=replace_once(s,'            float _LightResponse;','            float _GrassNormalScale;\n            float _SurfaceAOStrength;\n            float _DetailFadeStart;\n            float _DetailFadeEnd;\n            float _LightResponse;')
s=replace_once(s,'            float _UseVertexMasks;','            float _UseVertexMasks;\n            float4 _SurfacePaintBounds;')
s=replace_once(s,'smoothstep(28,65,distance(i.worldPosition,_WorldSpaceCameraPos))','smoothstep(_DetailFadeStart,max(_DetailFadeStart+.01,_DetailFadeEnd),distance(i.worldPosition,_WorldSpaceCameraPos))')
s=replace_once(s,'            pathMask = lerp(pathMask,','''#if defined(_LC_SURFACE_PAINT)
            float2 paintUV=(i.worldPosition.xz-_SurfacePaintBounds.xy)*_SurfacePaintBounds.zw;
            if(all(paintUV>=0)&&all(paintUV<=1))pathMask=LCSurfaceRoad(i.worldPosition,_SurfacePaintBounds);
#endif
            pathMask = lerp(pathMask,''')
s=replace_once(s,'surfaceDetail*lerp(.45h,1,dirtWeight)','surfaceDetail*lerp(_GrassNormalScale,1,dirtWeight)')
s=replace_once(s,'surfaceDetail*.35h','surfaceDetail*_SurfaceAOStrength')
s=replace_once(s,'            #pragma fragment frag\n','            #pragma fragment frag\n            #pragma shader_feature_local _ _LC_SURFACE_PAINT\n')
(ROOT/'Assets/_Game/Shaders'/rel).write_text(s,'utf-8')
for rel in ['Surface/LC_SurfacePaint.hlsl']:
    (ROOT/'Assets/_Game/Shaders'/rel).write_bytes((FROZEN/rel).read_bytes())
for item in manifest:
    p=ROOT/item['path'];item['mergedSHA256']=hashlib.sha256(p.read_bytes()).hexdigest()
(OUT/'shader-merge-manifest.json').write_text(json.dumps(manifest,indent=2),'utf-8')
print('Merged Meadow properties, shared deformation and passes; retained MAIN Linear/SSAO. Terrain snow/cliff changes excluded.')
