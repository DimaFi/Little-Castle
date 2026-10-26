# SpatialIndirectStudy: локальный эксперимент 26.10.2026

Не опубликован автоматически. Родительский Meadow checkpoint: 9e8c126abd2bf34a15df3bcbf01f7285cfe90843.
Это delta к lighting integration copy с уже импортированным Meadow snapshot,
не самостоятельный Unity project и не готовая миграция нового редактора карты.

payload содержит собственные новые/изменённые source assets с GUID. Общие shaders
переносить по функциям из patch; не заменять новые ветки целиком. Original before
sources сохранены под reference. ProjectSettings там служат только контрольными
снимками, их не применять к игровому проекту. Сторонние модели/текстуры не включены;
GUID и source SHA — reference/meadow-source-manifest.json. Старые map tool files
не входят в payload. Полный согласованный baseline пакет ранее опубликован в GitHub.

Начать с docs/reports/spatial-indirect-prototype-20261026-ru.md и экспериментального
ADR. Сходимость кеша занимает секунды, геометрия ограничена шестью AABB и плоской
землёй. Не включать в основной игре/редакторе по умолчанию. Baseline без keyword
сохраняет исходное выражение ambient. Новая Scene служит только стендом.

evidence включает сырые финальные измерения и labelled contact sheets реальных
Player PNG. Полные27 PNG и76 контрольных PNG остаются в Research/SpatialIndirect_20261026.
Локальный player-source-receipt связывает текущий собранный Player с manifest.
Точные хеши файлов packet — local-packet-manifest.json; zip не меняет источники.
