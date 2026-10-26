from pathlib import Path
import json
import re

root=Path(__file__).resolve().parents[2];data=Path('E:/Games_Develop/Research/SpatialIndirectStage1_20261026');s=json.loads((data/'stage1-summary.json').read_text('utf-8'))
assert s['pass_'];lines=[]
lines.append('''# GI: первый проход ускорения кеша — 26.10.2026

Статус: **проверенный исследовательский результат; производственный GI не принят**.
Родитель — опубликованный `100c1745924493d2d8be0f29b5541c695273ed6e`.
Работа в `E:/Games_Develop/Little-Castle_LightingIntegration_20261025`.

## Результат

Разделены геометрический перенос и lighting injection. Изменение албедо использует
сохранённые веса без первичной трассировки. Движение солнца/луны переоценивает их
видимость по сохранённым попаданиям. Геометрическая правка сохраняет незатронутый
surface cache; в дальней части halo воспроизводит вторичное перекрытие без поиска
первичных попаданий. Исходная математическая модель и шейдерный вывод сохранены.

Для измерений оставлена прежняя сцена, поле49×9×49=21609, шаг1m,128 лучей, радиус6m,
шесть AABB, native Meadow/дорожка/Oak A/B/C/D. Новая палитра или геометрия не
подменяли ускорение. Копия редактора карты и её приложения не управлялись.

## Профиль и реализация

Диагностический профиль исходного solver:2.4512 млн primary rays,441417 shadow
rays,171792 sky rays,17.773245 млн box tests. Первичный поиск занял~62% instrumented
Editor времени. Cold plain Editor solve~1.70s, с per-ray timers~1.96s: это
инвазивная диагностика стадий, не Release benchmark. Финальные reference-profile
и cached-profile JSON сохранены; оптимизированный cold проход уменьшил реальные
slab tests до3.631907 млн, primary queries до1.914112 млн, shadow queries до200099.

`SpatialIndirectTrace` сохраняет serial reference Solve. В него добавлены
диагностические счетчики, stable proxy ID и консервативный finite-range candidate
mask. Новый `SpatialIndirectTransport` хранит8-byte hit links (distance + packed
ray/face/id), а также агрегированные L1-веса окружения и источников по отражателям.
Цвет неба/солнца/материала подставляется отдельно. Сокращены повторные shadow queries
для однородной грани без других препятствий в поддержке; новые приближённые
координаты shadow ray не вводились.

При geometry commit первичные данные инвалидируются в пределах R от старой/новой
геометрии; в оставшейся области2R+0.52m сохраняются точные hit links и воспроизводится
sky/shadow visibility. Поверхностные ячейки инвалидируются локально с учетом
квантованного footprint. Stable IDs сохраняют связи при удалении/уплотнении массива
proxy. Очередь dedup и revision gate прежние. На roof edit очередь остаётся9801
проба:2977 primary rebuild,6012 secondary replay, остальные не требуют переноса
из-за solid status. Цветовая правка по-прежнему обновляет8019 проб, без geometry
rebuild или direct reweight. Это тот же объем выходных данных, не уменьшенный стенд.

CPU solve budget остался2ms. Safety cap для cached пути8192 проб вместо256:
быстрая injection использует имеющийся бюджет; reference обычно достигает времени
задолго до старого cap. Пределы записаны во всех update JSON. Upload и commit CPU
измерены отдельно: лимит solve не является жёстким лимитом полного кадра.

Новый код serial-only: scratch arrays и mutable surface dictionary не thread-safe.
Это ещё не Jobs/Burst/compute, BVH, приоритет видимых проб или streamed/moving field.
Память меняется в обмен на повторное использование переноса; cache не save-state.

## Настоящие измерения

Три независимые пары, порядок Reference→Cached / Cached→Reference / Reference→Cached.
Release, Unity6000.5.5f1/URP17.5, RTX4070 Ti SUPER/i7-12700F, D3D12,1920×1080,
Linear/HDR/Neutral, MSAA4, SSAO, soft shadows2048/2cascades/90m. Время до сходимости
измерено при target60Hz; для steady benchmark VSync/лимит FPS off,120 warmup+600
обычных camera-render интервалов на режим. PNG/CPU dumps вне замеров solve/steady.
Во время шести Release запусков наших Unity/build/image-analysis jobs не было.
OS memory sampler подключён к последним трём Players с polling250ms; его наличие
указано явно. Остальные пользовательские приложения не изолировались.

Ниже median из3 запусков; raw min/max и каждый запуск в `stage1-summary.json`
и `Final01..03*/updates.json`. CPU — суммарный solve всей очереди, не один кадр.

| Сценарий | Пробы | CPU ref / cached,ms | Сходимость ref / cached,s |
|---|---:|---:|---:|''')
for v in s['updates']:
    lines.append(f"| {v['reason']} | {v['probes']} | {v['referenceCPUms']['median']:.2f} / {v['cachedCPUms']['median']:.2f} | {v['referenceWallMs']['median']/1000:.3f} / {v['cachedWallMs']['median']/1000:.3f} |")
lines.append('''
![Измерения CPU и времени сходимости](../integration-packages/lighting-gi-cache-stage1-20261026/evidence/stage1-performance.png)

Статистика steady (median / диапазон mean GPU по3 запускам; frame значения —
median отдельных run percentiles, не pooled percentiles):

| Solver / GI | GPU mean,ms (min–max) | GPU samples всего | frame median / p95 / p99,ms |
|---|---:|---:|---:|''')
for v in s['steady']:
    g=v['gpuMeanMs'];lines.append(f"| {v['mode']} / {'on' if v['indirect'] else 'off'} | {g['median']:.3f} ({g['minimum']:.3f}–{g['maximum']:.3f}) | {v['gpuSamples']} | {v['frameMedianMs']['median']:.3f} / {v['frameP95Ms']['median']:.3f} / {v['frameP99Ms']['median']:.3f} |")
lines.append('''
Кеш не добавляет нового GPU pass; shader files побайтно прежние. Разброс GPU/CPU
между запусками не приписывается только solver. DXGI swapchain timestamps
предупреждались как некорректные; wall интервалы — Stopwatch на рендере камеры,
не display latency. Это маленький стенд на мощном GPU, не гарантия большой карты.

## Качество и проверки

**193/193 Edit Mode,6/6 Play Mode**. Новые проверки: conservative broad
phase, albedo/sky/sun against reference без primary retrace, локальный roof/Undo
с сохранением stable IDs и cache вне dirty области, смена конечного радиуса,
прогретая injection/direct reweight без managed allocation с positive GC control,
микроперемещение Bounds на старой границе пробы и ULP roundoff у конца луча.
Cache identity использует точное сравнение center/extents, а не tolerant Bounds==;
candidate mask имеет малый консервативный запас от float normalization roundoff.
Прежние проверки energy, roof, offscreen bounce и stale result rejection сохранены.

Во всех3 парах сравнивались8 полных CPU полей:24×21609 проб, все16 float/probe,
включая status. Максимальная абсолютная ошибка5.0664e-7 при пороге1e-5, NaN/Inf нет.
Это ошибка суммирования при агрегировании линейных весов, не новая модель света.
Undo, внекадровый отражатель, preview isolation и независимость от камеры прошли.
Первая пара содержит54 непустых PNG1920×1080:27 эталонных и27 кешированных;
включая день/закат/ночь и camera sequence. MAE изображений максимум~0.268/255,
сопоставим с прежним SSAO blue-noise разбросом; все кадры не объявляются точными
pixel copies. Просмотрены общий/подкрышей виды, форма/контакты и сравнение.

![Реальные кадры первого парного прогона](../integration-packages/lighting-gi-cache-stage1-20261026/evidence/stage1-game-comparison.png)

Release и Development builds Succeeded,0 errors,1 прежнее warning CS0618
LocalSandboxController.FindFirstObjectByType. Runtime-errors успешных прогонов пусты.
Пилот с опечаткой в новом тесте исправлен; его failed compile log не считается
финальной проверкой. Скрытый Development diagnostic не получил обычные кадры
камеры и остановлен по точному PID/path; его результаты исключены. Повторный
видимый `AllocationVisible` завершился exit0/PASS, затем окончательный
`AllocationFinal` повторён после точных geometry guards. Другие приложения не закрывались.

## Память и аллокации
''')
lines.append(f"Полезные данные кеша достигли{s['maximumCachedPayloadBytes']}bytes (~{s['maximumCachedPayloadBytes']/1048576:.2f}MiB), плюс managed headers/индексы/ёмкость dictionary. GPU probe buffer прежний1382976bytes. Это не оценка полного heap по одной формуле; все counters слотов/links/surfaces сохранены.")
for mode,v in s['managedHeapPhaseBoundaries'].items():lines.append(f"Managed heap на границах фаз nocapture запусков {mode}: {v['minimum']/1048576:.2f}–{v['maximum']/1048576:.2f}MiB, включая остальную сцену/движок.")
lines.append('''
OS sampler наблюдал только наши финальные Players; ниже количество и peak working
set по Windows, observed private bytes и точные PID/args в raw JSON.
Это общая память процесса, не доказательство, что кеш уменьшил RAM: число прогонов,
GC/driver/native allocations различаются. Полные записи с PID/args сохранены.
VRAM peak отдельно не измерен; масштабирование на большое поселение остаётся открытым.

В Release контрольная аллокация4096bytes дала0 у GC.GetAllocatedBytesForCurrentThread.
Поэтому bytes counter помечен unsupported, значения-1, а не «0 allocations».
[Unity counters reference](https://docs.unity.com/en-us/engine/6000.5/manual/analysis/profiler/counters-reference)
также отмечает GC Allocated In Frame как недоступный в Release. GC.CollectionCount
сохранён как whole-player diagnostic, не точный счетчик одного solver.

Для достоверного allocation-call count собран отдельный Development Player с
Recorder GC.Alloc, FilterToCurrentThread и positive4096-byte control. Его времена
не используются в Release comparison. Recorded calls, а не bytes:

| Фаза | Solver allocation calls | Command allocation calls |
|---|---:|---:|''')
for v in s['allocationDiagnostic']:lines.append(f"| {v['reason']} | {v['solverAllocationCalls']} | {v['commandAllocationCalls']} |")
lines.append('''
Setup отдельно21689 calls, cold solve29654, roof removal270. Warm injection,
sun refresh и Undo не выделяли managed память внутри measured solver scope;
proxy command создаёт8–10 объектов вне solve. Это не «zero-GC вся игра».
Сумма GC.Alloc не равна росту managed heap: см.
[официальное описание измерения allocations](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/performance-memory/managed-memory/track-garbage-collection).

## Пределы результата и следующий шаг

Большая albedo edit уже реагирует за31–33ms. Геометрическая крыша всё ещё сходится
за1.5–2s, initial fill~4.6s, световой refresh~0.7s. Цель полусекунды для геометрии
не подтверждена; стандартный малый geometry case ещё не выделен отдельно.
Приоритет видимых проб, плавные версии и непрерывная injection суток не внедрены.
Следующий проход: профиль оставшихся primary/shadow kernels, tighter dependencies
и приоритет видимой области; затем согласованный snapshot нового рельефа/объектов.
Включать GI в основной проект до этих проверок рано. Поле всё ещё fixed AABB/L1,
плоская земля, без canopy/multiple bounce/local emitters. Tiny Glade остаётся
художественным ориентиром, этот проход сохраняет изображение и ускоряет расчет.

## Исходники, воспроизведение и интеграция

Замороженные before-файлы и SHA: `docs/lighting-integration/stage1-baseline-100c174/`.
`stage1-manifest.json`, `stage1-audit.json`, `gi-100c174-to-stage1.patch` перечисляют
3 изменённых research runtime-файла и5 новых source-файлов с meta. Общие7 shader
файлов, исходная сцена, материалы, cache queue, старый builder и ProjectSettings
не изменены. MAIN Assets/Packages/Settings сверены с прежним source manifest.
Новые источники — Profile, Transport, AllocationCounter, Stage1Build, TransportTests.
Сторонний код и бинарные модели/текстуры не добавлялись.

Build: LittleCastle.Editor.SpatialIndirectStage1Build.BuildLinear. Он читает прежнюю
сцену без Setup/Save и восстанавливает3 ProjectSettings файла byte-for-byte.
`-giDevelopment` создаёт отдельную диагностическую сборку в Development/.
Player: `-giStudy -giOutput <path> -giDump`, `-giReference` выбирает эталон,
`-giNoCapture` пропускает PNG, `-giAlloc` включает диагностический Recorder.
Видимое обычное окно требуется для camera-frame benchmark. Update JSON указывает
mode/probeLimit/budget, command/upload time, cache counts и supported flags.

Протокол: Tools/LightingStudy/run_stage1.ps1, verify_stage1.py, summarize_stage1.py,
sample_stage1_memory.ps1, audit_stage1.py. Полные bin поля, PNG, Players и logs
сохранены в `E:/Games_Develop/Research/SpatialIndirectStage1_20261026`.
Пакет интеграции содержит код/GUID, patch, raw JSON/XML, визуальные сравнения и
хеши объемных local-only артефактов; не является автоматически активированной
миграцией MAIN или заменой нового редактора карты.
''')
path=root/'docs/reports/lighting-gi-cache-stage1-20261026-ru.md';path.write_text('\n'.join(lines),'utf-8')
text=path.read_text('utf-8')
memory=[]
for mode in ('Reference','Cached'):
    records=[v for v in s['osMemorySamples'] if mode in v['commandLine']]
    if records:
        ws=[v['peakWorkingSetBytes']/1048576 for v in records];private=[v['peakPrivateBytes']/1048576 for v in records]
        memory.append(f"{mode}: {len(records)} Player, peak working set {min(ws):.1f}–{max(ws):.1f}MiB, observed private {min(private):.1f}–{max(private):.1f}MiB.")
text=text.replace('точные PID/args в raw JSON.','точные PID/args в raw JSON.\n'+'\n'.join(memory))
path.write_text(text,'utf-8')

# Keep the canonical plan/handoff aligned with the final measured build, while
# preserving their earlier baseline and future-stage details.
u={v['reason']:v for v in s['updates']}
def seconds(reason,kind):return u[reason][kind+'WallMs']['median']/1000
compact='; '.join(f"{name}: {seconds(key,'reference'):.3f}→{seconds(key,'cached'):.3f}s" for key,name in [('initial-field','initial'),('offscreen-reflector-grey','albedo'),('roof-removal','roof removal'),('roof-undo','Undo'),('sunset-light-refresh','sunset'),('night-light-refresh','night'),('day-light-refresh','day')])
plan=root/'docs/plans/lighting-next-stages-20261026-ru.md';pt=plan.read_text('utf-8')
start=pt.index('## Обновление после первого прохода');end=pt.index('## Зафиксированная база',start)
pt=pt[:start]+f'''## Обновление после первого прохода этапа1 — 26.10.2026

Реализован и проверен кеш geometry transport / lighting injection:193 Edit Mode,
6 Play Mode,3 финальные Release пары,24 полных поля, max coefficient error
{s['maxCoefficientError']:.5g}. Median convergence: {compact}.
Solve budget2ms сохранён; transport payload~{s['maximumCachedPayloadBytes']/1048576:.2f}MiB плюс overhead.
Warm solver не аллоцирует по отдельной Development GC.Alloc диагностике с positive
control; cold fill/часть geometry rebuild и proxy commands выделяют память.
Добавлены регрессии микроперемещения Bounds и float roundoff у конца луча.

Отчёт: `docs/reports/lighting-gi-cache-stage1-20261026-ru.md`.
Source/evidence: `docs/integration-packages/lighting-gi-cache-stage1-20261026/`.
Общие shaders/палитра/сцена не подменяли performance comparison.
Геометрическая цель0.5s ещё не достигнута, малый geometry case отдельно не выделен.
Следующий проход1Б: оставшиеся primary/shadow kernels, более точные dependencies,
приоритет видимой области; затем этап2 с согласованными версиями и плавными сутками.
Fixed flat-AABB поле остаётся экспериментом, production activation/миграции нет.

'''+pt[end:];plan.write_text(pt,'utf-8')
handoff=Path('E:/Games_Develop/Little-Castle/docs/handoffs/lighting-to-map-authoring-20261025-ru.md');ht=handoff.read_text('utf-8');start=ht.index('## Этап1: ускорение кеша GI')
ht=ht[:start]+f'''## Этап1: ускорение кеша GI — 26.10.2026

Родитель — опубликованный100c1745924493d2d8be0f29b5541c695273ed6e.
Разделены geometry transport и lighting injection, сохраняются точные primary
hit links и L1-веса. Colour/light edits не повторяют поиск primary. Geometry
commit сохраняет незатронутый surface cache; primary rebuild вR, secondary replay
в оставшейся2R+0.52m области. Stable IDs переживают compaction массива proxy.
Добавлены exact geometry bounds identity и conservative margin от float roundoff.

Финальная версия:193/193 Edit Mode,6/6 Play Mode;3 Release пары с чередованием
порядка.24 полных поля по21609 проб, max coefficient error{s['maxCoefficientError']:.5g}
при tolerance1e-5;27 настоящих image pairs, max MAE{s['maxImageMeanByteError']:.3f}/255
с обычным SSAO noise. Все успешные Players exit0, runtime-errors пусты.
Первый парный прогон содержит54 PNG, остальные2 пары без PNG с полными CPU dumps.

Median convergence: {compact}.
RTX4070 Ti SUPER/i7-12700F,1920×1080,D3D12,Linear/HDR/MSAA4/SSAO, тот же fixture.
Solve budget2ms, safety cap cached8192/reference256; commit/upload отдельно.
Steady120warmup+600 обычных кадров на каждый режим, без PNG/dumps в измерениях.
Это маленький стенд; точные min/max, CPU, GPU samples/frame percentiles в summary.

Transport payload{s['maximumCachedPayloadBytes']}bytes (~{s['maximumCachedPayloadBytes']/1048576:.2f}MiB) плюс overhead;
GPU buffer прежний1382976bytes. Release bytes-counter не прошёл positive4096-byte
control и помечен unsupported/-1. Отдельный Development Recorder GC.Alloc main
thread откалиброван: warm colour/time/Undo solver0 calls, cold fill/roof removal
и proxy command выделяют память. Setup/cold/command counts, managed heap phase
boundaries и OS peak working set записаны отдельно. Это не zero-GC всей игры;
VRAM peak отдельно не измерен. Скрытый Development без camera frames исключён,
остановлен только собственный PID; AllocationFinal в видимом окне PASS.

Изменены3 research runtime-файла Trace/Volume/StudyReview. Добавлены5 source с
meta: Profile, Transport, AllocationCounter, Stage1Build, TransportTests. Shared7
shaders, Scene/private materials, старый builder/cache queue и3 ProjectSettings
сохранены; MAIN Assets/Packages/Settings не менялись. Рабочая копия карты не
редактировалась. GUID новых файлов проверены без collisions.
Before snapshot11 файлов: docs/lighting-integration/stage1-baseline-100c174/.
Точные SHA/current и проверки: stage1-manifest.json/stage1-audit.json;
delta: gi-100c174-to-stage1.patch в том же каталоге lighting-копии.

Отчёт: docs/reports/lighting-gi-cache-stage1-20261026-ru.md.
Артефакты: E:/Games_Develop/Research/SpatialIndirectStage1_20261026:
stage1-summary.json, Final01..03*, AllocationFinal, JSON/XML/bin/PNG/Players.
Source/evidence packet: docs/integration-packages/lighting-gi-cache-stage1-20261026/,
ветка codex/lighting-gi-cache-stage1-2026-10-26. Крупные local-only артефакты и
compiled Players индексированы SHA256; их бинарники не публикуются автоматически.
План следующих этапов обновлён фактическим результатом.

Геометрические~1.5–2s и initial~4.6s ещё не соответствуют0.5s цели. Следующий
проход1Б: primary/shadow kernels и приоритет видимых проб; затем плавные версии,
непрерывные сутки и snapshot нового рельефа/объектов. Fixed grid/L1/flat AABB,
canopy/emitters/production activation остаются открытыми. Редактор не блокируется.
''';handoff.write_text(ht,'utf-8')
print(path)
