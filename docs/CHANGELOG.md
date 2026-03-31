# PassNotes — CHANGELOG (укрупнённый)

Дата фиксации: 2026-03-31 (Europe/Moscow)

> Это **укрупнённый** changelog по ключевым «вехам» и решениям (что вошло в рабочую линию, а что было откатано/вычеркнуто).

## Main-line and service fixes (активная линия и стабилизация вне main-line)

### Этап 0 / Подблок 0.49 — дефолтный язык интерфейса `en-US` для новых установок
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- В `SettingsStore` дефолтный `Language` переключен с `ru-RU` на `en-US`, чтобы новая чистая установка и новый `settings.json` стартовали с английским UI.
- Уже существующие пользовательские настройки не мигрируются принудительно: если `Language` ранее был сохранен, приложение продолжает использовать его как есть.
- Правка подтверждена ручной проверкой и зафиксирована отдельным git-checkpoint `c9f60c1`.

### Этап 0 / Подблок 0.48 — docs sync для public-ready репозитория
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `docs/RELEASE_CHECKLIST.md` и `docs/RELEASE_NOTES.md` переписаны под фактическую структуру корня: команды сборки теперь указывают на `PassNotes.csproj` в корне, а installer-route прямо вынесен за пределы текущего working tree.
- Зафиксировано, что корневой `RunPassNotes.vbs` является repo/dev launcher, а не release launcher для portable package.
- `docs/CURRENT_CONTEXT.md`, `docs/STATUS.md`, `docs/CHANGELOG.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md` и `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md` синхронизированы под фактически закрытые `Этап 0 / Подблок 0.45` и `Этап 0 / Подблоки 0.46-0.48`, а также под последний архивный checkpoint `_290`.

### Этап 0 / Подблок 0.47 — public-facing README, MIT license и выравнивание репозиторной витрины
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `README.md` полностью переписан как публичная входная точка проекта: добавлены понятное позиционирование, ключевые возможности, security/data model, команды сборки/запуска, release publish и объяснение состава `docs/`.
- В корень добавлен `LICENSE` с лицензией MIT как часть public-ready оформления репозитория.
- Публичный нейминг выровнен вокруг `PassNotes Desktop` без необоснованного глобального rename внутренних идентификаторов и без изменения имени `exe`.

### Этап 0 / Подблок 0.46 — audit репозитория и hardening `.gitignore`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Проведен локальный audit структуры репозитория, tracked files и публичной поверхности проекта без удаления файлов вслепую.
- Подтверждено отсутствие явных секретов в tracked files; публичные ссылки Boosty / GitHub / Telegram / YooMoney / email оставлены как допустимая часть открытого репозитория по явному пользовательскому решению.
- `.gitignore` расширен под WPF/.NET/Visual Studio/VS Code/local publish flow: отдельно закрыты `artifacts/`, `publish/`, IDE-user files, temp/log outputs и локальный installer output.

### Этап 0 / Подблок 0.43 — финальная синхронизация статусной документации
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `docs/CURRENT_CONTEXT.md`, `docs/STATUS.md`, `docs/CHANGELOG.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md` и `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md` синхронизированы под фактически завершенные `Этап 5 / Подблок 5.19` и `Этап 0 / Подблок 0.43`.
- В статусных документах зафиксированы актуальные runtime-темы `standard`, `light`, `arctic-white`, `midnight-slate`, `amber-circuit`, новый дефолт первого запуска `Amber` и отсутствие автоматически активного следующего main/service блока.
- Инвариант поиска уточнен до фактического поведения: отдельного постоянного режима “все записи” нет, но явный глобальный поиск по непустому запросу допустим и остается частью текущего маршрута.

### Этап 0 / Подблок 0.42 — обновление help по поиску, терминам и backup guidance
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Help-страницы `manual.md`, `faq.md` и `vaults-and-backups.md` в RU/EN синхронизированы под реальное поведение поиска, пояснение интерфейсных терминов и важное предупреждение про папку вложений рядом с backup.
- Для поиска зафиксирована понятная пользовательская модель: context chip показывает выбранный контекст, его сброс позволяет искать по всем записям через непустой запрос, а пустой запрос без контекста не означает постоянный режим “все записи”.
- В backup-docs добавлено явно выделенное предупреждение: если удалить созданную вместе с backup папку вложений, полное восстановление состояния с вложениями может стать невозможным.

### Этап 0 / Подблоки 0.40-0.41 — cleanup support-маршрута и legacy-helper хвостов
**Архив:** `PassNotes_Block230_Stage0_Block0.41_SupportAndLegacyCleanup_288.zip`

- Из `SupportAuthor` удален подтвержденный мертвый action-layer, неиспользуемые стили и локальный дубль URL-availability route; экран переведен на общий `ExternalUrlService`.
- Из `EntryEditorView` и `MainWindow` удалены подтвержденные legacy-helper методы без вызовов, оставшиеся после ранней декомпозиции и старого оконного маршрута.
- Cleanup закрыт чистой сборкой и checkpoint-ом без изменения пользовательского поведения.

### Этап 0 / Подблоки 0.37-0.39 — URL-actions для ссылки записи
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Для ссылки записи добавлен общий shared route `EntryUrlActions` с единым copy/open-поведением и нормализацией web-URL без схемы через `https://`.
- В главном окне у URL записи появились inline-действия `Copy` и `Open`, а в контекстное меню записи добавлены пункты `Копировать ссылку` и `Открыть ссылку в браузере`.
- В окне записи такие же действия добавлены непосредственно справа от поля ссылки и в его ПКМ; после отдельного layout-pass поле URL возвращено почти к прежней ширине, а action-rail редактора визуально выровнен.

### Этап 0 / Подблоки 0.32-0.36 — Amber по умолчанию и компактный support layout
**Архивы:** `PassNotes_Block230_Stage0_Block0.33_SupportQuickActionsAndAmberDefault_286.zip`, `PassNotes_Block230_Stage0_Block0.36_SupportCompactLinksLayout_287.zip`

- Для первого запуска и новой установки темой по умолчанию стал `Amber`, при этом сохраненные `ThemeId` существующих пользователей не мигрируются.
- В support-окне добавлены quick actions, copy-toast и затем выполнена компактная перекомпоновка карточек до формата `логотип слева + ссылки справа`.
- После ручной проверки layout-route и copy-flow были подтверждены как рабочие и зафиксированы checkpoint-ами.

### Этап 0 / Подблоки 0.27-0.31 — support author, контакты и cleanup help feedback
**Архивы:** `PassNotes_Block230_Stage0_Block0.27_SupportAuthorHostedView_284.zip`, `PassNotes_Block230_Stage0_Block0.31_SupportContactsHelpCleanup_285.zip`

- В приложение встроен hosted-раздел `Поддержать автора / Контакты` с Boosty, GitHub, ЮMoney, Telegram и Email внутри общего baseline-маршрута.
- Исправлен runtime-crash route на email-иконке и приведены в порядок локальные support assets и semantic icon resources.
- Из help удален старый раздел `Контакты и обратная связь`, потому что контакты были перенесены в отдельный UI-маршрут.

### Этап 5 / Подблоки 5.16-5.19 — локальный theme/runtime polish
**Архивы:** `PassNotes_Block230_Stage5_Block5.19_LightThemeSurfaceHierarchy_282.zip`, `PassNotes_Block230_Stage5_Block5.19_ThemedSliderPolish_283.zip`

- Выполнен локальный polish runtime-theme слоя после live switching: подстроена иерархия светлых surface-слоев, выровнена читаемость secondary surfaces и доведены runtime-хвосты.
- Пользовательские display names тем укорочены до однословного формата `Standard / Sage / Frost / Midnight / Amber` без изменения внутренних `themeId`.
- Themed slider и связанные мелкие runtime-control routes доведены до согласованного вида; ручная проверка подтверждена пользователем.

### Этап 0 / Подблок 0.25 — фикc рассинхрона tree selection и active context
**Архив:** `PassNotes_Block230_Service_FolderTreeContextSync_280.zip`

- Закрыт корневой service-дефект дерева, при котором визуально выделенная папка могла расходиться с реальным active context в правой панели и статус-баре.
- Исправлены две основные причины: утечка one-shot suppress-флага в Explorer-like tree routes и перенос долгоживущего mismatch между `_selectedFolderNode` и `_activeFolderNode` через rebuild/service-маршруты.
- После фикса transient RMB-route остается допустимым только на время открытого контекстного меню; steady-state дерева после закрытия меню снова схлопывается к реальному active context.

### Этап 0 / Подблок 0.24 — release hardening, cleanup предрелизных дефектов и короткий smoke
**Архив:** `PassNotes_Block230_Service_ReleaseHardeningDocsSync_278.zip`

- Проведен предрелизный audit (`аудит`) критичных маршрутов без нового cleanup-рефакторинга: отдельно проверены `selection`, hosted input/focus family, `attachments`, `backup / import / export`, `tray` и Release build.
- Исправлены два подтвержденных предрелизных дефекта: конфликт глобального hosted `Esc` с popup/dropdown route и дублирующий resource key `AttachmentsSaveFailed`, из-за которого Release build шел с warnings.
- После точечных фиксов подтверждены чистые сборки `Debug codexverify` и `Release`, а затем пользователь подтвердил ручную проверку и короткий smoke по критичным пунктам (`multi-select / Ctrl+A / Del / drag&drop / attachments / backup / export / import / tray / help`).
- На текущем этапе `Этап 0 / Подблок 0.24` считается закрытым; новый service-подблок после него не открыт.

### Этап 5 / Подблоки 5.14-5.15 — runtime-переключение темы на лету и coverage-pass
**Архив:** `PassNotes_Block230_Stage5_Block5.15_RuntimeThemeLiveSwitch_279.zip`

- Реализован общий runtime-механизм live theme switching без перезапуска: preview в settings, commit по `Save` и rollback по `Cancel`/close-without-save.
- Текущая тема теперь применяется сразу через общий `ThemeRuntimeManager`/`AppThemeCatalog` route, а выбор в настройках интегрирован в baseline runtime-theme contract вместо локальных хаках по окнам.
- Во втором проходе закрыты остаточные live-refresh хвосты в secondary surfaces; пользователь подтвердил ручной GUI-smoke, поэтому `Этап 5 / Подблоки 5.14-5.15` считаются закрытыми.

### Этап 0 / Подблок 0.23 — bounded decomposition `MainWindow` и `EntryEditorView`
**Архивы:** `PassNotes_Block230_Service_EntryEditorAttachmentsSplit_276.zip`, `PassNotes_Block230_Service_EntryEditorLifecycleSplit_277.zip`

- В рамках безопасной декомпозиции из `MainWindow.xaml.cs` вынесены отдельные partial-кластеры для hosted dialog lifecycle, `UI preferences` и `folder panel/search` без изменения поведения.
- Из `EntryEditorView` вынесены самостоятельные partial-файлы `EntryEditorView.Attachments.cs` и `EntryEditorView.Lifecycle.cs`, что закрыло предрелизную stop-line cleanup без углубления в рискованный рефактор `MainWindow`.
- Cleanup-линия перед релизом после `0.23` была сознательно остановлена, а следующим service-шагом открыт уже не новый refactor, а стабилизационный проход `0.24`.

### Этап 0 / Подблок 0.22 — аудит хвостов и удаление подтвержденного orphan-code
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Проведен статический audit (`аудит`) хвостов, dead-code (`мертвого кода`) и cleanup-кандидатов: большого пласта реально мертвого кода не подтверждено.
- Из проекта удален подтвержденный orphan `Behaviors/DataGridSelectedItemsBehavior.cs`; сборка после удаления осталась чистой.
- Зафиксировано, что основной оставшийся риск текущего периода сидит не в мусорных файлах, а в крупных service-entry points (`точках входа сервисной логики`) и общем техдолге сопровождения.

### Этап 5 / Подблок 5.7 — официальная регистрация полного набора runtime-тем и doc-sync
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Опорные docs синхронизированы с фактическим runtime-состоянием проекта: официально зафиксированы темы `standard`, `light` / `Sage Light`, `arctic-white` / `Arctic White` и `midnight-slate` / `Midnight Slate`.
- `docs/CURRENT_CONTEXT.md`, `docs/STATUS.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md` и `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md` обновлены так, чтобы активным основным подблоком считался `Этап 5 / Подблок 5.7`, а `Этап 5 / Подблок 5.6` фиксировался как последний завершенный.
- В русской локали названия тем выровнены под тот же принцип перевода, что и у `Стандартной`: `Шалфейная светлая`, `Арктическая белая`, `Полуночно-сланцевая`.

### Этап 5 / Подблок 5.7 — новая темная runtime-тема Midnight Slate и baseline-fixes theme-contract
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Добавлена новая отдельная runtime-тема `midnight-slate` / `Midnight Slate` с собственным словарем `Themes/Theme.MidnightSlate.xaml`, регистрацией в `AppThemeCatalog` и отображаемым именем в UI.
- `Midnight Slate` встроена в общий theme runtime scaffold (`каркас runtime-темы`) и покрывает app-owned UI через существующий `Brush.*` contract, без изменения бизнес-логики, MVVM и layout.
- Для устойчивой посадки новых тем на baseline исправлены корневые theme-contract хвосты: добавлен `Brush.Caret`, выровнен `ComboBox`/toolbar text route, введен `Brush.GridHeaderBackground`, а `TreeView` переведен на более полный theme-aware host contract без system/white fallback.

### Этап 5 / Подблок 5.6 — новая runtime-тема Arctic White
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Добавлена новая отдельная runtime-тема `arctic-white` / `Arctic White` с собственным `themeId`, отображаемым именем в UI и словарем `Themes/Theme.ArcticWhite.xaml`.
- `Arctic White` оформлена как отдельный светлый вариант внутри существующей системы runtime-тем и не заменяет ни `standard`, ни `light` / `Sage Light`.
- Новый словарь темы встроен в общий `ThemeRuntimeManager`/`AppThemeCatalog` маршрут, поэтому `MainWindow`, dialogs, tree/grid/input family, popup/context surfaces и прочие app-owned routes получают тему через общий baseline-contract.

### Этап 5 / Подблок 5.5 — официальное переименование текущей light theme в Sage Light
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Текущая уже настроенная runtime-тема `light` официально переименована в `Sage Light` без пересоздания theme dictionary и без повторной настройки палитры.
- Пользовательское имя темы в UI переведено на `Sage Light`, при этом внутренний `themeId = "light"` и `Themes/Theme.Light.xaml` сохранены ради совместимости с theme switching и сохраненными настройками.
- Ключевые docs синхронизированы так, чтобы текущая light theme фиксировалась как `Sage Light`, а старое имя `Светлая` больше не использовалось как официальное пользовательское название.

### Этап 5 / Подблок 5.5 — замена office-blue light-концепции на warm-neutral + olive
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Канонический light-theme документ `docs/PASSNOTES_LIGHT_THEME_TZ.md` заменен на olive-версию из приложенного `PassNotes_LightTheme_TZ_For_Codex_Olive.md`; старый office-blue вариант больше не считается источником истины.
- `docs/CURRENT_CONTEXT.md`, `docs/STATUS.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md` и `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md` синхронизированы под новую warm-neutral + natural olive концепцию runtime-темы `light` / `Sage Light`.
- `Themes/Theme.Light.xaml` переведен с office-blue accent family на centralized warm-neutral + olive palette-contract: обновлены surface/background roles, accent/focus/select family, popup/tree/grid states, semantic `Info` role и связанные secondary keys без разбрасывания hardcoded цветов по окнам.
- Shared baseline-control layer продолжает использовать тот же `Brush.*` runtime-contract, поэтому `MainWindow`, `TreeView`, `DataGrid`, `ContextMenu`, input family и dialog family получают новую olive-концепцию через централизованные theme resources, без изменения бизнес-логики и layout.

### Этап 5 / Подблок 5.5 — канонизация light theme ТЗ и выравнивание palette-contract
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Приложенный документ по светлой теме перенесен в репозиторий как `docs/PASSNOTES_LIGHT_THEME_TZ.md` и зафиксирован как главный источник истины по runtime-теме `light` / `Sage Light`.
- `docs/CURRENT_CONTEXT.md`, `docs/STATUS.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md` и `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md` синхронизированы так, чтобы `Этап 5 / Подблок 5.5` считался уже активным блоком и ссылался на новый канонический документ.
- `Themes/Theme.Light.xaml` выровнен под палитру и семантические роли из канонического light-theme ТЗ в духе Windows 11-like Light без разбрасывания hardcoded цветов по окнам.
- `Themes/Baseline.Neutral.Brushes.xaml` расширен централизованными semantic brush roles (`семантическими ролями кистей`), а shared styles в `Themes/Baseline.Neutral.Controls.xaml` точечно переведены на новый disabled/divider/check-state contract (`контракт disabled/divider/check-state`) без изменения бизнес-логики и layout.

### Этап 5 / Подблок 5.4 — стабилизация и точечная baseline-доводка первой дополнительной runtime-темы
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Локальные toast-дубли в `SettingsHostedView` и `PasswordGeneratorHostedView` переведены на общий baseline-contract (`baseline-контракт`) `BaselineToastBorder` + `BaselineToastText`.
- `AppMessageDialogWindow` и `AppMessageContentHostedView` переведены на `BaselineDialogBodyScrollViewer`, чтобы message/dialog scroll layer (`слой прокрутки message/dialog`) опирался на общий dialog baseline-contract (`baseline-контракт диалогов`).
- Контрольная сборка после доводки зеленая: `0` ошибок и `0` предупреждений.
- Отдельный живой GUI-smoke в этой сессии не фиксировался как самостоятельный приемочный этап; закрытие `Этапа 5 / Подблока 5.4` оформлено по явному пользовательскому решению после code/build stabilization pass (`прохода кодовой/сборочной стабилизации`).
- Активные документы маршрута синхронизированы под закрытый `Этап 5 / Подблок 5.4` и следующий подтверждаемый шаг `Этап 5 / Подблок 5.5`.

### Этап 0 / Подблок 0.18N — финальный pre-theme audit
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Проведен финальный предтемизационный audit (`аудит`) после service-линии `0.18C-0.20` и DPI/layout-дочистки `0.18M1-0.18M2`.
- Route/docs/handoff (`маршрут/документы/handoff`) сведены к одной картине; подтверждено, что `0.18M1-0.18M2` считаются закрытыми на рабочих масштабах `100% / 125% / 150%`, а ограничение `175%` остается временно допустимым вне обязательного scope по явному пользовательскому решению.
- Контрольная сборка зеленая; реальных блокеров перед возвратом к отдельному подтверждению `Этапа 5 / Подблока 5.4` не выявлено.

### Этап 0 / Подблок 0.18M2 — responsive hosted/dialog layout stabilization
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Введен отдельный DPI/responsive stabilization pass (`стабилизационный проход`) для hosted/dialog слоя после service-линии `0.18C-0.20`, без возврата к старым ошибочным схемам с глобальным `shell-scroll`.
- Для `Entry` и соседних крупных hosted-view собран более единый baseline-контракт внутренних scrollable regions (`прокручиваемых зон`) через `Themes/Baseline.Neutral.Primitives.xaml` и `Themes/Baseline.Neutral.Controls.xaml`.
- Подблок закрыт на рабочих масштабах `100% / 125% / 150%`; масштаб `175%` оставлен как временно допустимое ограничение вне обязательного scope по явному пользовательскому решению.
- Следующим служебным шагом перед возвратом к дизайну остается `Этап 0 / Подблок 0.18N`.

### Этап 0 / Подблок 0.18M1 — app-level DPI baseline и нормализация геометрии MainWindow
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Для проекта включен более явный DPI-aware baseline (`DPI-осведомленный базовый контракт`) и введен helper для нормализации bounds (`границ окна`) `MainWindow` относительно текущей рабочей области.
- Persisted UI prefs (`сохраненные настройки интерфейса`) расширены контекстом рабочей области и DPI-scale, чтобы старые bounds не применялись наивно после смены масштаба Windows.
- Это стало foundation (`базой`) для следующего responsive/layout pass в `0.18M2`.

### Этап 0 / Подблок 0.18K — полный regression-smoke по hosted lifecycle, trash-family и lock-state model
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Машинный и кодовый аудит service-линии `0.18C-0.20` дополнен подтвержденным пользователем живым ручным GUI-smoke по ключевым маршрутам `lock / unlock / recovery / trash-family / hosted family`.
- На текущем этапе `0.18K` считается закрытым по пользовательскому подтверждению ручного прогона; новые баги, если они всплывут, должны уже заводиться как отдельные возвратные дефекты, а не как незакрытый статус самого smoke-блока.
- Следующим служебным шагом перед возвратом к дизайну остаётся только `Этап 0 / Подблок 0.18N`.

### Этап 0 / Подблок 0.18L — синхронизация статуса и маршрута после service-линии `0.18C-0.20`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `docs/STATUS.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/CHANGELOG.md` и `AGENTS.md` синхронизированы с фактической service-линией стабилизации после `Этапа 5 / Подблока 5.3`.
- Зафиксировано, что service-подблоки `0.18C-0.18L` и `0.20` входят в актуальную стабилизационную линию после `5.3`, а статус `0.18K` позже был закрыт отдельным подтвержденным ручным GUI-smoke.
- Следующий допустимый маршрут после этой service-линии зафиксирован как `0.18N`, а затем отдельное подтверждение `5.4`.

### Этап 0 / Подблок 0.20 — baseline-fix remaining hosted flicker routes в `trash-family`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `trash-family` переведён на единый chained hosted flow (`сцепленный hosted-маршрут`) без старых разрывов `message -> password -> result` через teardown (`схлопывание`) между шагами.
- Тяжёлые UI refresh (`обновления интерфейса`) для restore/delete сценариев корзины отложены до безопасного момента после финального hosted-chain, чтобы не провоцировать мерцание фона под полупрозрачным overlay (`оверлеем`).
- Restore/delete маршруты корзины переведены на staged vault update (`подготовленное изменение хранилища`) с commit (`коммитом`) только после успешного save, без грязной live-мутации `_vault` до сохранения.

### Этап 0 / Подблок 0.18J — финальная зачистка remaining lock-state consumers
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Оставшиеся consumers (`потребители`) старой семантики `IsLocked / IsUnlocked` в `MainWindow` переведены на согласованную lifecycle-state model (`модель состояний жизненного цикла`) и session-aware guards (`сессионные guard-условия`).
- Service-routes `backup / import / export / vault switch`, dangerous actions (`опасные действия`), trash-actions, search/focus helpers и attachment-routes приведены к одной модели `working unlocked state` (`рабочего разблокированного состояния`) и `IsSessionUnlocked`.
- Добавлен единый helper (`вспомогательный метод`) для безопасного возврата рабочего unlocked UI после временного service-lock (`сервисной блокировки`) без разрозненных локальных кусков `IsLocked = false / RefreshGrid / Update...`.

### Этап 0 / Подблоки 0.18C-0.18I2 — hosted recovery lifecycle, modal contract и state-model stabilization
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Из проекта удалён passive legacy window-layer (`пассивный legacy-слой окон`) `Entry / Settings / Comment / PasswordGenerator`, а shared draft contracts (`общие draft-контракты`) вынесены из старых окон в нейтральные файлы.
- Hosted dialog system (`система hosted-диалогов`) переведена на более единый shell-driven contract (`контракт общей hosted-оболочки`), chained replace-routes (`сцепленные маршруты замены`) и атомарные state-transitions (`переходы состояния`) без старых классов close/reopen мерцания.
- `unlock / restore / change password / folder / message` family (`семейство сценариев`) и lifecycle-state model (`модель состояний жизненного цикла`) для `lock / unlock / recovery` переведены на общий baseline-подход без прежней перегрузки `IsLocked` как единственного источника истины.

### Этап 5 / Подблок 5.3 — первая дополнительная runtime-тема `Sage Light`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- В `AppThemeCatalog` добавлена первая дополнительная runtime-тема `light` / `Sage Light`, а список тем в настройках теперь подтверждает реальную работу theme-system (`системы тем`) минимум на двух вариантах.
- Добавлен `Themes/Theme.Light.xaml` как отдельный palette dictionary (`словарь палитры`) поверх уже собранного baseline-scaffold (`baseline-каркаса`) без локальных window-level overrides (`оконных локальных переопределений`).
- `standard` сохранена как тема по умолчанию, а `light` подключается через уже существующий `ThemeRuntimeManager` и применяется тем же безопасным маршрутом после перезапуска.
- Активные документы маршрута синхронизированы под закрытый `Этап 5 / Подблок 5.3` и следующий подтверждаемый шаг `Этап 5 / Подблок 5.4`.

### Этап 5 / Подблок 5.2 — структурная дочистка theme-ready базы и фиксация полного theme coverage
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `Themes/Baseline.Neutral.Primitives.xaml` выделен как отдельный structural dictionary (`структурный словарь`), а `Themes/Baseline.Neutral.Brushes.xaml` очищен до brush/color roles (`ролей кистей и цветов`).
- `App.xaml` переведен на схему `primitives + icons + controls`, а текущая runtime-тема `standard` продолжает подгружаться через `ThemeRuntimeManager` и `Themes/Theme.Standard.xaml`.
- `HelpWindow` переведен на общую baseline-shell (`baseline-оболочку`) для app-owned части окна: toolbar (`панели инструментов`), TOC (`оглавления`), browser host (`контейнера браузера`) и overlay ошибки.
- Добавлен `docs/THEME_COVERAGE_CHECKLIST_STAGE5_BLOCK5_2.md` как отдельный checklist (`чеклист`) обязательного theme coverage и smoke-check (`короткой проверки`) перед первой дополнительной темой.
- Активные документы маршрута синхронизированы под закрытый `Этап 5 / Подблок 5.2` и следующий подтверждаемый шаг `Этап 5 / Подблок 5.3`.

### Этап 5 / Подблок 5.1 — запуск новой основной дизайн-ветки и runtime theme scaffold
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Запущена новая отдельная основная дизайн-ветка `Этап 5 / Подблоки 5.X`.
- Текущий внешний вид приложения официально оформлен как runtime-тема `standard` через `ThemeRuntimeManager` и `Themes/Theme.Standard.xaml`.
- `App.xaml` переведен на схему `baseline brushes/controls/icons + active runtime theme`, без визуального изменения интерфейса.
- `Themes/Theme.Default.xaml` и `Themes/Theme.PipBoy.xaml` выведены из живого маршрута в `Themes/_Archive/` как исторические theme-assets (`theme-артефакты`).
- `AGENTS.md`, `docs/STATUS.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md` синхронизированы под старт линии `5.X`.
### Этап 90 / Подблок 90.31 — закрытие линии хвостов `0.X` и синхронизация документов
**Архив:** `PassNotes_Block230_Service_CloseTailLine0X_DocsSync_260.zip`

- В `AGENTS.md`, `docs/STATUS.md`, `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/NAKED_BASELINE_ICON_TOKEN_LAYER_CONTRACT.md` и `docs/CHANGELOG.md` зафиксировано, что временная линия хвостов `Этап 0 / Подблок 0.X` закрыта.
- Исторически закрытыми хвостами текущего периода считаются `Этапы 0.1-0.6`, включая помощь/справку, `Поддержать автора`, каркас выбора темы и системные tray notifications (`уведомления трея`).
- Следующим допустимым маршрутом зафиксировано только отдельное создание и подтверждение новой основной дизайн-ветки, куда будет входить только дизайн.
- Линия `0.X` больше не должна предлагаться как активный маршрут без отдельного явного запроса.
### Этап 0 / Подблок 0.1 — снятие `2.1` с маршрута и перевод проекта на линию хвостов `0.X`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `AGENTS.md`, `docs/STATUS.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md`, `docs/NAKED_BASELINE_ICON_TOKEN_LAYER_CONTRACT.md` и `docs/CHANGELOG.md` синхронизированы под новый маршрут.
- Удален документ `docs/THEME_SPECIFICATION.md`, потому что `Этап 2 / Подблок 2.1` и старая немедленная theme/spec ветка сняты с активного плана.
- Зафиксировано, что текущая работа идет по временной линии хвостов `Этап 0 / Подблок 0.X`, а не по старому следующему шагу `2.1`.
- Следующий хвостовой подблок открывается только по отдельному подтверждению как `Этап 0 / Подблок 0.2`.

### Этап 90 / Подблок 90.27 — синхронизация статуса после закрытия `1.38`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `docs/STATUS.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/CHANGELOG.md` и `AGENTS.md` синхронизированы после закрытия `Этапа 1 / Подблока 1.38`.
- Зафиксировано, что ветка `Этапов 1.25-1.38` полностью завершена.
- Следующим основным плановым шагом по отдельному подтверждению остается `Этап 2 / Подблок 2.1`.

### Этап 1 / Подблок 1.38 — baseline-unification (`baseline-унификация`) toast-family
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Toast-кисти переведены на app-owned (`управляемые приложением`) значения без зависимости от `SystemColors.Info*` как от основного источника визуала.
- Введен `BaselineToastText`, а XAML toast-узлы `MainWindow` и `EntryWindow` вместе с runtime info toast (`runtime-информационными toast-уведомлениями`) сведены к общей связке `BaselineToastBorder` + `BaselineToastText`.
- `PopupToastController` сохранен как behavioral layer (`поведенческий слой`); после пользовательского подтверждения блок `1.38` считается закрытым.

### Этап 90 / Подблок 90.26 — синхронизация статуса после закрытия `1.29`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `docs/STATUS.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/CHANGELOG.md` и `AGENTS.md` синхронизированы после закрытия `Этапа 1 / Подблока 1.29`.
- Зафиксировано, что ветка `Этапов 1.25-1.37` полностью завершена.
- Следующим основным плановым шагом по отдельному подтверждению становится `Этап 2 / Подблок 2.1`.

### Этап 1 / Подблок 1.29 — политика колонки `Обновлено` и дочистка заголовков `EntriesGrid`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `UpdatedUtcColumn` переведена на скрытое состояние по умолчанию при старте приложения; колонка включается вручную через ПКМ (`правую кнопку мыши`) из уже существующих меню.
- Заголовки `EntriesGrid` переведены с хрупкой индексной логики на явное обновление по именованным колонкам, а состояние `Обновлено` сведено в единый helper (`helper`, `вспомогательный метод`).
- После пользовательского подтверждения `Этап 1 / Подблок 1.29` считается закрытым; ветка `Этапов 1.25-1.37` завершена полностью.


### Этап 90 / Подблок 90.25 — синхронизация статусных и handoff-документов после закрытия `1.35-1.37`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `docs/STATUS.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md`, `docs/CHANGELOG.md` и `AGENTS.md` синхронизированы с фактическим закрытием `1.35-1.37` и служебного `90.24`.
- На тот момент было зафиксировано, что после закрытия `1.37` автоматически активного основного блока по-прежнему нет: `1.29` остаётся перенесённым по отдельной договорённости.
- Служебный контекст обновлён до следующего архивного суффикса `_258`.

### Этап 1 / Подблок 1.37 — baseline-интеграция checkbox-family
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Для `BaselineCheckBox` добавлен полноценный app-owned `ControlTemplate` (`шаблон контрола`), чтобы квадратик чекбокса, галочка и состояния больше не зависели от system/default WPF template (`системного/дефолтного WPF-шаблона`).
- Folder checkbox (`чекбокс папки`) в `MainWindow` возвращён на общий baseline-contract (`baseline-контракт`) через `BasedOn`, а локально оставлена только tree-specific visibility (`tree-specific-видимость`).
- Hover (`наведение`) квадратика усилен до читаемой тонкой тёмной рамки; после пользовательского подтверждения блок `1.37` считается закрытым.

### Этап 1 / Подблок 1.36 — unified input family для `TextBox` / `PasswordBox`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `BaselineTextBox` и `BaselinePasswordBox` переведены на app-owned templates (`app-owned-шаблоны`), а системная синяя focus-рамка (`рамка фокуса`) убрана как корневая причина.
- Search host (`контейнер поиска`) переведён на общий `BaselineInputHostBorder`, чтобы поисковые поля и обычные поля ввода жили на одной baseline input-family (`семье полей ввода`).
- После пользовательского подтверждения блок `1.36` считается закрытым.

### Этап 1 / Подблок 1.35 — системная дочистка dialog shell
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `BaselineDialogSurfaceBorder` переведён из тяжёлого framed box (`рамочного блока`) в нейтральный layout-host (`layout-контейнер`), что устранило эффект «коробки внутри коробки» во всех dialog windows (`диалоговых окнах`) defect family (`дефектного семейства`).
- Исправление выполнено на shared baseline-слое без локального обхода окон по одному.
- После пользовательского подтверждения блок `1.35` считается закрытым.

### Этап 90 / Подблок 90.24 — передача фокуса от дерева папок к `EntriesGrid`
**Архив:** `PassNotes_Block230_Service_EntriesGridFocusHandoff_257.zip`

- Исправлен mouse-path (`путь мышью`) обычного клика по записи: `EntriesGrid` теперь забирает keyboard focus (`клавиатурный фокус`) у `FolderSearchBox` / `FolderTree`, как это уже происходило при переходе стрелками клавиатуры.
- Это убрало необходимость дополнительного клика по пустому месту таблицы, чтобы стрелки начинали работать по `EntriesGrid`.
- Служебный блок `90.24` считается закрытым и зафиксирован отдельным checkpoint (`checkpoint`, `чекпоинтом`) и архивом `_257`.


### Этап 1 / Подблок 1.34 — baseline-интеграция `ComboBox / popup item family`
**Архив:** `PassNotes_Block230_Stage1_Block1.34_PopupItemFamily_BaselineSync_256.zip`

- Для `ComboBoxItem`, `MenuItem` и popup-item buttons (`кнопок popup-элементов`) введена отдельная popup item family (`семья popup-элементов`) на shared baseline-слое.
- Цвета `hover` (`наведения`) и `current/selected` (`текущего/выбранного состояния`) у popup item family выровнены под уже принятую маркировку дерева папок и таблицы записей.
- `BaselineDialogComboBoxItem`, `BaselineContextMenuItem` и `BaselinePopupMenuItemButton` переведены на единый popup-contract (`popup-контракт`) состояний без возврата к локальным разрозненным схемам.
- После пользовательского подтверждения `Этап 1 / Подблок 1.34` считается закрытым; `Этап 1 / Подблок 1.29` остается перенесенным по отдельной договоренности и не активируется автоматически.

### Этап 1 / Подблок 1.33 — отдельная grid-family выделения для `EntriesGrid`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Для `EntriesGrid` выделена отдельная grid selection family (`семья выделения таблицы`) с отдельными grid-токенами `hover` (`наведения`), active selection (`активного выделения`) и inactive selection (`неактивного выделения`).
- `BaselineDataGridCell` переведен на grid-specific states (`grid-специфичные состояния`) без смешения с tree/list family (`семьей дерева и списков`).
- Цвета `hover` (`наведения`) и `selection` (`выделения`) таблицы выровнены под уже принятую маркировку дерева папок, при этом `grid lines` (`линии сетки`) и границы ячеек сохранены как основной механизм визуального разделения строк.

### Этап 1 / Подблок 1.32 — tree/list-family выделения для дерева папок и обычных списков
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- В baseline-слое выделена отдельная tree/list selection family (`семья выделения дерева и списков`) для дерева папок, списка вложений и других обычных списков.
- `BaselineTreeViewItem`, `BaselineListBoxItem` и `BaselineListViewItem` переведены на новые tree/list-токены `hover` (`наведения`), active selection (`активного выделения`) и inactive selection (`неактивного выделения`).
- Это устранило прежнее смешение слабого list/tree selection (`выделения списка/дерева`) с таблицей и подготовило отдельную grid-family для `EntriesGrid`.

### Этап 1 / Подблок 1.31 — baseline-template для list item family вложений
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Для `BaselineListBoxItem` добавлен полноценный shared `ControlTemplate` (`шаблон контрола`), чтобы список вложений больше не зависел от system/default WPF template (`системного/дефолтного WPF-шаблона`).
- Голубой system-hover (`системный hover`, `системное наведение`) у вложений устранен через baseline-template integration (`интеграцию baseline-шаблона`), а не локальной маскировкой в `EntryWindow`.
- `BaselineListViewItem` унаследовал ту же baseline-логику через `BasedOn`, без введения отдельного локального special-case (`специального случая`).
### Этап 1 / Подблок 1.28 — финальная baseline-интеграция стрелок дерева папок
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Внешняя ручка панели папок `FolderHandle` и внутренний `expander` дерева переведены на единый shared baseline-contract (`общий shared baseline-контракт`) compact chevron controls (`компактных chevron-контролов`).
- В `Themes/Baseline.Neutral.Controls.xaml` добавлен shared baseline primitive (`общий baseline-примитив`) для tree chevrons (`tree chevrons`, `шевронов дерева`), чтобы убрать локальное дублирование `hover` (`наведения`) / `pressed` (`нажатия`) состояний.
- `MainWindow` переведен на использование этого общего контракта для внешней ручки и внутренней стрелки дерева, а `UpdateFolderHandleArrow()` синхронизирован с новым `template-part` (`template-part`, `частью шаблона`).
- После пользовательской визуальной проверки `Этап 1 / Подблок 1.28` считается закрытым; следующим активным основным шагом становится `Этап 1 / Подблок 1.29`.

### Этап 1 / Подблок 1.30 — baseline-интеграция вкладок окна настроек
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `BaselineTabControl` и `BaselineTabItem` переведены с частичного style-only (`только style`) контракта на полноценные shared templates (`общие шаблоны`), чтобы убрать зависимость от system/default WPF template (`системного/дефолтного WPF-шаблона`).
- Голубой system-hover (`системный hover`, `системное наведение`) у вкладок окна настроек устранён через baseline-template integration (`интеграцию baseline-шаблона`), а не локальной маскировкой.
- После пользовательской проверки `Этап 1 / Подблок 1.30` считается закрытым; `Этап 1 / Подблок 1.29` остаётся перенесённым по отдельной договорённости и не активируется автоматически.

### Этап 90 / Подблок 90.23 — синхронизация правила общей baseline-интеграции и опорных документов
**Архив:** `PassNotes_Block230_Service_BaselineIntegrationRulesSync_255.zip`

- В `AGENTS.md` добавлено обязательное правило общей baseline-интеграции: любое новое изменение по умолчанию должно сначала проверяться на встраивание в единую baseline-систему проекта.
- В `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md`, `docs/NAKED_BASELINE_MASTER_PLAN.md`, `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md` и `docs/STATUS.md` это правило зафиксировано как активная процессная норма новой линии, а не как локальная рекомендация.
- Статусные документы синхронизированы до фактического маршрута: закрыты `1.25-1.27`, активным возвратным проходом считается `1.28`, следующим шагом после его закрытия остается `1.29`, затем `2.1`.
- Обновлен служебный контекст архивной линии, чтобы следующий архивный суффикс после этого checkpoint (`контрольной точки`) был уже `_256`.

### Этап 90 / Подблоки 90.19-90.20 — стабилизация EntriesGrid против возврата дефекта первых строк
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- В Themes/Baseline.Neutral.Controls.xaml для DataGridCell убрана скрытая зависимость от DataGridRow.Background/Foreground, чтобы видимое selection/hover state не возвращалось к ложной смешанной схеме.
- Из `MainWindow` удален always-on `entries-hit-test.log` trace-path, который использовался как временная диагностика верхней зоны таблицы.
- Комментарии и baseline-документ синхронизированы с текущим stable contract: строка несет semantic state, а видимое состояние таблицы отрисовывает ячейка.

## Baseline-reset (новая линия дизайна)

### `Этап 90 / Подблок 90.9` — внеплановая служебная задача: переход на новую master-линию naked baseline UI
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Прошлый основной цикл baseline-reset (`Этапы 1.x`-`4.1`) официально переведён в статус завершённого исторического подготовительного цикла.
- Создан новый главный план `docs/NAKED_BASELINE_MASTER_PLAN.md` как основной источник истины для следующей беседы и новой линии работ.
- Создан отдельный handoff-документ `docs/NEW_CHAT_HANDOFF_NAKED_BASELINE.md` для безопасного старта новой беседы без возврата к старой активной нумерации.
- `docs/STATUS.md`, `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md` и `AGENTS.md` синхронизированы под новую main-line схему: новая основная нумерация начинается заново с `Этап 1 / Подблок 1.1`, а `Этап 90 / Подблок 90.X` остается служебной линией.

### `Этап 1 / Подблок 1.1` — shared visual primitives и правила naked baseline
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- В `Themes/Baseline.Neutral.Brushes.xaml` расширен shared neutral layer: добавлены app-wide metrics и структурированы общие surface/state/token families для naked baseline.
- В `Themes/Baseline.Neutral.Controls.xaml` оформлен shared control-layer: помимо implicit input-baseline зафиксированы opt-in baseline styles и контракты для `Button`, `ToggleButton`, `TabControl`, `TabItem`, `ListBoxItem`, `ListViewItem`, `DataGridRow`, `DataGridCell`, `DataGridColumnHeader`.
- Для `DataGrid` закреплено owner-rule: строка владеет selection/hover state, а ячейка только зеркалит row-state и владеет собственным cell chrome.
- Для `TreeViewItem` зафиксировано, что shared baseline уже владеет state-family и токенами, а локальным до `Этапа 1 / Подблока 1.2` остается только template/layout wiring.
- Добавлен документ `docs/NAKED_BASELINE_STAGE1_BLOCK1_1_SHARED_PRIMITIVES.md`, который фиксирует owner-matrix, норму, временно допустимые зоны и запреты для следующих подблоков.
- `docs/STATUS.md` синхронизирован: следующий основной шаг новой линии теперь `Этап 1 / Подблок 1.2`.

### `Этап 1 / Подблок 1.2` — hard reset `MainWindow`
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `MainWindow.xaml` очищен от большей части локальной общей visual-state/color-логики в зонах toolbar, menu popup, search, context chip, toast, folder pane и entries grid.
- Верхняя chrome-зона `MainWindow` переведена на shared baseline styles `BaselineToolbar*`, `BaselinePopupSurfaceBorder`, `BaselinePopupMenuItemButton`, `BaselineSearchTextBox`, `BaselineHostedSearchTextBox`, `BaselineSearchClearButton`, `BaselineInfoChipBorder`, `BaselineToastBorder` и `BaselineInsetStripBorder`.
- `BaselineTreeViewItem` расширен до owner-style для `hover`, `focus`, `selected`, `inactive selected` и `drop target`; локальный `FolderTreeItemStyle` теперь держит только template/layout wiring, active-context marker и suppression selection-visual в folder multi-select mode.
- `DataGrid` в `MainWindow` оставлен на shared contracts `BaselineDataGridRow`, `BaselineDataGridCell` и `BaselineDataGridColumnHeader`, а локальные ресурсы сведены к header context menu и column-specific content templates.
- Добавлен документ `docs/NAKED_BASELINE_STAGE1_BLOCK1_2_MAINWINDOW_HARD_RESET.md`, а `dotnet build -p:UseAppHost=false -p:OutDir=bin\Debug\net8.0-windows\codexverify\` снова проходит чисто (`0` warnings / `0` errors).
- `docs/STATUS.md` синхронизирован: следующий основной шаг новой линии теперь `Этап 1 / Подблок 1.3`.
### `_231` — Этап 1 / Подблок 1.1: инвентаризация baseline-reset и нейтральная ресурсная схема
**Архив:** `PassNotes_Block230_Stage1_Block1.1_BaselineReset_NeutralResources_231.zip`

- Добавлены `Themes/Baseline.Neutral.Brushes.xaml` и `Themes/Baseline.Neutral.Controls.xaml` как active baseline-схема.
- `App.xaml` переведён на прямое подключение baseline-словарей.
- `Theme.Default.xaml` оставлен как compatibility-wrapper, `Theme.PipBoy.xaml` сохранён как parked theme asset.
- Вынесены общие neutral-resources для dialog/toast/overlay и убраны дубли `DialogButton`/`PassNotesSeparator` в безопасных точках.
- Добавлен документ `docs/BASELINE_RESET_STAGE1_BLOCK1_1_INVENTORY.md`.

### `_232` — Этап 1 / Подблок 1.2: перевод remaining legacy windows на baseline-layer
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `LoginWindow`, `MasterPasswordPromptWindow`, `CommentWindow` и `FolderDialog` переведены на нейтральный dialog baseline (`DialogWindowBackground` + `DialogSurfaceBackground/Border`).
- Кнопки этих окон переведены на shared-style `DialogButton` без изменения обработчиков и пользовательских сценариев.
- Legacy-окна больше не выпадают из baseline-reset по базовым поверхностям, кнопкам и читаемости текста.
- Добавлен документ `docs/BASELINE_RESET_STAGE1_BLOCK1_2_LEGACY_WINDOWS.md`.

### `_233` — Этап 2 / Подблок 2.1: каркас и обязательное содержание спецификации тем
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Добавлен документ `docs/THEME_SPECIFICATION.md` как отдельный источник истины для будущей theme-реализации.
- В документе явно разведены baseline-layer и future theme-layer.
- Зафиксированы обязательные разделы theme-spec: текст, поверхности, границы, состояния, иконки, окна, контролы, контраст и единообразие.
- Зафиксировано, что в Подблоке 2.1 не утверждаются конкретные палитры, декоративные решения и XAML-реализация тем.

### `_234` — Этап 2 / Подблок 2.2: состав theme-линейки и единая token-модель
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- В `docs/THEME_SPECIFICATION.md` зафиксирован минимальный состав новой линии: `Baseline Neutral` как reference baseline, `PipBoy` как ближайшая целевая тема, `Theme.Default` как compatibility wrapper, а не тема.
- Введена единая semantic token-модель, общая для всех будущих тем.
- Зафиксированы обязательные token-группы и матрица покрытия окон, UI-областей, контролов и состояний.
- Зафиксировано, что новые темы не добавляются в линию без отдельного согласованного подблока.

### `Этап 90 / Подблок 90.1` — внеплановая служебная задача: перенос прошлых архивов в `archives/`
**Архив:** не создавался.

- Ретро-уточнение: эта задача ранее была названа как `Этап 2 / Подблок 2.1`, но официально перенесена в служебную внеплановую линию.
- Создана папка `archives/` в корне проекта.
- Прошлые zip-архивы перенесены из корня проекта в `archives/` без переименования и без изменения их содержимого.
- Основной план baseline-reset не перенумеровывался и не сдвигался.

### `Этап 90 / Подблок 90.2` — внеплановая служебная задача: синхронизация правил и активного маршрута
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `AGENTS.md` синхронизирован с фактическими рабочими договорённостями: архивы не создаются автоматически после каждого подблока, а checkpoint commit не создаётся на каждую задачу.
- `docs/STATUS.md` переведён на новый активный маршрут: следующий основной блок — единый baseline-скелет интерфейса, а не продолжение старой активной линии `Этап 2 / Подблок 2.3`.
- `docs/DESIGN_RESET_AND_THEME_REBUILD_STRATEGY.md` синхронизирован с активной baseline-базой `_230` и новым порядком: baseline-скелет → спецификация `Светлой темы` → реализация `Светлой темы`.
- Зафиксировано, что `PipBoy` остаётся parked/reference-line, но не текущим активным направлением реализации.

### `Этап 90 / Подблок 90.3` — внеплановая служебная задача: выравнивание страницы hotkeys в HelpWindow
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Генерация страницы `Горячие клавиши` переведена с markdown-таблиц на управляемую HTML-разметку для секций hotkeys.
- Для всех секций введена единая сетка колонок `Действие / Клавиши / Примечание`, чтобы колонка `Клавиши` и колонка примечаний не «плавали» между блоками.
- В `HelpWindow` добавлены локальные CSS-правила только для hotkeys-таблиц без изменения общей логики других страниц справки.

### `Этап 90 / Подблок 90.4` — внеплановая служебная задача: убрать отдельную колонку примечаний со страницы hotkeys
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Страница `Горячие клавиши` переведена с трех колонок на две: `Действие` и `Клавиши`.
- Отдельная колонка `Примечание` убрана как визуально пустая и перегружающая layout.
- Редкие пояснения вроде `блокируется при вводе` перенесены внутрь строки действия как локальная вторичная подпись.

### `Этап 90 / Подблок 90.5` — внеплановая служебная задача: выровнять FolderDialog после baseline-reset
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `FolderDialog.xaml` переведён с жесткой внутренней сетки на ту же более устойчивую схему label + input, которая уже используется в других baseline-диалогах.
- Убрана фиксированная высота поля ввода, из-за которой визуально съезжала внутренняя компоновка окна.
- Окно переведено на `SizeToContent=Height` с сохранением ширины, чтобы содержимое больше не выглядело зажатым и смещённым.

### `Этап 90 / Подблок 90.6` — внеплановая служебная задача: устранить обрезку текста в NameBox у FolderDialog
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- В `FolderDialog.xaml` для `NameBox` убран конфликтный `VerticalContentAlignment="Center"`, который давал визуальную обрезку текста на части DPI/тем.
- Для поля добавлены `MinHeight` и внутренний `Padding`, чтобы однострочный ввод отображался стабильно и без срезанного текста.

### `Этап 90 / Подблок 90.7` — внеплановая служебная задача: устранить обрезку правого края NameBox в FolderDialog
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `FolderDialog` переведён с жесткой внешней ширины на `SizeToContent="WidthAndHeight"` с `MinWidth="380"`, чтобы окно подстраивалось под реальную требуемую ширину содержимого.
- Это устраняет конфликт между клиентской шириной окна, внутренними отступами и `MinWidth` у `NameBox`, из-за которого визуально обрезался правый край поля.

### `Этап 90 / Подблок 90.8` — внеплановая служебная задача: выровнять текст и устранить обрезку букв в shared-style DialogButton
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- `DialogButton` в `Themes/Baseline.Neutral.Controls.xaml` переведён с тесной фиксированной высоты на устойчивую baseline-схему с `MinHeight`, безопасным padding и явным центрированием содержимого.
- Для shared-style включены `UseLayoutRounding` и `SnapsToDevicePixels`, чтобы кнопки в диалогах и настройках вели себя как единый механизм, а не как набор локальных исключений.
- Исправление сделано на уровне общего стиля, а не одной конкретной кнопки, поэтому распространяется на все окна, где используется `DialogButton`.

### `Этап 3 / Подблок 3.1` — полный аудит проекта перед cleanup
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Добавлен документ `docs/PROJECT_AUDIT_STAGE3_BLOCK3_1.md` как отдельный источник истины для cleanup-линии `Этапа 3`.
- Зафиксированы подтвержденные safe cleanup-кандидаты: пустая папка `analysis_frames/`, дублирующий `.gitignore.txt`, неигнорируемая папка `archives/` и устаревший `README.md`.
- Зафиксированы зоны, которые нельзя смешивать с безопасным cleanup: монолиты `MainWindow.xaml.cs` / `EntryWindow.xaml.cs`, массив пустых `catch { }`, инвентаризация `MessageBox` и остаточные local-style exceptions в `MainWindow.xaml`.
- Подтверждено актуальное build/code health состояние: проект собирается чисто (`0` warnings / `0` errors), поэтому следующий шаг должен быть точечным cleanup-проходом, а не аварийным ремонтом сборки.
### `Этап 3 / Подблок 3.2` — безопасный cleanup подтвержденного мусора и рабочей среды
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Удалены подтвержденные локальные артефакты: пустая папка `analysis_frames/` и дублирующий файл `.gitignore.txt`.
- `.gitignore` дополнен правилами для `archives/` и `analysis_frames/`, чтобы milestone-архивы и локальные анализаторные хвосты больше не засоряли `git status`.
- Код приложения, XAML, theme-ресурсы и runtime-поведение не менялись; cleanup ограничен рабочей средой и синхронизацией docs.
- Следующий шаг после этого safe cleanup — `Этап 3 / Подблок 3.3` с точечным code health cleanup по зонам риска из аудита.
### `Этап 3 / Подблок 3.3` — точечный code health cleanup MainWindow selection/UI-safe механики
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- Перед началом правок создан git checkpoint `8ffe0a5` (`checkpoint: before mainwindow selection cleanup`).
- В `MainWindow.xaml.cs` добавлены локальные helper-методы для единых операций selection и active-context UI, чтобы убрать дубли и сократить число бессмысленных `catch { }` в центральной UI-зоне.
- Повторяющиеся блоки очистки selection, установки single selection и обновления active context переведены на единый механизм без изменения UX-сценариев.
- Точечные nullable-risk места в selection-кластере дожаты до чистой сборки: `dotnet build` снова проходит с `0` warnings / `0` errors.
### Этап 4 / Подблок 4.1 — единый baseline-скелет interactive states и базовых control metrics
**Архив:** не создавался по текущему правилу: архивы только по явному запросу или на отдельном стабильном milestone.

- В MainWindow.xaml таблица записей доведена до более явной row-owned state-логики: hover, selected и inactive selected теперь опираются на одну neutral family без локальной hardcoded hover-кисти.
- Дерево папок переведено на ту же neutral state-family: добавлены hover/focus состояния, согласованные с selection-state и не конфликтующие с drop-target логикой.
- В Themes/Baseline.Neutral.Brushes.xaml добавлены общие baseline-кисти для focus/list-item hover и disabled input background.
- В Themes/Baseline.Neutral.Controls.xaml добавлены shared baseline styles для TextBox, PasswordBox, ComboBox и CheckBox, чтобы базовые input-controls работали как единый нейтральный механизм до начала Светлой темы.
- Добавлен документ docs/BASELINE_SKELETON_STAGE4_BLOCK4_1.md, который фиксирует, что именно считается baseline-скелетом перед следующей дизайн-линией.

## Рабочая линия (актуально)

### `..._179` — Финал (оптимальный): UI prefs (минимум)
**Архив:** `PassNotes_Block159_OptFinal_UiPrefs_Min_WindowRowHeight_Version_179.zip`

- Добавлено: запоминание **размера/позиции окна** + Normal/Maximized.
- Добавлено: запоминание **высоты строк** (RowHeight).
- Добавлено: `UiPrefsVersion` (сброс UI prefs при будущих редизайнах).

### `..._178` — Финал (оптимальный): P1.2 (ClipboardSecurity + Copied→popup)
**Архив:** `PassNotes_Block158_OptFinal_P1_Sub1.2_178.zip`

- Дожаты правки «оптимального уровня»:
  - копирование логина через `ClipboardSecurity` там, где нужно;
  - замена оставшихся MessageBox «Copied…» → popup/toast там, где было в зоне правок.

### `..._176` — Финал MVP-3B2 (железобетон)
**Архив:** `PassNotes_Block156_MVP3B2_Final_176.zip`

- Вложения Variant B (pending до Save) — стабильно и безопасно.
- Orphan cleanup (attachments): логи BEGIN/SKIP/END/ERROR + ERROR-trigger, edge-cases, отчёт без путей.
- Backup/Restore: dangling meta не блокирует; self-heal dangling meta Unlock+rate-limit.
- Smoke-инварианты: multi-select/Ctrl+A/Del/drag&drop/трей, инвариант клика в пустоту.


## Ветка «Финал (идеальный)» (актуально поверх оптимального)

### `..._221` — R2.1.4: Inno Setup — полировка pipeline и payload
**Архив:** `PassNotes_Block201_R2.1.4_InnoSetupPolish_221.zip`

- `build-installer.ps1`: компиляция Inno Setup выполняется из папки `installer\`, чтобы относительные пути в `.iss` всегда работали.
- Inno Setup: из устанавливаемого payload исключены `*.pdb`.
- `installer/RunPassNotes.vbs`: сообщение об ошибке сделано bilingual (EN/RU).



### `..._224` — R2.1.4.3: Inno Setup — fix Duplicate identifier DWORD + build script exit-code
**Архив:** `PassNotes_Block204_R2.1.4.3_InnoFix_Dword_BuildExit_224.zip`

- Inno Setup: удалено переопределение типа `DWORD` в секции `[Code]` (в новых версиях он уже встроен), чтобы не падало с `Duplicate identifier "DWORD"`. 
- `build-installer.ps1`: добавлена проверка `$LASTEXITCODE` после `ISCC.exe` — скрипт корректно падает при ошибке компиляции вместо ложного `Done`.

### `..._223` — R2.1.4.2: Inno Setup — fix CustomMessages + fix build-installer.ps1 repoRoot
**Архив:** `PassNotes_Block203_R2.1.4.2_InnoFix_CustomMessages_BuildScript_223.zip`

- Inno Setup: исправлены `CustomMessages` (формат `ru.CloseRunningApp` вместо ошибочного `CloseRunningApp.ru`).
- build-installer.ps1: `repoRoot` теперь строковый путь (`.Path`), устранён сбой `System.Object[] -> System.String` при `Join-Path`.
### `..._220` — R2.1.3: Inno Setup — guard «приложение запущено» + smoke/docs
**Архив:** `PassNotes_Block200_R2.1.3_InnoSetupGuard_Docs_220.zip`

- Inno Setup: installer/uninstaller просит закрыть `PassNotes.exe` (Retry/Cancel), чтобы избежать повреждения файлов.
- Документы: обновлены `docs/RELEASE_CHECKLIST.md` и `docs/RELEASE_NOTES.md` (installer шаги и smoke).

### `..._219` — R2.1.2: publish → installer (build-installer.ps1) + auto-version
**Архив:** `PassNotes_Block199_R2.1.2_BuildInstaller_219.zip`

- Добавлен `build/build-installer.ps1`: читает версию из csproj, делает publish и компилирует Inno Setup через ISCC.
- Inno Setup `.iss`: поддержка define'ов `/DMyAppVersion` и `/DPublishDir`.

### `..._218` — R2.1.1: базовый Inno Setup installer (per-user)
**Архив:** `PassNotes_Block198_R2.1.1_InnoSetupBase_218.zip`

- Добавлен `installer/PassNotesDesktop.iss` (per-user установка без админа, ярлыки, запуск после установки).
- Добавлен отдельный `installer/RunPassNotes.vbs` для установленного приложения.

### `..._217` — R1.5.4: полировка перед инсталлятором (docs + мелкие безопасные улучшения)
**Архив:** `PassNotes_Block197_R1.5.4_Polish_217.zip`

- Логи: `DiagnosticsLog` теперь пишет UTF-8 (устойчивее для RU/EN).
- Документы: устранено дублирование/путаница по `AUDIT.md` (в корне оставлен redirect на `docs/AUDIT.md`).
- Документы: актуализированы `docs/STATUS.md` и `docs/CHANGELOG.md` (R1.4–R1.5).

### `..._216` — R1.5.3.3: унификация toast (Settings + Password Generator)
**Архив:** `PassNotes_Block196_R1.5.3.3_ToastUnify_216.zip`

- `SettingsWindow` и `PasswordGeneratorWindow` переведены на единый `PopupToastController`.
- Убраны локальные Timer/CTS/Task.Delay для toast, закрытие popups стало предсказуемым.

### `..._215` — R1.5.3.2: IO gate логирование через DiagnosticsLog
**Архив:** `PassNotes_Block195_R1.5.3.2_IoGateDiagnostics_215.zip`

- `VaultIoGate`: slow-wait логирование переведено на `DiagnosticsLog.AppendLine("IO_GATE_WAIT", ...)`.

### `..._214` — R1.5.3.1: SettingsStore атомарный Save + fallback Load
**Архив:** `PassNotes_Block194_R1.5.3.1_SettingsAtomic_214.zip`

- `settings.json` сохраняется атомарно (temp → replace/move) + `settings.json.bak`.
- При повреждённом `settings.json` возможен fallback на `.bak` + auto-heal с диагностикой.

### `..._213` — R1.5.2: Safe cleanup (минимально рискованные правки)
**Архив:** `PassNotes_Block193_R1.5.2_SafeCleanup_213.zip`

- Убраны очевидные дубли/копипаста в зоне toast генератора.
- Null-safe правки по `Grid` в сценариях Lock и restore из корзины.
- Минимальная диагностика вместо части silent `catch {}` в критичных IO ветках.

### `..._212` — R1.5.1: инвентаризация техдолга
**Архив:** `PassNotes_Block192_R1.5.1_TechDebt_212.zip`

- Добавлен `docs/TECH_DEBT.md` (P0/P1/P2: где, риск, что сделать, как проверить).

### `..._211` — R1.4.1 hotfix: фикс сборки
**Архив:** `PassNotes_Block191_R1.4.1_FixBuild_211.zip`

- Hotfix: восстановлена совместимость вызовов `ShowCopyToast(...)` (опциональный cursorPoint), фикc CS7036.

### `..._210` — R1.4.1: toast у курсора для копирования через ПКМ
**Архив:** `PassNotes_Block190_R1.4.1_CopyToastCursor_210.zip`

- При копировании логина/пароля через контекстное меню (ПКМ) toast подтверждения появляется рядом с курсором.
- При копировании через иконки (toolbar) toast остаётся привязан к иконке.

### `..._209` — R1.3: релизная документация (checklist + release notes)
**Архив:** `PassNotes_Block189_R1.3_ReleaseDocs_209.zip`

- Добавлено: `docs/RELEASE_CHECKLIST.md` (сборка/publish, состав релиза, smoke/regression).
- Добавлено: `docs/RELEASE_NOTES.md` (Release Notes 0.1.0).
- Обновлено: `docs/CHANGELOG.md` и `docs/STATUS.md` (ссылки на релизную документацию).

### `..._208` — R1.2: версия 0.1.0 + отображение в Help → About
**Архив:** `PassNotes_Block188_R1.2_Version_208.zip`

- Зафиксировано: `Version=0.1.0`, `AssemblyVersion/FileVersion=0.1.0.0`, `InformationalVersion=0.1.0`.
- About (RU/EN): отображается версия `0.1.0` (подстановка токена `{APP_VERSION}`).

### `..._207` — R1.1: публичное имя “PassNotes Desktop” (UI + metadata)
**Архив:** `PassNotes_Block187_R1.1_PublicName_UI_Metadata_207.zip`

- Публичное имя “PassNotes Desktop” в UI и метаданных сборки.
- Важно: **exe/пути/%APPDATA% не переименовывали**, миграций нет.

### `..._206` — I3.4: финальная синхронизация docs после PD2.1 + вычеркивание шрифта
**Архив:** `PassNotes_Block186_I3.4_DocsSync_206.zip`

- Обновлены: `docs/STATUS.md`, `docs/CHANGELOG.md`, `docs/AUDIT.md`.
- Обновлён: `docs/PassNotes_Plan_Final_Ideal.md` (I4/PD1 «шрифт/масштаб» вычеркнут; PD2.1 отмечен как выполненный).

### `..._205` — PD2.1 hotfix: исправление ошибки сборки (XAML header context menu)
**Архив:** `PassNotes_Block185_PD2.1_FixBuild_205.zip`

- Исправлено: ошибка XAML компиляции по `Opened` в контекстном меню заголовка таблицы.

### `..._204` — PD2.1: toggle колонки «Обновлено» (Updated) в таблице записей
**Архив:** `PassNotes_Block184_PD2.1_ToggleUpdatedColumn_204.zip`

- Добавлено: пункт меню (Checkable) «Показывать колонку “Обновлено” / Show “Updated” column».
- Доступно из: контекстного меню таблицы и заголовка.
- Состояние: только в рамках текущей сессии (без сохранения между запусками).

### `..._203` — I3.3: синхронизация документации + правка плана
**Архив:** `PassNotes_Block183_I3.3_DocsUpdate_203.zip`

- Обновлены: `docs/STATUS.md`, `docs/CHANGELOG.md`, `docs/AUDIT.md`.
- Скорректирован: `docs/PassNotes_Plan_Final_Ideal.md` (I3.2: About как страницы внутри Help, без AboutWindow/Settings).

### `..._202` — I3.2: “О программе / About” внутри справки (RU/EN)
**Архив:** `PassNotes_Block182_I3.2_AboutInHelp_202.zip`

- Добавлено: `docs/help/ru/about.md` + `docs/help/en/about.md`.
- Обновлены: `docs/help/*/navigation.md` и `docs/help/*/index.md` (пункт About в TOC/главной).

### `..._201` — I3.1: manual + FAQ (RU/EN)
**Архив:** `PassNotes_Block181_I3.1_ManualFaq_201.zip`

- Добавлено: `docs/help/ru|en/manual.md` и `docs/help/ru|en/faq.md`.
- Обновлены: `docs/help/*/navigation.md` и `docs/help/*/index.md` (пункты Manual/FAQ).

### `..._200` — I2: Help RU/EN — CSS-полиш таблиц
**Архив:** `PassNotes_Block180_I2_HelpCssPolish_200.zip`

- Минимальный CSS-полиш таблиц в HelpWindow (чтобы таблицы не сливались).

## Экспериментальные ветки (неактуально / откатано)

### Автозапуск Windows (отказались)
- Делались попытки (архивы с пометками Autostart), но итоговое решение: **не внедрять** из соображений безопасности/риска.

### Расширенные UI prefs (последняя папка/сортировка/колонки)
- Пробовали расширение prefs, но оно оказалось нестабильным/непрогнозируемым в реальных сценариях.
- Итоговое решение: оставить **минимум** (окно + RowHeight) и вернуться к расширению позже, если будет новая архитектура/время.

---

См. также: `docs/STATUS.md`

- 0.1.0: Inno Setup: fix LoadStringFromFile type mismatch in process guard (R2.1 hotfix).






