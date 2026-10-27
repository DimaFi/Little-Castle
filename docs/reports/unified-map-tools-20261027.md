# Little Castle — единая система кистей, этап 1

Ветка: `codex/unified-map-tools-20261027`. Изолированная копия:
`E:/Games_Develop/Little-Castle_BrushFoundation_20261027`.

## Результат

Окно и пункт меню называются **World Editor**. Инструкция самостоятельной
проверки: `docs/workflows/test-world-editor-ru.md`. База текущей сцены
подключается при открытии окна автоматически.

Существующие луг/дорожка/стирание, подъём/опускание, роща, одиночные операции
деревьев и контрольные точки рек подключены к одному runtime API инструментов.
Оба интерфейса используют общие lifecycle, world-space настройки, выбор
поверхности и транзакции. River draft/Apply и tree crown picking сохранены
специализированными. Новые биомы/снег/ассеты не разрабатывались.

Созданы registry/controller/context/settings, ScriptableObject preset,
authoritative surface query, общий falloff/контур и batch региональных событий.
Убраны независимые mouse loops деревьев/рек. Форматы session/save и ID объектов
переиспользованы. Добавлена обратимо сериализуемая мягкость surface stroke.
Существующие мягкие края сохранены для старых данных.

Исправлена совместимость terrain/tree views с несколькими локальными событиями
одной ревизии: они больше не пропускают соседние регионы из-за общего revision ID.
Ни shaders, ни материалы, ни параметры освещения не входят в refactoring diff.
Проверочный запуск использует существующий ApplyGlobals текущей Meadow-сцены,
чтобы увидеть её прежнее освещение вне Play Mode.

## Проверки Unity

Результаты находятся в `Research/BrushFoundation_20261027` рядом с рабочими копиями.
Не перезаписывались исторические отчёты и Player других этапов.

- Unity **6000.5.5f1**, Direct3D12 для графических проверок; URP **17.5.0**.
- Профильные EditMode: **62/62 Passed** в полном финальном прогоне.
  Включают 23 новых случая: общий lifecycle трёх surface modes, hold/cancel,
  права, zero strength, настройки штриха, long-drag budget/endpoint, falloff,
  negative borders, локальные события, старый fingerprint, save replay,
  4096 логических деревьев, настоящий preset asset reload и runtime dependencies.
- Профильные PlayMode: **7/7 Passed**. Реальные Meadow adapter/GPU masks,
  terrain chunks/растения/деревья/коллизия, river bounds, stable tree selection,
  общий controller и несколько регионов одной ревизии.
- Реальные `SceneView.SendEvent(MouseDown/Drag/Up)` при размерах камеры
  **12 / 90 / 1200**: три одинаковых пути по **15 точек**, радиус **3 м**.
  Тот же SceneGUI поднимает землю (**332 узла**); Undo/Redo и повторное открытие
  авторского JSON сохраняют результат. `sceneview-automated.txt` содержит PASS.
- Ray/triangle тесты на склоне с расстояний **5 / 500 / 50 000 м**: Passed.
  Первый дефект float precision исправлен переносом луча к envelope карты.
- Отдельная проверка настоящих native grass и каталогов: validator ошибок
  не обнаружил; старые DeadTree/деревья без leaf-материала дают ожидаемые
  предупреждения отсутствия foliage renderer. Геометрия/маски ветра не менялись.
- GUI через computer-use: окно, категории registry, настройки силы, контур и
  zoom проверены на существующей Meadow-сцене. Native drag automation не
  использован как единственное доказательство: его события отдельно проверены
  через реальный Unity SceneView event adapter.
- Отдельный Release Player `Player/Meadow_UnifiedTools.exe`: сборка Succeeded,
  **0 ошибок / 1 прежнее obsolete API warning** в LocalSandboxController.
  Автоматические проверки покрытия: **9/9 PASS**, `Errors` в Player report пуст.
  Кадры 01_meadow и 02_close просмотрены. Это существующая Meadow-сцена,
  без регенерации источников, смены шейдеров или визуального редизайна.
  RTX 4070 Ti SUPER, i7-12700F, D3D12, 1600×900, MSAA4.
  Release GC recorder недоступен; нули GC/GPU CSV не объявляются замером
  отсутствия аллокаций или нулевого GPU времени.

Полный PlayMode: **14/14 Passed**, включая существующий finite-map streaming сценарий.

Полный EditMode: **243 total / 241 Passed / 2 Failed**. Два непрофильных сбоя:

1. `ConceptMaterialAndCatalogReferenceRealApprovedAssets` — старый concept
   material ссылается на локальную `TerrainStarter_v001/T_Oak_Kit_Palette.png`.
2. `RuntimeQuality_ChangesCloneAndRestoresSlotWithoutChangingSharedAsset` —
   старый URP quality template сообщает soft shadows=true, тест ожидает false.

Эти тесты/quality implementation/asset inputs не менялись данным рефакторингом.
Не скрыты и не ослаблены assertions; это ограничения общего baseline,
которые требуют отдельной сверки с владельцем rendering/asset integration.
Полный проект не объявляется зелёным только на основании профильных тестов.

## Нагрузка и ограничения

Радиус 12 м с **4096** исходными tree records: **3.458 мс** на локальную data
операцию в Editor (один замер; без отрисовки леса). ID и число деревьев
сохраняются после height Undo/Redo. Это не GPU/FPS benchmark большой карты.
Тест большой поверхности подтверждает обновление только резидентных tiles,
неизменность удалённого tile и отсутствие whole-map allocation.

Во время непрерывного ввода — максимум 32 dabs за tick, 15 Hz и sampling budget
4 мс между dabs. Стоимость зависимых представлений/уведомлений в этот лимит не входит.
Commit/Undo/Redo/Cancel сохраняют точный набор регионов длинного штриха. Height radius
ограничен размером lattice; старый диапазон до 32 м сохранён и на мелкой сетке.
Единичная операция большого радиуса всё ещё синхронная. Явные Save/смена tool
могут дождаться оставшегося пути. Точное время на огромной карте и слабом GPU
**NOT RUN**; адаптер Meadow ограничен своим участком, настоящий streamer ручной
карты по-прежнему отдельная задача. Пределы не повышались до неограниченного
радиуса ради формального редактирования всей карты одним dab.

Не проверено вручную исчерпывающее сочетание всех river drafts, каталогов,
камер и пользовательских preset на большой production-карте. Существующие
автоматические river/tree тесты сохранены. Ветка новых берегов/воды и PR #79
не объединялись. Исходные рабочие копии и пользовательский Player не сбрасывались.

## Продолжение и подключение

- Архитектура и аудит веток: `docs/architecture/unified-map-tools.md`.
- Краткая инструкция и реальный пример: `docs/workflows/add-map-brush-ru.md`.
- Биомы: следующий модуль может использовать тот же controller; его field
  deltas/save migration и адаптер PR #79 ещё нужно реализовать отдельно.
- Зима: shared input готов, snow simulation/material integration отсутствует.
- Игровое строительство: runtime operations доступны без UnityEditor;
  экономика, серверная авторизация и сеть не реализованы.

Не делать merge вслепую в старый main: там отсутствуют исходные ручные кисти.
Первый коммит `d2094da` фиксирует существующие source dependencies, следующий
коммит содержит собственно этап 1. Сцены и локальные art fixtures нужны для
полного визуального Meadow стенда и остаются во внешней библиотеке.
На этом этапе разработка останавливается; этап 2 и биомы не запускались.
