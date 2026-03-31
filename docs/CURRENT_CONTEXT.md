# PassNotes — Current Context

## Текущая рабочая база

- Активная база: `_230`
- Следующий суффикс архива: `_292`

## Активная ветка

- Текущее направление: новая основная дизайн-ветка `Этап 5 / Подблоки 5.X`
- Последний завершенный основной блок: `Этап 5 / Подблок 5.19`
- Текущий активный основной блок: — (следующий основной блок до отдельного подтверждения не открыт)
- Последний завершенный service-блок: `Этап 0 / Подблок 0.49`
- Текущий активный service-блок: `Этап 0 / Подблок 0.50` — public GitHub snapshot / first push по отдельному подтверждению пользователя
- Канонический дизайн-документ Sage Light: `docs/PASSNOTES_LIGHT_THEME_TZ.md`
- Актуальная концепция runtime-темы `light` (документное имя: `Sage Light`): warm-neutral premium productivity UI с natural olive accent; прежняя office-blue трактовка больше не является источником истины.
- Официально зарегистрированные runtime-темы: `standard` (`Standard` / `Стандартная`), `light` (`Sage` / `Шалфей`), `arctic-white` (`Frost` / `Иней`), `midnight-slate` (`Midnight` / `Полночь`), `amber-circuit` (`Amber` / `Янтарь`).
- Для первого запуска и новой установки runtime-темой по умолчанию является `amber-circuit`; сохраненные `ThemeId` существующих пользователей не мигрируются и продолжают использоваться как есть.
- Для первого запуска и новой установки язык интерфейса по умолчанию: `en-US`; уже сохраненный `Language` существующих пользователей не мигрируется.
- `light` сохраняет внутренний `themeId = "light"` ради совместимости, а `Arctic White`, `Midnight Slate` и `Amber Circuit` зарегистрированы как отдельные runtime-темы через `AppThemeCatalog` и собственные theme dictionaries.
- Текущая лучшая по читаемости палитра `Amber Circuit` зафиксирована в `Themes/Theme.AmberCircuit.xaml`; опорные цвета: `AppBackground #090B0E`, `WindowBackground #0E1216`, `SurfaceBackground #151B21`, `SurfaceAltBackground #18232A`, `TextPrimary #ECD3A6`, `Accent #F0A33A`, `AccentHover #FFB958`, `ControlHover #1B2D34`, `GridHeaderBackground #1A1714`, `SelectionBackground #312317`, `SelectionBorder #F0A33A`, `TabSelectedBackground #1C1611`.

## Что отложено

- `Этап 2 / Подблок 2.1` и старая немедленная theme/spec-ветка: не стартуют и не оживляются как активный маршрут
- Масштаб `175%` в DPI/layout-хвосте: временно допустимое ограничение вне обязательного scope
- Другие крупные service-ветки: только по новому явному решению пользователя
- Installer/distribution route для Boosty: следующий отдельный service-маршрут после закрытия `Этап 0 / Подблок 0.50`
- Текущий ближайший практический шаг: public GitHub snapshot / первый push (`Этап 0 / Подблок 0.50`) без публикации полной локальной истории; installer-route откладывается до закрытия этого шага

## Ключевые инварианты

- Не ломать multi-select / Ctrl+A / Del / drag&drop / tray
- Клик в пустоту снимает выделение, правый контекст не сбрасывается
- Не вводить отдельный постоянный режим “все записи”; допустим только явный глобальный поиск по непустому запросу
- Только безопасные форматы import/export/backup
- Не переименовывать exe
- Не менять `%APPDATA%` и пути данных
- Публичное имя приложения: `PassNotes Desktop`

## Не делать

- Автозапуск Windows
- Restore в исходную папку
- Расширенные UI prefs
- Масштаб/размер шрифта дерева и таблицы
- Незашифрованный JSON export
- Преждевременное натягивание темы вне подтвержденной ветки `5.X`

## Источники истины

- `docs/STATUS.md`
- `docs/CHANGELOG.md`
- `docs/PassNotes_Plan_Final_Ideal.md`
- `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md`
- `docs/NAKED_BASELINE_MASTER_PLAN.md`
- `docs/PASSNOTES_LIGHT_THEME_TZ.md`
- `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`
