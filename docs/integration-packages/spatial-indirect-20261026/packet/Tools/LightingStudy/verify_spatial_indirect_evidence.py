"""Validate real Player outputs; do not alter the captured images."""
from pathlib import Path
import argparse
import hashlib
import json
import xml.etree.ElementTree as ET
import numpy as np
from PIL import Image

p = argparse.ArgumentParser()
p.add_argument('--run', default='FinalPrecision')
a = p.parse_args()
root = Path('E:/Games_Develop/Research/SpatialIndirect_20261026')
run = root / a.run
baseline = Path('E:/Games_Develop/Research/LightingMeadow_20261025/Acceptance/Linear/Captures')
regression = root / 'MeadowRegressionPrecision'
control = root / 'FrozenBaselineRepeat'
failures, images, comparisons, tests = [], [], [], []
for source in sorted(run.glob('*.png')):
    data = np.asarray(Image.open(source).convert('RGB'))
    good = data.shape == (1080, 1920, 3) and float(data.std()) > 2
    if not good:
        failures.append('Empty/wrong size: ' + source.name)
    images.append(dict(path=source.name, sha256=hashlib.sha256(source.read_bytes()).hexdigest(), std=float(data.std()), pass_=bool(good)))
if len(images) != 27:
    failures.append('Expected 27 prototype PNGs, got ' + str(len(images)))
if not (run / 'result.txt').read_text('utf-8').startswith('PASS') or (run / 'runtime-errors.txt').read_text('utf-8').strip():
    failures.append('Player runtime checks failed')
for source in sorted(baseline.glob('*.png')):
    current = regression / source.name
    if not current.exists():
        failures.append('Missing regression: ' + source.name)
        continue
    before = np.asarray(Image.open(source).convert('RGB'), dtype=np.int16)
    after = np.asarray(Image.open(current).convert('RGB'), dtype=np.int16)
    delta = np.abs(before - after)
    repeat = np.asarray(Image.open(control / source.name).convert('RGB'), dtype=np.int16)
    control_delta = np.abs(before - repeat)
    item = dict(path=source.name, identical=bool(np.array_equal(before, after)), meanAbsoluteByteError=float(delta.mean()), maxByteError=int(delta.max()), changedPixels=int(np.count_nonzero(delta.max(axis=2))), controlMeanAbsoluteByteError=float(control_delta.mean()), controlMaxByteError=int(control_delta.max()), controlChangedPixels=int(np.count_nonzero(control_delta.max(axis=2))))
    # Exact equality is required where the frozen HDR repeat itself is exact.
    # Three HDR oak views have sparse one-byte quantisation differences even in
    # the untouched Player: compare both magnitude and spatial support to control.
    # Other captures
    # include URP blue-noise SSAO (Random.value offsets and cycling textures).
    # Report raw errors and compare a bounded empirical noise envelope; this is a
    # regression heuristic, not a proof that all visual changes are acceptable.
    item['criterion'] = ('exact-static-HDR' if item['controlMaxByteError'] == 0 else 'frozen-player-HDR-quantization-envelope') if source.name.startswith('hdr-') else 'frozen-player-AO-noise-envelope'
    if item['criterion'] == 'exact-static-HDR':
        item['pass_'] = item['identical']
    elif item['criterion'] == 'frozen-player-HDR-quantization-envelope':
        item['meanLimit'] = item['controlMeanAbsoluteByteError'] * 1.5 + .000002
        item['maxLimit'] = item['controlMaxByteError']
        item['changedPixelsLimit'] = item['controlChangedPixels'] * 1.5 + 10
        item['pass_'] = item['meanAbsoluteByteError'] <= item['meanLimit'] and item['maxByteError'] <= item['maxLimit'] and item['changedPixels'] <= item['changedPixelsLimit']
    else:
        item['meanLimit'] = max(.05, item['controlMeanAbsoluteByteError'] * 1.5 + .02)
        item['maxLimit'] = max(3, item['controlMaxByteError'] + 3)
        item['pass_'] = item['meanAbsoluteByteError'] <= item['meanLimit'] and item['maxByteError'] <= item['maxLimit']
    comparisons.append(item)
    if not item['pass_']:
        failures.append('Regression exceeds criterion: ' + source.name)
if len(comparisons) != 76:
    failures.append('Expected 76 regression references')
if (regression / 'runtime-errors.txt').read_text('utf-8').strip() or not (regression / 'checks.txt').read_text('utf-8').startswith('PASS'):
    failures.append('Meadow regression runtime checks failed')
for name in ('editmode-final.xml', 'playmode-final.xml'):
    node = ET.parse(root / name).getroot()
    item = dict(path=name, **{k: node.attrib.get(k) for k in ('result', 'total', 'passed', 'failed', 'skipped')})
    tests.append(item)
    if item['result'] != 'Passed' or item['failed'] != '0':
        failures.append('Tests failed: ' + name)
result = dict(pass_=not failures, run=a.run, prototypeImages=images, baselineRegression=comparisons, tests=tests, failures=failures)
(root / 'evidence-validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps(dict(pass_=result['pass_'], images=len(images), regressionImages=len(comparisons), byteIdentical=sum(v['identical'] for v in comparisons), tests=tests, failures=failures), ensure_ascii=False, indent=2))
if failures:
    raise SystemExit(1)
