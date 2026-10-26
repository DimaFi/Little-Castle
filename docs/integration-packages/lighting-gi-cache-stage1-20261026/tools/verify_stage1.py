from pathlib import Path
import argparse
import json
import numpy as np
from PIL import Image

p=argparse.ArgumentParser();p.add_argument('--reference',default='PilotReference');p.add_argument('--cached',default='PilotCached');a=p.parse_args()
root=Path('E:/Games_Develop/Research/SpatialIndirectStage1_20261026');ref=root/a.reference;cached=root/a.cached
failures=[];fields=[];images=[]
for source in sorted(ref.glob('*-probes.bin')):
    before=np.fromfile(source,dtype='<f4');after=np.fromfile(cached/source.name,dtype='<f4')
    assert before.shape==after.shape==(21609*16,)
    delta=np.abs(before-after);maximum=float(delta.max());passed=maximum<.00001 and bool(np.isfinite(after).all())
    if not passed:failures.append(source.name)
    fields.append(dict(path=source.name,probes=21609,maxAbsoluteCoefficientError=maximum,rms=float(np.sqrt(np.mean(delta.astype(np.float64)**2))),pass_=passed))
if len(fields)!=8:failures.append('Expected eight full-field dumps')
for source in sorted(ref.glob('*.png')):
    b=np.asarray(Image.open(source).convert('RGB'),dtype=np.int16);c=np.asarray(Image.open(cached/source.name).convert('RGB'),dtype=np.int16)
    good=b.shape==c.shape==(1080,1920,3) and float(c.std())>2
    if not good:failures.append(source.name)
    d=np.abs(b-c);images.append(dict(path=source.name,meanAbsoluteByteError=float(d.mean()),maxByteError=int(d.max()),valid=good))
for directory in (ref,cached):
    if not (directory/'result.txt').read_text('utf-8').startswith('PASS') or (directory/'runtime-errors.txt').read_text('utf-8').strip():failures.append(str(directory))
before=json.loads((ref/'updates.json').read_text('utf-8'))['reports'];after=json.loads((cached/'updates.json').read_text('utf-8'))['reports']
updates=[]
for b,c in zip(before,after):
    if b['reason']!=c['reason'] or b['solved']!=c['solved'] or b.get('solveBudgetMs')!=c.get('solveBudgetMs'):failures.append('Workload/budget differs: '+b['reason'])
    updates.append(dict(reason=b['reason'],probes=b['solved'],referenceCPUms=b['solveMs'],cachedCPUms=c['solveMs'],cpuSpeedup=b['solveMs']/max(.00001,c['solveMs']),referenceWallMs=b['wallMs'],cachedWallMs=c['wallMs'],cachedGeometryBuilds=c.get('geometryBuilds'),cachedPayloadBytes=c.get('transportPayloadBytes')))
result=dict(pass_=not failures,reference=a.reference,cached=a.cached,coefficientTolerance=.00001,fields=fields,images=images,updates=updates,failures=failures)
destination=root/(a.reference+'-vs-'+a.cached+'.json');destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8')
print(json.dumps(dict(pass_=result['pass_'],fields=len(fields),images=len(images),maxCoefficientError=max(x['maxAbsoluteCoefficientError'] for x in fields),maxImageMeanError=max((x['meanAbsoluteByteError'] for x in images),default=None),updates=updates,failures=failures),ensure_ascii=False,indent=2))
if failures:raise SystemExit(1)
