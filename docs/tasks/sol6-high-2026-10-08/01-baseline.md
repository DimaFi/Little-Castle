# [Sol 6 High][P0] Восстановить проверяемый baseline Unity: 3 сбоя тестов

Исполнитель: **Sol 6 / High** (`gpt-6-sol`, `reasoning_effort=high`). Это параметры для ручного запуска, Issue сам агента не запускает.

Репозиторий: `DimaFi/Little-Castle`. База: ветка **`current-unity-fix`**, не старый `main`; foundation commit `edb9eae5949966f8a0b7a71a4f0354ee7d427a8e`. Сначала прочитать `AGENTS.md`, `docs/handoffs/sol6-high-mobile-2026-10-08.md`, `docs/reports/bridge-world-v002-2026-10-08.md` и архитектуру своего подсистемного участка.

Общие ограничения: конечная карта, детерминизм/negative chunk seams, plain authoritative data отдельно от presentation, Built-in renderer, без watermills/URP/HDRP/зеркального выравнивания ресурсов. Не редактировать чужие файлы без согласованной переуступки. Перед изменениями проверить актуальные commits и работающих исполнителей. При отсутствии Unity/Blender/LFS не объявлять проверки PASS: зафиксировать точную команду и необходимость проверки на ПК.

## Результат
Воспроизвести, объяснить и минимально исправить три сбоя предыдущего прогона: NightLightEmitter initial enabled, Streamer first-load budget (0 вместо 1), PlayMode old mesh release. Не ослаблять asserts ради зелёного отчёта; сначала проверить, не устарели ли предположения тестов после cooperative streaming.

## Владение файлами
- `Assets/_Game/Scripts/World/Rendering/NightLightEmitter.cs`.
- `Assets/_Game/Scripts/World/Streaming/WorldStreamer.cs`, `StreamedChunkView.cs`.
- `Assets/_Game/Tests/Editor/NightLightingRuntimeTests.cs`, `WorldGenerationIntegrationTests.cs`.
- `Assets/_Game/Tests/PlayMode/WorldStreamerPlayModeTests.cs`.
- `docs/reports/sol6-baseline-verification.md`.
Публичные контракты: исправлять жизненный цикл/fade и streaming scheduling, сохраняя serialized fields, API, delta persistence и бюджеты. Остальные rendering/generation/profile файлы не принадлежат этой задаче.

## Зависимости
Первая задача. Выполнять последовательно до других Unity интеграций. Исходные результаты: 16 новых tests PASS, полный EditMode 76/78, PlayMode 0/1; они сохранены в `docs/reports/verification-2026-10-08/`.

## Приёмка
- [ ] Корневая причина каждого сбоя доказана, baseline comparison выполнен или отсутствие явно отмечено.
- [ ] Полный compile/EditMode/PlayMode прогон; новые 16 тестов не сломаны.
- [ ] Mesh cleanup/cache/runtime-delta и fade проверены без instant pop.
- [ ] Нет изменений Main* settings, сцены или расширения light budget для обхода ошибки.
- [ ] Отчёт: файлы, API, команды, фактические результаты, коммит/PR и оставшиеся ограничения.
