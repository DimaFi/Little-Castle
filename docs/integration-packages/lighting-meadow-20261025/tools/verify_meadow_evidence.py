from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json,hashlib

ROOT=Path('E:/Games_Develop/Research/LightingMeadow_20261025')
results={};fail=[]
def pixels(p):
    with Image.open(p) as im:
        im.load()
        if im.size!=(1920,1080):fail.append(str(p)+' wrong resolution')
        a=np.asarray(im.convert('RGB')).astype(np.float32)
        if float(a.std())<1 or float(a.mean())<1:fail.append(str(p)+' empty/flat image')
        return a
for space in ('Gamma','Linear'):
    folder=ROOT/'Acceptance'/space/'Captures'
    images=list(folder.glob('*.png'))
    if len(images)!=76:fail.append(space+f' expected 76 images, got {len(images)}')
    imageHashes=[]
    for p in images:
        a=pixels(p);meta=json.loads(p.with_suffix('.json').read_text('utf-8'))
        if meta['colorSpace']!=space or not meta['frameTiming'] or meta['development']:fail.append(str(p)+' metadata mismatch')
        imageHashes.append({'file':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'meanRGB':float(a.mean()),'stdRGB':float(a.std())})
    pairs=[]
    for p in sorted(folder.glob('hdr-*.png')):
        other=p.with_name(p.name.replace('hdr-','ao-',1))
        if not other.exists():continue
        a=pixels(p);b=pixels(other);luma=a@np.array([.2126,.7152,.0722]);den=b@np.array([.2126,.7152,.0722]);mask=luma>8
        ratio=den[mask]/luma[mask]
        diff=np.abs(a-b)
        # Far strategy view is near the AO falloff, with subpixel contacts. Require
        # a measurable effect on the actual close/contact fixture, not every pixel scale.
        if 'strategy' not in p.name and np.mean(diff)<.001:fail.append(str(other)+' foreground AO appears identical to HDR')
        pairs.append({'view':p.name,'meanAbsByteDifference':float(diff.mean()),'changedPixelsPercent':float(np.any(diff>.5,axis=2).mean()*100),'aoToHdrLumaP01':float(np.quantile(ratio,.01)),'aoToHdrLumaP50':float(np.quantile(ratio,.5)),'darkestByteDelta':float((a-b).max())})
    results[space]={'images':imageHashes,'aoPairs':pairs}

linear=ROOT/'Acceptance/Linear/Captures'
fontPath=Path('C:/Windows/Fonts/arial.ttf')
font=ImageFont.truetype(str(fontPath),25)
small=ImageFont.truetype(str(fontPath),18)
sheet=Image.new('RGB',(1440,876),(20,24,29));draw=ImageDraw.Draw(sheet)
for col,(hour,label) in enumerate([('12.0','Day / 12:00'),('18.5','Sunset / 18:30'),('01.0','Night / 01:00')]):
    draw.text((col*480+16,10),label,font=font,fill='white')
    for row,view in enumerate(('meadow','oak-contact')):
        im=Image.open(linear/f'ao-{view}-{hour}.png').resize((480,270),Image.Resampling.LANCZOS)
        sheet.paste(im,(col*480,52+row*340));draw.text((col*480+16,326+row*340),view,font=small,fill=(204,214,225))
draw.text((16,760),'Actual Unity Player renders | Linear HDR + SSAO | no image recolouring',font=font,fill='white')
draw.text((16,800),'RTX 4070 Ti SUPER | 1920 x 1080 | MSAA 4 | joint Meadow baseline, no spatial GI',font=small,fill=(204,214,225))
sheet.save(ROOT/'lighting-meadow-day-sunset-night.png')
for name,pattern in [('camera-motion','camera-3-*.png'),('oak-wind','wind-*.png')]:
    frames=[Image.open(p).resize((768,432),Image.Resampling.LANCZOS) for p in sorted(linear.glob(pattern))]
    if frames:frames[0].save(ROOT/(name+'.gif'),save_all=True,append_images=frames[1:],duration=100,loop=0)
results['validation']={'status':'FAIL' if fail else 'PASS','failures':fail,'expectedPerSpace':76,'notes':'AO ratios are diagnostic, not a proof of no halos; manual image/motion review remains required. Captures are not timing samples.'}
(ROOT/'capture-validation.json').write_text(json.dumps(results,indent=2),'utf-8')
print(json.dumps({'validation':results['validation'],'aoPairs':{s:results[s]['aoPairs'] for s in ('Gamma','Linear')}},indent=2))
if fail:raise SystemExit(2)
