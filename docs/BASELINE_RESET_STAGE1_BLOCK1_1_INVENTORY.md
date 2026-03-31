# Этап 1 / Подблок 1.1 — инвентаризация baseline-reset и единая нейтральная ресурсная схема

Дата фиксации: 2026-03-08 (Europe/Moscow)

## 1. Источник истины для этого подблока

- Активная baseline-база для текущего нового захода: `_230`.
- Стратегический документ остаётся опорным: `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md`.
- Ссылка на архив `229` в стратегии трактуется как историческая стартовая ссылка стратегии, но не как текущая активная база этого подблока.
- Все старые неудачные попытки по дизайну и темам после стабильной недизайнерской базы считаются закрытой веткой и не продолжаются.

## 2. Что инвентаризировано

- `App.xaml` и его `MergedDictionaries`.
- словари из `Themes`.
- локальные `Window.Resources` и локальные `Style` в окнах.
- usages `Brush.*`, `SystemColors.*` и жёстких `#HEX` в XAML.
- ключевые окна: `MainWindow`, `SettingsWindow`, `ChangePasswordWindow`, `EntryWindow`, `HelpWindow`, `PasswordGeneratorWindow`, `LoginWindow`, `MasterPasswordPromptWindow`, `CommentWindow`, `FolderDialog`.

## 3. Классификация текущего состояния

### 3.1. Active baseline

- `App.xaml` подключает нейтральные baseline-словари напрямую:
  - `Themes/Baseline.Neutral.Brushes.xaml`
  - `Themes/Baseline.Neutral.Controls.xaml`
- Это active runtime baseline, а не theme-layer.
- В baseline-схему вынесены semantic brushes для поверхностей, текста, границ, selection, drag&drop, dialog/toast/overlay.
- В baseline-схему вынесены shared styles: `DialogButton`, `PassNotesSeparator`, app-wide hooks `ExplorerSelectionBehavior`.

### 3.2. Legacy

Пока остаются на legacy/стандартном WPF-виде и не переводились в этом подблоке:

- `LoginWindow.xaml`
- `MasterPasswordPromptWindow.xaml`
- `CommentWindow.xaml`
- `FolderDialog.xaml`

### 3.3. Parked theme assets

- `Themes/Theme.PipBoy.xaml` — parked theme asset, не активен и не подключён в `App.xaml`.

### 3.4. Conflict candidates

- смешение `Brush.*` и прямых `SystemColors.*` в одних и тех же окнах;
- дубли локального `DialogButton` в `SettingsWindow` и `ChangePasswordWindow`;
- локальный `PassNotesSeparator` только в `MainWindow`;
- жёсткий overlay-цвет в `HelpWindow`;
- историческая путаница между активной baseline-базой `_230` и стратегической ссылкой на `229`;
- историческое имя `Themes/Theme.Default.xaml`, из-за которого baseline выглядел как theme-layer.

## 4. Что сделано в Подблоке 1.1

- Добавлены `Themes/Baseline.Neutral.Brushes.xaml` и `Themes/Baseline.Neutral.Controls.xaml`.
- `App.xaml` переведён на прямое подключение baseline-словарей.
- `Themes/Theme.Default.xaml` переведён в compatibility-wrapper над baseline-словарями.
- Централизованы neutral-resources для dialog/toast/overlay без изменения поведения.
- Убраны локальные дубли `DialogButton` и `PassNotesSeparator` там, где это было безопасно.
- Прямые `SystemColors` и жёсткий overlay-цвет заменены на семантические ключи в целевых окнах.

## 5. Что сознательно не делалось

- Не менялась бизнес-логика.
- Не менялись MVVM, данные, трей, импорт/экспорт/бэкап, пути, `exe`, публичное имя.
- Не включалась тема и не делался редизайн.
- Не менялся layout экранов.
- Не переводились все окна подряд в новую визуальную схему.

## 6. Целевая baseline-структура после Подблока 1.1

- `Baseline.Neutral.Brushes.xaml` — единый источник нейтральных semantic brushes.
- `Baseline.Neutral.Controls.xaml` — единый источник общих neutral styles и app-wide control hooks.
- `Theme.Default.xaml` — compatibility-wrapper, не active baseline source.
- `Theme.PipBoy.xaml` — parked theme asset для будущих этапов, не используется сейчас.

## 7. Что считается нормой сейчас

- В проекте есть одна активная нейтральная baseline-схема ресурсов.
- Повторяющиеся neutral-элементы централизованы.
- Theme-asset `PipBoy` припаркован и не влияет на runtime UI.
- Baseline и theme не смешиваются в точке подключения `App.xaml`.

## 8. Что временно допустимо

- Наличие legacy-окон на стандартном WPF-виде без немедленного перевода в baseline.
- Наличие compatibility-wrapper `Themes/Theme.Default.xaml` до полного завершения baseline-reset.
- Наличие window-specific styles в `MainWindow`, если они не дублируют shared neutral-resources.

## 9. Что ещё не норма

- Не все secondary dialogs переведены на единый baseline-слой.
- В проекте ещё есть историческое имя `Theme.Default.xaml`, пусть и уже не в active runtime-точке.
- Нужен следующий подблок для remaining legacy windows.

## 10. Как проверять

1. Проверить, что `App.xaml` подключает `Themes/Baseline.Neutral.Brushes.xaml` и `Themes/Baseline.Neutral.Controls.xaml`.
2. Проверить, что `Themes/Theme.PipBoy.xaml` не подключён в `App.xaml`.
3. Проверить, что `Themes/Theme.Default.xaml` остался wrapper-слоем.
4. Проверить, что `SettingsWindow.xaml` и `ChangePasswordWindow.xaml` больше не содержат локального `DialogButton`.
5. Проверить, что toasts в `MainWindow.xaml`, `EntryWindow.xaml`, `SettingsWindow.xaml`, `PasswordGeneratorWindow.xaml` используют `Brush.Toast*`.
6. Проверить, что overlay в `HelpWindow.xaml` использует `Brush.OverlayBackground`.
7. Выполнить `dotnet build`.

## 11. Изменённые файлы этого подблока

- `App.xaml`
- `Themes/Baseline.Neutral.Brushes.xaml`
- `Themes/Baseline.Neutral.Controls.xaml`
- `Themes/Theme.Default.xaml`
- `MainWindow.xaml`
- `SettingsWindow.xaml`
- `ChangePasswordWindow.xaml`
- `EntryWindow.xaml`
- `HelpWindow.xaml`
- `PasswordGeneratorWindow.xaml`
- `docs/BASELINE_RESET_STAGE1_BLOCK1_1_INVENTORY.md`
