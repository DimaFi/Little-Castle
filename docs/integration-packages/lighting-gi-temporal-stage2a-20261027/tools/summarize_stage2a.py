from pathlib import Path
import json,statistics as st,xml.etree.ElementTree as ET
import numpy as np
from PIL import Image,ImageDraw,ImageFont

data=Path('E:/Games_Develop/Research/SpatialIndirectStage2A_20261026');old=Path('E:/Games_Develop/Research/SpatialIndirectStage1C_20261026/Guarded01Priority');serial=old.parent/'GuardedSerial'
mapping={'day':'day-light-refresh','restored':'day-light-refresh','sunset':'sunset-light-refresh','night':'night-light-refresh'};runs=[];checks=[]
def load(p):return json.loads(p.read_text('utf-8'))
def check_result(folder):
    assert (folder/'result.txt').read_text('utf-8').startswith('PASS') and not (folder/'runtime-errors.txt').read_text('utf-8').strip()
for round_ in range(1,4):
    fields=[];images=[];pair=[]
    for mode in ('Immediate','Temporal'):
        folder=data/f'Final{round_:02}{mode}';check_result(folder);j=load(folder/'temporal-steps.json');assert j['rapidUndoExact'];assert j['geometryBarrierPassed'] if mode=='Temporal' else True
        run=dict(round=round_,mode=mode,steps=j['steps'],benchmarks=load(folder/'benchmarks.json')['reports']);assert len(run['benchmarks'])==3;runs.append(run);pair.append(run)
    a=data/f'Final{round_:02}Immediate';b=data/f'Final{round_:02}Temporal'
    for name,source in mapping.items():
        arrays=[np.fromfile(p,dtype='<f4') for p in (a/(name+'-probes.bin'),b/(name+'-probes.bin'),old/(source+'-probes.bin'),serial/(source+'-probes.bin'))]
        assert all(v.shape==(21609*16,) and np.isfinite(v).all() for v in arrays)
        err=float(abs(arrays[0]-arrays[1]).max());previous=float(abs(arrays[2]-arrays[1]).max());ref=float(abs(arrays[3]-arrays[1]).max());assert max(err,previous,ref)<1e-5
        fields.append(dict(name=name,maxModeError=err,maxStage1CError=previous,maxSerialError=ref))
    for p in a.glob('*.png'):
        x=np.asarray(Image.open(p).convert('RGB'),dtype=np.int16);y=np.asarray(Image.open(b/p.name).convert('RGB'),dtype=np.int16);assert x.shape==y.shape==(1080,1920,3) and y.std()>2
        images.append(dict(path=p.name,meanAbsoluteByteError=float(abs(x-y).mean()),maxByteError=int(abs(x-y).max())))
    check=dict(pass_=True,round=round_,fields=fields,images=images);checks.append(check);(data/f'Final{round_:02}-comparison.json').write_text(json.dumps(check,indent=2),'utf-8')
def dist(v):return dict(median=st.median(v),minimum=min(v),maximum=max(v))
steps=[]
for i in range(5):
    row=dict(reason=runs[0]['steps'][i]['reason'])
    for mode in ('Immediate','Temporal'):
        entries=[r['steps'][i] for r in runs if r['mode']==mode];row[mode]={k:dist([v[k] for v in entries]) for k in ('wallMs','firstPublicationMs','solveMs','publishMs','uploadedBytes','commitMs','commitUploadMs')};row[mode]['maxSolveFrameMs']=max(v['maxSolveFrameMs'] for v in entries);row[mode]['maxSolvePublishFrameMs']=max(v['maxSolvePublishFrameMs'] for v in entries)
    steps.append(row)
steady=[]
for mode in ('Immediate','Temporal'):
    for i,phase in enumerate(('settled-off','settled-on','history-read-on')):
        entries=[r['benchmarks'][i] for r in runs if r['mode']==mode];steady.append(dict(mode=mode,phase=phase,gpuMeanMs=dist([v['gpu']['meanMs'] for v in entries]),gpuSamples=sum(v['gpu']['samples'] for v in entries),frameMedianMs=dist([v['frame']['medianMs'] for v in entries]),frameP95Ms=dist([v['frame']['p95Ms'] for v in entries]),frameP99Ms=dist([v['frame']['p99Ms'] for v in entries])))
cycles=[];curves=[]
for name in ('FinalCycle1x','FinalCycle8x','FinalCycle8xImmediate'):
    folder=data/name;check_result(folder);j=load(folder/'temporal-steps.json');cycle=j['cycle'];assert cycle and cycle['gameMinutes']>=1440 and cycle['geometryRebuilds']==0
    all_frames=load(folder/'temporal-frames.json')['frames'];capture_times={}
    for f in all_frames:capture_times.setdefault(f['requestedMinutes'],f['time'])
    frames=[v for v in all_frames if v['phase']=='continuous'];assert len(frames)>1000
    t=np.array([v['time'] for v in frames]);color=np.array([[v['color'][k] for k in ('x','y','z')] for v in frames]);delta=np.linalg.norm(np.diff(color,axis=0),axis=1)
    lag=[];age=[];previous=frames[0]['publishedMinutes'];lastVersion=frames[0]['version'];latest=previous
    for f in frames:
        if f['version']!=lastVersion:previous=latest;latest=f['publishedMinutes'];lastVersion=f['version']
        displayed=previous*(1-f['blend'])+f['publishedMinutes']*f['blend'] if j['temporal'] else f['publishedMinutes']
        lag.append(max(0,f['gameMinutes']-displayed))
        target_time=capture_times.get(f['publishedMinutes'],f['time']);old_time=capture_times.get(previous,target_time)
        display_time=old_time*(1-f['blend'])+target_time*f['blend'] if j['temporal'] else target_time
        age.append(max(0,f['time']-display_time))
    assert np.isfinite(color).all();cycles.append(dict(name=name,temporal=j['temporal'],**cycle,frames=len(frames),cycleWallExcludingSettleSeconds=cycle['realSeconds']-j['steps'][-1]['wallMs']/1000,maxClockSnapshotAgeGameMinutes=max(lag),p95ClockSnapshotAgeGameMinutes=float(np.percentile(lag,95)),maxFixedPointColorFrameDelta=float(delta.max()),p99FixedPointColorFrameDelta=float(np.percentile(delta,99)),wholePlayerGCMeasured=False))
    cycles[-1]['maxSnapshotAgeSeconds']=max(age);cycles[-1]['p95SnapshotAgeSeconds']=float(np.percentile(age,95))
    curves.append((name,t-t[0],np.array(lag),color))
tests=[]
for name in ('editmode.xml','playmode.xml'):
    n=ET.parse(data/name).getroot();assert n.attrib['result']=='Passed';tests.append(dict(path=name,**{k:n.attrib.get(k) for k in ('total','passed','failed','skipped')}))
j=dict(pass_=True,pairs=3,fields=12,probesPerField=21609,maxModeError=max(v['maxModeError'] for c in checks for v in c['fields']),maxStage1CError=max(v['maxStage1CError'] for c in checks for v in c['fields']),maxSerialError=max(v['maxSerialError'] for c in checks for v in c['fields']),imagePairs=len(checks[0]['images']),maxImageMeanByteError=max(v['meanAbsoluteByteError'] for v in checks[0]['images']),steps=steps,steady=steady,cycles=cycles,tests=tests,gpuBufferBytes=dict(Immediate=1382976,Temporal=2765952),additionalCPUPublicationPayloadBytes=2765952)
(data/'stage2a-summary.json').write_text(json.dumps(j,indent=2),'utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',24);canvas=Image.new('RGB',(1920,1172),'#1b2027');draw=ImageDraw.Draw(canvas)
for row,name in enumerate(('temporal-under-roof-sunset','temporal-overview-night')):
    for col,mode in enumerate(('Immediate','Temporal')):
        x,y=col*960,row*586;draw.text((x+14,y+8),'Прежняя публикация' if col==0 else 'Согласованная версия + переход',font=font,fill='white');canvas.paste(Image.open(data/f'Final01{mode}'/(name+'.png')).convert('RGB').resize((960,540),Image.Resampling.LANCZOS),(x,y+46))
canvas.save(data/'stage2a-game-comparison.png')
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
fig,axes=plt.subplots(2,1,figsize=(12,7),sharex=True)
for name,t,lag,color in curves:
    if '8x' not in name:continue
    axes[0].plot(t,color[:,0],label=name,linewidth=.9);axes[1].plot(t,lag,label=name,linewidth=.9)
axes[0].set_ylabel('Fixed-point indirect R (linear)');axes[1].set_ylabel('Clock snapshot age (game minutes)');axes[1].set_xlabel('Real seconds, accelerated 8x cycle');axes[0].legend();axes[0].grid(alpha=.2);axes[1].grid(alpha=.2);fig.tight_layout();fig.savefig(data/'stage2a-continuous.png',dpi=150);plt.close(fig)
progress=load(data/'FinalProgress/temporal-frames.json')['frames'];p=[v for v in progress if v['phase']=='sunset'];t=np.array([v['time'] for v in p]);t-=t[0]
fig,axes=plt.subplots(2,1,figsize=(10,5),sharex=True)
for key,col in [('x','red'),('y','green'),('z','blue')]:axes[0].plot(t,[v['color'][key] for v in p],color=col,label=key)
axes[1].plot(t,[v['blend'] for v in p],label='blend');axes[1].plot(t,np.array([v['pending'] for v in p])/21609,label='pending fraction');axes[0].set_ylabel('Fixed-point indirect, linear');axes[1].set_xlabel('Real seconds; diagnostic fade 1 s, captures enabled');axes[1].legend();fig.tight_layout();fig.savefig(data/'stage2a-transition.png',dpi=150);plt.close(fig)
print(json.dumps({k:v for k,v in j.items() if k not in ('steps','steady')},indent=2))
