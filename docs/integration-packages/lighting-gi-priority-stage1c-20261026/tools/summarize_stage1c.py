from pathlib import Path
import argparse,hashlib,json,statistics as stat,xml.etree.ElementTree as ET
import numpy as np
from PIL import Image,ImageDraw,ImageFont

parser=argparse.ArgumentParser();parser.add_argument('--prefix',default='Guarded');args=parser.parse_args();prefix=args.prefix
root=Path('E:/Games_Develop/Research/SpatialIndirectStage1C_20261026');previous=Path('E:/Games_Develop/Research/SpatialIndirectStage1B_20261026/Final01After');serial=root/(prefix+'Serial')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();runs=[];checks=[]
assert (serial/'result.txt').read_text('utf-8').startswith('PASS') and not (serial/'runtime-errors.txt').read_text('utf-8').strip()
for round_ in range(1,4):
    folders=[root/f'{prefix}{round_:02}{m}' for m in ('Fifo','Priority')];fields=[];images=[];pair=[]
    for source in sorted(folders[0].glob('*-probes.bin')):
        arrays=[np.fromfile(p/source.name,dtype='<f4') for p in (*folders,serial)];assert all(a.shape==(21609*16,) and np.isfinite(a).all() for a in arrays)
        error=float(np.abs(arrays[0]-arrays[1]).max());ref=float(np.abs(arrays[2]-arrays[1]).max());assert error<1e-5 and ref<1e-5
        old=None
        if (previous/source.name).exists():old=float(np.abs(np.fromfile(previous/source.name,dtype='<f4')-arrays[1]).max());assert old<1e-5
        fields.append(dict(path=source.name,maxCoefficientError=error,serialError=ref,previousStage1BError=old))
    assert len(fields)==12
    for p in sorted(folders[0].glob('*.png')):
        a=np.asarray(Image.open(p).convert('RGB'),dtype=np.int16);b=np.asarray(Image.open(folders[1]/p.name).convert('RGB'),dtype=np.int16);assert a.shape==b.shape==(1080,1920,3) and b.std()>2
        images.append(dict(path=p.name,meanAbsoluteByteError=float(np.abs(a-b).mean()),maxByteError=int(np.abs(a-b).max())))
    for mode,directory in zip(('Fifo','Priority'),folders):
        assert (directory/'result.txt').read_text('utf-8').startswith('PASS') and not (directory/'runtime-errors.txt').read_text('utf-8').strip()
        run=dict(round=round_,mode=mode,updates=json.loads((directory/'updates.json').read_text('utf-8'))['reports'],benchmarks=json.loads((directory/'benchmarks.json').read_text('utf-8'))['reports']);assert len(run['updates'])==12;runs.append(run);pair.append(run)
    for a,b in zip(pair[0]['updates'],pair[1]['updates']):
        for key in ('reason','solved','solveBudgetMs','probeLimit','geometryBuilds','environmentReplays','transportPayloadBytes','visibleSupportSolved','queuePayloadBytes'):assert a[key]==b[key],(a['reason'],key,a[key],b[key])
        assert a['priorityMode'] is False and b['priorityMode'] is True
        assert a['visibleSupportRenderedMs']>=0 and b['visibleSupportRenderedMs']>=0
    check=dict(pass_=True,round=round_,fields=fields,images=images);checks.append(check);(root/f'{prefix}{round_:02}-comparison.json').write_text(json.dumps(check,indent=2),'utf-8')
def dist(v):return dict(median=stat.median(v),minimum=min(v),maximum=max(v))
rows=[]
for i in range(12):
    row=dict(reason=runs[0]['updates'][i]['reason'],probes=runs[0]['updates'][i]['solved'],visibleSupportProbes=runs[0]['updates'][i]['visibleSupportSolved'])
    for mode in ('Fifo','Priority'):
        entries=[r['updates'][i] for r in runs if r['mode']==mode];row[mode]={key:dist([v[key] for v in entries]) for key in ('wallMs','solveMs','uploadMs','commitMs','priorityClassifyMs','firstChangedSupportRenderedMs','visibleSupportRenderedMs')};row[mode]['maxUpdateMs']=max(v['maxTotalUpdateFrameMs'] for v in entries);row[mode]['geometryBuilds']=entries[0]['geometryBuilds'];row[mode]['environmentReplays']=entries[0]['environmentReplays']
    rows.append(row)
steady=[]
for mode in ('Fifo','Priority'):
    for enabled in (False,True):
        entries=[b for r in runs if r['mode']==mode for b in r['benchmarks'] if b['indirect']==enabled]
        steady.append(dict(mode=mode,indirect=enabled,gpuMeanMs=dist([v['gpu']['meanMs'] for v in entries]),samples=sum(v['gpu']['samples'] for v in entries),frameMedianMs=dist([v['frame']['medianMs'] for v in entries]),frameP95Ms=dist([v['frame']['p95Ms'] for v in entries]),frameP99Ms=dist([v['frame']['p99Ms'] for v in entries])))
tests=[]
for name in ('editmode-final.xml','playmode-final.xml'):
    n=ET.parse(root/name).getroot();assert n.attrib['result']=='Passed';tests.append(dict(path=name,**{k:n.attrib.get(k) for k in ('result','total','passed','failed','skipped')}))
j=dict(pass_=True,pairedReleaseRuns=3,fullFieldComparisons=36,serialComparisons=36,previousStageComparisons=24,maxCoefficientError=max(v['maxCoefficientError'] for c in checks for v in c['fields']),maxSerialError=max(v['serialError'] for c in checks for v in c['fields']),maxPreviousStageError=max(v['previousStage1BError'] for c in checks for v in c['fields'] if v['previousStage1BError'] is not None),imagePairs=len(checks[0]['images']),maxImageMeanByteError=max(v['meanAbsoluteByteError'] for v in checks[0]['images']),updates=rows,steady=steady,tests=tests,transportPayloadBytes=max(v['transportPayloadBytes'] for r in runs for v in r['updates']),queuePayloadBytes=runs[0]['updates'][0]['queuePayloadBytes'],playerFiles=[dict(path=p.relative_to(root/'Player').as_posix(),sha256=sha(p),bytes=p.stat().st_size) for p in (root/'Player').rglob('*') if p.is_file()])
j['runPrefix']=prefix
motion=json.loads((root/(prefix+'Motion')/'motion-checks.json').read_text('utf-8'));assert motion['pass'] and motion['exactField'];j['movingCameraCheck']=motion
(root/'stage1c-summary.json').write_text(json.dumps(j,indent=2),'utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',24);canvas=Image.new('RGB',(1920,1172),'#1b2027');draw=ImageDraw.Draw(canvas)
for row,view in enumerate(('gi-overview-day','gi-small-cube-moved')):
    for col,mode in enumerate(('Fifo','Priority')):
        x,y=col*960,row*586;draw.text((x+14,y+8),'FIFO' if col==0 else 'Приоритет проб в кадре',font=font,fill='white');canvas.paste(Image.open(root/f'{prefix}01{mode}'/(view+'.png')).convert('RGB').resize((960,540),Image.Resampling.LANCZOS),(x,y+46))
canvas.save(root/'stage1c-game-comparison.png')
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
chosen=[rows[i] for i in (0,3,4,9,10)];y=np.arange(len(chosen));fig,axes=plt.subplots(1,2,figsize=(13,5.2))
for ax,metric,title in zip(axes,('visibleSupportRenderedMs','wallMs'),('Frustum-support convergence, rendered (ms)','Full-field convergence (ms)')):
    for offset,mode,color in ((-.16,'Fifo','#7d8793'),(.16,'Priority','#248fba')):
        v=[r[mode][metric]['median'] for r in chosen];lo=[r[mode][metric]['minimum'] for r in chosen];hi=[r[mode][metric]['maximum'] for r in chosen];ax.errorbar(v,y+offset,xerr=[np.array(v)-lo,np.array(hi)-v],fmt='o',capsize=4,color=color,label=mode)
    ax.set_yticks(y,['Initial','Roof removal','Roof undo','Small cube move','Small cube undo']);ax.invert_yaxis();ax.set_title(title);ax.grid(axis='x',alpha=.25)
axes[1].legend();fig.suptitle('Stage 1C: median and min/max of 3 pairs; same Player, 2 ms incl. classification');fig.tight_layout();fig.savefig(root/'stage1c-latency.png',dpi=160);plt.close(fig)
print(json.dumps({k:v for k,v in j.items() if k not in ('updates','steady','playerFiles')},indent=2))
for row in rows:print(row['reason'],'visible',round(row['Fifo']['visibleSupportRenderedMs']['median']),round(row['Priority']['visibleSupportRenderedMs']['median']),'full',round(row['Fifo']['wallMs']['median']),round(row['Priority']['wallMs']['median']))
