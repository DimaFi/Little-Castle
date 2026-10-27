from pathlib import Path
import hashlib,json,statistics as st,xml.etree.ElementTree as ET
from PIL import Image,ImageDraw,ImageFont

root=Path('E:/Games_Develop/Research/LightingGameViews_20261026')
load=lambda p:json.loads(p.read_text('utf-8'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
runs=[];conditions=None;rows=[]
for round_,prefix in ((1,'Final'),(2,'Verified'),(3,'Verified')):
    for mode in ('Base','GroundFog'):
        folder=root/f'{prefix}{round_:02}{mode}'
        assert (folder/'result.txt').read_text('utf-8').startswith('PASS'),folder
        assert not (folder/'runtime-errors.txt').read_text('utf-8').strip(),folder
        j=load(folder/'game-views.json');bench=load(folder/'benchmarks.json')['reports']
        assert len(j['views'])==15 and len(bench)==30 and len(j['motion'])==20
        assert j['groundRelativeFog']==(mode=='GroundFog') and j['grassMaximumDistance']==128
        for view in j['views']:
            assert not view['grass']['forceFullDetail'] and abs(view['distance']-view['requestedDistance'])<.01
            assert view['shadowDistance']==90 and view['fov']==50
            offset=max(0,view['distance']-35) if mode=='GroundFog' else 0
            # These are runtime snapshots; the atmosphere asset remains unchanged.
            assert view['fogEnd']>view['fogStart']>=0
            for gi in (False,True):
                b=bench[view['benchmarkOffset']+int(gi)]
                assert b['indirect']==gi and b['frame']['samples']==600 and b['gpu']['samples']>0
                assert b['width']==1920 and b['height']==1080 and b['colorSpace']=='Linear' and not b['development']
                c={k:b[k] for k in ('device','processor','unity','colorSpace','api','width','height','development','pipeline')}
                if conditions is None:conditions=c
                assert c==conditions
                rows.append(dict(run=folder.name,mode=mode,hour=view['hour'],distance=view['requestedDistance'],indirect=gi,frame=b['frame'],gpu=b['gpu'],allocatedBytes=b['allocatedBytes']))
        runs.append(dict(name=folder.name,views=j['views'],motion=j['motion']))

for run in runs:
    for view,base in zip(run['views'],runs[0]['views']):
        offset=max(0,view['distance']-35) if 'GroundFog' in run['name'] else 0
        assert abs(view['fogStart']-base['fogStart']-offset)<.02 and abs(view['fogEnd']-base['fogEnd']-offset)<.02
        # Ground-following smoothing/culling can change one patch near a threshold.
        # Preserve the raw native counts rather than manufacturing identical LODs.

def dist(values):return dict(median=st.median(values),minimum=min(values),maximum=max(values))
summary=[]
for mode in ('Base','GroundFog'):
    for hour in (12,18.5,1):
        for distance in (11,35,75,115,190):
            for gi in (False,True):
                group=[v for v in rows if (v['mode'],v['hour'],v['distance'],v['indirect'])==(mode,hour,distance,gi)]
                assert len(group)==3
                summary.append(dict(mode=mode,hour=hour,distance=distance,indirect=gi,gpuMeanMs=dist([v['gpu']['meanMs'] for v in group]),gpuSamples=sum(v['gpu']['samples'] for v in group),frameMedianMs=dist([v['frame']['medianMs'] for v in group]),frameP95Ms=dist([v['frame']['p95Ms'] for v in group]),frameP99Ms=dist([v['frame']['p99Ms'] for v in group])))
tests=[]
for name in ('editmode-final.xml','playmode-final.xml'):
    j=ET.parse(root/name).getroot();assert j.attrib['result']=='Passed';tests.append(dict(path=name,**{k:j.attrib[k] for k in ('total','passed','failed')}))
for folder in ('Final01Base','Final01GroundFog'):
    for name in [v['name']+'-'+gi+'.png' for v in runs[0]['views'] for gi in ('off','on')]+['game-oak-close.png']+[f'game-motion-{i:03}.png' for i in range(0,120,6)]:
        p=root/folder/name;im=Image.open(p);assert im.size==(1920,1080) and max(im.convert('RGB').getextrema()[0])-min(im.convert('RGB').getextrema()[0])>3

result=dict(pass_=True,pairs=3,viewsPerRun=15,benchmarks=180,ordinaryFrames=108000,warmupFramesPerMeasurement=120,conditions=conditions,tests=tests,summary=summary,runs=runs,limitations=['Finite sparse fixture, not a settlement/map workload','No moving characters or world streaming','Oak expectedLod is an estimate; visibility indicators include shadows/crossfade','GI field fixed49x9x49 with flat proxy ground','Optional fog changes only test runtime; no main-project activation','Final02/03 incomplete runs and initial Final01 pilot excluded'])
(root/'game-views-summary.json').write_text(json.dumps(result,indent=2),'utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',23)
def sheet(name,entries):
    canvas=Image.new('RGB',(1920,586*((len(entries)+1)//2)),'#1b2027');d=ImageDraw.Draw(canvas)
    for i,(folder,file,label) in enumerate(entries):
        x=(i%2)*960;y=(i//2)*586;d.text((x+12,y+9),label,font=font,fill='white');canvas.paste(Image.open(root/folder/file).convert('RGB').resize((960,540),Image.Resampling.LANCZOS),(x,y+46))
    canvas.save(root/name)
sheet('strategy-camera-day-night.png',[(f'Final01GroundFog',f'game-{hour:.1f}-d{distance}-on.png',f'{"День" if hour==12 else "Ночь"}; дистанция {distance} м; GI on') for distance in (11,75,190) for hour in (12,1)])
sheet('strategy-camera-fog-ab.png',[(f'Final01{mode}',f'game-12.0-d{distance}-on.png',f'{"Исходный туман" if mode=="Base" else "Туман от наземного фокуса (тест)"}; {distance} м') for distance in (75,190) for mode in ('Base','GroundFog')])
sheet('strategy-camera-gi-ab.png',[(f'Final01GroundFog',f'game-12.0-d{distance}-{gi}.png',f'{distance} м; GI {gi}; одинаковая камера') for distance in (35,75) for gi in ('off','on')])
sheet('strategy-camera-oak-and-motion.png',[(f'Final01GroundFog',file,label) for file,label in [('game-oak-close.png','Дуб вблизи; родной LODGroup'),('game-motion-000.png','Маршрут камеры: начало'),('game-motion-060.png','Маршрут камеры: середина'),('game-motion-114.png','Маршрут камеры: отдаление')]])
print(json.dumps(dict(pass_=True,pairs=3,benchmarks=len(rows),tests=tests),indent=2))
