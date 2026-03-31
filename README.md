# PassNotes Desktop

PassNotes Desktop is a local Windows app for storing passwords, notes, comments, and attachments in an encrypted vault. It is built with WPF/.NET and aims to keep everyday desktop workflows practical without sacrificing local-first security.

## Why PassNotes

- Local-first by default: data stays on your machine.
- Encrypted storage: vault data is protected with a master password.
- Safe data flows: backups, import, and export are designed around encrypted formats.
- Practical desktop UX: folders, search, multi-select, drag and drop, tray integration, built-in help, and runtime themes.

## Core capabilities

- encrypted vault for passwords and notes;
- folders and entries with comments and metadata;
- encrypted attachments stored alongside the vault in a vault-specific sidecar folder;
- backup and restore flows that account for attachment integrity;
- secure import and export with `.pnexp`;
- built-in RU/EN help content;
- tray support, hotkeys, search, and multi-select workflows;
- multiple runtime themes for the app-owned WPF UI.

## Security and data model

- By default, application data is stored under `%APPDATA%\PassNotes`.
- Vault encryption uses `AES-GCM` with `PBKDF2-SHA256`.
- Supported export and backup flows do not rely on plaintext JSON.
- Attachments are handled in an encrypted form and are saved atomically with entry changes.
- PassNotes Desktop is a local desktop app. It does not provide built-in cloud sync.

## Who this project is for

- Windows users who want a local-first password and notes manager.
- People who prefer explicit control over where their data lives.
- Reviewers, employers, or contributors who want to inspect a real WPF/.NET desktop project with a security-focused scope.

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK
- Optional: Visual Studio 2022 for a full IDE workflow

## Build from source

```powershell
dotnet restore .\PassNotes.csproj
dotnet build .\PassNotes.csproj -c Debug
```

## Run from source

```powershell
dotnet run --project .\PassNotes.csproj
```

Windows convenience launcher:

- `RunPassNotes.vbs` can be started from the repository root.
- It is a local development launcher for this repo snapshot.
- It is not a packaged release launcher.

## Create a release publish folder

```powershell
dotnet publish .\PassNotes.csproj -c Release -o .\artifacts\publish\win-x64
```

Notes:

- `artifacts/` is a local output folder and is ignored by Git.
- The current public-ready repository snapshot does not include a dedicated installer pipeline yet.
- Installer/distribution work is planned as a separate next route after repository publication prep.

## Repository layout

- `PassNotes.csproj` — main WPF project file
- `Themes/` — runtime theme dictionaries and shared baseline resources
- `Resources/` — strings, icons, support assets, app icon assets
- `docs/help/` — user-facing RU/EN help content bundled with the app
- `docs/RELEASE_CHECKLIST.md` — maintainer release/publish checklist
- `docs/RELEASE_NOTES.md` — current release summary

Some files in `docs/` are maintainer-facing planning and status documents. They are kept intentionally for project tracking, but they are not required to build or use the app.

## Current limitations

- No built-in cloud sync
- No plaintext JSON export
- No installer in the current working tree
- Windows-only target because this is a WPF desktop app

## Roadmap focus

- installer/distribution route after repository publication prep;
- continued release hardening and packaging cleanup;
- further polish of the current desktop UX and documentation.

## License

This repository is licensed under the MIT License. See [LICENSE](LICENSE).
