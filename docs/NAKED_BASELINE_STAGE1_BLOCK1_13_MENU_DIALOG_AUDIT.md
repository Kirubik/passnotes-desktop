# PassNotes — финальный audit menu/dialog layer после Этапа 1 / Подблока 1.13

Дата фиксации: 2026-03-11 (Europe/Moscow)
Активная baseline-база: `_230`

## 1. Охват audit

Audit покрывает practical menu/dialog boundary для app-owned WPF UI:

- shared WPF `ContextMenu` / `MenuItem`;
- text context menu layer для `TextBox` / `PasswordBox`;
- app-owned dialog windows;
- app-owned message dialog layer (`OK`, `YesNo`, `YesNoCancel`);
- startup/recovery dialogs в `App.xaml`;
- message/confirm dialogs в `MainWindow`.

Системная внешняя граница ОС в этот audit не включается как дефект:

- tray menu;
- `OpenFileDialog`;
- `SaveFileDialog`;
- `FolderBrowserDialog`.

## 2. Норма сейчас

- В `Themes/Baseline.Neutral.Controls.xaml` есть единый shared baseline для:
  - `BaselineContextMenu`;
  - `BaselineContextMenuItem`;
  - `BaselineContextMenuSeparator`;
  - `BaselineTextCommandsContextMenu`.
- Контекстные меню `MainWindow` и `EntryWindow` используют общий app-owned baseline-механизм.
- Text context menu layer приведен к одному подходу для:
  - `EntryWindow`;
  - `CommentWindow`;
  - `LoginWindow`;
  - `ChangePasswordWindow`;
  - `FolderDialog`;
  - `MasterPasswordPromptWindow`;
  - `PasswordGeneratorWindow`;
  - `SettingsWindow`;
  - search-полей `MainWindow`.
- Собственные диалоговые окна приложения живут в одной neutral baseline-семье и не опираются на системный `MessageBox`.
- `AppMessageDialogWindow` используется как единый app-owned message dialog layer для:
  - secondary dialogs;
  - startup/recovery flow в `App.xaml`;
  - `MainWindow`.
- В app-owned кодовых сценариях больше не остается прямых `MessageBox.Show(...)`.
- Сборка проходит успешно через:
  `dotnet build -p:UseAppHost=false -p:OutDir=bin\Debug\net8.0-windows\codexverify\`

## 3. Временно допустимо

- Системными остаются только OS-owned поверхности:
  - tray menu через `NotifyIcon` / `ContextMenuStrip`;
  - file/folder dialogs.
- Это не считается дефектом practical variant, пока theme boundary зафиксирована именно так.
- Полная ручная GUI smoke-проверка по всем сценариям все еще требуется после кодового audit.

## 4. Не норма / дефект

- На момент кодового audit незакрытых прямых `MessageBox.Show(...)` в app-owned flows не найдено.
- Незакрытых WPF `ContextMenu`, которые выпадали бы в системное меню Windows в согласованном app-owned охвате, по коду не найдено.
- Основной открытый риск теперь не кодовый, а проверочный:
  - возможны точечные GUI-дефекты, которые можно поймать только ручным smoke-pass.
- `docs/STATUS.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md` и `docs/NAKED_BASELINE_MASTER_PLAN.md` до этого audit отставали от фактического прогресса линии; это исправляется в рамках `1.13`.

## 5. Ожидаемое финальное состояние

- Practical menu/dialog boundary считается закрытой для всей app-owned WPF поверхности.
- Дальнейший маршрут после этого audit определяется актуальным master-plan и может включать дополнительную предтемизационную ветку дочистки до старта `Этапа 2 / Подблока 2.1`.
- Любые найденные позже точечные дефекты menu/dialog layer должны оформляться отдельно:
  - либо как hotfix уже выполненного слоя;
  - либо как отдельная внеплановая service task;
  но не как неявное продолжение `1.13`.

## 6. Что проверить вручную после audit

1. Контекстные меню дерева, таблицы, заголовка таблицы, комментария и вложений.
2. Text context menu layer в `EntryWindow`, `CommentWindow`, secondary dialogs, `SettingsWindow`, search-полях `MainWindow`.
3. App-owned `OK`, `YesNo`, `YesNoCancel` dialogs в `MainWindow`.
4. Secondary dialogs и локальные ошибки/валидацию.
5. Startup/recovery dialogs в `App.xaml`.
6. Ручные backup/import/export/restore сценарии.
7. Подтверждение, что системными остались только tray menu и file/folder dialogs ОС.
