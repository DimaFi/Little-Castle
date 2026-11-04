# Little Castle — вода, чёрная растительность, дальние грозовые тучи
Дата: 2026-11-04. **Исходники для теста в Unity, НЕ прошедшая Game-приёмку версия.**

## База и способ интеграции
- Текущая ветка: `gpt6/water-grass-clouds-polish-20261104`.
- Основа: `gpt6/biome-precipitation-distant-storm-20261104` / PR #96, commit `d61000a`.
- PR #96 — stacked на PR #95 (`codex/integrated-project-checkpoint-20261104`).
- Для Codex: fetch, проверить свежий HEAD всех трёх веток, не применять повторно
  исторические PR; интегрировать по цепочке в чистом worktree с сохранением
  незакоммиченной авторской карты, сцены, водопада и собственного Art.
- Публичный checkpoint всё ещё имеет не менее 32 неразрешённых ссылок на
  локальные tree/rock assets. Их не придумывали и не заменяли чужими.
- **Никаких merge в main или чужой WIP**, никаких изменений map/save/scene.

## Вода: что изменено и почему
1. `RainStormController.UpdateSurfaces`: плоские меши луж больше **не накладываются
   на русло реки**. Раньше `TrySurface` распознавал waterline и там размещался
   тот же плоский прозрачный quad, который предназначен для мокрой земли.
   При сочетании с прозрачным водным материалом такие накладки способны давать
   видимые полигональные пятна/сортировку. Наземные лужи и укрытия сохранены.
   **Ограничение:** специальные дождевые ряби на самой реке ещё нужно художественно
   согласовать с ConnectedWater; здесь не создавали новые quads.
2. `MapRiverReflections`: при нехватке лимитированных reflection slots на
   одновременно видимые высоты воды больше не образуется смесь planar/fallback
   на соседних речных секциях: `coherentPlanarReflections` даёт единый fallback.
   `enablePlanarReflections` позволяет сравнить water material с planar и без него.
3. `RiverVisualQualityProfile.refractionBackground` в актуальном
   `Resources/MapAuthoring/RiverVisualQuality.asset`: 0.22 вместо
   неявного .55. Уменьшение эффекта бледного фона/дна: сохранить речную палитру.
   Все значения регулируемые через Inspector; не править авторские water materials
   и непринятый waterfall WIP.

**Важно:** кадр показывает серые клинообразные полосы, но **точную первопричину
не подтвердили на локальной карте**. Если полосы останутся с RainStorm Off,
затем с planar Off, следом проверить mesh UV/normal, opacity/depth/opaque
buffers в ConnectedWater. Не объявлять воду исправленной без Game-проверки.

## Чёрные пятна / исчезающие текстуры после кисти
- `MeadowScatterRenderer` теперь избегает *явно* битых shader/material ссылок
  и LOD-мешей с аномально большими bounds (включая `Hidden/InternalErrorShader`).
  Состояние безопасности кешируется вне рендер-цикла.
- Два **неразрушающих** Inspector переключателя:
  `debugHideScatter` — показать кадр без процедурных камней/цветов/мелочи;
  `debugOnlyPrototype = -1` — все, 0..N — один конкретный prototype.
  `rejectBrokenScatterMaterials` — защита от заведомо некорректных LOD/material.
- Context menu компонента: `Diagnose painted scatter visuals`. Выводит
  `stableId`, `sourceAssetPath`, LOD и material warnings.
- Эти проверки **не меняют сохранённые кисти и префабы**. По скриншоту пока
  нельзя уверенно доказать, что тёмные полигоны создаёт scatter, а не дыра
  terrain-геометрии, шейдерный shadow pass или пропавшая альфа.
- Если пятна остаются при `debugHideScatter=true` — инспектировать
  `MapTerrainPresentation`, `LC_Terrain`, paint mask, shadow passes и
  локальные текстуры. Не менять растительность наугад.

## Небо, грозовая масса и горы
- `StylizedDayNightSky.shader`: отдельный **дальний** процедурный многослойный
  cloud bank с фрактальным noise (low cost), тёмным нижним краем, рваной
  массой и плавным fade. Не volumetric raymarch, не меши в камере.
  При `_StormCloudMass == 0` новый слой выключен.
- `AtmosphereProfile`: новые `stormBankLower` (.065),
  `stormBankReach` (.78), `stormBankDensity` (.84) через Inspector;
  fallback для сцен без complete AtmosphereProfile в
  `DayNightLightingController.Atmosphere`.
- `LC_MountainPreview.shader`: мягкая, очень ограниченная world-space дымка
  только на **дальних высоких** горных склонах/вершинах при грозе.
  Настройка `_StormPeakVeil`, 0 отключает. Не влияет на физику/коллайдеры.
  Это художественное приближение к тучам у вершин, не настоящая объёмная
  облачная геометрия перед горой.
- Сама геометрия молний, частота и ветвления PR #96 **оставлены**.
  Добавлена только направленная подсветка облачной массы из точки
  начала уже существующей молнии (`_LC_StormFlashDirection`);
  больше нет слепящего равномерного осветления всего skybox.

## Тесты
В `WeatherVisualPolishTests`: pure EditMode guards на полный набор
видимых reflection slots, некорректный LOD scatter, свойства sky shader
и диапазон нового AtmosphereProfile. Тесты добавлены, **но не выполнялись
в Unity в этом чате**. Реальной компиляции shaders/assemblies, Player build,
PlayMode, GPU/FPS/overdraw/GC-замеров нет.

## Приёмка Codex в реальном Unity
1. Проверить package Unity 6000.5.5f1 + URP 17.5, импорты .meta, compile;
   запустить `WeatherVisualPolishTests`, `RainStormMathTests`, visual
   water/scatter/atmosphere и связанные Editor + PlayMode тесты.
2. Один и тот же кадр воды **Off → Rain → Storm** с `enablePlanarReflections`
   true/false и `refractionBackground` 0, .22, .55. Смотреть швы, низкую
   камеру, оба уровня воды, waterfall, прозрачную сортировку, берега.
3. В области чёрных пятен оставить ту же кисть, включить
   `MeadowScatterRenderer.debugHideScatter`. Если пятна пропали —
   `debugOnlyPrototype` и `Diagnose painted scatter visuals`, затем
   ремонт конкретного локального material/texture/prefab. Если остались —
   проверка terrain/lighting, не обвинять scatter без доказательств.
4. Небо: Off/морось/ливень/гроза, 18:30/полдень/ночь, камера низко/высоко,
   вид на самые дальние горы; проверить `stormBankLower/Reach/Density`,
   гром и кнопку молнии. Убедиться, что молния освещает тучи вокруг своего
   источника, но не слепит камеру; горные вершины остаются читаемыми.
5. Low/Medium/High GPU ms, draw calls, overdraw, GC. Проверить
   снятие storm-профиля, отмену, закрытие Game и восстановление базового sky.
6. Не заменять старую пользовательскую сцену checkout-версией.
   Сначала подтвердить local scene/art и ресурсные GUID; тогда сделать
   визуальную приёмку и перенести проверенное в текущий проект.

## Ограничения
Обещать «полностью исправлено» без реального Unity Game нельзя.
Вода могла иметь независимую регрессию Graph/отражений;
чёрные карты могли быть missing local art; низкие облака могут требовать
последующего art pass на дальних горных prefabs. Здесь реализованы изолированные
исходники и диагностические переключатели для проверки этих гипотез.
