# GI, этап1Б: ускорение пересечений и видимости — 26.10.2026

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
| Первое заполнение | 21609 | 4.701 [4.666–4.712] | 2.765 [2.729–2.948] | 546.97 → 309.28 |
| Отражатель: серый | 8019 | 0.031 [0.030–0.031] | 0.031 [0.031–0.031] | 2.12 → 2.17 |
| Отражатель: возврат | 8019 | 0.033 [0.033–0.033] | 0.033 [0.033–0.033] | 2.18 → 2.14 |
| Удаление крыши | 9801 | 1.532 [1.532–1.532] | 0.831 [0.815–0.897] | 185.90 → 98.55 |
| Undo крыши | 9801 | 1.955 [1.951–1.967] | 0.934 [0.933–0.936] | 239.97 → 112.63 |
| Закат | 21609 | 0.701 [0.685–0.702] | 0.335 [0.335–0.350] | 83.84 → 40.05 |
| Ночь | 21609 | 0.651 [0.651–0.653] | 0.300 [0.300–0.301] | 77.73 → 34.48 |
| День | 21609 | 0.684 [0.669–0.750] | 0.301 [0.300–0.302] | 81.07 → 35.14 |

After максимум solve+upload кадра среди всех сценариев/запусков: 2.470ms.
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
| Before / GI off | 0.859 [0.854–0.859] | 1515 | 1.347 / 2.331 / 2.729 |
| Before / GI on | 1.274 [1.256–1.274] | 1727 | 1.363 / 2.250 / 2.565 |
| After / GI off | 0.854 [0.853–0.858] | 1577 | 1.255 / 2.032 / 2.563 |
| After / GI on | 1.281 [1.264–1.286] | 1659 | 1.329 / 2.460 / 2.666 |

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
