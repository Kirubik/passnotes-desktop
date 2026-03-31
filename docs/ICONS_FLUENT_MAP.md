# PassNotes Fluent Icons Map

## Назначение

Этот документ фиксирует канонический переход PassNotes на единый стандарт иконок:

- единственный источник исходных иконок: официальный репозиторий Microsoft Fluent UI System Icons;
- единый baseline-aware icon layer внутри WPF;
- карта соответствий `действие -> Fluent icon` до начала массовой замены в UI.

Основной источник:

- [microsoft/fluentui-system-icons](https://github.com/microsoft/fluentui-system-icons)
- [README](https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/README.md)
- [SVG package README](https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/packages/svg-icons/README.md)
- [MIT license](https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/LICENSE)

## Текущее состояние проекта

### Что уже есть

- Семантический слой иконок живет в [Themes/Baseline.Neutral.Icons.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/Themes/Baseline.Neutral.Icons.xaml).
- Базовый рендер иконок идет через `TextBlock` и `Segoe MDL2 Assets` в [Themes/Baseline.Neutral.Controls.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/Themes/Baseline.Neutral.Controls.xaml).
- Иконки подключаются на app-level через [App.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/App.xaml).

### Что сейчас является проблемой

- Текущий semantic layer хранит не app-owned vector assets, а строковые MDL2 glyph values.
- В UI есть raw bypass-маршруты мимо общего semantic layer:
  - иконки дерева папок в [MainWindow.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/MainWindow.xaml)
  - `FavoriteStarGlyphStyle` в [MainWindow.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/MainWindow.xaml)
- Семейство copy-иконок сейчас составное и собрано из MDL2-глифов.
- В проекте нет готовой WPF SVG-библиотеки: в [PassNotes.csproj](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/PassNotes.csproj) из NuGet используется только `Markdig`.

### Вывод по архитектуре

Правильный маршрут миграции:

1. использовать Fluent repo как единственный источник сырьевых SVG;
2. хранить в проекте только нужный поднабор исходных Fluent assets с оригинальными именами;
3. поверх них создать WPF-ready icon assets;
4. сохранить semantic keys уровня `Icon.*`, чтобы окна зависели не от конкретных имен Fluent-файлов, а от baseline-contract.

## Инвентаризация текущих semantic keys

Текущие app icons, уже заведенные в semantic layer:

- `Icon.Menu`
- `Icon.Settings`
- `Icon.Help`
- `Icon.Support`
- `Icon.Import`
- `Icon.Export`
- `Icon.Backup.Create`
- `Icon.Backup.Restore`
- `Icon.Folder`
- `Icon.Lock`
- `Icon.Unlock`
- `Icon.Exit`
- `Icon.Navigate.Back`
- `Icon.Navigate.Forward`
- `Icon.Add`
- `Icon.Edit`
- `Icon.Delete`
- `Icon.MultiSelect`
- `Icon.ClearSelection`
- `Icon.ShowPassword`
- `Icon.Generator`
- `Icon.Trash`
- `Icon.NoFolder`
- `Icon.Favorites`
- `Icon.Copy.Login.Primary`
- `Icon.Copy.Password.Primary`
- `Icon.Copy.Overlay`
- `Icon.Favorite.Empty`
- `Icon.Favorite.Filled`
- `Icon.Menu.Checkmark`
- `Icon.Menu.SubmenuArrow`
- `Icon.Close.Small`

## Raw bypasses, которые нужно убрать в следующих подблоках

- `FontFamily="Segoe MDL2 Assets"` для иконок дерева папок в [MainWindow.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/MainWindow.xaml)
- `FavoriteStarGlyphStyle` с `Segoe MDL2 Assets` в [MainWindow.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/MainWindow.xaml)
- Любые прямые визуальные glyph-маршруты, где окно зависит от MDL2, а не от semantic icon resource

## Каноническая структура хранения

### Рекомендуемая структура

- `Resources/Icons/Fluent/Source/`
- `Resources/Icons/Fluent/Source/regular/`
- `Resources/Icons/Fluent/Source/filled/`
- `Themes/Baseline.Neutral.IconAssets.xaml`
- `Themes/Baseline.Neutral.Icons.xaml`

### Правила слоя

- В `Source/` кладется только используемый поднабор иконок из официального Fluent repo.
- Имена файлов сохраняются в официальном формате Fluent, например:
  - `ic_fluent_folder_16_regular.svg`
  - `ic_fluent_star_16_filled.svg`
- В `Baseline.Neutral.IconAssets.xaml` живут WPF-ready ресурсы для конкретных Fluent assets.
- В `Baseline.Neutral.Icons.xaml` живут только semantic aliases уровня приложения:
  - `Icon.Entry.Add`
  - `Icon.Folder.Add`
  - `Icon.Favorite.Filled`
  - `Icon.Password.Show`
  - и так далее.

### Почему именно так

- Fluent repo остается единственным источником истины.
- UI не зависит от raw filenames.
- Можно централизованно переопределять state pairs и составные иконки.
- Миграция идет через baseline, а не через хаотичную замену по окнам.

## Правила выбора Fluent-иконок

- По умолчанию использовать `regular`.
- `filled` использовать только для явных активных или выбранных состояний.
- Основной рабочий размер для toolbar/menu/list/grid routes: `16`.
- Для случаев, где в Fluent нет `16`, допустимо брать `20` и масштабировать через общий WPF icon host.
- Не смешивать иконки из других библиотек.
- Если single-icon аналога нет, допускается:
  - app-owned композиция из нескольких Fluent icons;
  - либо отдельная пометка как кандидат на будущую кастомную Fluent-style отрисовку.

## Каноническая карта соответствий

### App shell и глобальные действия

| Действие | Fluent icon | Стиль | Базовый размер | Статус | Примечание |
|---|---|---:|---:|---|---|
| Главное меню | `panelLeftHeader16Regular` | regular | 16 | direct | Ближайший Fluent-аналог к app menu / hamburger |
| Настройки | `settings16Regular` | regular | 16 | direct | Канонический settings symbol |
| Справка | `questionCircle16Regular` | regular | 16 | direct | Лучше читается, чем plain question |
| Поддержать автора | `heart16Regular` | regular | 16 | direct | Мягкая и дружелюбная семантика |
| О приложении | `info16Regular` | regular | 16 | direct | Для будущего about-route |
| Тема | `colorFill16Regular` | regular | 16 | direct | Основной theme selector symbol |
| Выход | `arrowExit16Regular` | regular | 16 | direct | Лучше отделяет выход из приложения от simple close |

### Навигация и служебные glyph-ы

| Действие | Fluent icon | Стиль | Базовый размер | Статус | Примечание |
|---|---|---:|---:|---|---|
| Назад в справке | `chevronLeft16Regular` | regular | 16 | direct | Для help-nav и аналогичных small nav routes |
| Вперед в справке | `chevronRight16Regular` | regular | 16 | direct | Парная иконка к back |
| Закрыть поиск / чип / small close | `dismiss12Regular` | regular | 12 | direct | Замена текущего `Icon.Close.Small` |
| Checkmark в меню | `checkmark16Regular` | regular | 16 | direct | Замена raw unicode glyph |
| Стрелка submenu | `chevronRight16Regular` | regular | 16 | direct | Замена raw unicode glyph |
| Tree collapsed chevron | `chevronRight12Regular` | regular | 12 | direct | Для tree disclosure |
| Tree expanded chevron | `chevronDown12Regular` | regular | 12 | direct | Для tree disclosure |
| Несортированная колонка | `arrowSort16Regular` | regular | 16 | direct | Вместо текстового `↕` |
| Сортировка по возрастанию | `arrowSortUp16Regular` | regular | 16 | direct | Вместо текстового `▲` |
| Сортировка по убыванию | `arrowSortDown16Regular` | regular | 16 | direct | Вместо текстового `▼` |

### Поиск, контекст и контентные области

| Действие | Fluent icon | Стиль | Базовый размер | Статус | Примечание |
|---|---|---:|---:|---|---|
| Поиск записей | `search16Regular` | regular | 16 | direct | Общий search symbol |
| Поиск папок | `folderSearch16Regular` | regular | 16 | direct | Лучше различает folder-search и entry-search |
| Открыть папку / открыть папку бэкапов | `openFolder16Regular` | regular | 16 | direct | Для action, а не для folder state |
| Контекст папки / активная папка | `folder16Regular` | regular | 16 | direct | Базовый folder route |
| Без папки | `folderProhibited16Regular` | regular | 16 | direct | Прямой и понятный аналог |
| Избранное, пустое состояние | `star16Regular` | regular | 16 | direct | Для неактивного favorite |
| Избранное, активное состояние | `star16Filled` | filled | 16 | direct | Для активного favorite |
| Добавить в избранное | `starAdd16Regular` | regular | 16 | direct | Действие, а не состояние |
| Убрать из избранного | `starDismiss16Regular` | regular | 16 | direct | Четче, чем plain `starOff` |
| Корзина | `delete16Regular` | regular | 16 | direct | Канонический trash/delete container |

### Работа с записями

| Действие | Fluent icon | Стиль | Базовый размер | Статус | Примечание |
|---|---|---:|---:|---|---|
| Добавить запись | `documentAdd16Regular` | regular | 16 | direct | Лучше отделяет запись от папки |
| Редактировать запись | `documentEdit16Regular` | regular | 16 | direct | Канонический entry edit |
| Удалить запись | `delete16Regular` | regular | 16 | direct | Нормальный delete-to-trash route |
| Восстановить запись из корзины | `deleteArrowBack16Regular` | regular | 16 | direct | Очень хорошо отделяется от import |
| Удалить запись навсегда | `deleteDismiss20Regular` | regular | 20 | direct | Нет 16px версии, нужен общий scale route |
| Очистить корзину | `deleteLines20Regular` | regular | 20 | closest | Ближайший bulk-delete вариант |
| Перейти к папке записи | `folderArrowRight16Regular` | regular | 16 | direct | Нормальная nav-to-folder семантика |

### Копирование, пароль и безопасность

| Действие | Fluent icon | Стиль | Базовый размер | Статус | Примечание |
|---|---|---:|---:|---|---|
| Копировать логин | `personKey16Regular + copy16Regular` | regular | 16 | composite | Single-icon аналога нет; нужен составной Fluent route |
| Копировать пароль | `password16Regular + copy16Regular` | regular | 16 | composite | Single-icon аналога нет; нужен составной Fluent route |
| Показать пароль | `eye16Regular` | regular | 16 | direct | Базовый show route |
| Скрыть пароль | `eyeOff16Regular` | regular | 16 | direct | Парный hide route |
| Генератор паролей | `keyMultiple16Regular` | regular | 16 | direct | Лучше подчеркивает generation/multiple variants |
| Заблокировать | `lockClosed16Regular` | regular | 16 | direct | Канонический lock action |
| Разблокировать | `lockOpen16Regular` | regular | 16 | direct | Канонический unlock action |
| Безопасность / security section | `lockShield16Regular` | regular | 16 | direct | Для более общей security семантики |

### Импорт, экспорт, бэкапы, восстановление

| Действие | Fluent icon | Стиль | Базовый размер | Статус | Примечание |
|---|---|---:|---:|---|---|
| Импорт vault | `documentArrowDown16Regular` | regular | 16 | direct | Файл приходит в приложение |
| Экспорт vault | `documentArrowUp16Regular` | regular | 16 | direct | Файл уходит из приложения |
| Создать бэкап | `archive16Regular` | regular | 16 | direct | Канонический archive/save snapshot |
| Восстановить из бэкапа | `archiveArrowBack16Regular` | regular | 16 | direct | Лучше отделяется от import/export |
| Папка бэкапов | `openFolder16Regular` | regular | 16 | direct | Action to open location |

### Папки

| Действие | Fluent icon | Стиль | Базовый размер | Статус | Примечание |
|---|---|---:|---:|---|---|
| Папка по умолчанию | `folder16Regular` | regular | 16 | direct | Базовый folder symbol |
| Добавить папку | `folderAdd16Regular` | regular | 16 | direct | Есть прямой Fluent-эквивалент |
| Редактировать папку | `folder16Regular + documentEdit16Regular` | regular | 16 | composite | У Fluent нет хорошего single-icon folder edit |
| Удалить папку | `folder16Regular + delete16Regular` | regular | 16 | composite | Нужен отдельный folder-vs-entry distinction |
| Восстановить папку | `folderArrowUp16Regular` | regular | 16 | direct | Хорошо читается как restore folder |
| Мультивыбор папок | `selectAllOn16Regular` | regular | 16 | direct | Лучше, чем abstract multi-select glyph |
| Очистить отмеченные папки / selection | `selectAllOff16Regular` | regular | 16 | direct | Прямой semantic pair |

### Комментарии и вложения

| Действие | Fluent icon | Стиль | Базовый размер | Статус | Примечание |
|---|---|---:|---:|---|---|
| Открыть полное окно комментария | `open16Regular` | regular | 16 | direct | Простое и понятное действие |
| Вложения, общий символ | `attach16Regular` | regular | 16 | direct | Базовый attachment route |
| Добавить вложение | `attach16Regular` | regular | 16 | direct | Текущий UX не требует отдельного plus glyph |
| Открыть вложение | `open16Regular` | regular | 16 | direct | Универсальный open action |
| Сохранить вложение как | `save16Regular` | regular | 16 | direct | Канонический save route |
| Скопировать имя файла | `copy16Regular` | regular | 16 | direct | Нормальный clipboard action |
| Удалить вложение | `delete16Regular` | regular | 16 | direct | Простое remove action |

### Кандидаты на отдельный review в следующем подблоке

| Маршрут | Предварительное решение | Почему нужен review |
|---|---|---|
| `Copy login` / `Copy password` | composition из двух Fluent icons | Важно удержать различимость в 16px |
| `Folder edit` / `Folder delete` | composition | У Fluent нет хороших single-icon аналогов |
| `Trash empty` | `deleteLines20Regular` | Нужен визуальный smoke-check против `delete16Regular` и `deleteDismiss20Regular` |
| `Menu` | `panelLeftHeader16Regular` | Нужен живой визуальный выбор против `navigation16Regular` / `reOrder16Regular` |
| `Theme` | `colorFill16Regular` | Нужен живой выбор против `paintBrush16Regular` |

## Базовые правила внедрения в следующих подблоках

### Подблок 5.12

Нужно сделать baseline icon pipeline:

- завести папку с Fluent source assets;
- завести WPF-ready `DrawingImage`/эквивалентный resource layer;
- перевести `Baseline.Neutral.Icons.xaml` на semantic aliases;
- ввести reusable baseline presenter для small icons, composite icons и state icons.

### Подблок 5.13

Нужно перевести UI пакетно:

- [MainWindow.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/MainWindow.xaml)
- [EntryEditorView.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/EntryEditorView.xaml)
- [HelpHostedView.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/HelpHostedView.xaml)
- [Themes/Baseline.Neutral.Controls.xaml](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/Themes/Baseline.Neutral.Controls.xaml)

И отдельно убрать raw MDL2 bypasses.

## Что сейчас сознательно вне scope

- tray icon в [TrayService.cs](/Users/FKK/Desktop/PassNotes_Codex/PassNotes/TrayService.cs), потому что это WinForms `NotifyIcon` и отдельный brand/icon pipeline;
- exe/app icon branding;
- любые иконки из сторонних библиотек;
- хаотичная локальная подмена иконок по одному месту без перевода на baseline.
