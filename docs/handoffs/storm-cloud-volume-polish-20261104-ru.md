# Little Castle — грозовые тучи: устранение плоских полос и улучшение объёмного вида
Дата: 2026-11-04 · новая ветка: `gpt6/storm-cloud-volume-polish-20261104`
Основа: PR #98 @ `58199c5fa4734f440c20a16987e9d696b547f715`.

## Почему на Game-скриншоте 18:40 облака не похожи на референс
1. `CloudLayer` продолжал занимать до 100% кадра при Storm. Этот старый
   плоский overcast рисует вытянутые полосы, которые перебивают новые формы.
2. `StormCloudBank` / `StormPeakCloudLayer` использовали 2D value noise
   с делением координат на высоту взгляда. Вдали это даёт размытые
   горизонтальные слои вместо округлых кучевых облаков.
3. Skybox всегда позади гор. Модуль summit clouds из PR #98 существует
   только на `LandscapeBorderPresentation`, а его
   `peakCloudMinimumDistance=115` м мог выключать карточки на игровой камере,
   особенно при приближении.
4. Мы не подтвердили, что в локальной пользовательской сцене активен
   `Little Castle/Sky/Stylized Day Night`, или что горы используют
   `LandscapeBorderPresentation`. Утверждать обратное без Game QA нельзя.

## Реализовано
- `LC_StormCloudField.hlsl` — **новый** компактный 3D value-noise
  field, 2 octave Low/Medium и 3 octave High. Не ray-marching
  и не тяжёлый volumetric clouds; даёт округлую форму в skybox
  без плоской проекции.
- `StylizedDayNightSky.shader` — переработаны оба storm bank слоя:
  varied vertical outlines, pillowy density, shaded undersides,
  painterly lit crests, optional fake self-shadow on High,
  направленная вспышка существующей молнии. Сильное ослабление
  прежнего стрипа overcast при Storm (но сохраняется как фон).
  `_StormCloudMass=0` возвращает старую спокойную погоду.
- `MountainBorderProfile` — трёхрядные billow облака перед
  вершинами по умолчанию, 14 anchor bands, регулируемая ширина и
  прозрачность; безопасный lower-distance threshold 58 м вместо
  115 м (фрагментный fade + камера не пересекает облако).
- `MountainPeakCloudPresentation` — один батч из 2–3
  staggered cards на точку высокогорья, отчёт `CardCount`.
  `LandscapeBorderPresentation` сообщает количество созданных
  карточек; ContextMenu «Diagnose far mountain storm clouds».
- `LC_MountainPeakClouds.shader` — объёмно выглядящий силуэт
  из 3 округлых lobes с 3D-noise эрозией, мягкими краями,
  затенённым низом и локальной подсветкой при молнии.
- `RainStormController` — во время Storm на Game debug panel строка
  **«Тучи: NN% | горы: NN%»**, считывает *фактически активный*
  RenderSettings.skybox и world cloud intensity, а не желаемое
  значение из RainStormProfile. Если материал несовместим:
  «Облака: проверь skybox / шейдер».
- Регрессионные тесты требуют поддерживаемых shader и
  проверяют новые параметры профиля. В эту PR не входят исправления
  воды/снега/травы — они остаются в #96-#98.

## Что нельзя считать проверенным
**Ни одна Unity C# assembly или HLSL программа здесь не компилировалась в
настоящем Unity.** Не запускались PlayMode, GPU profiler и local author map.
Всё выше — изменения исходников и статический просмотр связей.
Код должен пройти Unity import/compile/QA до переноса в production.

## Codex — как перенести и принять
1. Сохранить локальные WIP / сцену / авторскую карту и 32 отсутствующих
   Art GUID; PR #95 → #96 → #97 → #98 → эта ветка (stacked).
   Не делать прямой checkout поверх несохранённой локальной сцены.
2. Unity 6000.5.5f1 + URP17.5: C# compile, HLSL compile,
   `WeatherVisualPolishTests`, `MountainPeakStormCloudTests`,
   `RainStormMathTests`, `AmbientSnowfallTests`, PlayMode.
3. В той же Game-сцене, на той же камере, в режиме **Гроза**:
   - при 100% осадков в панели должно быть «Тучи: ...%»,
     а не «проверь skybox». Если 0%: проверить
     `RainStormController.Settings.driveExistingAtmosphere`,
     `atmosphere`, `DayNightLightingController` и source skybox.
   - выделить `Mountain border · north sector`, проверить
     `LandscapeBorderPresentation` и `profile`. В ContextMenu
     «Diagnose far mountain storm clouds» проверить, что карточек > 0,
     shader доступен и Storm intensity > .03.
   - облака должны быть **чёткими крупными округлыми формами**,
     а не горизонтальными шлейфами; самые дальние вершины частично
     окутаны карточками, но остаются читаемыми.
   - проверить Low/Medium/High *Atmosphere* отдельно от
     Low/Medium/High *Rain*; эти меню раньше были независимы.
4. Отдалённая молния должна освещать только соседнюю облачную массу;
   геометрию/частоту/тайминг молний в этой ветке не меняли.
5. Off/Rain/Storm, день/18:30/ночь, zoom в сторону гор:
   карточки исчезают плавно до 58 м, не накрывают игровую камеру
   или здания, не меняют физику и карту.
6. Измерить sky GPU time/overdraw High и Low, материал активной сцены,
   нет ли shader compilation errors. При дорогом High снизить
   `atmosphereQuality`, не переписывая атмосферу и не трогая воду.
7. В случае несовпадения со сценой: дорабатывать данные **на реальном
   mountain profile** после первого Game-скриншота; не считать
   procedural noise точным эквивалентом художественного референса.

## Не изменялось
Генерация молний, дождь/снег, вода, водопад, текстуры травы, основная
карта и её сохранения. Ветка Draft, без merge в main.
