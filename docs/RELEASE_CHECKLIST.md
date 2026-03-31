# PassNotes Desktop — RELEASE CHECKLIST

Дата фиксации: 2026-03-31 (Europe/Moscow)

> Цель: собрать повторяемый release/publish-пакет из текущей базы и быстро проверить, что критичный функционал жив.
>
> Текущая граница этого документа: public-ready репозиторий и portable publish. Installer-route в текущем working tree отсутствует и готовится отдельно.

---

## 1) Build (Release)

### 1.1. Команды из корня репозитория

```powershell
# Clean (опционально)
dotnet clean .\PassNotes.csproj -c Release

# Build
dotnet build .\PassNotes.csproj -c Release

# Publish в локальную staging-папку
dotnet publish .\PassNotes.csproj -c Release -o .\artifacts\publish\win-x64
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
- [x] Installer-route не входит в текущую public-ready базу и готовится отдельно.

---

## 6) Troubleshooting

### 6.1. Help пустой / не грузится

- Проверь, что рядом с exe присутствует папка `help\` и внутри есть `ru\`, `en\`, `_assets\`.

### 6.2. Вложения не удаляются после просмотра

- Убедись, что внешний просмотрщик уже отпустил файл.
- Если Windows еще держит handle, подожди несколько секунд и повтори.
