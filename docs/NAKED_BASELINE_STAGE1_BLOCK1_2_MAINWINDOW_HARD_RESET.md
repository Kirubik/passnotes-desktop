# Этап 1 / Подблок 1.2 — hard reset MainWindow

Дата фиксации: 2026-03-09 (Europe/Moscow)

## Цель блока

Перевести `MainWindow` на единый naked baseline UI без локальных визуальных исключений в общих state-family, чтобы окно опиралось на shared baseline-layer, а локальный XAML отвечал только за уникальный layout, templates и behavior wiring.

## Что переведено на shared baseline

- Верхний toolbar и menu/popup-слой переведены на shared baseline families `BaselineToolbar*`, `BaselinePopupSurfaceBorder`, `BaselinePopupMenuItemButton` и `BaselineIconGlyph`.
- Entry search, folder search, clear-buttons, context chip и popup-toasts переведены на shared primitives `BaselineSearchTextBox`, `BaselineHostedSearchTextBox`, `BaselineSearchClearButton`, `BaselineInfoChipBorder`, `BaselineToastBorder` и `BaselineSearchHostBorder`.
- Folder multi-select strip переведен на shared inset-surface primitive `BaselineInsetStripBorder`.
- Для дерева папок shared style `BaselineTreeViewItem` теперь владеет общей state-family `hover / focus / selected / inactive selected / drop target`; локальный `FolderTreeItemStyle` оставляет только template, expander, active-context marker и правило скрытия selection-visual в folder multi-select mode.
- Для таблицы записей закреплен shared contract `BaselineDataGridRow` + `BaselineDataGridCell` + `BaselineDataGridColumnHeader`; локально в `MainWindow` оставлены только header context menu, уникальный sort-glyph content template и column-specific templates.

## Что сознательно оставлено локальным в MainWindow

- Breadcrumbs layout и command wiring.
- Folder tree expander geometry, папочные иконки, checkbox visibility logic и active-context marker.
- Column-specific templates таблицы (`★`, sort glyph, favorite cell content).
- Layout-решения split-pane, row-height strip и grid composition.

## Норма сейчас

- `MainWindow` использует shared baseline как основной владелец общих interactive states и neutral visual primitives.
- Popup/search/chip/toast/tree/grid больше не держат разрозненную локальную state/color-логику там, где уже существует shared baseline-решение.
- Дерево и таблица используют одну согласованную neutral family для hover/selection/focus/inactive-selection без перехода к `Светлой теме`.

## Временно допустимо

- `EntryWindow`, `SettingsWindow`, диалоги и secondary windows еще не переведены на тот же hard reset и остаются задачами следующих подблоков.
- В `MainWindow` остаются локальные templates и content-слои, если они описывают уникальную структуру окна, а не общую visual-state механику.

## Не норма / следующий шаг

- Остальные окна пока еще не посажены на тот же уровень naked baseline consistency.
- Следующий обязательный шаг новой линии: `Этап 1 / Подблок 1.3` — hard reset `EntryWindow` и `SettingsWindow`.

## Проверка

1. Выполнить `dotnet build -p:UseAppHost=false -p:OutDir=bin\Debug\net8.0-windows\codexverify\`.
2. Проверить верхний toolbar, dropdown menu, search-поля, clear-buttons и context chip.
3. Проверить folder tree: hover, selected, inactive selected, drag target, folder multi-select mode.
4. Проверить entries grid: full-row selection, sort glyph, header context menu, row height.
5. Убедиться, что подблок не начал `Светлую тему` и не затронул другие окна.
