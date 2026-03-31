# Этап 5 / Подблок 5.2 — theme coverage checklist и smoke-check

## 1. Обязательный theme coverage (`охват темы`)

Тема обязана покрывать следующие app-owned (`принадлежащие приложению`) поверхности:

- `MainWindow`
- `EntryWindow`
- `SettingsWindow`
- `LoginWindow`
- `ChangePasswordWindow`
- `FolderDialog`
- `AppMessageDialogWindow`
- `SupportAuthorWindow`
- `HelpWindow` shell (`оболочку окна справки`), кроме HTML-содержимого `WebBrowser`
- popup/toast layer (`слой popup/toast-уведомлений`)
- WPF `ContextMenu` / `MenuItem`
- icon token layer (`слой токенов иконок`)

## 2. Контрольные семейства контролов

При добавлении новой темы должны реагировать:

- `Button`, `ToggleButton`
- `TextBox`, `PasswordBox`, `ComboBox`, `CheckBox`
- `ListBox`, `ListBoxItem`, `ListViewItem`
- `TreeViewItem`
- `DataGrid`, `DataGridRow`, `DataGridCell`, `DataGridColumnHeader`
- `TabControl`, `TabItem`
- popup-item family (`семейство popup-элементов`)
- dialog shell (`оболочка диалогов`)
- toast/popup surfaces (`поверхности toast/popup`)

## 3. Обязательные состояния

После добавления новой темы вручную проверять:

- `normal` (`обычное состояние`)
- `hover` (`наведение`)
- `pressed` (`нажатие`)
- `focused` (`фокус`)
- `disabled` (`неактивное состояние`)
- `checked` (`отмеченное состояние`) там, где применимо
- active selection (`активное выделение`)
- inactive selection (`неактивное выделение`)
- drop target (`цель перетаскивания`)
- locked / unlocked (`заблокированное / разблокированное состояние`) ключевых экранов
- toast / popup visibility (`видимость toast / popup`)

## 4. Системные исключения ОС

Следующие поверхности не входят в обязательный theme coverage и считаются OS-owned (`принадлежащими ОС`):

- tray menu (`меню трея`)
- системные file dialogs (`файловые диалоги`)
- системные folder dialogs (`папочные диалоги`)
- системные balloon notifications (`системные всплывающие уведомления`)
- внутреннее HTML-содержимое `WebBrowser` в `HelpWindow`

## 5. Smoke-check (`короткая проверка`) перед приемкой новой темы

1. Запустить приложение и проверить старт, логин и открытие главного окна без resource errors (`ошибок ресурсов`).
2. Проверить `MainWindow`: меню, дерево папок, таблицу записей, toolbar (`панель инструментов`), popup/toast и правую панель.
3. Открыть `EntryWindow`, `SettingsWindow`, `ChangePasswordWindow`, `SupportAuthorWindow` и убедиться, что общая dialog shell (`оболочка диалогов`) не распадается по стилям.
4. Открыть `HelpWindow` и проверить baseline-оболочку окна, список оглавления, кнопки назад/вперед и overlay ошибки.
5. Проверить `hover`, `focus`, `selection`, `inactive selection`, `disabled` и `checked` на базовых контролах.
6. Проверить popup/menu family (`семейство popup/меню`) и toast family (`семейство toast-уведомлений`) в ключевых сценариях.
7. Проверить, что OS-owned поверхности остались системными и не получили ложных обязательств по теме.

## 6. Правило приемки

Новая тема считается готовой только если она проходит этот checklist (`чеклист`) без локальных window-level overrides (`локальных переопределений уровня окна`), которые ломают общий baseline-contract (`baseline-контракт`).