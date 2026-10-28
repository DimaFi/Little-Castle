# [Sol 6 High][P1] Состыковать воду, берега и подходы фиксированного моста

Исполнитель: **Sol 6 / High** (`gpt-6-sol`, `reasoning_effort=high`). Это параметры для ручного запуска, Issue сам агента не запускает.

Репозиторий: `DimaFi/Little-Castle`. База: ветка **`current-unity-fix`**, не старый `main`; foundation commit `edb9eae5949966f8a0b7a71a4f0354ee7d427a8e`. Сначала прочитать `AGENTS.md`, `docs/handoffs/sol6-high-mobile-2026-10-08.md`, `docs/reports/bridge-world-v002-2026-10-08.md` и архитектуру своего подсистемного участка.

Общие ограничения: конечная карта, детерминизм/negative chunk seams, plain authoritative data отдельно от presentation, Built-in renderer, без watermills/URP/HDRP/зеркального выравнивания ресурсов. Не редактировать чужие файлы без согласованной переуступки. Перед изменениями проверить актуальные commits и работающих исполнителей. При отсутствии Unity/Blender/LFS не объявлять проверки PASS: зафиксировать точную команду и необходимость проверки на ПК.

## Результат
Одна согласованная river-water поверхность и плавные road/terrain переходы через фиксированные sockets. Сейчас terrain stamp готов, но water level contract ещё не потребляется глобальной presentation.

## Владение файлами
Новые `Assets/_Game/Scripts/World/Rendering/BridgeCrossingPresenter.cs`, `RiverWaterPresenter.cs`, `BridgeSitePresentationUtility.cs`;
новые `Assets/_Game/Editor/ConceptCrossingPresentationBuilder.cs`, `Assets/_Game/Tests/Editor/BridgeCrossingPresentationTests.cs`;
`Assets/_Game/Scenes/ConceptBridgeTest.unity`, новый `Assets/_Game/Settings/World/ConceptWorld_v001/ConceptCrossingPresentation.asset`;
`docs/reports/fixed-crossing-presentation.md`. .meta — те же владельцы.
Сначала найти существующий renderer: если требуются другие файлы/общий water-family shader, запросить явную переуступку, не создавать параллельный дубликат. Публичный контракт: read-only потребление MacroWorldPlan/FixedBridgeSiteProfile, presentation options; authoritative profile/math не менять.

## Зависимости
Только после routing и prefab integration: scene ownership переходит от prefab задачи. Не запускать их параллельно. Затем scene переходит задаче landscape.

## Приёмка
- [ ] Вода у моста: baseElevation -0.85; без z-fighting, двойной воды, dry holes или ступеней.
- [ ] World-space/negative-border/yaw tests; оба подхода совпадают с sockets ±5.4.
- [ ] Полилинии и authored patch имеют согласованный переход, не phantom перпендикулярную реку.
- [ ] Bank decor принадлежит stable site, не случайно меняется и не блокирует walkable path; generic scatter не дублирует его.
- [ ] Streaming load/unload и runtime delta не создают повторных поверхностей.
- [ ] Actual close/gameplay/far Unity review; без водяных мельниц/renderer migration.
- [ ] Отчёт, API, тесты и коммит.
