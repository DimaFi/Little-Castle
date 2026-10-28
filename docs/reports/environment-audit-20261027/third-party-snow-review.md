# Бесплатные snow-референсы: первичные исходники и лицензии

Дата проверки: 2026-10-27. Целевой baseline: Unity6000.5.5f1, URP17.5.0 Forward. Это исследование для будущего отдельно согласованного этапа. Никакие сторонние пакеты, сцены, модели, текстуры или шейдеры не импортированы; production и обе рабочие копии не изменены. Remote source читался через web и HTTP в памяти; локально записаны только отчёт и metadata сравнения.

## Выбор для Little Castle

| Источник | Thin coverage | Временные следы | Совместимость с текущим baseline |
|---|---|---|---|
| Unity Shader Graph Production Ready / Weather / Snow | Предпочтительный официальный референс маски верхних поверхностей и смешивания свойств материала | Не готовый footprint lifecycle | Версионно ближайший: реальные assets уже находятся в локальном ShaderGraph17.5.0; SnowExample содержит UniversalTarget/UniversalLitSubTarget. Import/GPU compile **NOT RUN** |
| ZGeng/DeepSnowFootprint | Глубокая displacement/tessellation система не подходит поставленной цели тонкого слоя | Полезна общая идея world-space mask + направление отпечатка + fade; реализацию нужно существенно упростить | Исходный Built-in Deferred shader не совместим напрямую с URP Forward; Unity5.5.1f1, фактический shader target5.0, hull/domain/geometry |
| mert-dev-acc/SnowShaderGraph | Основная цель — vertex deformation, поэтому не брать как основу покрытия объектов | Camera/RenderTexture splatmap и blur можно изучить для локального непостоянного decal/mask слоя | Старый формат PBRMasterNode, требуется миграция/пересборка; точная Unity/SG package version в repo не зафиксирована. Compile **NOT RUN** |

Инженерный вывод: сначала сохранить собственные кисти, world-space data/save contract и текущий LC_Terrain snow path. Из внешнего материала выбирать отдельные идеи/небольшие функции после согласования, не заменять существующие LC lighting, wind и matched shadow passes готовым чужим материалом. Следы остаются необязательным локальным эффектом; authoritative biome/snow state от GPU trails не зависит.

## 1. Официальный Unity Weather/Snow

Первичный [документ Weather](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.shadergraph/Documentation~/Shader-Graph-Sample-Production-Ready-Weather.md) описывает Snow subgraph для верхних поверхностей: смешиваются цвет, гладкость, нормаль, metallic и emissive sparkles. Это материал, а не готовая система осадков, CPU накопления, map save или footprint persistence. Отдельные weather subgraphs позволяют выбирать нужные эффекты; забирать полный rain/weather набор для тонкого снега незачем.

Проверены read-only обе директории:

- `E:/Games_Develop/Little-Castle_BrushFoundation_20261027/Library/PackageCache/com.unity.shadergraph@8d7376ec43ee`
- `E:/Games_Develop/Little-Castle_LightingIntegration_20261025/Library/PackageCache/com.unity.shadergraph@8d7376ec43ee`

`package.json`: version17.5.0, unity6000.5, fingerprint `8d7376ec43ee0ba3dca1703338e2749e4cfdf0a8`. Семь файлов в двух копиях побайтно совпадают по SHA256: package.json, LICENSE.md, Weather documentation, Snow.shadersubgraph, Snow_Mask.shadersubgraph, Snow_Sparkles.shadersubgraph, SnowExample.shadergraph. Полные хеши: `shadergraph-sample-local-integrity.json` рядом с отчётом. Это fingerprint Unity package, а не заявленный git commit Graphics/master.

Локальные paths относительно package root:

- `Samples~/ProductionReady/Weather/Snow/Snow.shadersubgraph`: GraphData m_SGVersion=3; использует Snow_Mask, Snow_Sparkles, NormalBlend/NormalReconstructZ, SnowNCSTexture и Material Quality keyword.
- `Samples~/ProductionReady/Weather/Snow/Components/Snow_Mask.shadersubgraph`: m_SGVersion=3, имеются SnowMinAltitude, SnowMaxAltitude, NormalY, AltitudeMask, Ceiling. Их наличие не означает автоматически roof/interior occlusion; полноценный shelter query не доказан.
- `Samples~/ProductionReady/Weather/SnowExample.shadergraph`: m_SGVersion=3, содержит UniversalTarget + UniversalLitSubTarget и HDTarget/HDLitSubTarget. Это подтверждает URP target в файле, а не успешный GPU compile в нашей сцене.
- Объявление sample Production Ready Shaders в package.json указывает зависимости `com.unity.render-pipelines.core/Samples~/Common` и `com.unity.shadergraph/Samples~/Common`. Поэтому нельзя переносить только один graph без проверки GUID/subgraph/texture dependencies. Ничего не импортировалось.

Лицензия: [ShaderGraph LICENSE.md](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.shadergraph/LICENSE.md) и [корневой LICENSE.md Graphics](https://github.com/Unity-Technologies/Graphics/blob/master/LICENSE.md) указывают Unity Companion License, copyright2020 Unity Technologies ApS. В полном локальном дереве ShaderGraph package отдельного Samples LICENSE/Third-Party Notices override не найдено (`rg --files -uu`, фильтр license/notice/third-party). Поэтому оснований объявлять samples MIT нет; проверенный package-level режим — UCL.

По [актуальной UCL v1.4](https://unity.com/legal/licenses/unity-companion-license): использование связано с действительной Unity Engine License; разрешены без отдельной платы копирование/изменение/распространение в её рамках. Это не общая MIT-лицензия для произвольного движка. Нужно сохранять license/copyright notices в существенных частях и соблюдать отдельные third-party notices, если они сопровождают выбранные assets. Условия Unity Engine License не отменяются. UCL разделяет собственный контент игры и производные изменения самого Work; для последних содержит специальные условия прав Unity. Запрещено использование Work для конкурентного анализа/создания конкурирующего Unity продукта или сервиса. Вывод для текущей Unity-игры: подходящий бесплатный источник при соблюдении UCL и notices; не переносить его под MIT в общий независимый renderer toolkit.

## 2. ZGeng / DeepSnowFootprint

Проверенный head: `4380788f0e0aad707d2bdde5056cc2be247e2b85` (GitHub commits/master API, read-only). Первичные [README](https://github.com/ZGeng/DeepSnowFootprint/blob/4380788f0e0aad707d2bdde5056cc2be247e2b85/README.md) и [LICENSE.md](https://github.com/ZGeng/DeepSnowFootprint/blob/4380788f0e0aad707d2bdde5056cc2be247e2b85/LICENSE.md). README сообщает Unity5.5.1f1, deferred rendering и динамическое subdivision/displacement толстой снежной поверхности, отпечатки постепенно исчезают. Формулировка README о SM4+ не заменяет фактические требования конкретного shader.

В [SnowGroundShader.shader](https://github.com/ZGeng/DeepSnowFootprint/blob/4380788f0e0aad707d2bdde5056cc2be247e2b85/SnowGroundShader.shader) реально присутствуют target5.0, hull/domain/geometry entry points, LightMode Deferred, Built-in UnityCG/Tessellation/UnityPBSLighting includes и несколько GBuffer outputs. Это не URP Forward pass. Простая замена RenderPipeline tag не превращает его в совместимый shader. Displacement меняет геометрию и требует отдельного рассмотрения shadows/depth/colliders, противоречит нынешней цели тонкого покрытия.

[DistanceMapPainter.cs](https://github.com/ZGeng/DeepSnowFootprint/blob/4380788f0e0aad707d2bdde5056cc2be247e2b85/Assets/Snowprint/Scripts/DistanceMapPainter.cs) на foot event создаёт sprite GameObject через Resources.Load и кодирует позицию/угол в SpriteRenderer color. [sprite_fading.cs](https://github.com/ZGeng/DeepSnowFootprint/blob/4380788f0e0aad707d2bdde5056cc2be247e2b85/Assets/Resources/sprite_fading.cs) уменьшает alpha через Time.deltaTime, уничтожает объект после lifeSpan (default30сек). Следовательно полезен принцип временного масочного следа, но массовый Instantiate/Update/Destroy на каждый отпечаток не следует копировать для стратегии; будущая реализация может применять pooling/батч масочных stamps с локальным бюджетом.

[footCollide.cs](https://github.com/ZGeng/DeepSnowFootprint/blob/4380788f0e0aad707d2bdde5056cc2be247e2b85/Assets/Snowprint/Scripts/footCollide.cs) зависит от foot tag, фиксированного world-scale divisor40 и имени ThirdPersonController; вызывает event без null-check. Это демонстрационная сцена, не универсальный runtime module. Нужны явные bounds/world mapping, unit animation events/ground queries, отсутствие gameplay authority в presentation.

MIT: copyright2018 Zhen Geng; разрешает коммерческое использование, изменение и распространение при сохранении copyright и permission notice в копиях/существенных частях, без гарантий. Однако repository tree также содержит Unity Standard Assets/ThirdPerson/FirstPerson библиотеки. Root MIT не следует автоматически трактовать как relicensing всех чужих вложенных материалов: до любого реального reuse assets нужно проверить их собственные headers/notices. Сейчас не копировались ни code, ни assets; для нашей цели достаточно изучения маски/fade.

## 3. mert-dev-acc / SnowShaderGraph

Проверенный head: `c6c51f2eb901ae2a54f2b3f1aaed44ac4aa2508b`. Первичные [README](https://github.com/mert-dev-acc/SnowShaderGraph/blob/c6c51f2eb901ae2a54f2b3f1aaed44ac4aa2508b/README.md), [LICENSE](https://github.com/mert-dev-acc/SnowShaderGraph/blob/c6c51f2eb901ae2a54f2b3f1aaed44ac4aa2508b/LICENSE), [Snow.shadergraph](https://github.com/mert-dev-acc/SnowShaderGraph/blob/c6c51f2eb901ae2a54f2b3f1aaed44ac4aa2508b/Snow.shadergraph). README описывает camera-generated RenderTexture splatmap trails и vertex deformation с blur соседей; поверхность должна быть заранее subdivided либо tessellated другим shader. Для thin coverage можно исследовать только локальную splat mask/затухание, без displacement.

Фактический graph — старый вложенный JSON с m_SerializableNodes/JSONnodeData, PBRMasterNode m_NodeVersion=0, UniversalPBRSubShader. Поля m_SGVersion и актуальные UniversalTarget/SubTarget отсутствуют. В recursive tree нет package manifest, ProjectVersion или C# scripts. Поэтому **точную версию ShaderGraph/Unity установить по repo нельзя**; NodeVersion0 не означает ShaderGraph package0 и не доказывает совместимость17.5. README говорит URP/HDRP, но фактический master node содержит UniversalPBRSubShader; готовность HDRP не подтверждена этим файлом.

CustomFunction CalculateHeight суммирует 3×3 texture samples через локальный `mean_height`, который не инициализирован перед +=; выборка задаёт LOD1. Это статически обнаруженная причина возможных неопределённых результатов; перед reuse нужно инициализировать accumulator, проверить texture/mips contract и изменить old CustomFunction API при миграции. Камера с m_TargetTexture=0 и отсутствующие в репозитории C# runtime creators означают, что README не является доказательством самодостаточного текущего RT lifecycle. Миграция/import/compile не выполнялись.

MIT: copyright2020 Mert Kirimgeri, те же требования сохранения copyright/permission notice и отказ от гарантий. MIT разрешение не подтверждает производительность или совместимость графа. Проектные textures/FBX не переносились; при будущей выборке ресурсов сохранять provenance и проверять отдельные notices.

## Статусы и будущая проверка

- **PASS**: прочитаны первичные README/source/license трёх источников; идентифицированы обе сторонние revision SHA и локальный SG17.5 fingerprint.
- **PASS**: семь локальных SG sample/license/package файлов идентичны BrushFoundation ↔ LightingIntegration по SHA256.
- **PASS static research**: подходящая основа для thin material coverage — официальный Snow subgraph; два сторонних footprint проекта относятся преимущественно к деформации и требуют переработки.
- **NOT RUN**: импорт любых из этих samples/сторонних пакетов, миграция graph, shader/GPU compile, Player build, golden scene, profiling, portability/платформенные тесты. Исследование не объявляет готовый production снег.
- **Не выполнялось**: production integration, изменение biome/save/brush/shader кода, установка сторонних assets, публикация GitHub.

После отдельного разрешения минимальный эксперимент должен использовать исходные LC материалы и общие lighting globals, coverage до slope/acceptance, preserve wind/shadow/depth parity, ограниченную локальную маску следов с fade и GPU/CPU бюджетом. Авторитетные биомы/накопление/сохранения — отдельные данные; временные footprints по умолчанию не требуют persistence и полной карты render textures.
