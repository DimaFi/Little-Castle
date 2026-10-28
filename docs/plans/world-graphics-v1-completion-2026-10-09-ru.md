# Генерация и графика мира: план завершения v1

Обновление после реализации: первые этапы получили настоящий streamed concept
baseline, переносимый минимальный арт-комплект и175/175EditMode+5/5PlayMode.
Это не полное закрытие этапов1–5: вода, богатство природы/посёлка, macro startup
и финальные Player budgets ещё открыты. Свежая приёмка и кадры:
[concept-pass-1-5 report](../reports/concept-pass-1-5-2026-10-09.md).
Очередь18 дополнительных задач:
[Sol6High implementation queue](../tasks/chat-world-graphics-queue-2026-10-09.md).

Дата: 2026-10-09. Это план первой законченной версии окружения, не всей игры.
Цель — приятный, читаемый и стабильный процедурный мир в Unity Player:
луга, холмы, каменные уступы, лес, реки, одинаковые каменные мосты,
дороги и пригодные площадки для зданий. Концепты задают направление,
а не обязательную плотность объектов или точную копию картинки.

## Точка отсчёта

- Изолированная сборка desktop baseline + PR26–30 действительно скомпилирована:
  144/144 EditMode и 3/3 PlayMode прошли. Это не визуальная приёмка.
- На seed -10101/1024 m новые дороги восстановили одну связанную компоненту,
  2 fixed bridges. Макропланирование заняло 24.098 s — быстрый старт не доказан.
- Два независимых regression теста выявили оставшуюся ошибку abandoned roads
  и существовавший ранее пробел проверки геометрии. Оба исправлены на отдельной
  ветке `codex/chat-acceptance-fixes-2026-10-09`: новый полный EditMode153/153 PASS;
  повтор -10101/1024 m также VALID (23.953 s,2 bridges,1 component).
  Это закрывает routing regressions, но не весь этап1.
- Шейдеры solid/terrain/foliage/grass/emissive/night-light/distant уже существуют.
  Их наличие не означает, что все реальные модели/текстуры правильно подключены.
- Bridge v002 и его LOD/collision импортированы и проверялись в отдельном fixture.
  Маски дорог/влажности и normal seam stitching прошли data/mesh tests.
- Создание настоящей streamed concept scene в Unity прошло, но Play/GPU/FPS
  для неё ещё не приняты. Файл прошлого диагностического запуска сохранён
  вне Assets; повторяемое создание требует принятой изолированной ветки.
- Часть деревьев/зданий в WorldSpawnCatalog пока placeholders. Зелёные тесты
  не делают их качественными production assets.

Последний независимый аудит: `E:/Games_Develop/verification/chat-review-2026-10-09/REPORT.md`.
Checked boxes в старых roadmap могут отставать: ориентироваться на исходники
и свежие результаты, не запускать заново уже сделанные задачи Chat.

## Порядок работ и видимый результат

| Этап | Работа | Что увидим / условие завершения |
|---|---|---|
| 1. Надёжная карта | Реальные endpoints дорог, retiring failed links, политика river sources и отклонения неподходящих seed | На тестовых картах нет отрезанных поселений, phantom roads/bridges; явный failure вместо плохой карты |
| 2. Один живой участок | Настоящая streamed scene; вода, мост, подходы, луг/склон/лес; одинаковые камеры и время | Первые сравнимые игровые скриншоты, не Blender и не только baked fixture |
| 3. Земля и природа | Реальные текстуры, согласованное русло, уступы/камни, production foliage и маски | Спокойные луга, выразительные скалы, естественные берега, чистые дороги |
| 4. Поселение и свет | Проверенные дома/ворота/стены, площадки, материалы, дневная/вечерняя/ночная палитра | Мини-посёлок без щелей/парения; ночью различимы проходы и строения |
| 5. Масштаб и быстродействие | Bootstrap, локальная генерация, LOD/коллайдеры/свет, quality presets | Камера проходит карту без постоянных рывков, природа не мерцает и не исчезает резко |
| 6. Финальная приёмка v1 | Несколько seed/размеров,2/8/16 player configs, revisits/save delta, GPU Player capture | Повторяемый build и отчёт: данные, картинка, производительность отдельно PASS |

Не ждать всей большой карты ради картинки: маленький реальный участок этапа2
нужен рано. После каждого этапа — один screenshot set + короткий список
подтверждённых изменений и оставшихся проблем. Финальный большой аудит — один раз.

## 1. Корректность генерации — обязательный блок

1. Закрыть оба подтверждённых routing regressions. Компоненты объединяются
   только по дорогам, действительно доходящим до feature anchors. Failed desired
   links не должны портить валидный обход, но disconnected/minimum bridge errors
   нельзя скрывать. Нестрогий/legacy режим сохраняется.
2. Проверить river source eligibility для saved concept settings, особенно
   12345/54321 на1024 m: прежний аудит показал пустые river lists. Не рисовать
   выдуманную реку ради зелёного теста. Зафиксировать для пресетов минимальные
   требования к рекам/мостам; при невозможном seed — понятный отказ либо
   ограниченный воспроизводимый поиск следующего seed с отчётом.
3. Согласовать variable width у carving, SurfaceKind, wetness masks, water mesh
   и fixed bridge stamps. Добавить sharp-bend/confluence regression: текущие
   ближайший-сегмент и любой-сегмент правила различаются.
4. Сохранить мост: длина10.8 m, проход2.86 m, scale1, road+Z/river+X,
   sockets±5.4 m, waterY=base−0.85. Под мостом один общий terrain stamp;
   береговой декор не дублировать global scatter. Флаги на каменных входных
   опорах: по одному справа для входящего с каждого берега.
5. Проверить building footprints, foundation/entrance clearance, ресурсные
   зоны, возможные маршруты выхода из стартов. Не выравнивать/зеркалить весь мир
   и не добавлять emergency resources молча. Карта остаётся естественно неровной.
6. Проверить seed/negative coords/seams/stable IDs после cache eviction;
   закрепить generation-version/settings identity до persistent compatibility.

Критерий: валидатор проверяет реальную геометрию и проходы; неподходящая карта
явно отклоняется. Связность графа не заменяет будущую unit navigation проверку.

## 2–3. Главный визуальный проход

- Принять PR26/28/29/30 только после review; собрать отдельную
  `ConceptProceduralWorld`, не переписывая Main/ConceptBridgeTest.
- Подключить готовые BaseColor луга/земли/троп к LC_Terrain, проверить масштаб,
  mipmaps и повторы. `Concept_Terrain_Masked.mat` пока не имеет назначенных
  texture inputs: маски и палитра — только первый проход, не финальный материал.
- Лесная подстилка/сухая земля/каменные дорожки должны отражать данные SurfaceKind.
  Не включать семь дорогостоящих слоёв сразу; сначала минимальные покрытия,
  потом расширение только при видимой необходимости и замере стоимости.
- Faceted rock outcrops на настоящих scarp bands: высотная сетка сама не даёт
  всех каменных граней/нависаний концепта. Авторские LOD и простые коллайдеры.
  Не менять ресурсные deposits ради декоративных скал.
- Лес группами и просветами; камни/камыши/кусты по берегам; редкие цветы,
  травяные пятна, чистые входы в мост/дом и края дорог. Всё по seed/маскам.
- Production деревья подключать по утверждённому asset intake, начиная с одного
  Oak Kit. До изменения foliage shader — real mesh validator, отдельная листва
  или usable mask, UV/normals/tangents, close/far moving-shadow проверка.
  Bridge dressing grass без Vertex Colors и статический solid материал не
  считать уже подтверждённым ветровым asset. Не переписывать ветер из-за меша.
- Вода: стыки чанков/изгибы/притоки, одинаковая waterline, отсутствие z-fighting,
  читаемый цвет, спокойное движение/блики. Пена/контакт берега — только где
  дают заметный результат; не начинать с дорогой refraction/fullscreen воды.

## 4. Материалы, поселение, свет и приятные детали

- Короткий эталон:3–5 проверенных домов, ворота/стены, колодец, несколько деревьев,
  дорога к реке. Не размножать весь декоративный двор вокруг каждого дома.
  Игрок продолжает ставить свои здания; пример не заменяет placement validator.
- LC_StylizedLit: корректные BaseColor/Normal/AO/Roughness, единый масштаб и
  палитра камня/дерева/штукатурки/кровли. Проверять реальные Blender→Unity
  материалы, не считать FBX переносом сложных Blender node graphs.
- День: нейтрально-тёплый sun, прохладные мягкие тени, читаемые цвета.
  Вечер: тёплый свет без общего оранжевого фильтра.
  Ночь: прохладный, но различимый мир + тёплые окна/фонари.
- Все shared shaders потребляют `_LC_*`; нет независимых day/night clocks.
  Emission остаётся видимой без budget slot. Realtime lights — лишь небольшой
  ближний набор, без теней у повторяемых фонарей; far LOD без lights/probes.
- Цвет игрока: отдельные recolorable accents одежды/флагов через shared material
  и instance properties, одинаковый дизайн между фракциями. Все персонажи
  различимы по принадлежности; traveling merchant нейтральный белый/кремовый.
  Это контракт для будущих персонажей, не требование закончить их весь gameplay
  до готовности карты. Бесхозным мостам не назначать владельца автоматически.
- Только после базы: мелкая анимация флагов/листвы/воды, спокойный дым,
  редкие частицы, контактное затемнение, атмосферная даль. Не по скрипту на
  каждый лист/травинку. DOF/bloom/color grading/AO — опциональные A/B после профиля.
- Built-in сохраняем. URP/HDRP, Gamma→Linear, parallax/displacement не менять
  попутно: нужны отдельные сравнительные кадры и измеренное преимущество.

## 5. Быстродействие и качество

Предварительная цель:60 FPS при1080p; целевой класс ПК ещё надо подтвердить.
Это рабочая цель, НЕ достигнутая гарантия. Player measurements важнее Editor FPS.

Измерять отдельно: MacroWorldPlan, start selection, MATCH READY/prewarm,
first-visit chunks, p50/p95/p99/max frame CPU/GPU, GC allocations, memory trend,
triangles/draw calls/materials, shadow/ForwardAdd/foliage overdraw.
GC.GetAllocatedBytesForCurrentThread даёт0 в прежнем Unity audit: без проверки
счётчика нулевые значения нельзя объявлять отсутствием аллокаций.

Известные кандидаты: дорогой WorldTerrainProbe, синхронный macro startup,
O(N²) discovery в alternative connectivity repair, masks/stitching cost,
object instantiation, густая трава/лес, colliders и overlapping lights.
Исправлять измеренный bottleneck, не переписывать всё в Jobs/Task.Run.

Нужны ясные bootstrap состояния/progress/cancellation и prewarm только стартов.
Большая карта не должна материализоваться целиком. Low/Medium/High меняют
детали представления, траву/тени/LOD, но не состав игровых деревьев/ресурсов
и не seed/авторитетные данные. Дальние LOD сохраняют силуэт без дорогих теней,
probes, мелкого ветра и сложных коллайдеров.

Кромку конечной карты скрыть terrain padding и атмосферной далью, без щелей
и проходимого пространства за playable bounds. Fog of war — отдельный renderer
поверх авторитетных Hidden/Explored/Visible, не маска, определяющая генерацию.
Перед игровой/multiplayer приёмкой проверить visibility filtering и minimap,
чтобы картинка не раскрывала чужие юниты через ещё не разведанную область.

## Зависимости и владение будущих точечных задач

```text
A correctness ─┐
B river contract ├─> D real streamed scene ─> E materials/content ─> F GPU acceptance
C bootstrap cost┘               │                    │                  ↑
                                └────────────────────┴─> G measured optimization
```

Перед запуском каждой задачи проверить свежий HEAD/PR и точные изменения.
Следующее — границы для назначения, не команда запустить всех одновременно.

- A: MacroWorldPlanner, RealizedRoadConnectivityRepair, WorldRouteConnectivityValidator
  и их focused tests. API additive; диагностические counters сохранить.
- B: RiverNetworkPlanner/Settings, RiverTerrainCarvingStage, TerrainSurfaceStage,
  focused river tests. River width/depth/stable-ID contract согласовать с D.
- C: WorldTerrainProbe и отдельные timing/identity tests. Sampling API/output frozen.
- D: WorldStreamer, scene builder, новая procedural scene, finite profile integration,
  masks/normal seams/water presentation и lifecycle tests. Один владелец scene/config.
- E1: approved production exports/prefabs/materials/asset manifests, не generation code.
  E2: shared shader fixes только после real asset preflight, свои shader-specific tests.
  E1/E2 одновременно не меняют один материал/mesh/shader без переуступки.
- G: сначала отдельный profiler report; право менять bottleneck files назначается
  заново после согласования с A–E. Не выдавать WorldStreamer двум агентам.
- F/root: Unity launches, assembled-state review, GPU screenshots, benchmark matrix,
  shared docs, Git integration. Единственный владелец запуска Editor и сборки.

Каждое поручение перечисляет exact writable paths/classes, допустимые публичные
контракты, зависимости, минимальные тесты и report. Чужие файлы запрещены без
переуступки.5.6 Sol Extra High — небольшие сложные задачи; массовый аудит/много
агентов не нужны. Веб-задача сама не устанавливает модель/режим исполнителя.

## Полная приёмка окружения v1

- Несколько фиксированных seed (12345,54321,-10101,777) и допустимых размеров:
  valid либо воспроизводимое объяснение отказа, не замолчанные ограничения.
-2/8/16 player configs там, где preset допускает; это не доказательство реального
  multiplayer networking. Стартовые площадки/выходы/ресурсы проверяются отдельно.
- Полный unit/integration pass после завершённой сборки; negative borders,
  load-order/unload-return, delta persistence, отсутствие накопления мешей/объектов.
- Реальный Player: одна и та же сцена/seed/камера в day/sunset/night,
  close/gameplay/far; луг, лес, уступ, берег, мост, поселение.
- Визуально: нет открытых щелей, плавающих объектов, кривых подходов,
  шумной травы, заметных material seams и нечитаемой ночи.
- Performance budget фиксируется на целевом ПК и проходит повторный маршрут.
  Warm/cold startup различаются; полная карта заранее не прогревается скрыто.
- QA report содержит точные SHA/build/settings, evidence и отдельные статусы
  CODE/DATA/VISUAL/PERF. Umbrella задачи не закрывать только по зелёным NUnit.

Вне этой v1: водяные мельницы, бесконечный мир, полноценная экономика/боёвка,
все будущие здания/жители, сезоны/сложная погода. Карта и materials должны
принимать будущие ассеты без переработки генератора, но их отсутствие не
нужно скрывать обещанием «графика полностью закончена».
