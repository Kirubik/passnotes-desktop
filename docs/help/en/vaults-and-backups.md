# Vaults, backups, and another vault

This page explains the difference between the working vault, a reserve backup, and export, and how to switch between files safely.

---

## What is what

- **Vault** — the main working PassNotes file. Usually `vault.dat`.
- **Backup** — a reserve copy of that vault. Usually a `*.dat` file and, if attachments exist, a matching `*.attachments` folder.
- **Export `*.pnexp`** — a separate encrypted transfer file. It is not a normal backup and not the same as switching to another vault.

> If you want to continue working in another active file, you need another vault.  
> If you want to return the current vault to an earlier state, you need a backup.

---

## Where the current vault lives

- The current vault path is configured in Settings.
- Default path: `%APPDATA%\PassNotes\vault.dat`.
- Attachments live next to the vault in a separate folder: `<vaultFilePath>.attachments\*.pna`.
- If you change the vault file in Settings, the app switches to another working vault.

---

## Manual backup

### What it is

A manual backup is a reserve copy of the current working vault that you create yourself before a risky action: restore, import, vault-file switching, large cleanup, or data move.

### What is included

- the vault `*.dat` file;
- the attachments folder, if the vault has attachments.

### How to create it

1. Unlock the app.
2. Open the app menu.
3. Press **Create backup now**.
4. Wait for the **“Backup created: ...”** message.
5. If needed, press **Open backups folder** and check that the new file is there.

### Where it is stored

- Default folder: `%APPDATA%\PassNotes\Backups`.
- You can change the folder in Settings.

### How to confirm success

- The app shows **“Backup created: ...”**.
- A new `*.dat` file appears in the backups folder.
- If the vault has attachments, there may also be a `<backupName>.dat.attachments` folder.

> **Important:** if a `<backupName>.dat.attachments` folder was created next to the backup file, do not delete it separately. If the vault state contained attachments, restoring from that backup may fail or be incomplete without that folder.

### When a backup is not considered successful

- If the app says it could not copy attachments.
- If the app says the attachments folder is missing.
- If the app says an attachment file is missing.

In these cases PassNotes does not treat the backup as a valid successful copy.

---

## Automatic backup

- You can enable **automatic backups** in Settings and choose an interval.
- Automatic backups use the same backups folder.
- They start working after a successful login.
- Automatic backup is useful as a safety net, but it does not replace a manual backup before a dangerous action.

> If the app is configured to keep only the last regular backups, older regular copies may be removed automatically.

---

## Another vault

### When you need it

- You have separate vaults for different purposes.
- You moved a vault to another folder or disk.
- You need to open an existing vault from another location.

### How to switch to another vault

1. Open **Settings**.
2. Find **Vault file**.
3. Choose the needed `*.dat` file.
4. Save settings.
5. If the target file already exists, the app asks:
   - **Yes** — use the existing file;
   - **No** — replace it with the current vault;
   - **Cancel** — do nothing.

### What happens next

- Before switching, the app tries to create a **safety backup** of the current vault.
- If the target file does not exist yet, the current vault is copied to the new path and the app starts using it.
- If the target file exists and you choose **Yes**, the app switches to that file.
- If the target file exists and you choose **No**, that file is overwritten with the current vault.

> Use the file picker in Settings for vault switching. It is safer than manually renaming or replacing files next to a running app.

---

## How not to confuse vault and backup

- The **current vault** is the file the app is using right now.
- A **backup** is a spare copy for later restore.
- **Switching to another vault** changes the active working file.
- **Restore from backup** replaces the current working file with a reserve copy.
- **Export `*.pnexp`** is for transfer and exchange, not for normal working-vault switching.

---

## If another file uses a different master password

This is normal when:

- you opened an older or somebody else's vault;
- you restored from a backup created under another password;
- you switched to another working vault file.

What to do:

1. Check which file you selected: another vault or a backup.
2. Press **Unlock**.
3. Enter the password for that selected file.
4. Do not keep retrying the old password if you already know the selected file uses a different one.

If you are not sure which password is needed, first identify what you opened: the current vault, another vault, or a backup.

---

## Helpful habits

1. Keep at least one fresh manual backup separate from the working vault.
2. Close editor windows before restore and vault-file switching.
3. Do not keep your only backup in the same place where you frequently move the working vault manually.

---

## Quick links

- [Restore and common errors](./recovery.md)
- [FAQ / quick guide](./faq.md)
- [User manual](./manual.md)
