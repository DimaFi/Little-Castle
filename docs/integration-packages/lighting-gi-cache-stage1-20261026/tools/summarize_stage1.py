from pathlib import Path
import json
import statistics as stat
import xml.etree.ElementTree as ET
import numpy as np
from PIL import Image,ImageDraw,ImageFont

root=Path('E:/Games_Develop/Research/SpatialIndirectStage1_20261026')
checks=[];runs=[]
for i in range(1,4):
    check=json.loads((root/f'Final{i:02}Reference-vs-Final{i:02}Cached.json').read_text('utf-8'))
    assert check['pass_'];checks.append(check)
    for mode in ('Reference','Cached'):
        folder=root/f'Final{i:02}{mode}'
        updates=json.loads((folder/'updates.json').read_text('utf-8'))['reports']
        bench=json.loads((folder/'benchmarks.json').read_text('utf-8'))['reports']
        runs.append(dict(round=i,mode=mode,updates=updates,benchmarks=bench))
def dist(values):return dict(median=stat.median(values),minimum=min(values),maximum=max(values))
rows=[]
for index in range(8):
    a=[x['updates'][index] for x in runs if x['mode']=='Reference'];b=[x['updates'][index] for x in runs if x['mode']=='Cached']
    rows.append(dict(reason=a[0]['reason'],probes=a[0]['solved'],referenceCPUms=dist([v['solveMs'] for v in a]),cachedCPUms=dist([v['solveMs'] for v in b]),referenceWallMs=dist([v['wallMs'] for v in a]),cachedWallMs=dist([v['wallMs'] for v in b]),cachedCommitMs=dist([v['commitMs'] for v in b]),cachedMaxUpdateMs=max(v['maxTotalUpdateFrameMs'] for v in b),geometryBuilds=b[0]['geometryBuilds'],environmentReplays=b[0]['environmentReplays']))
gpu=[]
for mode in ('Reference','Cached'):
    for enabled in (False,True):
        values=[v for x in runs if x['mode']==mode for v in x['benchmarks'] if v['indirect']==enabled]
        gpu.append(dict(mode=mode,indirect=enabled,gpuMeanMs=dist([x['gpu']['meanMs'] for x in values]),gpuSamples=sum(x['gpu']['samples'] for x in values),frameMedianMs=dist([x['frame']['medianMs'] for x in values]),frameP95Ms=dist([x['frame']['p95Ms'] for x in values]),frameP99Ms=dist([x['frame']['p99Ms'] for x in values])))
tests=[]
for name in ('editmode-final.xml','playmode-final.xml'):
    n=ET.parse(root/name).getroot();assert n.attrib['result']=='Passed'
    tests.append(dict(path=name,**{k:n.attrib.get(k) for k in ('result','total','passed','failed','skipped')}))
summary=dict(pass_=True,pairedRuns=3,fullFieldComparisons=24,probesPerField=21609,maxCoefficientError=max(v['maxAbsoluteCoefficientError'] for c in checks for v in c['fields']),imageComparisons=len(checks[0]['images']),maxImageMeanByteError=max(v['meanAbsoluteByteError'] for v in checks[0]['images']),updates=rows,steady=gpu,tests=tests,
             maximumCachedPayloadBytes=max(v['transportPayloadBytes'] for x in runs if x['mode']=='Cached' for v in x['updates']),
             releaseAllocationCounterSupported=all(v['allocationCounterSupported'] for x in runs for v in x['updates']))
diag=root/'AllocationFinal';assert (diag/'result.txt').read_text('utf-8').startswith('PASS') and not (diag/'runtime-errors.txt').read_text('utf-8').strip()
allocation=json.loads((diag/'updates.json').read_text('utf-8'))['reports'];assert all(v['allocationRecorderSupported'] for v in allocation)
assert all(v['solverAllocationCalls']==0 for v in allocation if v['reason'] not in ('initial-field','roof-removal'))
summary['allocationDiagnostic']=[dict(reason=v['reason'],recorderSupported=v['allocationRecorderSupported'],solverAllocationCalls=v['solverAllocationCalls'],commandAllocationCalls=v['commandAllocationCalls'],setupAllocationCalls=v['setupAllocationCalls']) for v in allocation]
summary['managedHeapPhaseBoundaries']={mode:dist([v['managedUsedBytes'] for x in runs if x['mode']==mode and x['round']>1 for v in x['updates']]) for mode in ('Reference','Cached')}
summary['osMemorySamples']=json.loads((root/'os-memory-samples-final.json').read_text('utf-8-sig'))
(root/'stage1-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),'utf-8')

font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25)
canvas=Image.new('RGB',(1920,1172),'#1b2027');draw=ImageDraw.Draw(canvas)
for row,view in enumerate(('gi-overview-day','gi-under-roof-day')):
    for col,mode in enumerate(('Reference','Cached')):
        x,y=col*960,row*586;draw.text((x+14,y+8),'Исходный расчёт' if col==0 else 'Кеш переноса',font=font,fill='white')
        canvas.paste(Image.open(root/f'Final01{mode}'/(view+'.png')).convert('RGB').resize((960,540),Image.Resampling.LANCZOS),(x,y+46))
canvas.save(root/'stage1-game-comparison.png')
try:
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    chosen=[rows[i] for i in (0,1,3,4,5)]
    labels=['Initial field','Reflector albedo','Roof removal','Roof undo','Sunset refresh'];y=np.arange(len(chosen));fig,axes=plt.subplots(1,2,figsize=(13,5.2))
    for ax,metric,title in zip(axes,('WallMs','CPUms'),('Convergence at 60 Hz (ms)','Total CPU solve (ms)')):
        for offset,kind,color,label in ((-.16,'reference','#7d8793','Reference'),(.16,'cached','#248fba','Cached transport')):
            values=[v[kind+metric]['median'] for v in chosen];lo=[v[kind+metric]['minimum'] for v in chosen];hi=[v[kind+metric]['maximum'] for v in chosen]
            ax.errorbar(values,y+offset,xerr=[np.array(values)-lo,np.array(hi)-values],fmt='o',capsize=4,color=color,label=label)
        ax.set_xscale('log');ax.set_yticks(y,labels);ax.invert_yaxis();ax.set_title(title);ax.grid(axis='x',alpha=.25)
    axes[1].legend();fig.suptitle('Stage 1: median and min/max of 3 paired Release runs; solve budget 2 ms/frame')
    fig.tight_layout();fig.savefig(root/'stage1-performance.png',dpi=160);plt.close(fig)
except ImportError:
    pass
print(json.dumps(dict(pairs=3,fullFieldComparisons=24,maxCoefficientError=summary['maxCoefficientError'],maximumCachedPayloadBytes=summary['maximumCachedPayloadBytes'],updates=[dict(reason=v['reason'],referenceSeconds=round(v['referenceWallMs']['median']/1000,3),cachedSeconds=round(v['cachedWallMs']['median']/1000,3)) for v in rows]),ensure_ascii=False,indent=2))
