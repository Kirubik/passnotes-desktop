# Этап 1 / Подблок 1.2 — перевод remaining legacy windows на единый baseline-layer

Дата фиксации: 2026-03-08 (Europe/Moscow)

## 1. Источник истины для этого подблока

- Активная baseline-база для текущего нового захода: `_230`.
- Стратегический документ остаётся опорным: `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md`.
- Подблок 1.2 продолжает результат `docs/BASELINE_RESET_STAGE1_BLOCK1_1_INVENTORY.md` и не меняет active runtime baseline.

## 2. Целевые окна подблока

- `LoginWindow.xaml`
- `MasterPasswordPromptWindow.xaml`
- `CommentWindow.xaml`
- `FolderDialog.xaml`

## 3. Что сделано в Подблоке 1.2

- Все четыре remaining legacy windows переведены на нейтральный dialog chrome:
  - `Background="{DynamicResource Brush.DialogWindowBackground}"` на уровне `Window`;
  - внутренняя surface через `Brush.DialogSurfaceBackground` + `Brush.DialogSurfaceBorder`;
  - единый контейнер `Border` с нейтральной рамкой и скруглением;
  - кнопки переведены на shared-style `DialogButton`.
- Для текстовых подписей в диалогах использован `Brush.TextPrimary` там, где это было нужно для явной baseline-читаемости.
- Поведение окон, обработчики, биндинги, layout-сценарии и код-behind не менялись.

## 4. Что сознательно не делалось

- Не добавлялись новые theme-ресурсы и не менялась палитра baseline.
- Не менялись `Themes/Baseline.Neutral.Brushes.xaml` и `Themes/Baseline.Neutral.Controls.xaml`, потому что существующих ключей хватило.
- Не трогались `MainWindow`, `SettingsWindow`, `EntryWindow` и другие уже обработанные окна.
- Не менялась бизнес-логика, MVVM, импорт/экспорт/backup, tray и остальные инварианты.

## 5. Что считается нормой сейчас

- Active runtime baseline по-прежнему идёт через `App.xaml` → `Themes/Baseline.Neutral.Brushes.xaml` + `Themes/Baseline.Neutral.Controls.xaml`.
- `LoginWindow`, `MasterPasswordPromptWindow`, `CommentWindow` и `FolderDialog` визуально работают в той же нейтральной baseline-схеме, что и остальные уже подготовленные dialog windows.
- Shared-style `DialogButton` используется и в этих remaining legacy windows.

## 6. Что временно допустимо

- Внутренние `TextBox`/`PasswordBox` остаются на стандартном WPF control look, пока этап 1 ограничен baseline-reset без theme-реализации.
- Возможны локальные различия по плотности layout между диалогами, если они не нарушают читаемость и не выглядят как theme-fragment.

## 7. Что ещё не норма

- Этап 1 не создаёт полноценную тему и не задаёт детальную спецификацию состояний всех контролов.
- Следующий этап должен формализовать theme-spec до любой новой визуальной реализации.

## 8. Как проверять

1. Проверить, что `LoginWindow.xaml`, `MasterPasswordPromptWindow.xaml`, `CommentWindow.xaml` и `FolderDialog.xaml` используют `Brush.DialogWindowBackground` на уровне окна.
2. Проверить, что содержимое этих окон обёрнуто в `Border` с `Brush.DialogSurfaceBackground` и `Brush.DialogSurfaceBorder`.
3. Проверить, что кнопки в этих окнах используют `Style="{StaticResource DialogButton}"`.
4. Выполнить `dotnet build`.
5. Открыть эти окна в приложении и убедиться, что текст читается, surface нейтральная, кнопки выглядят единообразно, а сценарии работы не изменились.

## 9. Изменённые файлы этого подблока

- `LoginWindow.xaml`
- `MasterPasswordPromptWindow.xaml`
- `CommentWindow.xaml`
- `FolderDialog.xaml`
- `docs/STATUS.md`
- `docs/CHANGELOG.md`
- `docs/BASELINE_RESET_STAGE1_BLOCK1_2_LEGACY_WINDOWS.md`
