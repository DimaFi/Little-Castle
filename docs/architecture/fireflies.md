# Светлячки — архитектура и практическая проверка (Issue #85)

Статус: **черновая реализация, Unity/GPU и визуальная приёмка НЕ ВЫПОЛНЕНЫ**. Ветка изолирована от main. Ни один FPS/CPU/GPU показатель не измерялся в настоящей сцене.

## Выбор архитектуры и причины

- Данные находятся в `MapAuthoringDocument.fireflies`, `MapEditSave.fireflies`, `SurfacePaintData.fireflies`. Runtime `MapEditSession` проверяет ввод и владеет правками, общими Undo/Redo, `Changed`, экспортом полного авторского документа и v7 delta save.
- Отрисовка `FireflyWorldRenderer`: переиспользуемый quad, shader-driven движение/мерцание, `DrawMeshInstanced` по пространственным ячейкам 32×32 м и GPU depth-test. Без GameObject, Update, raycast или Light на каждого светлячка.
- `FireflyDistribution.Hash`: стабильный seed, абсолютные X/Z (включая отрицательные). Районные overrides разрешаются детерминированно: исключения, старший priority, лексикографический ID, плавное взвешивание; вычисление плотности O(число областей), не O(n²).
- Локальные области: эллипс/повёрнутый эллипс или полигон, имена, seed/ID, активность, приоритет, исключить/наследовать/множитель/задать, цвет и falloff. Работа из World Editor (в разделе «Биомы и снег»), перемещение в SceneView при выключенной кисти. Вершины полигона и размеры редактируются численно.
- Свет излучения не равен реальному освещению. `FireflyClusterLightPool` создаёт максимум два **общих** Point Light, без теней, с низким приоритетом -100, через существующие `NightLightEmitter` / `NightLightBudgetManager`; отсутствующий менеджер => ни одного источника.
- Shader опирается на имеющиеся `_LC_NightAmount` и `_LC_Wind`, не запускает собственный clock/экспозицию. Additive core/halo, near fade, optional depth fade, ограничение projected size, чёрный fog. Отражённые камеры отключены по умолчанию.
- `MapChangeKind.Fireflies` допускает локальную инвалидизацию только затронутых cells при изменениях высот/объектов/рек. Большие изменения конфигурации вызывают full invalidation только собственных light data, а не террейна/воды.

### Почему не Particle System / VFX Graph прямо сейчас

В репозитории нет VFX Graph в manifest (согласно ТЗ), добавление зависимости без сравнения рискует ухудшить совместимость. Instanced quads — **исходная гипотеза**, а не доказанный победитель: для объективного выбора ещё предстоит сравнить в основной карте 1k/10k/50k кандидатов против ограниченного Particle System с одинаковыми ракурсами и профилями; GPU overdraw и Bloom могут оказаться ограничителями. Визуальное качество настоящего движения тоже пока не подтверждено.

## Параметры и стоимость

Значения ниже — **начальные настройки кода**, не профилированные рекомендуемые бюджеты. `Map` — сохраняется в карте; `Area` — локальная настройка, `Runtime` — качество конкретного клиента, `Inspector` — компонент сцены/World Editor. Изменения данных карты создают undoable-команду; изменённые клетки строятся ограниченными порциями при следующем рендере. Настройки качества влияют при следующем кадре.

| Настройка | Единица / диапазон / default | Сфера, эффект, производительность |
|---|---|---|
| enabled | bool / true | Map; отключает мир и близкий слой |
| seed | int / 73421 | Map; новые координаты при смене, полная инвалидизация |
| densityPer100m2 | огни/100 м² / 0–80 / 5 | Map; линейно влияет на desired и заполнение GPU |
| globalDesiredCap | экземпляры / 0–50000 / 50000 | Map; предохранитель всех видимых |
| perAreaDesiredCap | экземпляры / 0–20000 / 20000 | Map; **в текущем коде дополнительно ограничивает cell**, точного per-area total cap пока нет |
| cellDesiredCap | экземпляры/cell / 0–2000 / 512 | Map; реально ограничено ещё instancing batch ≤1023 |
| minimumHeight / maximumHeight | м / 0–100, default .5/3.5 | Map; высота центра над грунтом или поверхностью воды |
| maximumSlopeDegrees | ° / 0–90 / 42 | Map; отсеивает крутой рельеф |
| color, colorVariation | HDR color; 0–1 / .16 | Map; тёплая палитра и естественный разброс |
| glow | множитель / 0–4 / 1.3 | Map; яркость emission/Bloom, увеличивает видимый overdraw |
| worldSize | м / .01–1 / .14 | Map; размер/экранный overdraw, ограничен также shader pixel bounds |
| motionAmplitude | м / 0–2 / .55 | Map; плавное GPU-движение |
| motionSpeed | скорость/с / 0–4 / .65 | Map; неодновременный полёт |
| flickerStrength | 0–.5 / .23 | Map; амплитуда мерцания без стробоскопа |
| windInfluence | 0–2 / .15 | Map; слабая зависимость от общего ветра |
| minimumNightAmount | 0–1 / .08 | Map; настройка входа эффекта по общему дню/ночи |
| near enabled | bool / true | Map; отдельное включение near |
| nearMode | NaturalAreas/Everywhere / NaturalAreas | Map; разрешённость near |
| desiredNearCount | экземпляры / 0–48 / 8 | Map; отдельное количество у камеры |
| nearRadius | м / 1–50 / 7 | Map; близкий 3D-объём |
| nearCameraMinHeight/MaxHeight | м / 0 и 22 | Map; высота камеры над поверхностью |
| area id/name/seed/priority | строки/int/0 | Area; устойчивые ID, порядок |
| area shape/center/radius/angle | ellipse/polygon, X/Z м, ° | Area; местоположение/геометрия |
| area falloff | 0–1 / .35 | Area; переход density/color |
| area densityMode/value | режим + 0–100 / Multiply(3) | Area; перезаписывает/умножает/исключает |
| area overrideColor/color | bool + color / off | Area; локальная палитра через instanced property |
| preset | Off/Low/Medium/High/Custom / Medium | Runtime; Low 450/0/55 м, Medium 1800/8/95 м, High 6500/24/155 м; Custom 6000/12/95 м |
| graphicsVsFps | 0–1 / .5 | Runtime; бюджет экземпляров ×(.4–1.6), дистанция ×(.75–1.2), near cap |
| autoAdjustToFrameTime | bool / false | Inspector; при Play опционально понижает/повышает бюджет после EMA с гистерезисом |
| targetFramesPerSecond | кадры/с / 30–144 / 60 | Inspector; цель автоматической адаптации, не гарантия FPS |
| minimumAdaptiveScale | 0.3–1 / .55 | Inspector; нижняя граница авто-качества |
| maxCellsBuiltPerFrame | cells / 1–24 / 4 | Inspector; предотвращает одномоментный rebuild |
| softDepthFade | bool / true | Inspector; shader depth sample, дополнительная GPU стоимость |
| allowReflectionCameras, reflectionCap | bool false, 0–32 / 0 | Inspector; отражение без повторной симуляции и отдельным бюджетом |
| enableRealtimeLighting | bool / true | Light pool; true создаёт до 1/2 источников только на Medium/High при наличии общего менеджера |
| mediumLights / highLights | источники / 0–2 / 1 и 2 | Light pool; только малое число общих Point Lights |
| intensity/radius | световая intensity 0–4/.42; радиус м 1–16/5 | Light pool; качество света и стоимость освещения пикселей |
| updateInterval/repositionSeconds | секунды .1–3/.65, .1–5/1.2 | Light pool; CPU выбор/плавность перемещения |
| selectionPriority | int / -100 | Light pool; фонари с большим приоритетом выигрывают общий бюджет |

Публичная диагностика `FireflyWorldRenderer`: desired / visible world, visible near, resident cells, ожидающие cells, world cap hit, текущий adaptiveScale. Доступны в World Editor/Inspector. Отчёта DrawCalls, GPU ms и bytes GPU из Unity Profiler **пока нет**.

## Проверка данных и безопасность

1. Открыть `Little Castle > Map > World Editor`. В разделе «Биомы и снег» создать эллипс и полигон; менять falloff/цвет/множитель/исключение, проверять SceneView и Undo/Redo. При `brush=false` у области доступна стрелка PositionHandle.
2. Сохранить JSON через «Сохранить JSON…», открыть повторно. Убедиться, что `environment/rivers/rockSections/trees` совпадают побайтно с baseline (за вычетом ожидаемой сериализации). Проверить `ApplyDocument` и повторное открытие проекта.
3. В Play Mode проверить `MeadowSurfacePainter.Session`, наличие `FireflyWorldRenderer`, `FireflyClusterLightPool`, `NightLightBudgetManager`, URP shader; в Editor профилировать и смотреть отладочную статистику.
4. Проиграть полный день→сумерки→ночь→рассвет. Для low стратегической и близкой свободной камеры проверить depth fade, непрозрачный дом, отражения, дальний лес, ночь и Dawn.
5. Переключать Off→Low→Medium→High→Custom и ползунок FPS/Графика, затем Play/Stop и domain reload. Проверить отсутствие оставшихся GPU-материалов/созданных Lights.
6. Запустить `LittleCastle.Tests.FireflyMapTests` в Unity Test Runner вместе с существующими EditMode/PlayMode. Дополнительно провести 20 с реального движения для каждого пресета и сравнить равные ракурсы с отключённым эффектом.

## Открытые ограничения (не закрывать #85 до исправления)

- Не проведено обязательное сравнение instanced quads / Particle System / VFX Graph в основной сцене; профилировщик Unity недоступен в текущем облачном окружении.
- Нет измерений CPU/GPU time, p95/p99, draw calls, overdraw, memory, GC Alloc, отдельных GPU платформ; невозможно подтвердить цели 0.2/0.4/0.6 мс CPU и 0.5/1/2 мс GPU при 1080p.
- Пересечение плотностей в приоритете deterministically resolved, **точный cap каждой области** не реализован, и отсутствуют dedicated far aggregate LOD, GPU indirect и отдельный экранно-площадной cap.
- Нет отдельного **дешёвого ground light-пятна**: только emissive и до двух realtime lights. Взаимодействие со стенами/крышами проходит через depth test, но защищённость под зданиями зависит от `placementExclusions`; требуется проверить на реальных моделях.
- Нет весов биомов, близости к деревьям/дорогам, исключений по погоде, осадкам, мокроте и ветровой анимации всех сцен; нет полноценного пользовательского меню качества вне World Editor/компонента.
- Существующий Shader soft depth использует camera depth texture — необходимо подтвердить, что Renderer Feature/URP Asset генерирует depth во всех необходимых камерах.
- Нет теста плавного перехода near при резком телепорте/развороте, возможны смены отдельных огоньков при пересечении spatial cell.
- Данные `FireflyMapData` доступны по v7 save, но нет отдельного сетевого транспорта/policies клиента; требуются PlayMode + EditMode тесты всей совместимости.

Система **не должна переводиться в main без Unity compile, тестов и визуального прогона**. Не установлены сторонние пакеты, неизвестные лицензии не использовались.
