"""Technical contact sheets and measured report insert; source PNGs stay untouched."""
from pathlib import Path
import argparse
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = Path('E:/Games_Develop/Research/SpatialIndirect_20261026')
p = argparse.ArgumentParser()
p.add_argument('--run', default='FinalPrecision')
a = p.parse_args()
RUN = OUT / a.run
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 25)


def sheet(items, cols, name, width=960):
    height = width * 1080 // 1920
    rows = (len(items) + cols - 1) // cols
    canvas = Image.new('RGB', (cols * width, rows * (height + 46)), '#1b2027')
    draw = ImageDraw.Draw(canvas)
    for i, (file, label) in enumerate(items):
        x, y = i % cols * width, i // cols * (height + 46)
        draw.text((x + 14, y + 8), label, fill='white', font=font)
        source = Image.open(RUN / (file + '.png')).convert('RGB')
        canvas.paste(source.resize((width, height), Image.Resampling.LANCZOS), (x, y + 46))
    canvas.save(OUT / name)


sheet([('legacy-overview-day', 'День: исходный ambient'),
       ('gi-overview-day', 'День: пространственный GI'),
       ('legacy-under-roof-day', 'Под крышей: исходный ambient'),
       ('gi-under-roof-day', 'Под крышей: пространственный GI')], 2, 'spatial-indirect-comparison.png')
sheet([('gi-overview-day', '12:00'), ('gi-overview-sunset', '18:30'),
       ('gi-overview-night', '01:00')], 3, 'spatial-indirect-time-of-day.png', 640)
sheet([('gi-under-roof-offscreen-grey', 'Отражатель вне кадра: серый'),
       ('gi-under-roof-offscreen-red', 'Отражатель вне кадра: красный'),
       ('gi-under-roof-day', 'Крыша на месте'),
       ('gi-under-roof-removed', 'После сноса и локального обновления')], 2, 'spatial-indirect-transport.png')

bench = json.loads((RUN / 'benchmarks.json').read_text('utf-8'))['reports']
updates = json.loads((RUN / 'updates.json').read_text('utf-8'))['reports']
check = json.loads((RUN / 'transport-checks.json').read_text('utf-8'))
lines = ['Финальный Player: PASS,27 непустых PNG1920×1080, включая12 кадров перемещения',
         'камеры и день12:00/закат18:30/ночь01:00. Runtime-errors пуст. Автопроверка',
         'результата: `evidence-validation.json`; просмотрены общий/ближний виды,',
         'крыша, guard и ночь. Проверка тонкой стенки остаётся ограниченной этим proxy.',
         '', 'Точка под крышей: irradiance/pi RGB='+str(tuple(round(check['covered'][v], 6) for v in 'xyz'))+',',
         'после сноса='+str(tuple(round(check['uncovered'][v], 6) for v in 'xyz'))+'. Undo вернул исходные',
         'коэффициенты детерминированно. ReflectorVisible=false; изменение его albedo',
         'за пределами frustum меняет стену: красный канал '+f"{check['grey']['x']:.6f}→{check['red']['x']:.6f}.",
         'CPU reference sample при переносе камеры не изменился. Это сравнение света',
         'в одной мировой точке, не утверждение о нулевом screen-space AO noise.',
         '', 'Условия: Unity6000.5.5f1/URP17.5, Release, Linear/HDR/Neutral, D3D12,',
         'RTX4070 Ti SUPER, i7-12700F,1920×1080, MSAA4, SSAO из Meadow baseline,',
         '2048 shadowmap/2cascades/90m, soft shadows. Оба режима в одном последовательном',
         'запуске:120 кадров прогрева,600 обычных endCameraRendering интервалов,',
         'FrameTimingStats=true, VSync/FPS limit off, без readback/PNG/очереди обновления',
         'во время benchmark. Сцена/камера одинаковы, ветер идёт. GI off в этом сравнении',
         'означает strength0 у включённого варианта; отдельная regression сцена выключает',
         'keyword полностью. Наши build/capture процессы во время benchmark не запускались;',
         'остальные пользовательские приложения не изолировались.',
         '', '| Режим | GPU mean,ms / samples | frame median / p95 / p99,ms |',
         '|---|---:|---:|']
for v in bench:
    f, g = v['frame'], v['gpu']
    lines.append(f"| GI {'on' if v['indirect'] else 'off'} | {g['meanMs']:.3f} / {g['samples']} | {f['medianMs']:.3f} / {f['p95Ms']:.3f} / {f['p99Ms']:.3f} |")
lines += ['', f"Разница GPU mean примерно+{bench[1]['gpu']['meanMs']-bench[0]['gpu']['meanMs']:.3f}ms в этом маленьком стенде.",
          'Это диагностический парный прогон, не обещание FPS большой карты. DXGI сообщил',
          'о некорректных swapchain timestamps: wall intervals измерены Stopwatch по',
          'рендеру камеры и не выдаются за display latency. GPU samples доступны не',
          'на каждом кадре; сырые JSON сохранены.',
          '', '| Изменение | Пробы / всего | CPU solve+upload,ms | До сходимости,s | GPU upload,bytes | max update одного кадра,ms |',
          '|---|---:|---:|---:|---:|---:|']
for v in updates:
    lines.append(f"| {v['reason']} | {v['solved']} / {v['totalProbes']} | {v['solveMs']:.1f}+{v['uploadMs']:.1f} | {v['wallMs']/1000:.2f} | {v['uploadedBytes']} | {v['maxTotalUpdateFrameMs']:.3f} |")
lines += ['', 'Initial CPU fill включает solver warmup и заполнение геометрического dictionary.',
          'Предел solve2ms кооперативный, не жёсткий realtime deadline: есть выброс',
          f"до{max(x['maxTotalUpdateFrameMs'] for x in updates):.3f}ms. Точную причину выброса без profiler trace не установили.",
          'Полное поле GPU=1382976bytes (~1.32MiB); CPU probe array такого же размера,',
          'плюс очередь/revisions/dictionary. В полном surface cache14316 entries;',
          'его реальная память/GC отдельно не измерены. Чистая смена света сохраняет',
          'surface cache, но обновляет все21609 проб активного поля. Локальный commit',
          f"отражателя затрагивает{check['localReflectorProbes']}/{check['totalProbes']}, крыши{check['localRoofProbes']}/{check['totalProbes']};",
          'не всю карту. Даже эти5–7s задержки слишком велики для финального редактора.',
          'Во время обновления могут видеться границы новых данных: temporal blending нет.', '']
report = ROOT / 'docs/reports/spatial-indirect-prototype-20261026-ru.md'
text = report.read_text('utf-8')
start, end = '<!-- FINAL_RESULTS -->', '<!-- END_FINAL_RESULTS -->'
insert = start + '\n\n' + '\n'.join(lines) + '\n' + end
if end in text:
    left = text.index(start)
    right = text.index(end) + len(end)
    text = text[:left] + insert + text[right:]
else:
    text = text.replace(start, insert)
report.write_text(text, 'utf-8')
print('Saved three labelled contact sheets; inserted measured report tables.')
