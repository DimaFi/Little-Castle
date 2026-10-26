from pathlib import Path
import hashlib,json,statistics as stat,xml.etree.ElementTree as ET
import numpy as np
from PIL import Image,ImageDraw,ImageFont

root=Path('E:/Games_Develop/Research/SpatialIndirectStage1B_20261026')
reference=Path('E:/Games_Develop/Research/SpatialIndirectStage1_20261026/Final01Reference')
checks=[];runs=[];failures=[]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for round_ in range(1,4):
    before=root/f'Final{round_:02}Before';after=root/f'Final{round_:02}After';fields=[];images=[]
    for source in sorted(before.glob('*-probes.bin')):
        arrays=[np.fromfile(p/source.name,dtype='<f4') for p in (before,after,reference)]
        assert all(a.shape==(21609*16,) and np.isfinite(a).all() for a in arrays)
        maximum=float(np.abs(arrays[0]-arrays[1]).max());serial=float(np.abs(arrays[2]-arrays[1]).max())
        good=maximum<.00001 and serial<.00001
        if not good:failures.append(source.name)
        fields.append(dict(path=source.name,maxAbsoluteCoefficientError=maximum,serialReferenceError=serial,pass_=good))
    assert len(fields)==8
    for p in sorted(before.glob('*.png')):
        a=np.asarray(Image.open(p).convert('RGB'),dtype=np.int16);b=np.asarray(Image.open(after/p.name).convert('RGB'),dtype=np.int16)
        good=a.shape==b.shape==(1080,1920,3) and b.std()>2
        if not good:failures.append(p.name)
        images.append(dict(path=p.name,valid=bool(good),meanAbsoluteByteError=float(np.abs(a-b).mean()),maxByteError=int(np.abs(a-b).max())))
    pair=[]
    for mode,directory in (('Before',before),('After',after)):
        assert (directory/'result.txt').read_text('utf-8').startswith('PASS')
        assert not (directory/'runtime-errors.txt').read_text('utf-8').strip()
        run=dict(round=round_,mode=mode,updates=json.loads((directory/'updates.json').read_text('utf-8'))['reports'],benchmarks=json.loads((directory/'benchmarks.json').read_text('utf-8'))['reports'])
        runs.append(run);pair.append(run)
    for a,b in zip(pair[0]['updates'],pair[1]['updates']):
        for key in ('reason','solved','solveBudgetMs','probeLimit','geometryBuilds','environmentReplays','transportPayloadBytes'):
            if a.get(key)!=b.get(key):failures.append('Different workload/budget/payload: '+a['reason']+' '+key)
    check=dict(round=round_,fields=fields,images=images,pass_=not failures)
    checks.append(check);(root/f'Final{round_:02}-comparison.json').write_text(json.dumps(check,indent=2),'utf-8')
assert not failures,failures
def dist(a):return dict(median=stat.median(a),minimum=min(a),maximum=max(a))
rows=[]
for i in range(8):
    row=dict(reason=runs[0]['updates'][i]['reason'],probes=runs[0]['updates'][i]['solved'])
    for mode in ('Before','After'):
        entries=[r['updates'][i] for r in runs if r['mode']==mode]
        row[mode]=dict(cpuMs=dist([v['solveMs'] for v in entries]),wallMs=dist([v['wallMs'] for v in entries]),commitMs=dist([v['commitMs'] for v in entries]),maxUpdateMs=max(v['maxTotalUpdateFrameMs'] for v in entries),geometryBuilds=entries[0]['geometryBuilds'],environmentReplays=entries[0]['environmentReplays'])
    rows.append(row)
gpu=[]
for mode in ('Before','After'):
    for enabled in (False,True):
        entries=[b for r in runs if r['mode']==mode for b in r['benchmarks'] if b['indirect']==enabled]
        gpu.append(dict(mode=mode,indirect=enabled,gpuMeanMs=dist([v['gpu']['meanMs'] for v in entries]),samples=sum(v['gpu']['samples'] for v in entries),frameMedianMs=dist([v['frame']['medianMs'] for v in entries]),frameP95Ms=dist([v['frame']['p95Ms'] for v in entries]),frameP99Ms=dist([v['frame']['p99Ms'] for v in entries])))
tests=[]
for name in ('editmode.xml','playmode.xml'):
    n=ET.parse(root/name).getroot();assert n.attrib['result']=='Passed'
    tests.append(dict(path=name,**{k:n.attrib.get(k) for k in ('result','total','passed','failed','skipped')}))
summary=dict(pass_=True,pairedReleaseRuns=3,fullFieldComparisons=24,serialReferenceComparisons=24,probesPerField=21609,maxCoefficientError=max(v['maxAbsoluteCoefficientError'] for c in checks for v in c['fields']),maxSerialReferenceError=max(v['serialReferenceError'] for c in checks for v in c['fields']),imagePairs=len(checks[0]['images']),maxImageMeanByteError=max(v['meanAbsoluteByteError'] for v in checks[0]['images']),tests=tests,updates=rows,steady=gpu,transportPayloadBytes=max(v['transportPayloadBytes'] for r in runs for v in r['updates']),players=[])
for mode,path in [('Before',Path('E:/Games_Develop/Research/SpatialIndirectStage1_20261026/Player')),('After',root/'Player')]:
    summary['players'].append(dict(mode=mode,files=[dict(path=p.relative_to(path).as_posix(),sha256=sha(p),bytes=p.stat().st_size) for p in path.rglob('*') if p.is_file()]))
workspace=Path(__file__).resolve().parents[2]
old=json.loads((workspace/'publication-gi-stage1/evidence-index.json').read_text('utf-8'))
expected={v['path'][len('Player/'):]:v['sha256'] for v in old['playerFiles'] if v['path'].startswith('Player/')}
actual={v['path']:v['sha256'] for v in summary['players'][0]['files']}
assert actual==expected,'Before Player must match all published Stage1 evidence hashes'
summary['frozenBeforePlayerMatchesPublishedHashes']=True
(root/'stage1b-summary.json').write_text(json.dumps(summary,indent=2),'utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',24);canvas=Image.new('RGB',(1920,1172),'#1b2027');draw=ImageDraw.Draw(canvas)
for row,view in enumerate(('gi-overview-day','gi-under-roof-day')):
    for col,mode in enumerate(('Before','After')):
        x,y=col*960,row*586;draw.text((x+14,y+8),'Опубликованный GI' if col==0 else 'Ускоренная трассировка',font=font,fill='white');canvas.paste(Image.open(root/f'Final01{mode}'/(view+'.png')).convert('RGB').resize((960,540),Image.Resampling.LANCZOS),(x,y+46))
canvas.save(root/'stage1b-game-comparison.png')
canvas=Image.new('RGB',(1920,406),'#1b2027');draw=ImageDraw.Draw(canvas)
for col,(view,label) in enumerate((('day','День'),('sunset','Закат'),('night','Ночь'))):
    draw.text((col*640+14,8),label,font=font,fill='white');canvas.paste(Image.open(root/'Final01After'/('gi-overview-'+view+'.png')).convert('RGB').resize((640,360),Image.Resampling.LANCZOS),(col*640,46))
canvas.save(root/'stage1b-time-of-day.png')
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
chosen=[rows[i] for i in (0,3,4,5,6)];y=np.arange(len(chosen));fig,axes=plt.subplots(1,2,figsize=(13,5.2))
for ax,metric,title in zip(axes,('wallMs','cpuMs'),('Convergence at 60 Hz (ms)','Total CPU solve (ms)')):
    for offset,mode,color in ((-.16,'Before','#7d8793'),(.16,'After','#248fba')):
        v=[r[mode][metric]['median'] for r in chosen];lo=[r[mode][metric]['minimum'] for r in chosen];hi=[r[mode][metric]['maximum'] for r in chosen]
        ax.errorbar(v,y+offset,xerr=[np.array(v)-lo,np.array(hi)-v],fmt='o',capsize=4,color=color,label=mode)
    ax.set_yticks(y,['Initial field','Roof removal','Roof undo','Sunset','Night']);ax.invert_yaxis();ax.set_title(title);ax.grid(axis='x',alpha=.25)
axes[1].legend();fig.suptitle('Stage 1B: median and min/max of 3 paired Release runs; solve budget 2 ms/frame');fig.tight_layout();fig.savefig(root/'stage1b-performance.png',dpi=160);plt.close(fig)
print(json.dumps({k:v for k,v in summary.items() if k not in ('players','steady')},indent=2))
