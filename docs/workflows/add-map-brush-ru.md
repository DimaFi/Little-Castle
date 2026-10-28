# Как создать и подключить новую кисть в Little Castle

Новый реальный пример — `MapEnvironmentTool`: две регистрации
`environment.biome/environment.snow`, общий `MapToolController`, операции
`BeginEnvironmentStroke/ApplyEnvironmentBrush`, локальные tile snapshots
в `MapEditSession.Environment.cs`. UI не владеет данными биома или снегом.
Региональные подписчики используют `MapChangeKind.Biomes/Snow`.
Схема сохранения и визуализации: [environment authoring](../architecture/environment-authoring-and-local-snow.md).

1. Выберите существующую runtime operation в `MapEditSession`. Если нужна новая
   операция, добавьте data contract, проверку прав/границ, локальную дельту,
   Undo/Redo и сохранение там. UI не должен менять списки карты напрямую.
2. Реализуйте `IMapTool` либо наследуйте `MapStrokeTool`. Задайте уникальный ID,
   название, категорию, `Controls` и maximum radius. Для кликов/контрольных точек
   используйте `MapClickTool` или свой `IMapTool` с нужным Gesture.
3. В Begin откройте session transaction; в Apply вызывайте узкую операцию;
   в End commit/cancel. Не читайте `Input`, `Event.current`, не создавайте
   камеру и не заводите новый обработчик мыши.
4. Зарегистрируйте модуль в `MapToolRegistry.ExistingBrushes()` либо в месте
   подключения специализированного каталога в `InitializeTools` окна.
   Общий popup автоматически использует метаданные registry. Для сложного
   draft оставьте дополнительные поля в соответствующем UI partial.
5. Подпишите представление/кеш на `session.Changed`. Проверяйте kind и bounds,
   обновляйте только резидентные данные. Для дорогой работы используйте
   preview=false или собственный бюджет очереди. Не меняйте карту из подписчика.
6. Добавьте проверки самого алгоритма и controller-level сценарий:
   stroke/hold/cancel/Undo/Redo/save, negative coordinates, borders, zoom.

Реальный пример — `MapSurfaceTool`, который уже обслуживает луг, дорожку и
стирание травы. Begin создаёт `SurfacePaintStroke` с общими radius/strength/
softness и специальными scatter/density/profile. Apply вызывает AppendPoint.
End унаследован от MapStrokeTool и вызывает CommitStroke/CancelStroke.
Rasterizer и scatter остаются специализированными потребителями данных.

```csharp
// Пример регистрации ещё одного отображения существующей механики.
// ID, title, category и операции нового алгоритма задаёт его модуль.
registry.Register(new MapSurfaceTool("surface.path-preview", "Дорожка", true, false));
```

Не добавляйте обработку нового ID в SceneGUI: registry и controller уже отвечают
за ввод. Callback `MapClickTool` подходит для выбора дерева по ray, посадки по
surface position и добавления control point. River Apply остаётся собственной
командой, поскольку draft из нескольких кликов не является круглым штрихом.

В окне `Little Castle → Map → World Editor` можно сохранить профиль кисти
в `.asset` и применить его. Профиль хранит tool ID и общие параметры; данные
библиотеки растений/геометрия реки принадлежат своим каталогам и draft.
Новый ScriptableObject-компонент размещайте в файле с совпадающим именем класса.

Runtime операции не должны ссылаться на UnityEditor. В будущем игровое действие
вызывает те же операции с ограниченным `MapEditAccess`; UI автора, затраты и
сетевая авторизация остаются отдельными адаптерами.
