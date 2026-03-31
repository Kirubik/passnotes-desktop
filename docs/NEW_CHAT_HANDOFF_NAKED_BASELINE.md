# PassNotes — handoff для новой беседы по naked baseline UI

## 1. Что считать активной базой

- Активная baseline-база: `_230`
- Активный runtime (`runtime-слой`) состоит из:
  - `App.xaml`
  - `Themes/Baseline.Neutral.Primitives.xaml`
  - `Themes/Baseline.Neutral.Icons.xaml`
  - `Themes/Baseline.Neutral.Controls.xaml`
  - `ThemeRuntimeManager` → `Themes/Theme.Standard.xaml` / `Themes/Theme.Light.xaml` / `Themes/Theme.ArcticWhite.xaml` / `Themes/Theme.MidnightSlate.xaml` / `Themes/Theme.AmberCircuit.xaml`

## 2. Что считать закрытым прошлым циклом

Предыдущий цикл baseline-reset и частичных дизайн-итераций считать завершенным историческим этапом.

Это значит:

- не продолжать старую активную нумерацию;
- не считать `Этап 4 / Подблок 4.2` следующим главным шагом;
- не возвращаться к точечным локальным fixes (`фиксам`) как к основному маршруту.

## 3. Что считать текущим активным планом

Главный источник истины для текущего периода:

- `docs/NAKED_BASELINE_MASTER_PLAN.md`

Закрыты и считаются baseline-опорой:

- `Этапы 1.1-1.14`
- возвратные cleanup-подблоки `1.16`, `1.17`, `1.15`, `1.20`, `1.19`, `1.21`, `1.18`, `1.22`, `1.23`, `1.24`
- отдельная дочистка `1.25-1.38`
- временная линия хвостов `0.1-0.6`

Считать зафиксированным следующее:

- `Этап 2 / Подблок 2.1` удален из текущего активного маршрута;
- прежняя немедленная theme/spec ветка не является следующим шагом;
- новая отдельная основная дизайн-ветка запущена как `Этап 5 / Подблоки 5.X`;
- блоки этой линии `Этапы 5 / Подблоки 5.1-5.19` уже выполнены;
- после `5.3` пройдена отдельная service-линия стабилизации `Этапов 0 / Подблоков 0.18C-0.20`;
- по этой service-линии подблоки `0.18C-0.18L` и `0.20` выполнены;
- `Этап 0 / Подблок 0.18K` закрыт по совокупности машинного/кодового аудита и подтвержденного пользователем живого ручного GUI-smoke;
- `Этап 0 / Подблок 0.18L` — синхронизация статусных документов и handoff — выполнен;
- `Этап 0 / Подблок 0.18M1` — app-level DPI baseline и нормализация геометрии `MainWindow` — выполнен;
- `Этап 0 / Подблок 0.18M2` — responsive hosted/dialog layout stabilization на рабочих масштабах `100% / 125% / 150%` — выполнен; масштаб `175%` оставлен как временно допустимое ограничение вне обязательного scope по явному пользовательскому решению;
- `Этап 0 / Подблок 0.18N` — финальный pre-theme audit — выполнен; route/docs/handoff согласованы, сборка зеленая, реальных блокеров перед `5.4` не выявлено;
- `Этап 5 / Подблок 5.4` — точечная baseline-доводка первой стабилизации runtime-темы `light` / `Sage Light` с выравниванием toast/message-scroll contract (`контракта toast/message-scroll`) и зеленой контрольной сборкой — выполнен; закрытие зафиксировано по явному пользовательскому решению.
- `Этап 5 / Подблок 5.5` — замена office-blue light-концепции на каноническую olive-based `Sage Light` систему — выполнен;
- `Этап 5 / Подблок 5.6` — отдельная runtime-тема `Arctic White` — выполнен;
- `Этап 5 / Подблоки 5.7-5.19` — регистрация полного runtime-theme набора, live theme switching и локальный theme/runtime-polish — выполнены;
- `Этап 0 / Подблоки 0.27-0.43` — support author / contacts, Amber по умолчанию, URL-actions, cleanup и docs-sync — выполнены;
- `Этап 0 / Подблок 0.45` — app icon integration в `exe` и `tray` — выполнен;
- `Этап 0 / Подблоки 0.46-0.48` — GitHub/public repo prep (`audit`, `.gitignore`, `README`, `LICENSE`, docs-sync) — выполнены;
- `Этап 0 / Подблок 0.49` — дефолтный язык интерфейса для новых установок переключен на `en-US` без миграции уже сохраненных пользовательских настроек;
- текущий активный service-шаг: `Этап 0 / Подблок 0.50` — public GitHub snapshot / first push без публикации полной локальной истории;
- канонический дизайн-документ `light` / `Sage Light`: `docs/PASSNOTES_LIGHT_THEME_TZ.md`;
- office-blue трактовку light theme считать неактуальной; активная редакция документа описывает warm-neutral + natural olive концепцию.

## 4. Главная цель текущего периода

Сохранить по всему WPF-native UI приложения единый naked baseline UI как готовую опору и развивать только отдельную подтвержденную дизайн-ветку `5.X`.

Это означает:

- не трогать дизайн хаотично вне линии `5.X`;
- не плодить локальные визуальные исключения;
- строить runtime-темы поверх baseline, а не поверх случайных локальных правок;
- считать официально зарегистрированный набор runtime-тем состоящим из `standard` / `Standard`, `light` / `Sage`, `arctic-white` / `Frost`, `midnight-slate` / `Midnight` и `amber-circuit` / `Amber`;
- считать `amber-circuit` темой первого запуска и новой установки, не мигрируя уже сохраненные `ThemeId`.

### 4.1. Зафиксированная practical theme boundary

Для текущей дизайн-ветки считать зафиксированным следующее правило:

- тема должна покрывать весь app-owned WPF UI;
- WPF `ContextMenu` / `MenuItem` и app-owned dialog layer уже доведены до общего app-owned/shared слоя до будущего design/theme pass (`прохода дизайна/темы`);
- tray menu и file/folder dialogs остаются системной внешней границей ОС и не считаются обязательным охватом practical variant (`практического варианта`).

Не трактовать это как разрешение локально темизировать одно отдельное меню или один отдельный диалог вне общего menu/dialog layer.

### 4.2. Обязательное правило общей baseline-интеграции

Любое новое изменение по умолчанию должно сразу проверяться на встраивание в единую baseline-систему проекта.

Если baseline-интеграция в конкретной задаче сейчас нежелательна, рискованна, преждевременна или может повредить стабильности, это нужно прямо указывать до начала реализации.

## 5. Что нельзя ломать

Сохранять инварианты:

- multi-select;
- `Ctrl+A`;
- `Del`;
- drag & drop;
- tray behavior;
- secure import/export/backup;
- правило клика в пустоту без сброса правого контекста.

## 5.1. Как считать баг закрытым

Любой баг, дефект, косяк или недоработка считается закрытым только после полного устранения первопричины и подтверждения стабильного, надежного результата.

Частичный фикс, временный обход, ослабление симптомов или локальная заплатка не считаются полным закрытием.

## 6. Какие документы читать первыми в новой беседе

1. `AGENTS.md`
2. `docs/NAKED_BASELINE_MASTER_PLAN.md`
3. `docs/STATUS.md`
4. `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md`
5. `docs/THEME_COVERAGE_CHECKLIST_STAGE5_BLOCK5_2.md`
6. `docs/PASSNOTES_LIGHT_THEME_TZ.md`

Дополнительно по контексту:

- `docs/PROJECT_AUDIT_STAGE3_BLOCK3_1.md`
- `docs/BASELINE_SKELETON_STAGE4_BLOCK4_1.md`
- `docs/NAKED_BASELINE_STAGE1_BLOCK1_13_MENU_DIALOG_AUDIT.md`
- `docs/NAKED_BASELINE_ICON_TOKEN_LAYER_CONTRACT.md`

## 7. С чего начинать работу в новой беседе

Начинать с:

- проверки `docs/CURRENT_CONTEXT.md` и `docs/STATUS.md` на предмет текущего закрытого состояния;
- подтверждения, что последние закрытые блоки сейчас: `Этап 5 / Подблок 5.19` и `Этап 0 / Подблок 0.49`;
- понимания, что текущий активный service-блок: `Этап 0 / Подблок 0.50`, а installer-route остается следующим отдельным маршрутом после него.

Считать зафиксированным:

- baseline-подготовка `Этапов 1.1-1.38` закрыта;
- линия хвостов `0.X` по ранним service-подблокам `0.1-0.6` закрыта;
- дополнительная service-линия стабилизации после `5.3` кодово закрыта на подблоках `0.18C-0.18J` и `0.20`;
- новая отдельная основная дизайн-ветка уже создана;
- исторические `Themes/_Archive/Theme.Default.xaml` и `Themes/_Archive/Theme.PipBoy.xaml` не являются живой runtime-основой;
- текущие runtime-темы `standard`, `light`, `arctic-white`, `midnight-slate` и `amber-circuit` грузятся через общий theme runtime scaffold (`каркас runtime-темы`).

## 8. Что считать закрытым к моменту handoff

Кодово и по baseline-механике считать закрытым:

- shared visual primitives;
- hard reset `MainWindow`, `EntryWindow`, `SettingsWindow`;
- visual baseline secondary dialogs;
- WPF `ContextMenu` / `MenuItem`;
- text context menu layer (`слой текстовых контекстных меню`);
- app-owned message/dialog layer (`слой app-owned сообщений/диалогов`) в `App.xaml`, `MainWindow`, secondary dialogs;
- practical menu/dialog boundary (`практическую границу меню/диалогов`) для всего app-owned WPF UI;
- shared icon token layer, миграцию `MainWindow` + `EntryWindow` на semantic icon tokens (`семантические токены иконок`) и tokenization shared control glyphs (`токенизацию глифов общих контролов`);
- стартовый runtime theme scaffold (`каркас runtime-темы`) и последующий полный runtime-theme набор до `Amber Circuit`;
- отдельную runtime-тему `Arctic White` как второй светлый вариант внутри общего runtime theme scaffold;
- отдельную runtime-тему `Amber Circuit` как тему первого запуска / новой установки внутри того же runtime theme scaffold;
- раздельные `primitives` (`структурные примитивы`) / brush-layer (`слой кистей`), baseline-shell (`baseline-оболочку`) `HelpWindow` и отдельный theme coverage checklist (`чеклист охвата темы`);
- hosted recovery lifecycle (`жизненный цикл hosted-восстановления`) и перевод `unlock / restore / message / change password / folder / trash-family` на общий hosted-contract и lifecycle-state model;
- удаление passive legacy window-layer (`пассивного legacy-слоя окон`) `Entry / Settings / Comment / PasswordGenerator`;
- финальную зачистку remaining lock-state consumers (`оставшихся потребителей состояния блокировки`) в `MainWindow`;
- code/build regression-аудит service-линии `0.18K`, дополненный подтвержденным пользователем живым ручным GUI-smoke.

Отдельный audit (`аудит`) этого состояния зафиксирован в:

- `docs/NAKED_BASELINE_STAGE1_BLOCK1_13_MENU_DIALOG_AUDIT.md`
- `docs/NAKED_BASELINE_ICON_TOKEN_LAYER_CONTRACT.md`
