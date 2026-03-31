# PassNotes Desktop — Release Notes

Дата фиксации: 2026-03-31 (Europe/Moscow)

---

## 0.1.0

### Основное

- **PassNotes Desktop** — публичное имя приложения в UI и метаданных сборки.
- Версия сборки: **0.1.0**.
- Репозиторий приведён к public-ready состоянию для аккуратной публикации на GitHub.

### Что входит в текущую базу

- локальное WPF/.NET приложение для хранения паролей, заметок, комментариев и вложений;
- зашифрованное хранилище с master password;
- безопасные backup / import / export потоки без plaintext JSON;
- встроенная справка RU/EN;
- search / multi-select / drag & drop / tray / hotkeys;
- runtime-темы `Standard`, `Sage`, `Frost`, `Midnight`, `Amber`.

### Состояние распространения

- текущая база готова для чистой source build и portable publish-сборки;
- release/docs синхронизированы с фактической структурой репозитория;
- отдельный installer-route в текущем working tree отсутствует и будет готовиться следующим отдельным маршрутом.

### Ограничения

- приложение ориентировано на локальное хранение данных;
- встроенной cloud sync нет;
- plaintext JSON export не поддерживается;
- dedicated installer пока не входит в текущий репозиторный snapshot.

---

## Для сборки

См. чеклист: `docs/RELEASE_CHECKLIST.md`
