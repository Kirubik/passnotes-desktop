# PassNotes — Hotkeys Inventory (I1.1–I1.2)

Источник плана: `docs/PassNotes_Plan_Final_Ideal.md`

Этот документ фиксирует **фактические** клавиатурные биндинги/обработчики и важное «клавиатурное поведение» (WPF defaults + наш код), как есть в текущей базе.

## Термины
- **binding** — явный `InputBindings`/`CommandBindings`/`KeyBinding`.
- **handler** — кастомный `PreviewKeyDown/KeyDown` и т.п.
- **default** — стандартное поведение WPF-контролов (например, `IsDefault/IsCancel`, текстовые команды Ctrl+C/V/A и т.д.).

## Глобальные правила/ограничения текущей реализации
- В `MainWindow` есть общий `PreviewKeyDown` для **Enter/стрелок** (без модификаторов) с защитой:
  - не вмешиваться при `Ctrl/Alt/Shift/Win`;
  - не вмешиваться, если источник — ввод (TextBox/PasswordBox/ComboBox);
  - не вмешиваться в меню/контекст-меню.
- В `EntryWindow` **не используется** `IsCancel=True` на Cancel-кнопке (иначе возможен двойной prompt), вместо этого Esc перехватывается вручную.

- Для window-level хоткеев (I1.2) действует **input-guard** (I1.3):
  - `Ctrl+N` / `Ctrl+Shift+N` **блокируются**, если фокус в любом поле ввода (TextBox/RichTextBox/PasswordBox и т.п.), чтобы исключить случайные действия при наборе.
  - Остальные хоткеи (`Ctrl+F`, `Ctrl+Shift+F`, `Ctrl+L`, `Ctrl+S`) разрешены (и всё равно уважают `CanExecute`).
  - При блокировке пишется маркер `HOTKEY_GUARD_BLOCK` (rate-limit: один раз за сессию на `id+focusedType`).


---

## MainWindow

### 1) Явные биндинги (InputBindings)

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания/конфликты |
|---|---|---|---|---|---|---|---|---|
| `main.search.focusEntries` | Фокус в поиск записей | Focus entries search | `Ctrl+F` | MainWindow (Window-level) | `!IsLocked` | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `MainWindow.xaml.cs` (Apply) | binding | Добавлено в I1.2 как window-level KeyBinding. |
| `main.search.focusFolders` | Фокус в поиск папок | Focus folders search | `Ctrl+Shift+F` | MainWindow (Window-level) | `!IsLocked` | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `MainWindow.xaml.cs` (Apply) | binding | Добавлено в I1.2 как window-level KeyBinding. |
| `main.entry.add` | Создать запись | Create entry | `Ctrl+N` | MainWindow (Window-level) | `!IsLocked && CanCreateEntry` | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `MainWindow.xaml.cs` (Apply) | binding | I1.3: блокируется при фокусе в любом поле ввода (TextBox/PasswordBox/etc.). Выполняет существующий пайплайн `Add_Click`. |
| `main.folder.add` | Создать папку | Create folder | `Ctrl+Shift+N` | MainWindow (Window-level) | `!IsLocked && CanCreateFolderHotkey()` | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `MainWindow.xaml.cs` (Apply) | binding | I1.3: блокируется при фокусе в любом поле ввода (TextBox/PasswordBox/etc.). Учитывает правила кнопки: не на `Без папки`, не когда папки свернуты. |
| `main.lock.toggle` | Заблокировать/разблокировать | Lock/Unlock toggle | `Ctrl+L` | MainWindow (Window-level) | Всегда | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `MainWindow.xaml.cs` (Apply) | binding | Если locked → запускает Unlock, иначе Lock. |
| `help.open` | Справка | Help | `F1` | MainWindow (Window-level) | Всегда | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `MainWindow.xaml.cs` (Apply) | binding | Открывает HelpWindow. Также доступно из меню: Меню → Справка. |
| `folders.delete` | Удалить выбранную папку / отмеченные папки | Delete selected / checked folders | `Del` | FolderTree (TreeView) | Фокус внутри FolderTree | `MainWindow.xaml` → `TreeView.InputBindings` | binding | В folder-multi-select режиме `Del` сохранён и работает по checked-папкам. |
| `entries.delete` | Удалить выбранные записи | Delete selected entries | `Del` | Entries Grid (DataGrid) | Фокус внутри DataGrid | `MainWindow.xaml` → `DataGrid.InputBindings` | binding | Не должно ломать multi-select. |
| `entries.selectAll` | Выбрать все записи | Select all entries | `Ctrl+A` | Entries Grid (DataGrid) | Фокус внутри DataGrid | `MainWindow.xaml` → `DataGrid.InputBindings` | binding | В текстовых полях `Ctrl+A` остаётся стандартным. |

### 2) Кастомное клавиатурное поведение (handlers)

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания/конфликты |
|---|---|---|---|---|---|---|---|---|
| `main.entries.arrowStayInList` | Стрелки Up/Down держат навигацию в списке записей (selection+focus) | Up/Down keep navigation inside entries list | `Up/Down` | MainWindow → Entries Grid | Без модификаторов; не в TextBox/PasswordBox/ComboBox; не в меню/контекст-меню; **если фокус в FolderTree — не вмешиваться** | `MainWindow.xaml.cs` → `MainWindow_PreviewKeyDown` | handler | При multi-select (`SelectedItems.Count>1`) не меняет selection, только фокус/scroll. |
| `main.panes.focusEntries` | Перевести фокус с дерева папок на список записей | Move focus from folder tree to entries grid | `Right` | MainWindow | Без модификаторов; не ввод; не меню; **если folder-multi-select — не вмешиваться** | `MainWindow.xaml.cs` → `MainWindow_PreviewKeyDown` | handler | Если нет selection в grid — ставит `SelectedIndex=0` (best-effort). |
| `main.panes.focusFolders` | Перевести фокус со списка записей на дерево папок | Move focus from entries grid to folder tree | `Left` | MainWindow | Без модификаторов; не ввод; не меню | `MainWindow.xaml.cs` → `MainWindow_PreviewKeyDown` | handler | Просто `FolderTree.Focus()` (best-effort). |
| `folders.activateOrToggleExpand` | Enter в дереве: (1) toggle expand/collapse если есть дети; (2) активировать папку как контекст справа | Enter in tree: toggle expand (if has children) + activate folder context | `Enter` | FolderTree | Без модификаторов; не ввод; не меню; **если folder-multi-select — Enter ничего не делает** | `MainWindow.xaml.cs` → `MainWindow_PreviewKeyDown` | handler | Включает обновление правой панели: `RefreshGrid()` + `UpdateActiveContextBindings()` и т.д. |
| `entries.openOrRestore` | Enter в списке: открыть запись (или restore из корзины) при одиночном выборе | Enter: open entry (or restore from Trash) when exactly one selected | `Enter` | Entries Grid | Без модификаторов; не ввод; не меню; `selectedCount == 1` | `MainWindow.xaml.cs` → `MainWindow_PreviewKeyDown` | handler | В Trash открытие заменяется на restore (`TryRestoreFromTrashByDoubleClick`). |
| `folders.search.clear` | Очистить поиск по папкам | Clear folder search | `Esc` | FolderSearchBox (TextBox) | Фокус в поле поиска папок | `MainWindow.xaml.cs` → `FolderSearchBox_PreviewKeyDown` | handler | Делает `FolderSearchBox.Clear()` и `Handled=true`. |
| `folders.multiselect.suppressNavigationKeys` | В режиме multi-select папок блокировать навигационные клавиши (кроме Delete) | In folder multi-select mode, suppress navigation keys (except Delete) | `Up/Down/Left/Right/Home/End/PageUp/PageDown/Enter/Space` | FolderTree | **Только** когда `IsFolderMultiSelectMode=true` | `MainWindow.xaml.cs` → `FolderTree_PreviewKeyDown` | handler | Удерживает взаимодействие в режиме “чекбоксы”, не даёт TreeView менять selection/focus. |

---

## EntryWindow

### 0) Явные биндинги (InputBindings)

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания/конфликты |
|---|---|---|---|---|---|---|---|---|
| `entry.save.ctrlS` | Сохранить запись | Save entry | `Ctrl+S` | EntryWindow (Window-level) | Пока не идёт сохранение (`!_isSaving`) | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `EntryWindow.xaml.cs` (Apply) | binding | Вызывает существующий пайплайн `TrySaveAndClose()` (как OK/Save). |
| `help.open` | Справка | Help | `F1` | EntryWindow (Window-level) | Всегда | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `EntryWindow.xaml.cs` (Apply) | binding | Открывает HelpWindow. |

### 1) Default (IsDefault/IsCancel) + важные исключения

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания |
|---|---|---|---|---|---|---|---|---|
| `entry.save.defaultEnter` | Сохранить запись | Save entry | `Enter` | EntryWindow | Когда Enter не поглощён контролом ввода (например, не в multiline) | `EntryWindow.xaml` → Save Button `IsDefault=True` | default | В `CommentBox` `AcceptsReturn=True`, поэтому Enter вставляет перевод строки и **не** должен триггерить Save. |

> **Важно:** Cancel-кнопка намеренно **без** `IsCancel=True` (см. ниже), поэтому Esc реализован вручную.

### 2) Handlers

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания/конфликты |
|---|---|---|---|---|---|---|---|---|
| `entry.cancel.esc` | Закрыть редактор (Cancel pipeline) | Close editor (cancel pipeline) | `Esc` | EntryWindow (window-level) | Всегда, пока окно активно | `EntryWindow.xaml`/`EntryWindow.xaml.cs` → `EntryWindow_PreviewKeyDown` | handler | Сделано вместо `IsCancel=True`, чтобы избежать **двойного** prompt про несохранённые изменения. |
| `entry.comment.ctrlEnterSave` | Сохранить из многострочного комментария | Save from multiline comment | `Ctrl+Enter` | `CommentBox` (TextBox) | Фокус в CommentBox | `EntryWindow.xaml.cs` → `CommentBox_PreviewKeyDown` | handler | Оставляет Enter как newline, но `Ctrl+Enter` вызывает `Ok_Click`. |
| `entry.attachments.selectAll` | Выбрать все вложения | Select all attachments | `Ctrl+A` | `AttachmentsList` (ListBox) | Фокус в списке вложений | `EntryWindow.xaml.cs` → `AttachmentsList_PreviewKeyDown` | handler | Реализовано вручную через `SelectedItems.Add(...)`. |
| `entry.attachments.delete` | Удалить выбранные вложения | Remove selected attachments | `Del` | `AttachmentsList` (ListBox) | Есть выбранные вложения | `EntryWindow.xaml.cs` → `AttachmentsList_PreviewKeyDown` | handler | Вызывает `AttachmentRemove_Click`. |

### 3) Текстовые команды (default)
- В `CommentBox` контекст-меню использует `ApplicationCommands.Cut/Copy/Paste/SelectAll`.
  - Стандартные жесты: `Ctrl+X/C/V/A`.
  - Источник: `EntryWindow.xaml` → `TextBox.ContextMenu`.

---

## CommentWindow (утилитное окно редактирования комментария)

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания |
|---|---|---|---|---|---|---|---|---|
| `comment.cancel.esc` | Отмена и закрыть | Cancel and close | `Esc` | Window-level | Всегда, пока окно активно | `CommentWindow.xaml.cs` → `CommentWindow_PreviewKeyDown` | handler | Draft не применяется. |
| `comment.apply.ctrlEnter` | Применить (OK) и закрыть | Apply (OK) and close | `Ctrl+Enter` | `CommentTextBox` | Фокус в текстовом поле | `CommentWindow.xaml.cs` → `CommentTextBox_PreviewKeyDown` | handler | Enter остаётся newline. |

---

## SettingsWindow

### 0) Явные биндинги (InputBindings)

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания |
|---|---|---|---|---|---|---|---|---|
| `settings.save.ctrlS` | Сохранить настройки | Save settings | `Ctrl+S` | SettingsWindow (Window-level) | Всегда | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `SettingsWindow.xaml.cs` (Apply) | binding | Выполняет `Ok_Click` (эквивалент кнопки Save). |
| `help.open` | Справка | Help | `F1` | SettingsWindow (Window-level) | Всегда | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `SettingsWindow.xaml.cs` (Apply) | binding | Открывает HelpWindow. |

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания |
|---|---|---|---|---|---|---|---|---|
| `settings.cancel.esc` | Закрыть без сохранения | Close without saving | `Esc` | SettingsWindow | Всегда, пока окно активно | `SettingsWindow.xaml` → Cancel Button `IsCancel=True` | default | Стандартное WPF поведение IsCancel. |
| `settings.save.defaultEnter` | Сохранить настройки | Save settings | `Enter` | SettingsWindow | Когда Enter не поглощён контролом ввода | `SettingsWindow.xaml` → Save Button `IsDefault=True` | default | Стандартное WPF поведение IsDefault. |

---

## PasswordGeneratorWindow

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания |
|---|---|---|---|---|---|---|---|---|
| `gen.generate.defaultEnter` | Сгенерировать пароль | Generate password | `Enter` | GeneratorWindow | Всегда (кнопка Generate — default) | `PasswordGeneratorWindow.xaml` → Generate Button `IsDefault=True` | default | Нажатие Enter триггерит генерацию. |
| `gen.close.esc` | Закрыть окно генератора | Close generator window | `Esc` | GeneratorWindow | Всегда | `PasswordGeneratorWindow.xaml` → Close Button `IsCancel=True` | default | Стандартное WPF поведение IsCancel. |
| `gen.length.digitsOnly` | Ограничение ввода длины: только цифры | Length input: digits only | (TextInput) | `LenBox` | При вводе текста | `PasswordGeneratorWindow.xaml(.cs)` → `LenBox_PreviewTextInput` | handler | Не хоткей, но важное keyboard-поведение. |
| `help.open` | Справка | Help | `F1` | PasswordGeneratorWindow (Window-level) | Всегда | `HotkeysCatalog.cs` + `HotkeysInstaller.cs` + `PasswordGeneratorWindow.xaml.cs` (Apply) | binding | Открывает HelpWindow. |

---

## LoginWindow

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания |
|---|---|---|---|---|---|---|---|---|
| `login.cancel.esc` | Отмена | Cancel | `Esc` | LoginWindow | Всегда | `LoginWindow.xaml` → Cancel `IsCancel=True` | default | |
| `login.ok.defaultEnter` | OK / Войти | OK / Login | `Enter` | LoginWindow | Всегда | `LoginWindow.xaml` → OK `IsDefault=True` | default | |

---

## MasterPasswordPromptWindow (prompt для операций, требующих master-password)

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания |
|---|---|---|---|---|---|---|---|---|
| `masterprompt.ok.defaultEnter` | OK / Подтвердить | OK / Confirm | `Enter` | Prompt window | Всегда | `MasterPasswordPromptWindow.xaml` → OK `IsDefault=True` | default | |
| `masterprompt.cancel.esc` | Отмена | Cancel | `Esc` | Window-level | Всегда | `MasterPasswordPromptWindow.xaml(.cs)` → `Window_KeyDown` | handler | Явно ставит `DialogResult=false` по Esc. |

---

## ChangePasswordWindow

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания |
|---|---|---|---|---|---|---|---|---|
| `changepwd.cancel.esc` | Отмена | Cancel | `Esc` | ChangePasswordWindow | Всегда | `ChangePasswordWindow.xaml` → Cancel `IsCancel=True` | default | |
| `changepwd.save.defaultEnter` | Сохранить новый пароль | Save new password | `Enter` | ChangePasswordWindow | Всегда | `ChangePasswordWindow.xaml` → Save `IsDefault=True` | default | |

---

## FolderDialog

| ActionId | Действие (RU) | Action (EN) | Жест | Скоуп | Когда активно | Источник | Тип | Примечания |
|---|---|---|---|---|---|---|---|---|
| `folderdlg.cancel.esc` | Отмена | Cancel | `Esc` | FolderDialog | Всегда | `FolderDialog.xaml` → Cancel `IsCancel=True` | default | |
| `folderdlg.save.defaultEnter` | Сохранить имя папки | Save folder name | `Enter` | FolderDialog | Всегда | `FolderDialog.xaml` → Save `IsDefault=True` | default | |

---

## Кандидаты на хоткеи для I1.2 (пока НЕ реализуем)

> `Ctrl+F`, `Ctrl+Shift+F`, `Ctrl+N`, `Ctrl+Shift+N`, `Ctrl+L`, `Ctrl+S` (Entry/Settings) — **реализованы в I1.2** как window-level KeyBindings.

Ниже — список действий, которые обычно ожидаемы в desktop-приложении и могут быть добавлены/стандартизированы в I1.2 (с обязательной защитой от конфликтов ввода и с учётом инвариантов):

### MainWindow
- `F2` — переименовать выбранную папку/запись (если есть сценарий).

### EntryWindow
- `Ctrl+W` / `Esc` — закрыть (уже есть Esc).

### Общие
- `F1` — HelpWindow (в блоке I2).

> Эти пункты — только заготовка для обсуждения/реализации в I1.2 и должны быть согласованы с защитой от конфликтов ввода (I1.3) и без поломки `Del/Ctrl+A/multi-select/drag&drop`.
