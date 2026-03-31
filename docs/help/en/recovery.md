# Restore and common errors

Use this page when you need to bring data back from a backup, open another file with another password, or understand a common recovery problem without risking the current vault.

---

## Restore from backup

### What restore does

- Takes the selected backup `*.dat`.
- Replaces the current working vault with it.
- Creates a **safety backup** of the current vault before replacement.
- Replaces attachments too if the backup contains them.

If the selected backup has no attachments and does not reference attachments, the current attachments folder may be removed so different vault states do not get mixed together.

### How to restore

1. Unlock the app.
2. Close editor windows and decide what to do with unsaved changes.
3. Open the app menu.
4. Press **Restore from backup...**.
5. Choose the needed `*.dat` file.
6. Confirm replacement of the current vault.
7. Wait for the result.

### What happens to the current vault

- The current vault is replaced by the selected backup.
- Before that happens, the app creates a separate safety backup of the current state.
- After a successful restore, the app tries to open the restored vault right away.

> If you are not sure which file you selected, stop and verify the backup name and location first. A pause here is safer than restoring the wrong file.

---

## What happens after a successful restore

There are two normal outcomes:

- **Password matches**: the app loads the restored data immediately and shows a success message.
- **Password is different**: the app stays locked. That does not mean restore failed. It means the selected backup uses another master password.

If the message shows the name of the safety backup, keep it. That is your fast rollback point if you restored the wrong file.

---

## Why you may need to log in again

After restore, the app must open the **restored** vault again.

The password is requested again because:

- the old password may not fit the restored file;
- the app must verify that the new current vault can actually be opened;
- this protects you when the old and restored vaults use different passwords.

If the app stays locked after restore, press **Unlock** and enter the password for the selected backup.

---

## What to do if restore failed

1. Read the exact error message.
2. Do not delete the current safety backup.
3. Check that you selected the correct file.
4. If the error mentions attachments, try another complete backup.
5. If the problem repeats, review `%APPDATA%\PassNotes\diagnostic.log` and `%APPDATA%\PassNotes\last_error.txt`.

If the app says the selected backup is missing the attachments folder or an attachment file, that copy is incomplete for that vault state.

---

## Different master password

The most common confusing case after restore or vault switching:

- the user enters the password for the **previous** vault;
- the app is already working with a **different** vault or a **restored** backup;
- the old password no longer fits.

Correct order:

1. Identify which file is selected now.
2. Press **Unlock**.
3. Enter the password for that file.

If you already know the selected backup or vault uses another password, that is not an app bug.

---

## Common errors

### Wrong password

- If the password no longer fits after restore or vault-file switching, check whether you selected another vault or a backup with another master password.
- If it does not fit during a normal login, make sure you are opening the vault you expected.

### Current vault not found

- If backup creation says the vault file is missing, check the current vault path in Settings.
- If you moved or deleted the active `*.dat` manually, locate the correct file first before creating a backup or switching the vault path.

### Backup not found

- Check `%APPDATA%\PassNotes\Backups` or the folder where you saved the copy.
- Make sure you are selecting `*.dat`, not `*.pnexp`.

### Selected backup is missing the attachments folder

- This is normal only if that vault state had no attachments.
- If you expected attachments, that backup is incomplete for the state you want to restore.

### Selected backup is missing an attachment file

- The backup does not contain all required files.
- Choose another backup or copy the full file set again.

### `restore failed`

- This usually means the selected file is damaged, incomplete, or unavailable.
- Do not keep retrying randomly. Keep the safety backup and try another copy first.

### The app cannot open the vault at startup

PassNotes may offer to:

- retry the password;
- restore from the previous vault version;
- choose a backup file manually.

If startup restore succeeds, the app asks for the password again.

---

## Short FAQ / quick guide

**How do I make a backup?**  
Unlock the app, press **Create backup now**, wait for the success message, and check the new file in the backups folder.

**How do I restore?**  
Press **Restore from backup...**, choose the right `*.dat`, confirm replacement, and then enter the password for the selected backup if needed.

**How do I switch to another vault?**  
Use the vault file setting, not import `*.pnexp`.

**What if the password is different?**  
Enter the password for the selected file, not the previous working vault.

**How do I avoid losing the current vault?**  
Before restore, import, and vault-file switching, create a separate manual backup first.

---

## Quick links

- [Vaults, backups, and another vault](./vaults-and-backups.md)
- [FAQ / quick guide](./faq.md)
- [User manual](./manual.md)
