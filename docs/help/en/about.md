# About

Version: {APP_VERSION}

## What is PassNotes Desktop <a id="about"></a>

PassNotes Desktop is a local WPF/.NET app for storing passwords and notes in an **encrypted vault**, with support for folders, entries, and attachments.

The goal is an offline-first manager that keeps your data on your computer and provides a straightforward UI.

## Status <a id="status"></a>

The project is under active development (**beta**). Functionality and behavior may evolve; keep recent backups before making important data changes.

## Key concepts <a id="concepts"></a>

- **Vault** — the primary encrypted storage.
- **Folders** — structure to organize entries.
- **Entries** — items with fields (e.g., username/password/notes) and optional attachments.
- **Export/Import** — via **`.pnexp`** (encrypted format).
- **Attachments** — files linked to an entry and stored encrypted.

## Privacy & security <a id="privacy"></a>

- PassNotes Desktop is designed for local data storage (no built-in cloud sync).
- **The master password cannot be recovered**: if it is lost, the vault contents become inaccessible.
- Export/import is only available in the **encrypted** **`.pnexp`** format.

## Quick links <a id="quicklinks"></a>

- [User manual](./manual.md)
- [FAQ](./faq.md)
- [Hotkeys](./hotkeys.md)
- [Table of contents](./navigation.md)
