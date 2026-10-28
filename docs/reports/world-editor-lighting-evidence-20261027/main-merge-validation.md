# MAIN functional merge — read-only validation, 27.10.2026

Статический проход MAIN относительно reviewed scratch merge, frozen BeforeMain, свежего map и lighting: 53/53 проверок PASS. Исходники не менялись, Unity не запускался. Визуальных выводов нет.

## Повторная проверка regression — PASS

Первоначальный P1 устранён родительским агентом: LightingMeadowReview component76295307 удалён из новой owned сцены; его script GUID и component reference в сцене отсутствуют. Непрерывный WorldTime остаётся serialized runAutomatically:1/speed1. Новый WorldEditorAcceptance требует RunAutomatically=true до собственных override, а LightingMeadowReview/SpatialIndirectStudyReview должны отсутствовать даже если disabled. Setup удаляет review после получения ссылок; FinalizeScene удаляет также inactive reviews. Source/static regression закрыт, Unity данным агентом не запускался.

Первоначальная finding evidence не изменялась: `main-merge-validation-initial-finding.json`. Она сохраняет disabled component76295307 и причину риска: безусловный Awake → SetHour → RunAutomatically=false. Current JSON отражает исправленную сцену и53/53 PASS.

## Подтверждено

- Все12 reviewed merged source и их meta в MAIN совпадают побайтно со scratch результатом. Terrain SHA256 `e6a492c93bbf9229419cf6eaf2248aa666bc15dbe07ec8e50536f99cea61a2d5`.
- InnerverseGrassStudy/ChunkTerrainVertexMasks/MeadowGrassRenderer/MapSurfaceGpuCache и core WorldTimeSystem/WorldClock/WorldTimeSettings совпадают со свежим map. MPB surfaceOverrides/RenderParams.matProps сохранены.
- RiverBank/RiverWater/Water/Snowfall, SurfacePaint, GroundPalette, SoftShadow совпадают со свежим map. Локальные snow/triplanar/normalAO сохранены в reviewed Terrain; deformation/instancing/passes/keywords унаследованы от ранее проверенного exact merged результата.
- Все shader include цепочки разрешаются: relative/Assets и фактические URP Packages через текущий Library/PackageCache. Пропущенных include нет.
- Сцена и достижимые prefab/asset/material dependencies не содержат SpatialIndirect runtime scripts или GI keywords. Shader support остаётся opt-in.
- Scene GUID dependencies разрешаются в Assets/PackageCache; local fileID ссылки разрешаются. Это не проверка каждого imported model subasset fileID.
- Единственный WorldTimeSystem1696229881 связан с DayNightLightingController1145319857 и StylizedLightingGlobals348784513. Settings MainWorldTimeSettings: start8h, day06–20/360s, night20–06/90s; scene serialized continuous1x. Старый benchmark Awake driver удалён; runtime clock evolution проверяет основной агент.
- MeadowPreview worldEditorMode:1. Preset SetView вызывает SyncFromCurrentView и возвращает strategy rig; worldEditor Start не запускает old preset automation. WorldEditorAcceptance запускается только с -weAccept/-weBenchmark.
- ProjectSettings activeColorSpace:1 (Linear).

Сохраняется baseline ограничение: Terrain/solid без собственного DepthNormals, SSAO должен использовать depth-reconstructed normals. Source проверка не утверждает работоспособность/качество runtime, ночи, seams или temporal стабильности. Совместные Player проверки выполняет родительский агент.
