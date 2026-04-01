# PassNotes Desktop — Release Notes

Дата фиксации: 2026-04-01 (Europe/Moscow)

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

- текущая база готова для чистой source build, self-contained portable publish, repeatable installer build и repeatable distribution packaging;
- release/docs синхронизированы с фактической структурой репозитория;
- в текущем working tree теперь присутствуют `build/build-installer.ps1`, `build/build-distribution.ps1` и `installer/PassNotesDesktop.iss`;
- базовый installer smoke (`install -> launch -> uninstall -> reinstall -> launch`) подтвержден на текущей базе;
- Boosty-ready distribution package подтвержден на текущей базе: installer `.exe`, portable `.zip`, `INSTALL_RU.txt`, `SHA256SUMS.txt`;
- publication handoff для ручной выкладки на Boosty подтвержден на текущей базе: `BOOSTY_POST_RU.txt` и `BOOSTY_HANDOFF_RU.txt` собираются автоматически вместе с distribution package;
- открытая публикация `PassNotes Desktop 0.1.0` на Boosty уже выполнена;
- version-to-version upgrade verification и code signing остаются следующими отдельными шагами.

### Ограничения

- приложение ориентировано на локальное хранение данных;
- встроенной cloud sync нет;
- plaintext JSON export не поддерживается;
- installer сейчас собирается без code signing;
- version-to-version upgrade smoke пока не закрыт как отдельный verification-pass.

---

## Для сборки

См. чеклист: `docs/RELEASE_CHECKLIST.md`
