# Little Castle — optional Physically Based Sky + volumetric lighting

Дата: 2026-11-04. **Статус: source-only Draft; Unity import / C# / shader compilation / GPU / Game QA НЕ ЗАПУСКАЛИСЬ.**
Эта ветка основана на PR #99, а не на `main`; цепочка #95 → #96 → #97 → #98 → #99 всё ещё находится на художественной приёмке. Авторская локальная карта, WIP водопада и локальные артовые GUID не публиковались. **Не делать checkout поверх рабочей сцены и не сливать автоматически.**

## Исправление после реального импорта Unity — 04.11.2026

Временная копия Unity 6000.5.5f1 обнаружила 13 ошибок: 10 CS0115 в upstream PBSkyURP и три CS0246 в нашем адаптере. В **этом же черновом PR #102** подготовлен исправляющий исходный код (commit `bba554e`):

- `Packages/com.jiaozi158.unity-physically-based-sky-urp/` — **embedded package**, исходная MIT-копия `jiaozi158` @ `f8f1f3fb` с сохранением оригинальных `.meta` и лицензии. Единственная изменённая программа `Runtime/PhysicallyBasedSkyURP.cs`: пять пар устаревших `OnCameraSetup/Execute` изолированы для Unity до 6000.4; пять `RecordRenderGraph` оставлены.
- `LittleCastle.Runtime.asmdef` и `LittleCastle.Editor.asmdef` теперь прямо ссылаются на `PBSkyURP` и `com.cqf.urpvolumetricfog.runtime`.
- Прежняя Git-зависимость PBSkyURP **по-прежнему закреплена** в `manifest.json`: Unity отдаёт приоритет одноимённому embedded package в `Packages/`. Для тестового отката можно удалить embedded каталог в **отдельной копии проекта**, вернувшись к исходному pin; в рабочем проекте это делать нельзя без резервной копии.
- Прочитать `Packages/com.jiaozi158.unity-physically-based-sky-urp/LC_PORT_NOTES.md`.

**Это устранение выявленных причин в исходниках, а НЕ подтверждённое исправление всех ошибок Unity.** Повторную компиляцию Unity, импорт шейдеров, Frame Debugger, PlayMode, Editor/Player build и GPU QA всё ещё нужно выполнить. Возможно появление дополнительных ошибок API/шейдеров.

## Выбранные пакеты и лицензии

| Источник | Роль | Pin | Лицензия |
|---|---|---|---|
| [jiaozi158/UnityPhysicallyBasedSkyURP](https://github.com/jiaozi158/UnityPhysicallyBasedSkyURP) | небо, физическое рассеивание, отражения | `f8f1f3fbc4708f378252b6188ce416a57ccd06af` | MIT |
| [CristianQiu/Unity-URP-Volumetric-Light](https://github.com/CristianQiu/Unity-URP-Volumetric-Light) | объёмный туман, направленный свет, точечные/spot источники | `354ebc5b6ed3c2c40dc9797c813b56d5da213c4e` | MIT |
| [phlippos/unity-urp-volumetric-fog](https://github.com/phlippos/unity-urp-volumetric-fog) | только справочное исследование и бенчмарки | не импортирован | **нет лицензии** |

Обе исходные зависимости закреплены в `Packages/manifest.json`; **PBSkyURP теперь также включён как приоритетный embedded package с минимальным локальным портом**, Volumetric Light остаётся нетронутой Git UPM dependency. Код не копировался в `Assets/`. Полные upstream MIT тексты зафиксированы в `docs/licenses/`, также присутствует `ATMOSPHERE_THIRD_PARTY_NOTICES.txt`. Эти уведомления надо включать в дистрибутив игры, если соответствующее ПО включено. Исходники третьего проекта не импортированы, потому что его автор прямо пишет о невыбранной лицензии.

## Что подготовлено

- `LittleCastleOptionalAtmosphereStack` — **opt-in**, по умолчанию OFF. Работает только в Play Mode, создаёт временный `VolumeProfile` в памяти, НЕ меняет авторские сцены/карты/префабы.
- Два источника неба: `ExistingStylized` (старый skybox в режиме Custom Sky) и `PhysicallyBased` (настоящая PBR атмосфера). Поскольку старые грозовые облака рисуются skybox-шейдером, параметр `keepStylizedStormClouds` по умолчанию возвращает во время Storm существующее небо, пока не готов отдельный cloud-compositing renderer.
- Новый объёмный туман включается отдельно от неба: плотность, дальность, высота, число шагов, учёт основного солнца и выбранных дополнительных источников. Опциональный `followWeatherForFog` мягко усиливает туман при осадках/грозе через `RequestedPrecipitation/RequestedStormStrength`, не меняя расчёт игровой погоды. Начальные значения: density 0.08, distance 120, heights 0–45, 32 steps, clear ×0.55 / storm ×1.25 — **не художественно подтверждены**.
- Не подменяем clock/simulation. Изначальный `DayNightLightingController` продолжает управлять солнцем/луной, временем и погодными параметрами. Для физического skybox добавлен ровно один условный `if`, который **никогда не выполняется без явного opt-in**. При отключении адаптер возвращает прежний источник неба.
- Старый Trilight ambient остаётся авторитетным: dynamic sky ambient третьей стороны выключен. Линейный старый fog подавляется при рендеринге нового тумана/рассеивания, чтобы избежать двойного затенения.
- `Little Castle > Atmosphere > Add volumetric to selected scene lights` — добавляет `VolumetricAdditionalLight` только явно выбранным Point/Spot lights в текущей сцене с Undo.
- `Little Castle > Atmosphere > Add volumetric to selected prefab assets` — изменяет **только явно выбранные source prefab assets** и добавляет компонент на их Point/Spot lights; существующие scene instances наследуют префабную конфигурацию. Перед вызовом сделать копию префабов; не использовать массово.
- `Little Castle > Atmosphere > Verify optional integration` — read-only диагностика наличия URP и пакетов. **Не включает Renderer Features автоматически**.

## Порядок безопасного запуска Unity 6000.5.5f1 + URP 17.5

1. Сначала сохранить свою карту, сцены, материалы, WIP и убедиться, что все внешние артовые GUID на месте. Использовать **изолированный worktree или копию проекта** с актуальным стеком PR #95–#99 и этой экспериментальной веткой.
2. Unity Package Manager должен выбрать embedded PBSkyURP и разрешить Git-пакет CristianQiu; проверьте фактический путь `Packages/com.jiaozi158.unity-physically-based-sky-urp` и существование обеих сборок. Проверить, что URP остаётся 17.5.0 и пакеты не понизили версию. Только после успешного import допускается обновить `packages-lock.json`. Если есть C#/shader errors — **остановиться** и откатить эксперимент, не трогать локальную основную сцену.
3. **Клонировать URP Renderer Data и URP Pipeline Asset** для отдельного тестового preset, а не редактировать активный production renderer. В копии Renderer Data вручную добавить `Physically Based Sky URP` и `Volumetric Fog Renderer Feature`. Проверить поля sky shader/LUT material; если нужны, назначить их ресурсами из установленного PBR Sky package. Обе Renderer Features должны работать только в тестовом preset.
4. Дублировать сцену World Editor как `Atmosphere_Comparison` (вне пользовательской исходной сцены). Подключить копию URP pipeline через тестовый Quality/Graphics preset. Добавить `LittleCastleOptionalAtmosphereStack` на один тестовый GameObject, назначить существующий `DayNightLightingController` и поставить `enableExperimentalStack = true`.
5. Начать с `SkySource=ExistingStylized`, `enableVolumetricFog=false`: это baseline на тестовом renderer. Проверить обычные дождь, снег, грозовые облака, молнии, ночь, смену сцены и горизонты.
6. Включить `enableVolumetricFog`. Сначала `enableSelectedAdditionalLights=false`, проверить солнце/луну и дымку. После выбора конкретных префабов/ламп добавлять `VolumetricAdditionalLight` и затем включать `enableSelectedAdditionalLights`. Не включать объём на всех источниках сразу.
7. Отдельно проверить `SkySource=PhysicallyBased`: ясный день, вечер, ночь, дождь. Во время сильной грозы по умолчанию возвращается старый skybox с нашими тучами, чтобы не потерять текущий арт. Для проверки нового физического неба в грозе только в тесте можно отключить `keepStylizedStormClouds`, **но PBR sky ещё не содержит наш облачный шейдер**.
8. Проверить взаимодействие с текущим `RenderSettings.fog`, горной дымкой, снегом, отражениями воды, дождевыми прозрачными частицами, камерой отражений, сменой quality, телепортом/зумом, запуском и остановкой Play.
9. В Unity Test Runner запустить `LittleCastleOptionalAtmosphereTests` (default-off и расчёт погодного fog), существующие atmosphere/weather/skybox/snow/river тесты и полный relevant EditMode/PlayMode набор. В сборке Player снять одинаковые скриншоты и GPU timing для old/off, Custom+fog, PBR+fog, и PBR без fog.
10. Вернуть original renderer, выключить/удалить тестовый мост и убедиться в полном отсутствии визуальных регрессий. Только после отчёта Codex и пользовательской визуальной приёмки рассматривать интеграцию в production.

## Известные технические риски

- **Сам пакет PBR sky не содержит готовые массивные грозовые тучи**. Он полезен для рассеивания/рассветов/неба, но отдельная многослойная cloud geometry/impostor система из задачи #100 ещё нужна.
- Пакеты поддерживают Unity 6 в своих репозиториях, но **компиляция именно связки Little Castle с URP 17.5 не подтверждена**. Renderer Features надо проверить на рабочем ПК.
- Пакеты и existing `DayNightLightingController` могут конкурировать за `RenderSettings.skybox`, ambient/reflection/fog, поэтому для A/B нужен ровно один активный владелец каждого эффекта. Экспериментальные Render Features активируются только на копии renderer.
- Fog после opaque не обязан правильно отображать прозрачные частицы, стекло, воду и полупрозрачные карточки облаков.
- `phlippos` не используется из-за отсутствия лицензии, но можно вернуться к нему после получения разрешения автора.
- Test component не настроит Renderer Features сам и не обещает, что тестовый preset будет компилироваться без правок. Текущая ветка — подготовленный **исходный код**, а не подтверждённая готовая Unity сцена.
