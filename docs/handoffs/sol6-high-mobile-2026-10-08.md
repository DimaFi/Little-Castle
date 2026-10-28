# Передача работы: телефон → Chat → GitHub → Codex на ПК

Дата: 2026-10-08. Все задания рассчитаны на **Sol 6 / High**
(`gpt-6-sol`, `reasoning_effort=high`). GitHub Issues — очередь заданий,
не доказательство запуска/завершения агента. Модель и reasoning выбираются
при запуске; при отсутствии нужной модели не подменять её молча.

Главная GitHub-очередь: [Issue #13](https://github.com/DimaFi/Little-Castle/issues/13).

## Откуда продолжать

- Unity: [DimaFi/Little-Castle — current-unity-fix](https://github.com/DimaFi/Little-Castle/tree/current-unity-fix).
  **Не старый main.** Сохранение прежней работы: `774c7d9`;
  новый мост/генерация: `edb9eae5949966f8a0b7a71a4f0354ee7d427a8e`.
- Арт: [DimaFi/Little-Castle_Assets — main](https://github.com/DimaFi/Little-Castle_Assets/tree/main).
  Исходники/релизы: `5075ef3`; raw-byte release checkout fix: `b4a0d2d`.
  LFS transfer: 172 unique objects, about 392 MB. Run `git lfs pull`.
- Существует отдельный draft [PR #6](https://github.com/DimaFi/Little-Castle/pull/6)
  из gameplay/preflight направления. Не считать его уже merged, не начинать
  новую работу с другой foundation-ветки и не объединять две реализации
  standardized bridges без анализа контрактов.

## Очередь и ownership

| Порядок | Issue | Основная область владения | Зависимости |
|---|---|---|---|
| A | [Baseline: 3 сбоя](https://github.com/DimaFi/Little-Castle/issues/7) | NightLightEmitter, WorldStreamer/StreamedChunkView, соответствующие старые tests | первая |
| B | [Маршруты и обязательные переправы](https://github.com/DimaFi/Little-Castle/issues/8) | Macro planners/data, bridge routing tests/docs | после A |
| C | [Prefab моста в Unity](https://github.com/DimaFi/Little-Castle/issues/9) | новый Bridge import/prefab, editor importer, concept catalog/scene | после A и арт-аудита |
| D | [Вода и подходы](https://github.com/DimaFi/Little-Castle/issues/10) | новые crossing/water presentation classes, concept scene | после B и C |
| E | [Рельеф и поверхность](https://github.com/DimaFi/Little-Castle/issues/11) | landform sampler/settings/stages/tests, concept scene | после D |
| F | [Производительность и handoff](https://github.com/DimaFi/Little-Castle/issues/12) | diagnostics, concept streaming settings, report/handoff | после E и всех предыдущих |
| Art | [Portable asset audit](https://github.com/DimaFi/Little-Castle_Assets/issues/1) | source-repo verification/build recipe tools | независимо; результат нужен C |

```text
A baseline ───> B routing ──────────┐
    └─────────> C prefab ───────────┼──> D water/approaches ──> E landscape ──> F budgets
Art portable audit ──> C           ┘
```

A выполняется первой. Затем B и C могут работать параллельно, если Art gate
готов. D/C/E меняют одну concept scene: строго последовательно с явной передачей
владения. В каждом Issue есть точные разрешённые файлы, публичные контракты,
минимальные tests и формат отчёта. Файлы вне списка не редактировать без
переуступки. Старый `bridge-world-ownership-2026-10-08.md` описывает уже
завершённую исходную работу, не параллельное разрешение новым исполнителям.

## Готовая команда для Chat с телефона

> Работай с DimaFi/Little-Castle, база current-unity-fix, модель Sol 6,
> reasoning High. Прочитай AGENTS.md и docs/handoffs/sol6-high-mobile-2026-10-08.md.
> Начни с issue 7; соблюдай её file ownership.
> Не запускай остальные зависимости раньше времени. Делай отдельные коммиты,
> отправляй их в GitHub и оставляй в Issue отчёт с commit SHA, тестами и тем,
> что нужно проверить на ПК. Не объявляй Unity проверки успешными, если Unity
> реально не запускался. Не используй reset/force-push и не меняй main.

Для следующего задания заменить номер Issue, предварительно проверить его
зависимости и свежие commits. Новую рабочую ветку создавать от актуального
`current-unity-fix`; при создании PR base должен быть `current-unity-fix`.
Не перезаписывать файлы одновременной работой нескольких cloud-задач.

## Честный baseline

Unity 6000.5.5f1: compilation PASS; 16 новых tests PASS. Полный EditMode
76/78, PlayMode 0/1. Ошибки освещения/streaming перечислены в
`docs/reports/bridge-world-v002-2026-10-08.md`. Исходные NUnit XML сохранены
в `docs/reports/verification-2026-10-08/`. Они НЕ являются результатами будущих
Sol задач. Не переписывать их после исправлений; добавить новый dated report.

Мост v002 прошёл source/FBX/Cycles/Eevee review, но ещё не импортирован и
не визуально принят в Unity. C# stamp/profile готовы; общая вода, socket
transitions, rerouting/connectivity и GPU budgets пока не закончены.

## Граница «всё загружено»

Опубликованы текущие отслеживаемые правки, новые scripts/settings/docs,
версионные source-art и releases. Исключены Unity Library/Temp/Logs, build
outputs, Blender recovery и Python cache. Старые ignored
`Models/Ready`, `Textures/Ready`, TerrainStarter Unity import и legacy
art library не превращены в production Git imports. Они остаются на этом ПК.
Новые cloud import tasks не должны ссылаться на эти отсутствующие папки —
нужны published release и воспроизводимый importer.

## Возвращение к ПК

Сначала проверить `git status` в обоих репозиториях. Сохранить новые локальные
правки отдельным коммитом/безопасным stash до pull; ничего не reset-ить.

```powershell
# Little-Castle
git fetch origin
git switch current-unity-fix
git pull --ff-only origin current-unity-fix

# Little-Castle_Assets
git fetch origin
git switch main
git pull --ff-only origin main
git lfs pull
```

При divergence остановиться и осознанно merge/rebase после проверки commits,
а не force-push. Если телефонные изменения остались в отдельной ветке/PR,
сначала проверить и интегрировать их, не считать автоматически принятыми.

На ПК повторить Unity import/compile, полный EditMode/PlayMode, production
validators и close/gameplay/far/day/night проверку. Архитектура: конечная карта,
детерминизм, plain-data generation, lazy presentation, Built-in renderer.
Нет watermills. Будущие персонажи имеют player-flag color accents; merchant
нейтральный белый/кремовый. Полные референсы/план — в concept-world-direction.
