# Пространственный непрямой свет: ограниченный прототип

26.10.2026. Работа только в `E:/Games_Develop/Little-Castle_LightingIntegration_20261025`.
Исходный Meadow baseline сохранён и опубликован как
[`9e8c126abd2bf34a15df3bcbf01f7285cfe90843`](https://github.com/DimaFi/Little-Castle/commit/9e8c126abd2bf34a15df3bcbf01f7285cfe90843).
Новые GI-изменения — локальный эксперимент после этого checkpoint.

## Получившееся поведение

Крыша ограничивает свет неба; освещённый отражатель вне кадра меняет цвет стены.
Поле хранится в мировых координатах и не пересчитывается от перемещения камеры.
Снос/Undo обновляют ограниченную область проб и соответствующие GPU диапазоны.
Preview не меняет committed геометрию. Это первый проверяемый шаг к
пространственному свету; художественное совпадение с Tiny Glade не достигнуто.

Сцена `Assets/_Game/Scenes/SpatialIndirectStudy_20261026.unity` создана на основе
сохранённого совместного LightingMeadow: native MeadowGrassRenderer, новая
земля/маски дорожки, approved Oak A/B/C/D. Добавлены простые диагностические
стены, тонкая крыша, шары и цветной отражатель. Исходная сцена не перезаписана.
Это не окончательная художественная композиция и не внедрение новых зданий
в production catalog. Старый CottageGarden для этого теста не используется.

## Реализация и шейдерный контракт

- `SpatialIndirectTrace`: независимая от камеры/Physics трассировка, immutable
  массив proxy. Ограничение8 AABB; на стенде6: плоская земля, крыша, задняя стена,
  разделитель, отражатель, принимающая панель. Индивидуальные листья/травинки
  исключены; кроны дубов пока также не включены в GI-представление.
- Поле49×9×49=21609 проб; шаг1m; начало(-24,0.05,-24), габарит48×8×48m.
  128 детерминированных Fibonacci-направлений, длина луча6m.
- Irradiance/pi хранится четырьмя RGB-коэффициентами L1: c0+cxNx+cyNy+czNz.
  c0=mean(L), cAxis=2mean(L*dAxis). Дополнительный pi в материале не вводится.
  Есть видимое небо и одно Lambert-отражение от proxy с albedo. На отражателе
  проверяется видимость солнца/луны, плюс один отражённый вклад окружения.
- Геометрическая видимость окружения на поверхности кешируется по face/cell0.5m,
  12 cosine hemisphere rays. Смена цветов света использует этот кеш повторно.
  Он ленивый и mutable внутри solver: текущая реализация выполняется последовательно,
  не безопасна для одновременного Solve из разных потоков. При geometry commit
  proxy snapshot и его surface dictionary заменяются; новые irradiance считаются
  только в dirty области. Не реализована локальная персистентность surface cache.
- `SpatialIndirectCache`: конечная очередь, dedup, отрицательные мировые координаты,
  revision на пробу. Устаревший in-flight результат не может перезаписать новый.
- `SpatialIndirectVolume`: бюджет solve2ms/кадр, максимум256 проб. Одна проба может
  немного превысить остаток бюджета; upload измеряется отдельно. Dirty halo=2R+0.52m
  включает второй луч, footprint ячейки и bias. Старый/новый AABB инвалидируются
  отдельно; `SetData` отправляет точные изменённые диапазоны.
- `LC_SpatialIndirect.hlsl`: восемь соседних проб, normal bias0.08m, исключение
  проб внутри proxy и segment visibility guard против протекания через тонкие
  стены. Если все валидные соседи перекрыты, возвращается0; неизвестные пробы
  используют исходный ambient. На XZ/верхней границе поля blend1cell к baseline.
- Общие solid/terrain/foliage/grass forward shaders заменяют только hemisphere
  ambient на пространственный, затем сохраняют прежний tint и общий AO. Direct,
  transmission/emission, material CBUFFER, маски дорожек, instancing, ветер,
  interaction, LOD и depth/shadow/normals остаются по базовому контракту.
  Root AO и SSAO продолжают сочетаться через прежний `min`, без дополнительного
  множителя AO для нового GI. Видимость неба внутри GI сама по себе физически
  уменьшает свет; тест всех сочетаний этого occlusion с SSAO ещё нужен.
- `_LC_SPATIAL_INDIRECT` глобально включается владельцем только этой сцены,
  при её уничтожении keyword/buffer освобождаются. Target4.5 условен для этого
  варианта; baseline targets3.0/3.5 не повышены. Базовый Meadow в том же Player
  проверяется с выключенным keyword.

Новый код:4 runtime research-файла,1 editor builder,1 набор тестов,1 HLSL include,
отдельные scene/materials и их GUID. Точный перечень и SHA256 находятся в
`../lighting-integration/spatial-indirect-manifest.json`. Изменения семи существующих
шейдерных файлов — в `meadow-baseline-to-spatial-indirect.patch`. Исходники до GI
заморожены в `gi-baseline-9e8c126/manifest.json`, связанном с опубликованной базой.

## Проверки и реальные измерения

Артефакты: `E:/Games_Develop/Research/SpatialIndirect_20261026`.
Финальный каталог кадров/JSON: `FinalPrecision`; предыдущие `Pass01/Pass02/Final` не являются
финальной версией. Первую невалидную сборку с shader errors исключили; builder
теперь требует и BuildResult.Succeeded, и totalErrors==0.

**186/186 Edit Mode и6/6 Play Mode**. Восемь GI-тестов покрывают пересечения
тонкой/параллельной геометрии, энергию постоянного окружения, перекрытие крышей,
albedo/затенение отражателя, sky bounce и повторное использование transport cache,
локальный dedup/preview, stale revision, эквивалентность локального обновления
полному пересчёту после перемещения/перекраски препятствия.

Финальный Release Linear build:0 ошибок,0 предупреждений. В предыдущей сборке
было1 прежнее deprecated API warning `LocalSandboxController.FindFirstObjectByType`.
Shader warnings о потенциально
неинициализированном результате устранены. Unity online services сообщали
certificate errors; они не остановили локальные тесты или сборку.

<!-- FINAL_RESULTS -->

Финальный Player: PASS,27 непустых PNG1920×1080, включая12 кадров перемещения
камеры и день12:00/закат18:30/ночь01:00. Runtime-errors пуст. Автопроверка
результата: `evidence-validation.json`; просмотрены общий/ближний виды,
крыша, guard и ночь. Проверка тонкой стенки остаётся ограниченной этим proxy.

Точка под крышей: irradiance/pi RGB=(0.032203, 0.040078, 0.050098),
после сноса=(0.171347, 0.218524, 0.304535). Undo вернул исходные
коэффициенты детерминированно. ReflectorVisible=false; изменение его albedo
за пределами frustum меняет стену: красный канал 0.100116→0.114424.
CPU reference sample при переносе камеры не изменился. Это сравнение света
в одной мировой точке, не утверждение о нулевом screen-space AO noise.

Условия: Unity6000.5.5f1/URP17.5, Release, Linear/HDR/Neutral, D3D12,
RTX4070 Ti SUPER, i7-12700F,1920×1080, MSAA4, SSAO из Meadow baseline,
2048 shadowmap/2cascades/90m, soft shadows. Оба режима в одном последовательном
запуске:120 кадров прогрева,600 обычных endCameraRendering интервалов,
FrameTimingStats=true, VSync/FPS limit off, без readback/PNG/очереди обновления
во время benchmark. Сцена/камера одинаковы, ветер идёт. GI off в этом сравнении
означает strength0 у включённого варианта; отдельная regression сцена выключает
keyword полностью. Наши build/capture процессы во время benchmark не запускались;
остальные пользовательские приложения не изолировались.

| Режим | GPU mean,ms / samples | frame median / p95 / p99,ms |
|---|---:|---:|
| GI off | 0.840 / 502 | 1.187 / 2.824 / 3.205 |
| GI on | 1.184 / 557 | 1.350 / 2.210 / 2.940 |

Разница GPU mean примерно+0.344ms в этом маленьком стенде.
Это диагностический парный прогон, не обещание FPS большой карты. DXGI сообщил
о некорректных swapchain timestamps: wall intervals измерены Stopwatch по
рендеру камеры и не выдаются за display latency. GPU samples доступны не
на каждом кадре; сырые JSON сохранены.

| Изменение | Пробы / всего | CPU solve+upload,ms | До сходимости,s | GPU upload,bytes | max update одного кадра,ms |
|---|---:|---:|---:|---:|---:|
| initial-field | 21609 / 21609 | 1815.6+15.0 | 14.95 | 1382976 | 2.962 |
| offscreen-reflector-grey | 8019 / 21609 | 691.3+5.6 | 5.64 | 513216 | 2.176 |
| offscreen-reflector-restored | 8019 / 21609 | 677.4+4.7 | 5.53 | 513216 | 2.284 |
| roof-removal | 9801 / 21609 | 709.1+5.9 | 5.79 | 627264 | 2.616 |
| roof-undo | 9801 / 21609 | 847.5+6.7 | 6.87 | 627264 | 2.544 |
| sunset-light-refresh | 21609 / 21609 | 1702.7+11.2 | 13.89 | 1382976 | 2.271 |
| night-light-refresh | 21609 / 21609 | 1723.8+11.5 | 14.08 | 1382976 | 2.223 |
| day-light-refresh | 21609 / 21609 | 1722.3+12.1 | 14.02 | 1382976 | 2.315 |

Initial CPU fill включает solver warmup и заполнение геометрического dictionary.
Предел solve2ms кооперативный, не жёсткий realtime deadline: есть выброс
до2.962ms. Точную причину выброса без profiler trace не установили.
Полное поле GPU=1382976bytes (~1.32MiB); CPU probe array такого же размера,
плюс очередь/revisions/dictionary. В полном surface cache14316 entries;
его реальная память/GC отдельно не измерены. Чистая смена света сохраняет
surface cache, но обновляет все21609 проб активного поля. Локальный commit
отражателя затрагивает8019/21609, крыши9801/21609;
не всю карту. Даже эти5–7s задержки слишком велики для финального редактора.
Во время обновления могут видеться границы новых данных: temporal blending нет.

<!-- END_FINAL_RESULTS -->

## Воспроизведение

### Контроль исходного Meadow без GI

В последнем Player повторена вся исходная матрица76 кадров с уничтоженным
GI-полем и выключенным keyword: `MeadowRegressionPrecision`. Отдельно запущен
старый неизменённый `LightingMeadow_20261025/Player_Linear` в каталог
`FrozenBaselineRepeat`, чтобы отличить изменение shader-кода от разброса capture.
Его executable не заменялся и не пересобирался.

9 HDR-кадров совпали с Acceptance побайтно по RGB. В трёх hdr-oak-contact
разница максимум1/255 на17/85/98 пикселях из2073600; повтор старого Player тоже
показывает sparse one-byte differences и больший средний разброс. Причина этих
единичных округлений отдельно GPU trace не установлена. В AO-кадрах средняя
ошибка до~0.33/255; она сопоставима с контрольным повтором старого Player.
В исходном URP `ScreenSpaceAmbientOcclusionPass.cs`, строки196–204, подтверждены
смена blue-noise texture и `Random.value` offsets — точное равенство AO между
запусками не ожидается. Общий depth/shadow/normals код при этом не изменён.

Начальная проверка требовала76 точных совпадений и закономерно не прошла;
сохранены `evidence-validation-initial-strict.json` и `evidence-validation-HDR-strict.json`.
Финальный verifier требует точного равенства для9 HDR-кадров, где точен и сам
старый Player. Для трёх oak HDR проверяются magnitude/число пикселей относительно
старого повторного прогона; для64 AO-кадров — ограниченный empirical noise envelope
от того же контроля. Пределы/сырые ошибки каждого PNG открыто записаны в JSON.
Это диагностическая регрессия и визуальный просмотр, не математическое
доказательство отсутствия любого изменения. Снижение точности ambient в GI-off
варианте предотвращено macro passthrough без промежуточного half3 аргумента;
повторный контроль показал, что редкие oak differences существуют и без этой правки.
Road/vegetation masks, Undo digest, native instancing/LOD и runtime checks прошли,
runtime-errors пуст. Основные ProjectSettings, frozen source snapshot и protected
shader functions/passes отдельно проверяет `spatial-indirect-audit.json`.

Автономный `Player/SpatialIndirectStudy.exe`, обычное видимое окно1920×1080.
`-giStudy -giOutput <абсолютный каталог>` выполняет проверки/кадры/замеры и выходит.
Без этого флага: F1 indirect on/off, F2 три камеры, F3 день/закат/ночь с явным
обновлением кеша, F4 крыша, F5 interpolation visibility guard, Esc выход.
Никакие пользовательские приложения, сохранения или сцены редактора не используются.
`-giMeadowRegression -lmCapture -lmOutput <каталог>` загружает сохранённую Meadow
сцену, уничтожает поле и запускает исходную матрицу снимков.

Повторная сборка: Unity batch executeMethod
`LittleCastle.Editor.SpatialIndirectStudyBuild.BuildLinear`. Builder временно
выбирает Linear/FrameTimingStats только для собственного Player, затем побайтно
восстанавливает GraphicsSettings/QualitySettings/ProjectSettings и прежние runtime
профили. Не запускать одновременно с чужим открытым проектом по этому же пути.
Tools/LightingStudy: `audit_spatial_indirect.py`,
`verify_spatial_indirect_evidence.py --run FinalPrecision`; frozen baseline не перезаписывать.

## Ограничения и следующая интеграция

Цена steady GI не включает обновление кеша. При бюджете2ms CPU текущая сходимость
занимает секунды даже для маленького поля — так включать на основной карте рано.
Нет ускоряющей структуры, compute/jobs, probe prioritisation, moving volume,
temporal blend версий или автоматической injection при непрерывном ходе суток.
F3/автотест явно запускают полное обновление активного bounded поля; постоянная
смена солнца пока не решена. Surface sky transport переиспользуется, но primary
visibility/sun traces при смене света рассчитываются снова.

AABB не представляют сложные крыши/стены, рельеф и кроны. Земля в proxy плоская,
поэтому она лишь приближённо совпадает с визуальным Meadow. L1 и сетка1m сглаживают
свет; конечные6m лучи дают приближение удалённых перекрытий. Visibility guard
устраняет пересечение именно query-to-probe сегмента, но не все ошибки переноса:
нужны отдельные тесты тонких полостей, углов, склонов и стыков тайлов. Блокированные
ячейки могут дать тёмные участки вместо утечки. Нет многократных отражений,
отражённых локальных emissive/point источников и участия полупрозрачных листьев.
GC/пиковая память surface dictionary и цена плотного поселения не профилированы.

Ночь стенда сохраняет видимость крупных форм, но настоящая приёмка ночных зданий
потребует бюджетных фонарей/окон и проверки управления; здесь новые фонари не
внедрялись. Мягкость солнца/палитра/SSAO остаются измеренной базой Meadow.

Повторно прочитан актуальный MapEditSession: `MapChangeKind.Surface|Decoration|
Height|Objects`, Save v4, `preview` и XZ Rect. В событии по-прежнему нет transaction
revision; authored.revision — версия базы. Новый Height/Objects API только прочитан,
его файлы не копировались в lighting. Будущий адаптер получает committed snapshot
высот/объектов, учитывает старые/новые bounds, владеет presentation epoch, не
дёргает GPU readback и не блокирует редактор. Каждая травинка/лист не становится
GI-препятствием. Точный контракт — `local-cache-invalidation-ru.md`.

Следующее исследование: компактная геометрия и ускорение локального transport,
разделение геометрии/injection, быстрое заполнение видимых проб и плавная
публикация. Только после этого совместный тест на новом рельефе и согласованный
перенос по функциям. См. экспериментальное ADR; это не принятие GI для игры.

## Tiny Glade и исходники

Ориентир сохраняется. Проверенные первичные ссылки и лицензии записаны в
`../lighting-integration/tiny-glade-source-research-20261026-ru.md`:
[официальная информация](https://pouncelight.games/tiny-glade/info/),
[доклад разработчика](https://www.youtube.com/watch?v=jusWW2pPnA0),
[kajiya GI overview](https://github.com/EmbarkStudios/kajiya/blob/main/docs/gi-overview.md),
[ранний country-slice](https://github.com/anopara/country-slice).
Внешний код в этот прототип не копировался. Он проверяет похожие свойства
пространственного света, но не воспроизводит полный renderer или shader code Tiny Glade.
