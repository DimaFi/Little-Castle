#ifndef LC_PATH_MATERIALS_INCLUDED
#define LC_PATH_MATERIALS_INCLUDED
// Shared repeat/clamp samplers keep the terrain below the hardware sampler limit.
TEXTURE2D_ARRAY(_LC_PathAlbedo);
TEXTURE2D_ARRAY(_LC_PathNormal);
TEXTURE2D_ARRAY(_LC_PathSurface);
TEXTURE2D_ARRAY(_LC_PathWeights);
float _LC_PathCount;
float4 _LC_PathBounds;
float4 _LC_PathSettings[32]; // world tiling, normal strength, height blend, snow retention
float4 _LC_PathTints[32]; // tint RGB, AO strength

void LCPathMaterials(float3 worldPosition,inout half3 color,out half3 detailNormal,
    out half ao,out half edgeHeight,out half materialAmount,out half snowRetention)
{
    detailNormal=half3(0,0,1);ao=1;edgeHeight=.5h;materialAmount=0;snowRetention=0;
    float2 control=(worldPosition.xz-_LC_PathBounds.xy)*_LC_PathBounds.zw;
    [branch] if(_LC_PathCount<2||any(control<0)||any(control>1))return;
    half3 colors=0,normals=0;half ambient=0,heights=0,snow=0,total=0,legacy=0;
    int count=min(32,(int)_LC_PathCount);
    [loop] for(int group=0;group<(count+3)/4;group++)
    {
        half4 weights=SAMPLE_TEXTURE2D_ARRAY_LOD(_LC_PathWeights,sampler_LinearClamp,control,group,0);
        [unroll] for(int channel=0;channel<4;channel++)
        {
            int layer=group*4+channel;if(layer>=count)break;
            half weight=weights[channel];
            if(layer==0){legacy=weight;continue;}
            [branch] if(weight>.001h)
            {
                float4 settings=_LC_PathSettings[layer];float2 uv=worldPosition.xz*settings.x;
                half3 properties=SAMPLE_TEXTURE2D_ARRAY(_LC_PathSurface,sampler_GrassTex,uv,layer).rgb;
                // Height affects only mixed pixels. Solid coverage keeps the complete original texture.
                weight*=exp2((properties.g-.5h)*settings.z*3*(1-weight));
                half3 albedo=SAMPLE_TEXTURE2D_ARRAY(_LC_PathAlbedo,sampler_GrassTex,uv,layer).rgb;
                half3 n=SAMPLE_TEXTURE2D_ARRAY(_LC_PathNormal,sampler_GrassTex,uv,layer).xyz*2-1;
                n.xy*=settings.y;n.z=max(.1h,n.z);
                colors+=albedo*_LC_PathTints[layer].rgb*weight;normals+=normalize(n)*weight;
                ambient+=lerp(1,properties.r,_LC_PathTints[layer].a)*weight;
                heights+=properties.g*weight;snow+=settings.w*weight;total+=weight;
            }
        }
    }
    [branch] if(total>.001h)
    {
        materialAmount=total/max(.001h,total+legacy);
        color=lerp(color,colors/total,materialAmount);
        detailNormal=normalize(normals);ao=ambient/total;edgeHeight=heights/total;
        snowRetention=snow/total*materialAmount;
    }
}
#endif
