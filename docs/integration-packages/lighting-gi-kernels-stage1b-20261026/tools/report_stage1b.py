from pathlib import Path
import json

root=Path(__file__).resolve().parents[2];data=Path('E:/Games_Develop/Research/SpatialIndirectStage1B_20261026');j=json.loads((data/'stage1b-summary.json').read_text('utf-8'));assert j['pass_']
names=['Первое заполнение','Отражатель: серый','Отражатель: возврат','Удаление крыши','Undo крыши','Закат','Ночь','День']
rows=[]
for name,row in zip(names,j['updates']):
    a,b=row['Before'],row['After'];wa,wb=a['wallMs'],b['wallMs']
    rows.append(f"| {name} | {row['probes']} | {wa['median']/1000:.3f} [{wa['minimum']/1000:.3f}–{wa['maximum']/1000:.3f}] | {wb['median']/1000:.3f} [{wb['minimum']/1000:.3f}–{wb['maximum']/1000:.3f}] | {a['cpuMs']['median']:.2f} → {b['cpuMs']['median']:.2f} |")
steady=[]
for x in j['steady']:
    v=x['gpuMeanMs'];steady.append(f"| {x['mode']} / {'GI on' if x['indirect'] else 'GI off'} | {v['median']:.3f} [{v['minimum']:.3f}–{v['maximum']:.3f}] | {x['samples']} | {x['frameMedianMs']['median']:.3f} / {x['frameP95Ms']['median']:.3f} / {x['frameP99Ms']['median']:.3f} |")
text='''# GI, этап1Б: ускорение пересечений и видимости — 26.10.2026

Сходимость прежнего стенда ускорена без изменения поля, лучей, палитры и shaders.
Родитель: `98da06954c1d8c027d275ea80f8300c8b65ff7e9`. Это следующий отдельный
исследовательский пакет, без активации GI/Linear в основной игре.

## Изменение

- Candidate nearest-hit kernel рано отбрасывает несовместимые интервалы X/Y/Z;
  создаёт итоговую hit-position после выбора ближайшего объёма.
  Сохранены finite inverse, строгий конец луча, inside-exit и порядок равных граней.
- Shadow/sky visibility прекращает поиск на первом препятствии: для этих запросов
  не требуется ближайшая позиция/нормаль. Старые serial `Trace`, `Slab`, `Solve`
  остаются независимым эталоном, без переписывания.
- При **точно нулевом** цвете солнца/луны его direct visibility не вычисляется.
  Переключение zero/nonzero повышает lighting epoch даже при прежнем направлении;
  существующие transport weights не используются как видимость включённого света.
- 72 исходных cosine sky directions и inverses вычисляются один раз для шести
  граней. Формула и12 samples/face прежние; нет квантования направления.
- Сборщик выбирает отдельный output по `-giStage1B`; сохранённую сцену не пересоздаёт.

## Проверки точности

197/197 Edit Mode,6/6 Play Mode. Четыре новые проверки:24000 детерминированных
лучей с thin boxes/negative coordinates/почти параллельными направлениями и ignore;
inside-exit/ties/строгие endpoints; zero→nonzero освещение с той же direction после
geometry edit; surface hemispheres всех шести граней. Существующий warm-allocation
тест с positive control проходит. Это не отдельная диагностика GC всего Player.

Три пары Release:24 сравнений полей по21609 проб ×16 float. Maximum absolute
coefficient error к опубликованному cached Player **0**; отдельно24 сравнения с
сохранённым serial reference:5.0663948059e-7 при tolerance1e-5, безNaN/Inf.
Before Player сверен целиком со всеми хешами из опубликованного Stage1 evidence-index.
27 image pairs (54 реальных PNG): maximum image MAE0.263/255. PNG не побитово
равны из-за обычного SSAO noise; нулевая ошибка относится к float field.
Runtime-errors пусты, все6 запусков exit0. Обе тестовые сессии и сборка exit0;
build Succeeded/0errors/1 прежний obsolete API warning.

## Измерения

RTX4070 Ti SUPER, i7-12700F, Unity6000.5.5f1/URP17.5, Windows D3D12,
Release1920×1080, Linear/HDR/Neutral, MSAA4, SSAO, soft shadows2048/2cascades/90m.
Before — неизменённый опубликованный Stage1 Player, After — новая сборка.
Порядок:Before/After, After/Before, Before/After. Ни Editor, ни другой наш benchmark
параллельно этим6 запускам не выполнялись; пользовательские приложения не изолировались.

Поле49×9×49,1m,128Fibonacci primary rays,6m range, те же6 AABB и native Meadow/Oaks.
Solve budget прежний2ms, cached cap8192. Upload/command не входят в solve budget.
Это кооперативный бюджет, а не жёсткая гарантия каждого кадра.
Workload/geometry builds/environment replays/payload одинаковы в каждой паре.

| Сценарий | Проб | Before: wall median [min–max], s | After: wall median [min–max], s | CPU solve median, ms |
|---|---:|---:|---:|---:|
'''+ '\n'.join(rows)+'''

After максимум solve+upload кадра среди всех сценариев/запусков: '''+f"{max(r['After']['maxUpdateMs'] for r in j['updates']):.3f}"+'''ms.
Для roof removal:2977 primary builds и6012 secondary replays, очередь9801;
оставшиеся solid cells не создают transport. Измеряется полная сходимость,
**не время первого видимого ответа**. Стандартная малая правка и видимая очередь
в этом пакете не реализованы; целевые0.25/0.5s для них не объявляются пройденными.

Steady:VSync/FPS limit off,120warmup+600 ordinary endCameraRendering intervals на
режим, без readback, PNG, dumps или solver work. Таблица содержит median трёх
per-run means GPU и min/max, а не объединённую среднюю всех samples. Frame columns —
median трёх значений median/p95/p99. DXGI предупреждает о swapchain timestamps;
Stopwatch intervals не являются измерением display/input latency.

| Режим | GPU mean: median [min–max], ms | GPU samples | Frame median / p95 / p99, ms |
|---|---:|---:|---:|
'''+ '\n'.join(steady)+'''

Этот пакет ускоряет CPU update, а не steady GPU. Колебания GPU/кадров не выданы
за результат новых kernels: shared shaders и GPU алгоритм не менялись.

Диагностический intrusive Editor profile After:primary157.3ms,shadow25.9ms,
sky51.3ms,projection27.4ms; весь plain Editor solve301.3ms. В прежнем сохранённом
профиле primary309.0ms,shadow72.5ms,sky83.2ms,plain532.7ms. Это одиночные Editor
прогоны с разными накладными расходами таймеров, не замена Release таблице.
Primary1914112 и sky171792 rays неизменны; shadow200099→183400;
box tests3631907→3485779 благодаря any-hit и inactive source.

Transport payload max8786312B (~8.38MiB) и GPU buffer1382976B сохранены.
Две статические таблицы72×Vector3 добавляют1728B payload на процесс плюс array
headers; они не умножаются на число проб. Managed dictionary/array headers extra.
OS/VRAM peak заново не измерялись. Release GC bytes counter не проходит прежний
positive control и остаётся unsupported/-1; не трактовать это как0B allocation.
Предыдущий Development GC.Alloc протокол относится к Stage1, не к новой сборке.

## Границы и перенос

Изменены4 существующих C# файла: Trace, Transport, TransportTests, Stage1Build.
Их `.meta`/GUID сохранены; новые runtime assets отсутствуют. Shared7 shaders,
поля сцены, материалы, reference solver/queue, Volume/Review и3ProjectSettings
остались прежними. MAIN Assets/Packages/Settings сверены без drift.
URP-копия карты, её tools/scenes/save/apps не изменялись. Новых Meadow snapshot
в этом проходе нет: source GUID/SHA наследуются от предыдущих пакетов.

Точные before/current SHA256: `docs/lighting-integration/stage1b-manifest.json`;
before snapshot11 файлов: `stage1b-baseline-98da069/`; audit:`stage1b-audit.json`;
patch:`gi-98da069-to-stage1b.patch`. Это delta поверх Stage1, применять по функциям,
не заменять whole-file divergent shaders или новые map-authoring runtime.

МодельGI прежняя:fixed field,flat ground proxy,6AABB/max8,L1,один diffuse bounce,
finite range; canopy/real terrain/local emitters не добавлены. Geometry update
ещё0.83–0.93s; первичное заполнение2.77s. Visible priority, версия результата,
crossfade, continuous WorldTime injection и production activation впереди.

По просьбе пользователя в следующий совместный визуальный стенд добавить готовые
модели для оценки вживую: архитектура/камень/дерево/крыша/пропсы из согласованных
ассетов, оставив диагностические куб, стены и крышу. Одни световые profiles и
поле для обеих зон; переключение камер и day/sunset/night, GI on/off и SSAO on/off.
Набор моделей не добавлялся в текущий performance A/B, чтобы сохранить нагрузку.
Действующие approved Oak A/B/C/D и native Meadow уже присутствуют.

## Артефакты и воспроизведение

`E:/Games_Develop/Research/SpatialIndirectStage1B_20261026`: Final01..03Before/After,
updates/benchmarks/transport-checks/result,24comparison fields и serial crosschecks,
XML,profile/build logs,54PNG,48probe dumps,Player,summary и сравнения.
`stage1b-summary.json` содержит сырые min/max/median,полные player-file SHA256.
Большие binaries/dumps остаются локально с SHA-index; пакет содержит код, patch,
hash manifests, raw JSON/XML, contact sheets, report и план.

1. В integration-копии запустить EditMode/PlayMode через Unity Test Runner.
2. Batch `LittleCastle.Editor.SpatialIndirectStage1Build.BuildLinear -giStage1B`.
   License IPC требует обычного Unity доступа; builder восстанавливает settings.
3. `Tools/LightingStudy/run_stage1b.ps1 -Repeats3` запускает сохранённый Stage1 и
   новый Player; не перезаписывает существующие Final каталоги.
4. `python Tools/LightingStudy/summarize_stage1b.py`, затем `audit_stage1b.py` и
   `report_stage1b.py`. Для повторной серии выбрать новый root/prefix в scripts
   явно, сохранив старые измерения.
'''
(root/'docs/reports/lighting-gi-kernels-stage1b-20261026-ru.md').write_text(text,'utf-8')
section='''## Обновление после этапа1Б — 26.10.2026

Ускорены primary intervals и any-hit shadow/sky visibility, precomputed72 sky
directions, exact-zero source gating с корректным zero→nonzero epoch.
197/197 Edit Mode,6/6 Play Mode,3 Release пары;24 full fields совпали побитово
с опубликованным cached Stage1, serial error5.0664e-7. Та же сцена/лучи/2ms budget.
Median:initial4.701→2.765s;roof removal1.532→0.831s;Undo1.955→0.934s;
sunset0.701→0.335s;night0.651→0.300s;day0.684→0.301s. Albedo~31ms как прежде.
Transport payload прежний~8.38MiB плюс overhead; static directions+1728B.
Отчёт:`docs/reports/lighting-gi-kernels-stage1b-20261026-ru.md`.

Следующий шаг1В — **приоритет видимых проб и стандартизированная малая правка**:
измерить first visible response/visible convergence отдельно от полного поля;
сохранить starvation bound для невидимой области и stale-work gate. Полная roof
сходимость ещё выше0.5s; не поднимать2ms budget ради целевого показателя.
После этого этап2:согласованные версии, плавное обновление и continuous WorldTime.

### Следующий визуальный стенд: готовые модели и диагностическая зона

Уточнение пользователя26.10:в будущем поставить часть готовых моделей для живой
оценки света, **оставив куб со стенами** для экспериментов. Добавлять это отдельным
сценовым изменением после текущего A/B, сохранив прежний benchmark без изменений.
Диагностическая зона:куб/тонкая стена/крыша/отражатель. Вторая зона:несколько
согласованных production моделей,разные камень/дерево/крыша и крупные/мелкие формы;
approved Oaks A/B/C/D и Meadow остаются частью окружения. Временный WorldSpawnCatalog
ради стенда не мигрировать. Выбор конкретных ещё не утверждённых моделей фиксировать
перед переносом, с GUID/SHA и source snapshot; реальные сетки проверять по AGENTS.
Общий набор day/sunset/readable night и cameras close/overview;GI/AO toggles,
ветер/LOD и shadow alignment. Не подменять этими моделями чистый кубический тест.
Это план, в пакете1Б новые модели не переносились.

'''
plan=root/'docs/plans/lighting-next-stages-20261026-ru.md';old=plan.read_text('utf-8');marker='## Обновление после этапа1Б — 26.10.2026'
if marker in old:old=old[:old.index(marker)]+old[old.index('## Обновление после первого прохода этапа1'):]
old=old.replace('## Обновление после первого прохода этапа1',section+'## Обновление после первого прохода этапа1',1)
start=old.index('## Следующий конкретный пакет работ');end=old.index('Tiny Glade остаётся ориентиром.',start)
old=old[:start]+'''## Следующий конкретный пакет работ

Этап1В:приоритет видимой области и малая commit-правка с численными/latency
проверками при прежних2ms. Затем версионирование/плавные сутки,после — committed
snapshot актуального рельефа/объектов и отдельный визуальный стенд готовых моделей.
Сохранить диагностический куб/стены/крышу. Не смешивать смену палитры/геометрии
с очередным чистым before/after замером kernels. Новый editor не блокируется.

'''+old[end:];plan.write_text(old,'utf-8')
handoff=Path('E:/Games_Develop/Little-Castle/docs/handoffs/lighting-to-map-authoring-20261025-ru.md');h=handoff.read_text('utf-8');marker='## Этап1Б: ускорение kernels GI — 26.10.2026'
if marker in h:h=h[:h.index(marker)].rstrip()+'\n'
h+='\n'+section.replace('## Обновление после этапа1Б — 26.10.2026',marker)+'''
Before/current SHA, audit и patch:stage1b-manifest.json,stage1b-audit.json,
gi-98da069-to-stage1b.patch в docs/lighting-integration lighting-копии.
Новых Meadow/mesh зависимостей нет, GUID существующих C# сохранены,7shared shaders,
сцена, материалы и3ProjectSettings неизменны; MAIN game files без drift.
Raw evidence:E:/Games_Develop/Research/SpatialIndirectStage1B_20261026.
Отдельный пакет:docs/integration-packages/lighting-gi-kernels-stage1b-20261026/;
ветка codex/lighting-gi-kernels-stage1b-2026-10-26. Публикация подтверждается
локальным publication-gi-stage1b/publication-receipt.json после remote проверки.
URP editor не менялся и не ожидает GI. Его новый height/tree snapshot ещё не перенесён.
''';handoff.write_text(h,'utf-8');print('Report, next-stage plan and MAIN handoff updated.')
