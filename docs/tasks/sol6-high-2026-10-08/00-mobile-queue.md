Модель для каждого ручного запуска: **Sol 6 / High** (`gpt-6-sol`, `reasoning_effort=high`). Задачи подготовлены, агенты автоматически не запущены.

## База и состояние
- Код: [current-unity-fix](https://github.com/DimaFi/Little-Castle/tree/current-unity-fix), **не main**. Новая foundation: `edb9eae`; сохранение прежних локальных правок: `774c7d9`.
- Арт: [Little-Castle_Assets/main](https://github.com/DimaFi/Little-Castle_Assets/tree/main), Git LFS. Исходники, релизы, референсы опубликованы: 278 LFS paths / 172 уникальных объекта, ~392 MB.
- [Полная памятка телефон → ПК](https://github.com/DimaFi/Little-Castle/blob/current-unity-fix/docs/handoffs/sol6-high-mobile-2026-10-08.md) с ownership, зависимостями, prompt и безопасными git-командами.
- Compile PASS, 16 новых tests PASS; общий EditMode 76/78, PlayMode 0/1. Исторические NUnit XML включены в docs/reports/verification-2026-10-08. Baseline fixes — первая задача; не выдавать исходные результаты за будущий green run.

## Очередь
- [ ] [[Sol 6 High][P0] Восстановить проверяемый baseline Unity: 3 сбоя тестов](https://github.com/DimaFi/Little-Castle/issues/7)
- [ ] [[Sol 6 High][P1] Гарантировать переправы и связность конечной карты](https://github.com/DimaFi/Little-Castle/issues/8)
- [ ] [[Sol 6 High][P1] Подключить Bridge_Stone_A v002 в отдельный Unity concept-профиль](https://github.com/DimaFi/Little-Castle/issues/9)
- [ ] [[Sol 6 High][P1] Состыковать воду, берега и подходы фиксированного моста](https://github.com/DimaFi/Little-Castle/issues/10)
- [ ] [[Sol 6 High][P2] Довести concept-рельеф и поверхность до читаемого игрового вида](https://github.com/DimaFi/Little-Castle/issues/11)
- [ ] [[Sol 6 High][P2] Проверить бюджеты concept-мира и мобильный→ПК handoff](https://github.com/DimaFi/Little-Castle/issues/12)
- [ ] [[Sol 6 High][P1] Проверить portable checkout и зависимости арт-релизов](https://github.com/DimaFi/Little-Castle_Assets/issues/1)

## Порядок и конфликтующие файлы
Baseline первым. Art portability audit независимый.
Routing и prefab могут идти параллельно после baseline; prefab ждёт art gate.
Затем water/approaches → landscape → performance. Concept scene передаётся
между prefab/water/landscape последовательно. Точные файлы, классы, изменяемые
публичные контракты и минимальные тесты указаны в каждом Issue; чужие файлы не
редактировать без переуступки.

## Работа с телефона
Запустить одну задачу, указав репозиторий, базовую ветку и нужную модель.
Делать небольшие коммиты и push; оставлять SHA/тесты/pending PC checks в Issue.
PR (если создаётся) направлять в current-unity-fix, не старый main.
Не автоматически merge отдельный draft PR #6 и не force-push.

## Граница проверки
Облачное наличие репозитория не означает наличие лицензированного Unity или
Blender. При отсутствии среды подготовить код/tests/точные команды и оставить
Unity visual acceptance pending для ПК. Released art v001/v002 не менять.
Главный контракт: finite seeded world, unchanged fixed-size bridge, streamed
presentation, Built-in renderer, без watermills и навязанного равенства ресурсов.
