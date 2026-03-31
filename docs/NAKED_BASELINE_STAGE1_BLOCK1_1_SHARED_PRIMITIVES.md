# Этап 1 / Подблок 1.1 — shared visual primitives и правила naked baseline

## Назначение

Этот документ фиксирует результат `Этапа 1 / Подблока 1.1` новой основной линии naked baseline UI.

Его задача:

- зафиксировать, что именно теперь считается shared visual primitives;
- развести ответственность между shared baseline и window-level XAML;
- подготовить безопасную опору для `Этапа 1 / Подблока 1.2` без смешения с `Светлой темой`;
- запретить возвращение к локальным визуальным исключениям как к норме.

## Что теперь входит в shared visual primitives

### 1. Shared neutral tokens

В `Themes/Baseline.Neutral.Brushes.xaml` теперь должны жить app-wide neutral primitives для:

- surface roles;
- text/icon roles;
- border/separator roles;
- interactive states;
- selection family;
- tab family;
- общих control metrics.

Это означает, что базовые значения для фонов, текста, рамок, hover/focus/pressed/selected/drop-target и контрольных отступов больше не должны заново придумываться на уровне каждого окна.

### 2. Shared control-layer

В `Themes/Baseline.Neutral.Controls.xaml` теперь должны жить:

- implicit baseline styles для `TextBox`, `PasswordBox`, `ComboBox`, `CheckBox`;
- shared metrics для `DialogButton`;
- opt-in shared baseline styles для:
  - `Button`
  - `ToggleButton`
  - `TabControl`
  - `TabItem`
  - `ListBoxItem`
  - `ListViewItem`
  - `DataGridRow`
  - `DataGridCell`
  - `DataGridColumnHeader`.

Это shared control-layer, а не финальная theme-реализация.

## Контракт владения visual states

| Семейство | Кто владелец состояния сейчас | Что пока может оставаться локальным | Что запрещено |
| --- | --- | --- | --- |
| `TextBox` / `PasswordBox` / `ComboBox` / `CheckBox` | shared baseline | только уникальный layout окна | локальные цвета hover/focus/disabled вместо shared baseline |
| `Button` / `ToggleButton` | shared baseline primitives | где именно стиль подключается, решают следующие подблоки | придумывать отдельную button-state схему на окно |
| `TabControl` / `TabItem` | shared baseline primitives | точка подключения в окне | отдельные tab-state цвета вне shared baseline |
| `ListBoxItem` / `ListViewItem` | shared baseline primitives | локальные tooltip/binding/контекстное меню | отдельные selection/hover схемы на список |
| `DataGridRow` | shared baseline semantic state host | локальный layout колонок и header content | ложный row-owned visual contract |
| `DataGridCell` | shared baseline visual state owner | локальный content template колонки | конфликтующая window-level selection/hover схема |
| `DataGridColumnHeader` | shared baseline primitives | sort glyph, header menu и колонко-специфика | локальные конфликтующие header-state цвета |
| `TreeViewItem` | shared baseline brushes и state-family rules | template, expander layout, active-context wiring до `Подблока 1.2` | отдельная tree-state семья вне shared baseline |

## Норма сейчас

- `App.xaml` продолжает подключать baseline runtime только через neutral dictionaries.
- Shared baseline-слой теперь содержит не только active brushes для input/select states, но и общие metrics для naked baseline.
- Для базовых interactive families в shared control-layer зафиксированы app-wide baseline styles/контракты.
- Для текущего stable `DataGrid` строка несет semantic selection state, а видимые hover/selection states отрисовывает ячейка.
- Для `TreeViewItem` закреплено, что shared baseline владеет state-family, а не случайный локальный набор цветов.

## Временно допустимо

- `MainWindow.xaml` пока сохраняет локальные `Window.Resources`, `TreeView.Resources` и `DataGrid.Resources`, потому что их полный reset является задачей `Этапа 1 / Подблока 1.2`.
- `EntryWindow.xaml` пока может держать локальный `ListBoxItem`-слой для attachments, пока `Этап 1 / Подблок 1.3` не переведет окно на shared mechanism.
- `SettingsWindow.xaml` пока может не подключать shared `TabItem`-style напрямую до своего hard reset в `Этапе 1 / Подблоке 1.3`.
- Secondary dialogs пока могут использовать только уже существующий `DialogButton`-mechanism без полной button restyling-схемы.

## Не норма / дефект

- Любые новые локальные visual state-цвета для общих control families.
- Любая новая попытка лечить окно точечными локальными brush-исключениями вместо shared baseline.
- Ложное описание `DataGrid` как row-owned visual state при фактически cell-owned rendering.
- Возврат к ситуации, где `TreeViewItem`, `ListBoxItem`, `TabItem` и `Button` живут в разных window-level state-семьях без общей baseline-опоры.

## Ожидаемое финальное состояние

- Все WPF-native окна приложения используют одну neutral baseline family для общих состояний.
- Window-level ресурсы отвечают только за уникальный layout, template-геометрию и behavior wiring.
- Shared baseline dictionaries владеют общими brush/metric/state primitives.
- `MainWindow`, `EntryWindow`, `SettingsWindow` и secondary dialogs переходят на единый naked baseline без локальных визуальных исключений.
- Только после этого поверх shared baseline может строиться `Светлая тема`.

## Правила для следующих подблоков

1. В `Подблоках 1.2+` нельзя вводить новые локальные цвета для общих состояний, если соответствующий token уже есть в shared baseline.
2. Для текущего stable `DataGrid` сохраняется правило: row carries semantic state, cell owns visible state.
3. Для `TreeViewItem` до завершения `Подблока 1.2` локальным может оставаться только template/layout часть, но не owner-модель состояний.
4. Для `TabItem`, `ListBoxItem`, `ListViewItem`, `Button`, `ToggleButton` новые подключения должны идти через shared baseline styles, а не через новые window-level state-схемы.
5. `Светлая тема`, финальная палитра и декоративные решения в этот документ не входят.

## Следующий шаг

Следующий основной шаг новой линии после фиксации этого документа:

- `Этап 1 / Подблок 1.2` — hard reset `MainWindow`

