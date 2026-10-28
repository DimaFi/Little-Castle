# [Sol 6 High][P1] Гарантировать переправы и связность конечной карты

Исполнитель: **Sol 6 / High** (`gpt-6-sol`, `reasoning_effort=high`). Это параметры для ручного запуска, Issue сам агента не запускает.

Репозиторий: `DimaFi/Little-Castle`. База: ветка **`current-unity-fix`**, не старый `main`; foundation commit `edb9eae5949966f8a0b7a71a4f0354ee7d427a8e`. Сначала прочитать `AGENTS.md`, `docs/handoffs/sol6-high-mobile-2026-10-08.md`, `docs/reports/bridge-world-v002-2026-10-08.md` и архитектуру своего подсистемного участка.

Общие ограничения: конечная карта, детерминизм/negative chunk seams, plain authoritative data отдельно от presentation, Built-in renderer, без watermills/URP/HDRP/зеркального выравнивания ресурсов. Не редактировать чужие файлы без согласованной переуступки. Перед изменениями проверить актуальные commits и работающих исполнителей. При отсутствии Unity/Blender/LFS не объявлять проверки PASS: зафиксировать точную команду и необходимость проверки на ПК.

## Результат
Заменить простой rejection unsupported crossing roads детерминированным bridge-aware подбором/перепланированием. Карта должна иметь валидные фиксированные переправы и практическую связность; если выполнить условия невозможно, bootstrap возвращает понятную ошибку или ограниченный deterministic retry, а не бесконечный цикл.

## Владение файлами
`Assets/_Game/Scripts/World/Macro/{BridgeSitePlanner,BridgePlannerSettings,MacroWorldPlan,MacroWorldPlanner,TerrainRoadPathPlanner,TerrainRoadPathPlannerSettings,WorldRoadData,WorldRoadConnectionData}.cs`;
новые `BridgeAwareRoutingPlanner.cs`, `WorldRouteConnectivityValidator.cs` в том же Macro/;
`Assets/_Game/Tests/Editor/FixedBridgeGenerationTests.cs`, новый `BridgeAwareRoutingTests.cs`;
`docs/architecture/fixed-bridge-crossings.md` и `bridge-aware-routing.md`.
Публичные контракты: только opt-in routing settings/data и diagnostics; сохранить legacy-off behavior и стабильные IDs. `FixedBridgeSiteProfile.cs` и модель не менять: 10.8 m, 2.86 m, scale 1, road +Z / river +X, sockets ±5.4 m, water -0.85 m.

## Зависимости
После задачи baseline. Может идти параллельно с prefab integration: файлы не пересекаются. Не трогать WorldStreamer, Main*, art releases или terrain sampler.

## Приёмка
- [ ] Нет реализованных unbridged road/river crossings или orphan sites.
- [ ] Сокеты, углы, ширины, grades, bends и footprint overlap проверены на реальной полилинии.
- [ ] Контролируемые retry/failure, bridge minimum/connectivity diagnostics без полной материализации карты.
- [ ] Same seed, changed seed, negative coords, разные размеры и 2/8/16 players; legacy regression.
- [ ] Задокументировать, что roadmap start fairness остаётся естественно неравномерным, без resource injection.
- [ ] Отчёт с API/ownership, тестами, числами, коммитом и ограничениями.
