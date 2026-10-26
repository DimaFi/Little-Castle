from pathlib import Path
import json

r=Path(__file__).resolve().parents[2];data=Path('E:/Games_Develop/Research/SpatialIndirectStage1C_20261026');j=json.loads((data/'stage1c-summary.json').read_text('utf-8'));assert j['pass_']
names=['Initial field','Albedo grey','Albedo restore','Roof removal','Roof undo','Sunset','Night','Day','Cube add','Cube move 0.5m','Cube undo','Cube remove'];rows=[]
for name,x in zip(names,j['updates']):
    a,b=x['Fifo'],x['Priority'];f=lambda m:f"{a[m]['median']:.1f} → {b[m]['median']:.1f}"
    v=b['visibleSupportRenderedMs'];rows.append(f"| {name} | {x['visibleSupportProbes']}/{x['probes']} | {f('firstChangedSupportRenderedMs')} | {f('visibleSupportRenderedMs')} [{v['minimum']:.1f}–{v['maximum']:.1f}] | {f('wallMs')} | {f('solveMs')} |")
steady=[]
for x in j['steady']:
    g=x['gpuMeanMs'];steady.append(f"| {x['mode']} / {'on' if x['indirect'] else 'off'} | {g['median']:.3f} [{g['minimum']:.3f}–{g['maximum']:.3f}] | {x['samples']} | {x['frameMedianMs']['median']:.3f} / {x['frameP95Ms']['median']:.3f} / {x['frameP99Ms']['median']:.3f} |")
report='''# GI, этап1В: приоритет проб в кадре — 26.10.2026

Реализован camera-frustum priority с сохранением фонового прогресса и прежнего
кооперативного2ms CPU budget. Родитель:`d1d92e0505110726be62f6ed408f33e446201ce8`.
Это исследовательская очередь, не активация production GI или Linear в игре.

## Поведение и инварианты

Глобальная FIFO и её видимое подмножество хранятся в intrusive lists по индексам
проб. Удаление/переносO(1), нет накопления stale entries/дубликатов после приоритета,
повторной правки или смены камеры. Из3 priority запросов каждый четвёртый отдаётся
старейшему запросу глобальной FIFO. Исходная очередьN запросов продвигается не более
чем за4N dequeue даже при постоянных повторных visible edits. Смена камеры не
обнуляет fairness counter. Дедупликация и per-probe revision/stale-work gate сохранены.

Проба приоритетна, если находится внутри шести плоскостей фрустума с запасом
`1.8*spacing+.08m`: консервативная поддержка восьми соседей интерполяции и bias.
Это включает скрытые/воздушные пробы, не является depth/occlusion visibility.
Классификация пересобирается при изменении view/projection во время pending work,
не чаще одного раза за4кадра. Между пересчётами остаются минимум3solver frames даже
при постоянном движении камеры. Только предпочтение очереди может запоздать на3кадра;
geometry/revision correctness не запаздывает. Новые dirty пробы проверяются при enqueue.
GeometryUtility пишет в заранее
созданный Plane[6]. Время классификации включено в тот же solve timer2ms; если оно
исчерпало бюджет, ray solve откладывается. Enqueue/commit, как прежде, измеряется
отдельно. Нет ray/R/GPU quality reduction. `-giFifo` сохраняет FIFO serving с той же
классификацией для общего latency-инструмента; `-giReference` выбирает serial solver.

Старые шесть proxy/поле/лучи/свет и shaders неизменны. В automated `-giPriorityStudy`
после исходных8 сценариев отдельно создаётся куб0.8×0.8×0.8m на уровне пола,
центр(-8.3,.65,3.2); commit add,move0.5m по-Z,Undo,remove. Создание renderer/collider
вне measured commit/solve; применяется существующий shared материал receiver,
не создаётся asset или новый production-model dependency. После remove прежнее
окружение восстанавливается. Прежние куб/стены/крыша остаются в стенде.

## Проверки

203/203 Edit Mode,6/6 Play Mode. Шесть новых тестов: conservative support с
отрицательными координатами;3:1 fairness при повторных правках и смене фрустума;
дедупликация/promotion/in-flight rejection; атомарный отказ некорректного frustum;
поле против FIFO/serial; allocation-free cache refresh/drain с положительным
4096B allocation control. Этот последний тест относится к чистому cache API,
не доказывает отсутствие GC во всём Unity Player/Camera API.

Три пары FIFO/Priority на **одной Release сборке**, плюс отдельный serial validation
запуск.36 полных полей по21609проб ×16float: FIFO/Priority error '''+f"{j['maxCoefficientError']:.9g}"+'''.
36 serial comparisons max error '''+f"{j['maxSerialError']:.9g}"+''';24 старых сценария с
сохранённым Stage1B max error '''+f"{j['maxPreviousStageError']:.9g}"+'''. NaN/Inf нет,tolerance1e-5.
'''+f"{j['imagePairs']}"+''' реальных image pairs; maximum image MAE '''+f"{j['maxImageMeanByteError']:.3f}"+'''/255;
Обычный SSAO noise и нефиксированный ветер в двух cube PNG не объявлены побитовым
PNG совпадением. В original27 captures ветер фиксирован; в cube phase после day
он восстановлен. Все7 основных Players и отдельная motion-проверка exit0,
runtime-errors пусты; сборка Succeeded/0errors/1 прежнее obsolete API warning.

## Как измеряется отклик

`firstChangedSupportRenderedMs` — первая обычная `endCameraRendering`, к которой
хотя бы одна valid-проба в frustum-support изменила коэффициенты и была загружена
в GPU. `visibleSupportRenderedMs` — первая обычная отрисовка с пустой очередью
этого support. Обе метрики считаются Stopwatch после запуска WaitCache, commitMs
выведен отдельно. В конце ожидания допускается один обычный render перед PNG/bin;
полная `wallMs` зафиксирована ДО этого дополнительного ожидания. Бенчмарк не делает
readback внутри latency/timing windows.

**Это метрики поддержки кадра, не доказательство изменения пикселя/заметности и не
input-to-display latency.** Первая изменённая проба может быть закрыта объектом;
консервативная сходимость может ждать неиспользуемые воздушные пробы. Pixel/temporal
качество, crossfade и реальные depth-based priorities ещё не исследованы.

## Измерения

RTX4070TiSUPER/i7-12700F, Unity6000.5.5f1/URP17.5,D3D12,Release1920×1080,
Linear/HDR/Neutral,MSAA4,SSAO,soft shadows2048/2cascades/90m. Прежнее поле49×9×49,
шаг1m,128ray/probe,range6m; на cube-сценариях7proxy, иначе6.2ms включая priority,
cap8192,update60Hz. Порядок:FIFO/Priority,Priority/FIFO,FIFO/Priority.
Другой наш Editor/benchmark не работал; пользовательские приложения не изолированы.
В таблице median3 runs, для Priority support также[min–max], ms. Полный разброс,
upload/commit/classify и per-frame maximum записаны в summary/raw updates.

| Сценарий | Support/все пробы | Первый изменённый support render FIFO→Priority, ms | Support convergence FIFO→Priority [range Priority], ms | Full FIFO→Priority, ms | CPU solve FIFO→Priority, ms |
|---|---:|---:|---:|---:|---:|
'''+ '\n'.join(rows)+'''

Сам куб мал, но finite2R+.52m influence halo по-прежнему даёт6084/6318 запросов;
это не изменение алгоритма support и не исключение дальних вкладов ради скорости.
0.25/0.5s исследовательские цели можно сопоставлять лишь с **указанными support
метриками этой малой правки**, не объявлять достигнутым обещание заметного отклика
любого edit или полной геометрической сходимости. Cold field и большой roof case
сохраняют заметную задержку. Priority не снижает общий объём solve.

Steady:VSync/FPS off,120warmup+600 normal camera intervals на on/off каждого run,
без pending work/PNG/dumps. GPU — median3 per-run means[min–max], samples суммированы;
frame — median трёх per-run median/p95/p99. Swapchain timestamp warnings сохранены
в raw logs; Stopwatch callback interval не равен физической задержке дисплея.

| Mode / GI | GPU mean median [range], ms | GPU samples | Frame median / p95 / p99, ms |
|---|---:|---:|---:|
'''+ '\n'.join(steady)+'''

CPU queue payload: '''+str(j['queuePayloadBytes'])+'''B (4int links+revision+2bool/probe и6planes),
прежняя queue/revision/queued payload~9B/probe, дельта~281KB плюс array headers.
Transport max '''+str(j['transportPayloadBytes'])+'''B (после cube links cache может расти),
GPU buffer прежний1382976B. OS/VRAM peak и новое Development GC.Alloc измерение
не выполнялись. Release byte counter unsupported/-1 как прежде; старый Development
отчёт не выдаётся за новый Player zero-GC. Классификация и cooperative maximum
могут превышать2ms при холодном JIT/GC; точные максимумы есть в summary.

## Интеграция и следующий этап

Изменены4 C#:Cache,Volume,StudyReview,Stage1Build. Добавлен PriorityTests с meta;
GUID прежних source сохранены, новый GUID уникален. Trace/Transport solver kernels,
7shared shaders,исходная scene/materials и3ProjectSettings unchanged. MAIN game files
без drift. Рабочая копия редактора карты и его приложения не менялись. Новых Meadow
snapshot нет; исходные asset hashes/GUID наследуются от опубликованных пакетов.

Before snapshot10 inputs:`docs/lighting-integration/stage1c-baseline-d1d92e0/`.
Точные before/current SHA:`stage1c-manifest.json`,audit:`stage1c-audit.json`,delta:
`gi-d1d92e0-to-stage1c.patch`. Применять поверх1Б; не заменять чужие whole-file
shader/map-editor версии. Camera priority — presentation, не авторитетный save/time.

Следующий этап2: согласованная версия результата, плавная публикация/переход после
commit и непрерывные сутки без накопления устаревшей работы. Сейчас GPU buffer
обновляется in-place, поэтому временные границы/разные возраста проб возможны.
После этого — committed snapshot нового рельефа/деревьев и отдельная визуальная
зона готовых моделей, сохранив куб/стены/крышу; выбор моделей по их реальным мешам
и правилам AGENTS. Палитра, SSAO/rootAO и художественный проход Tiny Glade отдельно.
Fixed L1/flat ground/max8AABB/no canopy остаются ограничениями, редактор не блокируется.

## Артефакты и повтор

`E:/Games_Develop/Research/SpatialIndirectStage1C_20261026`:Guarded01..03Fifo/Priority,
GuardedSerial,GuardedMotion,summary,raw JSON/XML/logs,58PNG и84probe dumps,Player и contact sheets.
Ранние Final* запуски до throttle камеры сохранены как предварительные, в summary
не включены. build-final.log содержит исправленный конфликт имён motion diagnostic,
не является финальной сборкой; build-guarded.log — успешная финальная сборка.
Player/source/raw SHA индексируются в пакете; большие bin/Player остаются локально.

1. EditMode/PlayMode Test Runner в lighting integration copy.
2. `LittleCastle.Editor.SpatialIndirectStage1Build.BuildLinear -giStage1C`:
   saved scene,Linear temporarily,settings restored.
3. `Tools/LightingStudy/run_stage1c.ps1 -Repeats3`:видимые standalone окна,
   никакого overwrite; при новой серии явно выбрать новый root/prefix и в summarizer.
4. `summarize_stage1c.py`, `audit_stage1c.py`, `report_stage1c.py`.
'''
motion=j['movingCameraCheck']
report=report.replace('## Как измеряется отклик',f"Отдельная moving-camera проверка: {motion['frames']} кадров, {motion['priorityRefreshes']} refresh, {motion['wallMs']:.1f}ms, полное завершение очереди и побитовое совпадение поля. Это один диагностический прогон с повторной lighting injection; не стресс-тест холодной геометрии или большой карты.\n\n## Как измеряется отклик")
(r/'docs/reports/lighting-gi-priority-stage1c-20261026-ru.md').write_text(report,'utf-8')
key=lambda index,metric:j['updates'][index]['Priority'][metric]['median']
section=f'''## Обновление после этапа1В — 26.10.2026

Очередь frustum-support priority (запас1.8*spacing+.08m),3видимых:1global FIFO,
без дублей, со старым revision gate; camera refresh не сбрасывает fairness.
Классификация внутри прежнего2ms budget.203 Edit+6Play,3 Release FIFO/Priority пары
на одном Player +serial run.36 fields max FIFO/Priority error{j['maxCoefficientError']:.8g},
serial{j['maxSerialError']:.8g};24 field regressions к1Б без изменения результата.
Куб0.8m,move0.5m: Priority support convergence{key(9,'visibleSupportRenderedMs'):.1f}ms,
полная{key(9,'wallMs'):.1f}ms. Roof support{key(3,'visibleSupportRenderedMs'):.1f}ms,
полная{key(3,'wallMs'):.1f}ms. Это conservative frustum-support/render metrics,
не pixel/input-to-display latency. Queue delta~281KB; solve quality не снижено.
Отчёт:`docs/reports/lighting-gi-priority-stage1c-20261026-ru.md`.

Далее **этап2: версия результата и плавное обновление**. Buffer сейчас in-place,
разный возраст проб возможен. Проверить commit/Undo/load во время pending work,
camera motion, continuous WorldTime и bounded work без starvation. Не повторять
готовый этап1В и не объявлять решённым pixel/temporal качество по latency queues.
После этого — актуальный terrain/object snapshot и готовые модели в отдельной
визуальной зоне. Диагностические куб,стены,крыша остаются; shaders/палитра в1В прежние.

'''
plan=r/'docs/plans/lighting-next-stages-20261026-ru.md';s=plan.read_text('utf-8');marker='## Обновление после этапа1В — 26.10.2026'
if marker in s:s=s[:s.index(marker)]+s[s.index('## Обновление после этапа1Б'):]
s=s.replace('## Обновление после этапа1Б',section+'## Обновление после этапа1Б',1);a=s.index('## Следующий конкретный пакет работ');b=s.index('Tiny Glade остаётся ориентиром.',a)
s=s.replace('Ближайший этап — ускорение кеша на\nсохранённом стенде. После него — устойчивое обновление и новый рельеф/объекты.','Ближайший этап — согласованная публикация проб и непрерывные сутки.\nПосле него — новый рельеф/объекты и совместный визуальный стенд.')
a=s.index('## Следующий конкретный пакет работ');b=s.index('Tiny Glade остаётся ориентиром.',a)
s=s[:a]+'''## Следующий конкретный пакет работ

Этап2: согласованные версии и плавное обновление непрямого света,continuous
WorldTime,commit/Undo/load во время незавершённой очереди. Сохранить2ms и reference
точность. Затем committed terrain/object snapshot,визуальная зона готовых моделей
с сохранением куба/стен/крыши и отдельный художественный проход. Editor не блокируется.

'''+s[b:];plan.write_text(s,'utf-8')
handoff=Path('E:/Games_Develop/Little-Castle/docs/handoffs/lighting-to-map-authoring-20261025-ru.md');h=handoff.read_text('utf-8');marker='## Этап1В: приоритет GI — 26.10.2026'
if marker in h:h=h[:h.index(marker)].rstrip()+'\n'
h+='\n'+section.replace('## Обновление после этапа1В — 26.10.2026',marker)+'''
Before/current SHA+GUID, audit и patch в lighting-копии:stage1c-manifest.json,
stage1c-audit.json,gi-d1d92e0-to-stage1c.patch. Новых Meadow зависимостей нет;
Trace/Transport,7shared shaders,scene/materials,3settings unchanged; MAIN game drift0.
Другая URP копия и apps не изменялись. Source/evidence packet:
docs/integration-packages/lighting-gi-priority-stage1c-20261026/.
Публикация нового пакета подтверждается publication-gi-stage1c/publication-receipt.json
после отдельной remote проверки; исходный1Б опубликованd1d92e0.
''';handoff.write_text(h,'utf-8');print('Report, plan and MAIN handoff updated.')
