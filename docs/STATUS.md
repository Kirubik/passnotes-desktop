# PassNotes — текущий статус проекта

Дата фиксации: 2026-03-31 (Europe/Moscow)

## 0) Активная baseline-база и статус после запуска дизайн-ветки 5.X

Дата фиксации: 2026-03-31 (Europe/Moscow)

- **Активная baseline-база текущего цикла:** `_230`
- **Статус прошлого основного цикла:** baseline-reset линия `Этапы 1.x`-`4.1` считается завершённым историческим подготовительным циклом и больше не является активным основным маршрутом.
- **Последний завершённый основной блок прошлого цикла:** `Этап 4 / Подблок 4.1`
- **Последний завершённый основной блок новой baseline-линии:** `Этап 1 / Подблок 1.38`
- **Последний завершённый хвостовой блок временной линии:** `Этап 0 / Подблок 0.6`
- **Статус временной хвостовой линии:** `Этап 0 / Подблоки 0.1-0.6` считаются закрытыми.
- **Исторические служебные фиксы:** `90.24`, `90.25`, `90.26` и `90.27` остаются зафиксированными и закрытыми.
- **Текущий активный операционный план:** `docs/NAKED_BASELINE_MASTER_PLAN.md`
- **Текущий handoff-документ для новой беседы:** `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`
- **Текущая основная дизайн-ветка:** `Этап 5 / Подблоки 5.X`
- **Последний завершённый основной блок новой дизайн-ветки:** `Этап 5 / Подблок 5.19`
- **Текущий активный основной блок новой дизайн-ветки:** — (следующий основной блок до отдельного подтверждения не открыт).
- **Последний завершённый служебный блок текущего периода:** `Этап 0 / Подблок 0.49`
- **Статус дополнительной service-линии текущего периода:** `Этап 0 / Подблоки 0.22-0.25` (audit cleanup, bounded decomposition, release hardening, tree selection / active context sync), `Этап 0 / Подблоки 0.27-0.43` (support author / contacts, Amber по умолчанию для первого запуска, URL-actions, cleanup и docs-sync), `Этап 0 / Подблок 0.45` (app icon integration), `Этап 0 / Подблоки 0.46-0.48` (GitHub/public repo prep) и `Этап 0 / Подблок 0.49` (default UI language `en-US` для новых установок) выполнены и закрыты.
- **Статус DPI/layout-хвоста:** `0.18M1-0.18M2` закрыты как отдельная service-ветка стабилизации DPI/responsive baseline; масштаб `175%` оставлен как временно допустимое ограничение вне обязательного scope по явному пользовательскому решению.
- **Статус предрелизного service-tail текущего периода:** `0.24-0.25`, последующий service/docs pass `0.27-0.43`, app icon route `0.45`, GitHub/public repo prep `0.46-0.48` и `0.49` (default UI language `en-US`) закрыты; `Этап 0 / Подблок 0.50` открыт как public GitHub snapshot / first push.
- **Текущий приоритет:** `Этап 0 / Подблок 0.50` — public GitHub snapshot / first push без публикации полной локальной истории; installer/distribution route и подготовка релизного кандидата идут следующим отдельным маршрутом после него.
- **Процессное правило закрытия багов:** любой баг, дефект, косяк или недоработка считается закрытым только после полного устранения первопричины и подтверждения стабильного, надежного результата; частичный фикс или временный обход не считается полным закрытием.
- **Активное правило baseline-интеграции:** любое новое изменение по умолчанию должно сразу проверяться на встраивание в единую baseline-систему проекта; изолированные частные решения без такой проверки запрещены, если baseline-интеграция возможна и нужна уже сейчас.
- **Статус baseline-линии:** закрытая подготовительная ветка `Этапов 1.1-1.24` и отдельная дочистка `Этапов 1.25-1.38` завершены; единый baseline-механизм собран и остается рабочей опорой для основной дизайн-ветки `5.X`.
- **Зафиксированная practical theme boundary:** весь app-owned WPF UI входит в будущий theme coverage; WPF `ContextMenu` / `MenuItem` и app-owned dialog layer уже унифицированы; tray menu и file/folder dialogs остаются системной границей ОС и не входят в обязательный practical coverage.
- **Статус служебных линий:** временная линия хвостов `Этап 0 / Подблок 0.X` по старым подблокам `0.1-0.6` закрыта; историческая service-линия `0.18C-0.20` и `0.18M1-0.18M2` закрыта как предыдущий стабилизационный контур; локальные service/docs passes `0.22-0.25`, `0.27-0.43`, `0.45` и `0.46-0.49` закрыты; `Этап 0 / Подблок 0.50` открыт как отдельный внешний publish-pass; линия `Этап 90 / Подблок 90.X` остается только в уже закрытых задачах.
- **Канонический дизайн-документ Sage Light:** `docs/PASSNOTES_LIGHT_THEME_TZ.md`
- **Актуальная концепция Sage Light:** warm neutral + natural olive, без office-blue как базового акцента.
- **Текущий активный маршрут внутри дизайн-ветки:** — (локальный theme/runtime-polish pass `5.16-5.19` завершен; следующий основной блок до отдельного подтверждения не открыт).
- **Active runtime baseline:** `App.xaml` → `Themes/Baseline.Neutral.Primitives.xaml` + `Themes/Baseline.Neutral.Icons.xaml` + `Themes/Baseline.Neutral.Controls.xaml` + `ThemeRuntimeManager` → `Themes/Theme.Standard.xaml` / `Themes/Theme.Light.xaml` / `Themes/Theme.ArcticWhite.xaml` / `Themes/Theme.MidnightSlate.xaml` / `Themes/Theme.AmberCircuit.xaml`
- **Официально зарегистрированные runtime-темы:** `standard` (`Standard` / `Стандартная`), `light` (`Sage` / `Шалфей`), `arctic-white` (`Frost` / `Иней`), `midnight-slate` (`Midnight` / `Полночь`), `amber-circuit` (`Amber` / `Янтарь`).
- **Тема по умолчанию для первого запуска / новой установки:** `amber-circuit` (`Amber` / `Янтарь`). Сохраненные темы существующих пользователей не мигрируются.
- **Archived historical theme assets:** `Themes/_Archive/Theme.Default.xaml`, `Themes/_Archive/Theme.PipBoy.xaml`
- **Опорные документы текущего периода:** `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md`, `docs/PASSNOTES_LIGHT_THEME_TZ.md`, `docs/THEME_COVERAGE_CHECKLIST_STAGE5_BLOCK5_2.md`, `docs/NAKED_BASELINE_STAGE1_BLOCK1_13_MENU_DIALOG_AUDIT.md`, `docs/PROJECT_AUDIT_STAGE3_BLOCK3_1.md`, `docs/BASELINE_SKELETON_STAGE4_BLOCK4_1.md`, `docs/NAKED_BASELINE_STAGE1_BLOCK1_1_SHARED_PRIMITIVES.md`, `docs/NAKED_BASELINE_STAGE1_BLOCK1_2_MAINWINDOW_HARD_RESET.md`, `docs/NAKED_BASELINE_ICON_TOKEN_LAYER_CONTRACT.md`

## 1) Актуальная база (истина)

- **Активная baseline-база текущего цикла:** `_230`
- **Последний подтвержденный git-checkpoint активной базы:** `c9f60c1`
- **Последний подтвержденный архивный checkpoint активной базы:** `PassNotes_Block230_Stage0_Block0.48_GitHubPublicRepoPrep_291.zip`
- **Статус текущей рабочей базы:** стабильная после ручной проверки последних изменений; GitHub/public repo prep и default-language pass `0.49` закрыты без отката активной базы.
- **Реальное состояние release/distribution части в текущем working tree:** release-docs и public repo surface присутствуют, installer/build pipeline файлы отсутствуют и будут открываться отдельным следующим маршрутом.

Исторические опорные точки:

- `PassNotes_Block204_R2.1.4.3_InnoFix_Dword_BuildExit_224.zip`
- `PassNotes_Block159_OptFinal_UiPrefs_Min_WindowRowHeight_Version_179.zip`
- `PassNotes_Block156_MVP3B2_Final_176.zip`

Дополнительно (релизная документация 0.1.0):

- `docs/RELEASE_CHECKLIST.md`
- `docs/RELEASE_NOTES.md`

## 2) Инварианты (не ломать)

- Дизайн/темы/цвета — не трогать вне подтвержденной основной дизайн-ветки `5.X`.
- UX: клик в пустоту снимает выделение, **контекст справа не сбрасывается**.
- Никакого отдельного постоянного режима “все записи”.
- Явный глобальный поиск по непустому запросу остается допустимым текущим поведением.
- Не ломать: multi-select / Ctrl+A / Del / drag&drop.
- Трей не ломать.
- Экспорт/импорт/бэкап — только безопасные форматы (без незашифрованного JSON).

## 3) Статус уровней «Финал»

### 3.1. Финал (железобетон) — ✅ достигнут (MVP-3B2)

Закрыто в рамках `..._176`:

- **Вложения: Variant B (pending до Save)**
  - можно добавлять вложения до сохранения записи;
  - нет “просачивания” при Cancel/Lock/Restore;
  - нет plaintext; всё остаётся безопасным.
- **Orphan cleanup (attachments)**
  - гарантированные логи `ATT_ORPHAN_CLEANUP_BEGIN → (SKIP | END | ERROR)`
  - отдельный лог-триггер `ATT_ORPHAN_CLEANUP_ERROR` с подробностями;
  - edge-cases meta/blob mismatch, dangling refs, защита от ложных переносов/пуржа;
  - `orphan_cleanup_report.txt` без путей;
  - backup/restore verify не блокируется dangling meta;
  - self-heal dangling attachment meta: Unlock + rate-limit + перед BackupNow/RestoreBackup;
  - исправлена логика корзины по вложениям: restore не удаляет blob, delete forever удаляет.
- **Smoke-инварианты**
  - multi-select/Ctrl+A/Del/drag&drop/трей не сломаны;
  - инвариант клика в пустоту сохраняется;
  - отдельный постоянный режим “все записи” не вводили; явный глобальный поиск по непустому запросу остается допустимым текущим поведением.

### 3.2. Финал (оптимальный) — ✅ закрыт в текущей редакции

Закрыто в рабочей линии `..._179` и ранее (P1):

- Копирование логина через `ClipboardSecurity` там, где нужно.
- Замена оставшихся MessageBox «Copied…» → ненавязчивые popup/toast там, где было в зоне правок.
- Поиск по URL — **уже присутствует** (не пере-реализовывали).
- Открытие файла хранилища и папки бэкапов из настроек — **уже присутствует**.
- **UI prefs (минимум):**
  - запоминание **размера/позиции окна** и состояния Normal/Maximized;
  - запоминание **высоты строк** (RowHeight);
  - добавлен `UiPrefsVersion` для безопасного сброса UI-настроек при будущем редизайне.

### 3.3. Финал (идеальный) — ✅ закрыт в текущей редакции

См. план: `docs/PassNotes_Plan_Final_Ideal.md`.

**Уже выполнено:**
- ✅ I1.1–I1.3: хоткеи внутри приложения + защита от конфликтов ввода.
- ✅ I2.1–I2.3: HelpWindow (RU/EN) + TOC (navigation.md), Back/Forward, якоря, запоминание последней страницы (сессия), F1/меню.
- ✅ CSS-полиш таблиц в HelpWindow (чтобы не сливалось).
- ✅ I3.1: `manual.md` + `faq.md` (RU/EN) — архив `_201`.
- ✅ I3.2: `about.md` (RU/EN) **внутри справки** — архив `_202`.
- ✅ I3.3: актуализация `STATUS/CHANGELOG/AUDIT` + правка плана (I3.2) — архив `_203`.
- ✅ PD2.1: toggle колонки «Обновлено» (контекстное меню таблицы + заголовка), сессия-only — `_204`.
- ✅ PD2.1 hotfix: исправление сборки (XAML header menu) — `_205`.
- ✅ I3.4: финальная синхронизация docs после PD2.1 + вычеркивание шрифта — `_206`.

**Осталось по текущему плану «Финал (идеальный)»:**
- — нет.


### 3.4. Release / Public Repo Prep (0.1.0) — ✅ актуализировано под текущую базу

Это **вне** плана «Финал (идеальный)» (план закрыт), но нужно для первого повторяемого релиза и аккуратной публичной публикации репозитория.

- ✅ R1.1: публичное имя “PassNotes Desktop” (UI + metadata), без смены exe/путей — `_207`.
- ✅ R1.2: версия **0.1.0** + отображение в Help → About — `_208`.
- ✅ R1.3: релизная документация (checklist + release notes) — `_209`.

Дополнительно (полировка UX + code health перед установщиком):

- ✅ R1.4.1: toast подтверждения “копирование” у курсора для контекстного меню (ПКМ), toolbar-иконки без изменений — `_210` (+ hotfix сборки `_211`).
- ✅ R1.5 (Code Health):
  - R1.5.1: `docs/TECH_DEBT.md` (инвентаризация техдолга) — `_212`.
  - R1.5.2: safe cleanup (дубли + null-safe + минимальная диагностика) — `_213`.
  - R1.5.3.1: `SettingsStore` атомарный Save + fallback Load на `.bak` + auto-heal — `_214`.
  - R1.5.3.2: `VaultIoGate` slow-wait логирование через `DiagnosticsLog` — `_215`.
  - R1.5.3.3: унификация toast (Settings + Password Generator) через `PopupToastController` — `_216`.
  - R1.5.4: финальная полировка docs + UTF-8 diagnostics log — `_217`.
- ✅ `Этап 0 / Подблок 0.22`: аудит хвостов и удаление подтвержденного orphan `DataGridSelectedItemsBehavior`.
- ✅ `Этап 0 / Подблок 0.23`: bounded decomposition (`декомпозиция`) `MainWindow` и `EntryEditorView`; архивы `_276`, `_277`.
- ✅ `Этап 0 / Подблок 0.24`: release hardening (`предрелизная стабилизация`) — hosted `Esc`/popup fix, cleanup дублирующего resource key и подтвержденный короткий smoke текущей базы.
- ✅ `Этап 0 / Подблок 0.45`: новый app icon интегрирован в `exe` и `tray`.
- ✅ `Этап 0 / Подблоки 0.46-0.48`: проведен GitHub/public repo prep — audit репозитория, hardening `.gitignore`, новый public-ready `README`, добавлена лицензия MIT, release/docs и статусные документы синхронизированы с фактической структурой текущего working tree; последний архивный checkpoint активной базы теперь `_291`.
- ✅ `Этап 0 / Подблок 0.49`: дефолтный язык интерфейса для новых установок и нового `settings.json` переключен на `en-US`; уже сохраненный `Language` существующих пользователей не мигрируется.

Installer-route:

- В текущем working tree installer/build pipeline файлы отсутствуют.
- Исторические упоминания installer-маршрута в changelog относятся к прошлым веткам/базам и не считаются подтверждением наличия этих файлов в текущем репозитории.
- Следующий отдельный практический маршрут после закрытия `Этап 0 / Подблок 0.50`: восстановление/подготовка installer/distribution route.


## 4) Что вычеркнули / от чего отказались (официально)

- **Автозапуск с Windows** — отказались (лишняя поверхность риска/безопасность + нестабильность, откат).
- **Restore из корзины в исходную папку** — отказались (edge-cases: удалённая папка, риск регрессий).
- **Расширенные UI prefs** (последняя папка/сортировка/ширины колонок/и т.п.) — отказались (нестабильность, решили оставить минимум: окно + RowHeight).
- **Масштаб/размер шрифта для дерева/таблицы записей** — вычеркнули (решение: шрифт/масштаб не трогаем).

## 5) UiPrefsVersion — как использовать при будущем редизайне

В настройках хранится `UiPrefsVersion`. В коде есть `CurrentUiPrefsVersion`.

Если при большом изменении UI/стилей/контролов нужно сбросить старые UI-числа (например, RowHeight), достаточно:

1) увеличить `CurrentUiPrefsVersion` (например, с 1 до 2);
2) при следующем запуске приложение автоматически:
   - не применит старые UI prefs,
   - сбросит на дефолты,
   - сохранит новую версию.

## 6) Финал (идеальный) — план и остаток работ

Источник истины: `docs/PassNotes_Plan_Final_Ideal.md`.

- **Сделано:** I1 (Hotkeys) ✅, I2 (Help RU/EN) ✅, I3 (Manual/FAQ/About + docs) ✅, PD2.1 (toggle «Обновлено») ✅.
- **Осталось:** — (в текущей редакции плана).

> Вне текущего плана «Финал (идеальный)» (дальний бэклог, по отдельному решению): теги, история изменений, PIN, импорт KeePass/CSV, глобальный поиск/фильтры/breadcrumb, undo, финальный редизайн.
---

См. также: [CHANGELOG](CHANGELOG.md)
См. также: [План «Финал (идеальный)»](PassNotes_Plan_Final_Ideal.md)
См. также: [Hotkeys inventory (I1.1)](HOTKEYS_INVENTORY.md)
См. также: [Hotkeys catalog (machine-readable)](hotkeys_catalog.json)

---

## Help (RU/EN)

- Repository: `docs/help/ru|en/*.md`
- Build output (next to exe): `help/ru|en/*.md` and shared assets in `help/_assets/`
- Open in-app help: **F1** or **Menu → Help** (MainWindow dropdown).


- R2.1 hotfix: fixed Inno Setup process guard type mismatch (LoadStringFromFile expects AnsiString).
