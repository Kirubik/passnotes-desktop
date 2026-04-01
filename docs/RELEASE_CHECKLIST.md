# PassNotes Desktop — RELEASE CHECKLIST

Дата фиксации: 2026-04-01 (Europe/Moscow)

> Цель: собрать повторяемый release/publish-пакет из текущей базы и быстро проверить, что критичный функционал жив.
>
> Текущая граница этого документа: public-ready репозиторий, self-contained portable publish, repeatable Inno Setup installer route, подтвержденный installer smoke текущей базы, готовый Boosty-ready distribution package, publication handoff и уже выполненная открытая публикация на Boosty. Version-to-version upgrade verification остается отдельным следующим шагом.

---

## 1) Build (Release)

### 1.1. Команды из корня репозитория

```powershell
# Clean (опционально)
dotnet clean .\PassNotes.csproj -c Release

# Build
dotnet build .\PassNotes.csproj -c Release

# Publish в локальную staging-папку
dotnet publish .\PassNotes.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\win-x64
```

Ожидаемый output:

- `artifacts\publish\win-x64\`

### 1.2. Что должно быть в publish-папке

Минимум:

- `PassNotes.exe`
- `.dll` зависимости
- `PassNotes.runtimeconfig.json`
- `PassNotes.deps.json`
- папка `help\` с `ru\`, `en\` и `_assets\`
- `PassNotesApp.ico`
- self-contained runtime files for `win-x64`

Что не должно попадать в publish-пакет:

- `diagnostic.log`, `last_error.txt`
- vault-файлы пользователя
- пользовательские backup-файлы
- временные файлы и локальные артефакты сборки

### 1.3. Что важно понимать про `RunPassNotes.vbs`

- Корневой `RunPassNotes.vbs` в текущей базе — это repo/dev launcher.
- Он подходит для запуска из корня проекта или рядом со стандартной debug-сборкой.
- Он не считается готовым launcher-скриптом для portable release-пакета.

---

## 2) Package (portable publish)

### 2.1. Что кладём в portable package

Берём содержимое `artifacts\publish\win-x64\` как есть.

Структура portable-пакета должна выглядеть примерно так:

```text
PassNotesDesktop_0.1.0/
  PassNotes.exe
  *.dll
  PassNotes.runtimeconfig.json
  PassNotes.deps.json
  PassNotesApp.ico
  help/
    ru/
    en/
    _assets/
```

### 2.2. Что не кладём

- исходники проекта;
- `.git`-служебные файлы;
- `bin/`, `obj/`, `artifacts/` целиком;
- любые логи, temp-файлы и пользовательские данные;
- корневой `RunPassNotes.vbs`, пока для release не подготовлен отдельный launcher-маршрут.

### 2.3. Repeatable installer build

Основная команда:

```powershell
.\build\build-installer.ps1
```

Что делает скрипт:

- читает версию из `PassNotes.csproj`;
- делает self-contained publish в `artifacts\publish\win-x64`;
- компилирует `installer\PassNotesDesktop.iss` через `ISCC.exe`;
- кладет готовый installer в `artifacts\installer\`.

Ожидаемый installer output:

- `artifacts\installer\PassNotesDesktopSetup_<version>.exe`

Текущая installer-модель:

- `Inno Setup`, per-user install (`PrivilegesRequired=lowest`);
- install dir: `%LOCALAPPDATA%\Programs\PassNotes Desktop`;
- пользовательские данные в `%APPDATA%\PassNotes` uninstall-скриптом не удаляются;
- installer сейчас собирается без code signing.

### 2.4. Boosty-ready distribution package

Основная команда:

```powershell
.\build\build-distribution.ps1
```

Что делает скрипт:

- запускает repeatable `build-installer.ps1`;
- пересобирает self-contained publish и installer текущей версии;
- формирует `artifacts\distribution\` как user-facing пакет для выкладки;
- создает portable zip без `.pdb`;
- кладет рядом `INSTALL_RU.txt`, `SHA256SUMS.txt`, `BOOSTY_POST_RU.txt` и `BOOSTY_HANDOFF_RU.txt`.

Ожидаемый output:

- `artifacts\distribution\PassNotesDesktopSetup_<version>.exe`
- `artifacts\distribution\PassNotesDesktop_<version>_portable.zip`
- `artifacts\distribution\INSTALL_RU.txt`
- `artifacts\distribution\SHA256SUMS.txt`
- `artifacts\distribution\BOOSTY_POST_RU.txt`
- `artifacts\distribution\BOOSTY_HANDOFF_RU.txt`

Что не должно попадать в distribution package:

- `install_smoke.log`
- `uninstall_smoke.log`
- `reinstall_smoke.log`
- `.pdb`
- пользовательские данные и локальные vault/backup-файлы

---

## 3) Pre-release smoke (5–10 минут)

### 3.1. Старт / базовые сценарии

- [ ] Запуск `PassNotes.exe` из publish-папки проходит успешно.
- [ ] Разблокировка/вход в vault проходит успешно.
- [ ] Создать папку, создать запись, сохранить.
- [ ] Поиск по записям/папкам работает.

### 3.2. Таблица записей

- [x] Multi-select (Ctrl/Shift), `Ctrl+A`, `Del` — не сломано.
- [x] Drag & drop записи на папку — работает.
- [ ] Контекстное меню таблицы и заголовка корректно скрывает/показывает колонку `Updated / Обновлено`.

### 3.3. Вложения

- [x] Добавить вложение → Save → вложение отображается.
- [x] Открыть вложение.
- [x] Удалить вложение → Save → вложение исчезает.

### 3.4. Корзина

- [ ] Удалить запись → запись попадает в корзину.
- [ ] Restore / Delete forever / Empty работают корректно.

### 3.5. Backup / Export / Import

- [x] BackupNow создаёт backup.
- [x] Export создаёт только безопасный формат `.pnexp`.
- [x] Import принимает валидный `.pnexp` и отклоняет неверный файл с понятным сообщением.

### 3.6. Tray

- [x] Сворачивание/восстановление из трея работает.

### 3.7. Help

- [x] F1 открывает справку.
- [x] Manual / FAQ / About открываются.
- [x] В About отображается версия `0.1.0`.

### 3.8. Installer smoke

Подтверждено на текущей базе `0.1.0` 2026-04-01 через реальный silent smoke (`install -> launch -> uninstall -> reinstall -> launch`):

- [x] Installer `PassNotesDesktopSetup_0.1.0.exe` запускается без ошибок.
- [x] Установка проходит успешно в per-user режим.
- [x] Ярлык Start Menu создаётся.
- [x] Desktop shortcut создаётся при выбранной задаче.
- [x] Запуск установленного `PassNotes.exe` проходит успешно.
- [x] Uninstall проходит успешно.
- [x] `%APPDATA%\PassNotes` не удаляется автоматически при uninstall.
- [x] Reinstall проходит успешно, установленная версия снова запускается.

### 3.9. Distribution packaging

Подтверждено на текущей базе `0.1.0` 2026-04-01:

- [x] `.\build\build-distribution.ps1` отрабатывает успешно.
- [x] В `artifacts\distribution\` лежат только user-facing файлы: installer, portable zip, `INSTALL_RU.txt`, `SHA256SUMS.txt`.
- [x] Smoke-логи остаются в `artifacts\installer\` и не попадают в distribution package.
- [x] Portable zip не содержит `.pdb`.

### 3.10. Publication handoff

Подтверждено на текущей базе `0.1.0` 2026-04-01:

- [x] В `artifacts\distribution\` автоматически создается `BOOSTY_POST_RU.txt` с готовым русским текстом публикации.
- [x] В `artifacts\distribution\` автоматически создается `BOOSTY_HANDOFF_RU.txt` с checklist для ручной выкладки.
- [x] Installer явно зафиксирован как основной рекомендуемый файл публикации.
- [x] Предупреждение про SmartScreen / отсутствие code signing явно включено в publication handoff.

---

## 4) Full regression (20–40 минут, опционально)

- [ ] Пройти полный сценарий: несколько папок/записей, перемещение drag & drop, массовое удаление, восстановление.
- [ ] Протестировать import/export на отдельном тестовом vault.
- [ ] Проверить, что после серии операций нет неожиданных ошибок в `diagnostic.log`.
- [ ] Проверить хоткеи и отсутствие конфликтов с вводом текста.

---

## 5) Known limitations / Out of scope

- [x] Автозапуск с Windows — не делаем.
- [x] Restore из корзины в исходную папку — не делаем.
- [x] Масштаб/размер шрифта для дерева/таблицы — вычеркнуто.
- [x] Code signing пока не входит в текущий route.
- [x] Базовый install/uninstall/reinstall smoke, Boosty-ready distribution packaging, publication handoff и открытая публикация на Boosty подтверждены на текущей базе; upgrade-between-versions по-прежнему вне текущего route.

---

## 6) Troubleshooting

### 6.1. Help пустой / не грузится

- Проверь, что рядом с exe присутствует папка `help\` и внутри есть `ru\`, `en\`, `_assets\`.

### 6.2. Вложения не удаляются после просмотра

- Убедись, что внешний просмотрщик уже отпустил файл.
- Если Windows еще держит handle, подожди несколько секунд и повтори.
