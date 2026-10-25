# Implementation details — atmosphere integration contract

## 1. Проверенный контекст проекта

- Unity 6000.5.5f1, **Built-in Render Pipeline**; в GraphicsSettings m_CustomRenderPipeline равен нулю, URP в Packages/manifest.json отсутствует.
- Текущая камера стратегии направлена вниз и приближает от приблизительно 11 до 190 единиц; это перспективная камера.
- DayNightLightingController управляет Directional Sun и Moon, процедурным skybox, Trilight ambient и RenderSettings.fog.
- StylizedLightingGlobals производит _LC_SunDirection, _LC_SunColor, _LC_MoonDirection, _LC_MoonColor, _LC_Daylight, _LC_Twilight, _LC_NightAmount, _LC_Wind и _LC_GameTime.
- LC_StylizedLit, LC_Foliage, LC_Terrain и другие текущие материалы уже поддерживают освещение и штатный туман.
- NightLightBudgetManager управляет ограниченным пулом локальных Point/Spot lights и остаётся неизменным.
- По текущей архитектурной документации пространство цвета — Gamma; переход в Linear нельзя делать побочно.
- Fog of War — отдельная **авторитетная** игровая система; этот модуль не влияет на видимость юнитов в симуляции или сохранениях.

## 2. Разделение ответственности

~~~text
WorldClock (авторитетное время)
  -> WorldTimeSystem
     -> DayNightLightingController: солнце, луна, небесные градиенты,
        Trilight + существующий RenderSettings.fogColor
     -> StylizedLightingGlobals: общие GPU параметры _LC_*
  -> Perspective Camera: цвет + Depth Texture
     -> LCAtmosphereRenderer: выбирает профиль и максимум 4 fog spheres
        -> LC_AtmosphereFog.shader: sample geometry depth,
           восстановление мировых лучей,
           интегрирование плотности и солнечного рассеяния
     -> final frame (чисто графический эффект)
~~~

Шейдер не создаёт и не двигает солнце; он читает текущее положение из глобальных данных. Контроллеру освещения не нужно узнавать о новом эффекте.

## 3. Восстановление расстояния по Depth Texture

Camera.CalculateFrustumCorners вычисляет четыре луча камеры в координатах view-space, затем TransformVector переводит их в world-space. В шейдере билинейная интерполяция по screen UV даёт луч viewRay.

Глубина LinearEyeDepth — это расстояние вдоль оси камеры, НЕ настоящая длина косого луча.

~~~text
worldRay = normalize(bilerp(cornerBL, cornerBR, cornerTR, cornerTL, UV))
distance = LinearEyeDepth(rawDepth) / max(0.0001, dot(worldRay, cameraForward))
distance = min(distance, MaxRayDistance)
~~~

Если depth равен дальней плоскости — не изменяем пиксель процедурного неба. Если объект слишком близок, не тратим GPU на fog loop. Для прозрачных объектов, не записывающих глубину, могут возникнуть неправильные наложения на фоне: это ожидаемое ограничение постпроцесса.

Проверить геометрию и платформенные UV в Frame Debugger на DirectX/Vulkan, особенно Unity UV_STARTS_AT_TOP, reversed-Z и сглаживание края гор.

## 4. Физически мотивированная, но упрощённая модель

Для каждого семпла луча на расстоянии t:

~~~text
position = cameraPosition + viewRay * t
distanceTerm = distanceDensity
heightTerm = heightDensity * exp(-max(0, position.y - heightBase) / heightFalloff)
noiseTerm = lerp(1, 0.5 + worldSpaceValueNoise(position.xz, wind, time), noiseStrength)
volumeTerm = sum(4 nearest spheres * smooth falloff)
density = max(0, (distanceTerm + heightTerm) * noiseTerm + volumeTerm)
~~~

В отличие от тумана, рассчитанного только от расстояния, такой эффект сгущается в низких долинах. Ветровой шум берёт direction/strength из _LC_Wind и presentation time из _LC_GameTime. Не используем UnityEngine.Random для процедурных физических параметров или игровой логики.

Для каждого участка длины ds:

~~~text
Tstep = exp(-density * ds)
accumulatedLight += totalTransmission * (1 - Tstep) * scatterColor
totalTransmission *= Tstep
finalColor = originalColor * totalTransmission + accumulatedLight
~~~

Это закон Бугера–Ламберта–Бера с дискретным передним интегрированием. Шейдер делает от 4 до 24 выборок на пиксель (по умолчанию 12). Плотность не бывает отрицательной; считаем в мировых единицах с осторожными параметрами.

## 5. Свет и реальные ограничения

Текущее scatterColor вычисляется из цвета действующего RenderSettings.fogColor плюс солнечный вклад.

~~~text
sunFacing = pow(max(0, dot(viewRay, currentSunDirection)), forwardExponent)
scatterColor = fogColor + sunColor * sunFacing * sunScatterStrength
~~~

Это художественный **forward-scattering glow**, а не физически точная фазовая функция Хеньи–Гринштейна и НЕ настоящие лучи за деревьями: вторичный луч от точки тумана до солнца и тень в нём не проверяются. Подсветка тумана ближайшими 4 уже активными NightLightEmitter Point Light теперь реализована как локальное затухающее приближение без проверки теней. Отдельные эмиссивные окна без Light по-прежнему не рассеивают свет в тумане; настоящая froxel light injection и тени не реализованы.

Непрямое освещение зданий всё ещё вычисляется существующим stylized ambient + локальными источниками, а не тем же решением, что у Tiny Glade. Для объёмного светового следа, тумана от фонарей и GI нужна дополнительная реализация по плану PARITY_ROADMAP.md.

## 6. Совместимость с текущим Built-in Fog

DayNightLightingController в LateUpdate снова устанавливает RenderSettings.fog. Если добавить fullscreen haze сверху, встроенные shader fog passes и новый эффект наложатся дважды.

LCAtmosphereRenderer выключает RenderSettings.fog в OnPreCull, только пока **его камера** рисует кадр, и восстанавливает его в OnPostRender / OnDisable. При отсутствии шейдера, неперспективной камере, выключенном компоненте или URP/HDRP этот эффект делает просто Graphics.Blit. В таком случае старый рендер остаётся.

Ограничение: RenderSettings — глобальная настройка; две синхронно или рекурсивно рендерящие камеры требуют дополнительного тестирования. Пока использовать только на одной основной игровой камере.

## 7. Биомы

LCAtmosphereZone содержит профиль, размер прямоугольника X/Z, приоритет и радиус смешения. Transform.InverseTransformPoint позволяет поворачивать зону.

~~~text
outside = length(max(abs(localXZ) - halfSize, 0))
weight = 1 - smoothstep(0, blendDistance, outside)
~~~

При overlap выбирается зона с максимальным priority, затем с максимальным weight. Её значения линейно смешиваются с Default Profile. **Это одна доминирующая зона на кадр, не полноценное смешивание N биомов.** В будущем необходимо применять мировые данные биома из процедурной генерации и вычислять camera-focus-weighted профиль, не создавая один GameObject на чанк.

Для 4 биомов первоначальные художественные акценты:

| Биом | Дневной цвет | Эффект |
|---|---|---|
| Люди / королевство | тёплый нейтральный | умеренная дальняя дымка |
| Эльфы | едва заметный розово-сиреневый | мягкий туман в редком лесу |
| Древесные жители | приглушённый зелёный | низинный туман под деревьями |
| Горные жители | холодный голубовато-белый | яркий воздушный снег/дымка |

Это НЕ четыре разных солнца. Небо/освещение остаются согласованными через уже существующий глобальный свет.

## 8. Локальные объёмы

LCAtmosphereFogVolume — сфера на Transform, с radius, density и edgeFeather. Компоненты регистрируются в статическом списке OnEnable/OnDisable; собственного Update у каждой сферы нет. Renderer выбирает четыре ближайших к камере в O(N*4), отправляет центры/радиусы/плотности в один Material. Это дешёвый первый шаг для долин, но не замена реальной river distance mask.

Несколько одинаковых пересекающихся сфер складывают оптическую плотность. Не применять сотни таких объектов на процедурной карте. Для длинных рек нужен distance field/macro river mask на мировых координатах.

## 9. Что происходит при включении и выключении

- Материал загружается один раз из Resources/Atmosphere/LC_AtmosphereFog и клонируется; скрытый Shader включается в player build благодаря ссылке Material.
- Камера запрашивает DepthTextureMode.Depth.
- При каждом кадре вычисляются 4 world-space frustum rays, параметры профиля, активные зоны, выбранные объёмы, 4 ближайших активных Point Light из существующего бюджетного пула и вызывается один Graphics.Blit.
- Никаких изменений PlayerSettings, QualitySettings, рендера растительности, WorldClock, сохранений и сцен в репозитории нет.
- При отключении освобождаются только созданный Material и временный ScriptableObject; RenderSettings.fog восстанавливается.
- Отсутствие компонента возвращает прежнюю систему. При переходе на URP старый OnRenderImage не переносится автоматически.

## 10. Профили и камера

Параметры являются стартовыми, а не эталонными:
- Дистанция тумана 18 мировых единиц, максимум интеграции 580.
- Плотность дальнего тумана 0.0022, высотная 0.011, высота основания 12, scale высотного убывания 23.
- Noise scale 0.018, strength 0.22.
- 12 samples по умолчанию. 24 samples — только эксперимент и GPU-профилирование.
- Far zoom 100–190 в стратегии важнее сохранить контраст юнитов и дорог. Настройка автоматического ослабления тумана по зуму пока отсутствует.

Свет солнца задаётся действующим DayNightLightingController; не размещать второе солнце ради тумана.

## 11. Тест-план перед объединением с main

1. Сборка C# и шейдера в Unity 6000.5.5f1; отсутствие Console errors.
2. EditMode: AtmosphereRendererTests; проверка привязки материала к Hidden шейдеру.
3. Screenshot A/B на одинаковом seed и camera transform: dawn / noon / dusk / night.
4. Масштабы: 11, 35, 100, 190; дорога, лес, река, горы, 1 дом, 100+ домов, 16-player scenario.
5. FPS + GPU ms median/p95: 0, 8, 12, 16, 24 samples; Windows standalone, VSync отключён. Цифры записать в PR.
6. Проверить UV на DX11/Vulkan, near/far depth, gradient sky seam, прозрачную листву, попадание лучей солнца в туман.
7. Проверить ночную читаемость, тёплые окна на холодном фоне и совместимость с NightLightBudgetManager.
8. Проверить замену встроенного тумана и восстановление его состояния при disable, scene switch, domain reload и дополнительной камере.
9. Проверить переход всех четырёх биомов на границах и camera focus у WorldStreamer.
10. Проверить отсутствие неучтённых CPU allocations, работу foliage wind, ShadowCaster, GPU instancing, LOD, ночных бюджетов.
11. Player build должен содержать Resources Material и реально показывать эффект.
12. Только после подтверждения кадрами и метриками выбирать производство или улучшение системы.

## 12. Риски / запреты

- Не передавать значение тумана в Fog of War/authoritative simulation.
- Не переписывать общие _LC_* контракт и текущие production shaders из-за одного эффекта.
- Не пытаться "улучшить" картинку резким повышением плотности: стратегия должна оставаться читаемой.
- Не менять Gamma на Linear без отдельного визуального сравнения.
- Не мигрировать на URP/HDRP ради шейдера без ADR и измерений.
- Не брать фрагменты чужого неразрешённого кода, паков, шрифтов и скриншотов в репозиторий.
