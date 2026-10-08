# [Sol 6 High][P2] Проверить бюджеты concept-мира и мобильный→ПК handoff

Исполнитель: **Sol 6 / High** (`gpt-6-sol`, `reasoning_effort=high`). Это параметры для ручного запуска, Issue сам агента не запускает.

Репозиторий: `DimaFi/Little-Castle`. База: ветка **`current-unity-fix`**, не старый `main`; foundation commit `edb9eae5949966f8a0b7a71a4f0354ee7d427a8e`. Сначала прочитать `AGENTS.md`, `docs/handoffs/sol6-high-mobile-2026-10-08.md`, `docs/reports/bridge-world-v002-2026-10-08.md` и архитектуру своего подсистемного участка.

Общие ограничения: конечная карта, детерминизм/negative chunk seams, plain authoritative data отдельно от presentation, Built-in renderer, без watermills/URP/HDRP/зеркального выравнивания ресурсов. Не редактировать чужие файлы без согласованной переуступки. Перед изменениями проверить актуальные commits и работающих исполнителей. При отсутствии Unity/Blender/LFS не объявлять проверки PASS: зафиксировать точную команду и необходимость проверки на ПК.

## Результат
Измеренная пригодность нового профиля (32 m chunk / 0.5 m samples), быстрый bootstrap и воспроизводимая передача на desktop. Это специализированная проверка concept-профиля, не дубль общего bootstrap roadmap: связана с существующей issue #5.

## Владение файлами
Новый `Assets/_Game/Editor/ConceptWorldPerformanceAudit.cs`;
новый `Assets/_Game/Tests/Editor/ConceptWorldPerformanceTests.cs`;
`Assets/_Game/Settings/World/ConceptWorld_v001/Concept_MainWorldStreamingSettings.asset`;
`docs/reports/concept-world-performance.md`, `docs/handoffs/sol6-high-mobile-2026-10-08.md`.
Публичные контракты: diagnostics и opt-in streaming budgets; не менять Core/Macro/WorldStreamer без отдельной переуступки с доказательствами.

## Зависимости
После baseline/routing/prefab/water/landscape. Выполнять последней, затем desktop acceptance.

## Приёмка
- [ ] 2/8/16 player configurations, несколько размеров и seeds.
- [ ] Отдельно bootstrap/MacroPlan/start time, detailed chunks/objects before MATCH READY, first-visit cost, memory/cache growth.
- [ ] Нет полной материализации карты; prefetch/active collider/far LOD budgets реально соблюдаются.
- [ ] LoadRadius в метрах и cache pressure учтены после перехода 64→32 m chunks.
- [ ] Нет увеличить density/budget без измерений или объявить nographics GPU benchmark.
- [ ] Full compile/EditMode/PlayMode + LFS clean checkout instructions.
- [ ] Телефонные commits/PR перечислены, desktop pull только fast-forward или осознанный merge, без reset/force push.
