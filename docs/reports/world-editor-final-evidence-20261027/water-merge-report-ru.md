# Выборочная интеграция RiverBanks water-only в MAIN

27.10.2026. Источник: `E:/Games_Develop/Little-Castle_RiverBanks_20261026`, snapshot `6a7d58c`. Назначение: `E:/Games_Develop/Little-Castle`. Источник и другие копии не менялись. Unity не запускалась, не закрывалась; commits/staging отсутствуют.

## Что перенесено

- `MapRiverData.cs`: source-поддержка явного layoutVersion 2 и глобального интеграла времени течения `TravelTime`. Default `MapRiverRecord.layoutVersion=1` сохранён. Старые записи не преобразуются.
- `RiverBankGeneration.cs`: source-алгоритмы v2 высот и детерминированной ширины, необходимые для уже существующих v2 данных. Никакие отклонённые cliff-меши/материалы/study-сцены не перенесены.
- `MapEditSession.Rivers.cs`: только адресные version-gated изменения для v2 QueryWater, vegetation edge и SandCoverage, а также source раскладка stable rock records при явном v2. MAIN `placementExclusions` сохранены. Для v1 оставлены прежние height formula, width и SandCoverage; история/save v5 не заменялись.
- `MapRiverPresentation.cs`: source water-only opt-in, слой 4, water renderer enumeration, flow-map property blocks, глобальные касательные, плотные округления торцов, bounds для волн, освобождение flow textures при eviction/dispose. Baseline rock presentation сохранён; source bank mass/palette/submerged-bed meshes исключены. Для v1 геометрическая ширина и отсутствие v2 caps сохраняются.
- `RiverBankMeshBuilder.cs`: исключительно source `FlowMap`, с сохранением source MonoScript GUID; экспериментальные функции построения cliff mass удалены из переносимого подмножества.
- `UberRiverReflectionAdapter.cs` и vendor PlanarReflectionManager/Volume/asmdef: source одна отражающая плоскость, scale 0.5, другой уровень воды использует probe fallback. Adapter читает материал через `RiverView.WaterMaterial`; при отключении снимает volume и устанавливает renderer fallback. Source исправления Unity 6.5 (`GetEntityId`, request destination, restore в finally) сохранены.
- `LittleCastle.Runtime.asmdef`: добавлена только ссылка `UberWaterPlannerReflection`; прежние ссылки URP/Core сохранены.
- Source экспортированный shader и его точный material/texture closure, CustomLighting/CubeMap includes, author GUI/asmdef и MIT notices. Source `.meta` сохраняются.

Из восьми устаревших texture slots рабочего материала удалены только поля, которых нет в реальном shader Properties/коде: `_Texture2D`, `_DistortionMap`, `_Intersection_Foam`, `_SurfaceFoamDistortionMap`, `_ShoreLineMask`, `_IntersectionFoam`, `_SurfaceDistortionMap`, `_CausticticsMap`. Это устраняет все пять unresolved GUID. Шесть активных texture properties разрешаются; активная каустика — локальная `Owned_Caustics.png`.

## Точная конфигурация для root

1. Catalog: `Assets/_Game/Art/Studies/RiverWater_20261026/RiverWater_Catalog.asset`, GUID `6510f8fcb7a81e74a88f8c885ccafb6e`.
2. Material: `Assets/_Game/Art/Studies/RiverWater_20261026/RiverWater_Uber_Tropical.mat`, GUID `20aa10e47ed08a44d9e44eaceaf9f3c3`.
3. Создавать view: `new MapRiverPresentation(session,catalog,true)`. Не регистрировать экспериментальный rock catalog для нового water-only session.
4. Runtime: `rt.ConfigureWaterOnly(true)` перед созданием новых рек. State equivalent: `riverTools.ConfigureNewRiverProfile(true)`. Только будущие StartRiver получают layoutVersion 2 и песчаные стороны; существующие записи/черновики не мигрируют. Вызовы API для нового water-only профиля добавлены по отдельной просьбе root.
5. Reflection opt-in: добавить `UberRiverReflectionAdapter`, назначить `.painter` текущему MeadowSurfacePainter, `.reflectionScale=.5f`. Материал уже имеет keyword `_ENABLEPLANERREFLECTION`. `studyCamera` должна быть действующей основной камерой; source manager рендерит Camera.main. Вода уже layer 4, отражатель исключает её.
6. Новые owned URP assets: `Assets/_Game/Settings/Rendering/LightingMeadow/WaterAO.asset`, GUID `c55bdc67f77c4f9ca3ae0a5bc26787df`; `WaterHDR.asset`, GUID `5bb183bdb31a415597993543036b9a3d`. Это clones существующих AO/HDR с изменением только имени и `m_RequireDepthTexture=1`, `m_RequireOpaqueTexture=1`. Исходные AO/HDR, их renderer, освещение/тени/AA не заменены. Root должен назначить один из новых assets новой авторской сцене и проверить, что camera overrides не выключают Depth/Opaque.
7. Runtime save/load guard: `rt.HasUnappliedDraft`, `rt.DiscardDraft()`. В общем Draw есть явная отмена и защита New/Select от молчаливой потери черновика. Смена категории через CloseTools/Stop сохраняет draft и режим для следующего OpenTools.

`MapTerrainPresentation`, `MapTerrainAnchor`, bridge patch, MapAuthoringWindow и MeadowPreview не менялись этой интеграцией. Навигация/AI module не переносились. Процедурная river chain не переключалась.

## Резервные копии и лицензии

`Before/Assets/...` и `Before/hashes.json` содержат четыре прежних MAIN файла плюс их `.meta`: MapRiverData, MapEditSession.Rivers, MapRiverPresentation, Runtime asmdef. `merge-manifest.json` перечисляет source-copy paths; `merge-verification.json` — SHA/closure.

MIT: MatrixRex Uber Stylized Water, Cyanilux Custom Lighting, Cathy Shih ShaderGraphVariables. Тексты source notices оставлены рядом с vendor assets. Сводный `THIRD_PARTY_NOTICES.txt` в этой папке следует включать в распространяемую сборку; автоматическое копирование в build root здесь не добавлялось. Полный ShaderGraph/source presets не импортированы, используется уже адаптированный экспортированный shader; повторная генерация из upstream Graph требует отдельного переноса depth/flow адаптаций. OpenGameArt node12987/производные и внешние demo caustic1/2/3 отсутствуют. Raw local assets не публиковались/не staged.

## Проверки

Статическая проверка GUID closure: 0 unresolved references рабочего catalog/material, 0 конфликтов перенесённых GUID, 6 активных texture properties разрешаются. 45/47 source-copy files побайтно точны; адаптированы material legacy slots и reflection adapter. FlowMap helper — намеренный source subset. Assembly GUID dependencies разрешены в MAIN PackageCache. `git diff --check`: PASS для затронутых tracked исходников.

Добавлены `MapRiverWaterInteropTests` (5 EditMode) и `MapRiverWaterInteropPlayModeTests` (1 PlayMode): v1 terrain/save replay, explicit v2 save/move/delete/history, placementExclusions, глобальный flow, активные texture refs, отсутствие bank/cliff объектов, tangents/upward triangles/caps/seams, wave bounds, flow texture eviction. `MapRiverWorkflowTests` расширены восьмым тестом explicit profile/discard.

**Unity compile, все тесты и visual/Player acceptance: NOT RUN этим агентом.** Root должен выполнить существующие MapRiverTests/MapToolTests/MapRiverPlayModeTests, новые workflow/interop tests, полные map/flower/lighting regressions и Player build. В реальной новой сцене проверить near/far day/night, движение flow/пены, depth/refraction/caustics, reflection plane, разные water levels, create/edit/delete/Undo/Redo/save/load v1/v2, seams/eviction и отсутствие logs. Исходные RiverBanks benchmark/52+6 tests не являются приёмкой MAIN.
