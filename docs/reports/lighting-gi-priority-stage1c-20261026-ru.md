# GI, этап1В: приоритет проб в кадре — 26.10.2026

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
запуск.36 полных полей по21609проб ×16float: FIFO/Priority error 0.
36 serial comparisons max error 5.06639481e-07;24 старых сценария с
сохранённым Stage1B max error 0. NaN/Inf нет,tolerance1e-5.
29 реальных image pairs; maximum image MAE 2.231/255;
Обычный SSAO noise и нефиксированный ветер в двух cube PNG не объявлены побитовым
PNG совпадением. В original27 captures ветер фиксирован; в cube phase после day
он восстановлен. Все7 основных Players и отдельная motion-проверка exit0,
runtime-errors пусты; сборка Succeeded/0errors/1 прежнее obsolete API warning.

Отдельная moving-camera проверка: 4 кадров, 1 refresh, 63.9ms, полное завершение очереди и побитовое совпадение поля. Это один диагностический прогон с повторной lighting injection; не стресс-тест холодной геометрии или большой карты.

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
| Initial field | 9289/21609 | 890.4 → 253.9 | 3022.1 → 2032.0 [2016.5–2312.1] | 3021.3 → 2964.0 | 345.9 → 336.9 |
| Albedo grey | 1412/8019 | 16.6 → 16.8 | 31.6 → 16.8 [14.7–16.8] | 30.8 → 31.2 | 2.6 → 2.8 |
| Albedo restore | 1412/8019 | 19.6 → 19.7 | 34.2 → 19.7 [19.6–20.0] | 33.4 → 33.7 | 2.3 → 2.6 |
| Roof removal | 4324/9801 | 232.8 → 16.1 | 849.2 → 551.2 [533.0–599.7] | 848.4 → 898.8 | 102.3 → 111.8 |
| Roof undo | 4324/9801 | 268.2 → 17.9 | 984.4 → 618.1 [588.0–618.3] | 983.6 → 999.8 | 118.1 → 120.4 |
| Sunset | 6428/21609 | 135.6 → 18.7 | 368.2 → 202.3 [185.8–226.1] | 366.9 → 382.8 | 42.6 → 44.5 |
| Night | 6367/21609 | 135.5 → 18.7 | 337.3 → 152.9 [152.1–949.1] | 336.3 → 318.9 | 39.5 → 38.5 |
| Day | 6367/21609 | 118.8 → 18.7 | 401.1 → 154.9 [135.8–169.6] | 399.7 → 319.5 | 46.7 → 37.6 |
| Cube add | 2755/6084 | 134.1 → 17.3 | 643.5 → 384.1 [367.6–460.2] | 642.5 → 549.0 | 76.0 → 66.0 |
| Cube move 0.5m | 2755/6318 | 151.0 → 17.8 | 551.1 → 384.8 [384.7–419.3] | 564.8 → 566.5 | 66.4 → 67.7 |
| Cube undo | 2755/6318 | 151.2 → 17.8 | 550.8 → 401.5 [384.6–512.6] | 549.9 → 583.5 | 66.6 → 70.5 |
| Cube remove | 2755/6084 | 121.0 → 4.0 | 504.3 → 354.3 [337.3–423.1] | 503.4 → 502.5 | 62.4 → 61.9 |

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
| Fifo / off | 0.973 [0.939–1.008] | 1332 | 1.737 / 3.197 / 3.612 |
| Fifo / on | 1.482 [1.327–1.628] | 1545 | 2.303 / 3.406 / 4.045 |
| Priority / off | 0.944 [0.942–0.964] | 1463 | 1.472 / 2.705 / 3.236 |
| Priority / on | 1.369 [1.360–1.393] | 1669 | 1.582 / 2.820 / 3.814 |

CPU queue payload: 475494B (4int links+revision+2bool/probe и6planes),
прежняя queue/revision/queued payload~9B/probe, дельта~281KB плюс array headers.
Transport max 8830704B (после cube links cache может расти),
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
