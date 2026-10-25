# Little Castle — Atmosphere & Lighting Lab

Статус: **экспериментальный, подключаемый вручную прототип на отдельной ветке**. Код записан в репозиторий, но пока не проверен запуском Unity, компиляцией шейдера на GPU или сравнением кадров.

## Зачем этот модуль

Цель — получить собственную систему сказочной атмосферы, визуально приближающую Little Castle к удачным решениям Tiny Glade: глубина воздуха, туман в низинах, мягкое смешение гор с небом, рассветное золотистое освещение, атмосферная разница между четырьмя биомами. Это НЕ копия исходного кода или фирменных материалов Tiny Glade.

В сентябре 2026 разработчики Tiny Glade рассказали о froxel-рендерере участвующей среды (participating media) и влиянии локальных фонарей на туман. В другой записи сообщается о собственном упрощённом ray tracing непрямого света. Наша первая версия использует другой, более простой метод: **постобработку по глубине с выборочным ray marching**. У неё нет настоящих объёмных теней, froxel-решётки или ray-traced GI. Есть недорогая приближённая подсветка тумана максимум четырьмя уже включёнными бюджетными Point Light, но не полный локальный froxel light transport. Называть первую версию точным техническим аналогом было бы неверно.

## Что есть в ветке

- Полноэкранный Built-in шейдер с Depth Texture, расстоянием, высотным туманом, ветровым шумом и приближённым солнечным рассеянием.
- Отдельный профайл LCAtmosphereProfile (ScriptableObject) с толщиной тумана, высотой, рассеивающим свечением, цветом для дня/ночи, количеством шагов.
- LCAtmosphereZone: большие зоны биомов, плавно смешиваемые по положению фокуса камеры, а не её высоте.
- LCAtmosphereFogVolume: локальные сферические участки тумана (четыре ближайшие на камеру).
- LCAtmosphereRenderer: один эффект на перспективную стратегическую камеру; использует текущий RenderSettings.fogColor, глобальные _LC_* и максимум 4 уже активных Point Light от NightLightBudgetManager для мягкого подсвечивания тумана (приближение без объёмных теней).
- Материал в Resources, чтобы Hidden-шейдер не исчез при сборке.
- Unity Editor меню для подключения компонента и генерации четырёх стартовых профилей (королевство, эльфы, древесные жители, горные жители).
- EditMode тесты оптического затухания, высотного тумана, перехода биомов и ссылки материала на шейдер.

## Где искать

- Assets/_Game/Scripts/World/Rendering/Atmosphere/ — компоненты и чистая математика.
- Assets/_Game/Shaders/Effects/LC_AtmosphereFog.shader — GPU-код.
- Assets/_Game/Resources/Atmosphere/LC_AtmosphereFog.mat — включение шейдера в сборку.
- Assets/_Game/Editor/LCAtmosphereQuickSetup.cs — инструменты настройки.
- Assets/_Game/Tests/Editor/AtmosphereRendererTests.cs — тесты.
- docs/experiments/atmosphere/IMPLEMENTATION.md — формулы, интеграция, API, настройки, испытания.
- docs/experiments/atmosphere/PARITY_ROADMAP.md — подробная разница с настоящим объёмным туманом Tiny Glade и следующие этапы.
- docs/decisions/ADR-0004-atmosphere-built-in-prototype.md — архитектурное решение.

## Подключение в Unity 6000.5.5f1

1. Переключиться на эту ветку. **Не объединять её с main до проверки**.
2. Открыть текущий Unity-проект, не создавать вложенный Unity-проект.
3. Проверить Console и выполнить EditMode тесты AtmosphereRendererTests.
4. Открыть тестовую сцену Assets/_Game/Scenes/WorldGenerationTest.unity.
5. Выбрать перспективную стратегическую Camera в Hierarchy.
6. Меню Little Castle > Rendering > Atmosphere > Attach To Selected Camera. Это добавит компонент без автоматического сохранения сцены.
7. Назначить Transform поля World Focus на TestFocus (тот же объект, что используется для стриминга).
8. В меню выбрать Create Four Example Profiles: появятся 4 независимых профиля в Assets/_Game/Settings/Atmosphere; автоматически на сцену они не применяются.
9. Назначить Kingdom как Default Profile. Для остальных территорий расставить редкие объекты с LCAtmosphereZone, задать размеры X/Z, blendDistance и соответствующий Profile.
10. Поставить несколько LCAtmosphereFogVolume в низинах рек, если это действительно помогает кадру.
11. В Play Mode переключать Enable Pass на компоненте и сравнивать одну и ту же камеру, время суток и карту.
12. Для гарантированного отката просто отключить/удалить компонент: оригинальная система освещения и тумана остаётся в проекте.

Важно: включённая опция Replace Legacy Distance Fog временно отключает стандартный туман RenderSettings во время рендера этой камеры. Это защищает от двойного тумана. Камеры миникарты/Scene View/кастомные рендеры необходимо протестировать отдельно.

## Реалистичный результат первого этапа

Работающая в перспективе система мягкого тумана и светового оттенка **после проверки в Unity**. На данном этапе нельзя обещать идентичные Tiny Glade лучи света за деревьями: для них необходимы реальные объёмные тени и освещение внутри тумана. Дорожная карта описывает реализацию этих эффектов отдельно.

## Источники и самостоятельность кода

- Tiny Glade 2026 froxel update: https://steamdb.info/patchnotes/25477972/
- Tiny Glade 2026 indirect lighting update: https://steamdb.info/patchnotes/25168681/
- Official Steam developer news: https://steamcommunity.com/app/2198150/allnews/
- Unity depth texture docs: https://docs.unity3d.com/Manual/SL-CameraDepthTexture.html
- Unity Built-in image effects: https://docs.unity3d.com/Manual/PostProcessingWritingEffects.html

Источники нужны для изучения общих графических методов и проверки заявлений разработчиков. Никакие закрытые файлы, ассеты и шейдеры Tiny Glade здесь не используются.
