# FAQ / quick guide

Below are short answers about real, currently working PassNotes Desktop functionality.
If you want the full overview, open the [User manual](./manual.md).

---

## 30-second quick guide

**Q: How do I quickly add an entry?**  
A: Pick a folder on the left, press **+** above the table, fill in the fields, and press **Save**.

**Q: How do I open a long comment in a larger window?**  
A: Open the entry, right-click the **Comment** field, and choose **Open comment in separate window**.

**Q: What can I do with right click?**  
A: It depends on where you click. The entries table gives entry actions, the folder tree gives folder actions, comment and text fields give editing commands, and attachments give open/save/remove/copy-name actions.

**Q: Why can’t I see the time column?**  
A: The **Updated** column is hidden by default. Turn it on through right-click on the table or the table header.

**Q: Where do I change the theme?**  
A: **Settings -> General -> Theme**.

---

## Search and navigation

**Q: Search does not show literally all entries. Is that normal?**  
A: It depends on the current state. If a context chip is shown next to the search field, it means a folder or special section is currently selected. If you clear that chip with the **x** and type a query, search runs across all entries. If the context is cleared but the query is empty, the list may be empty. That is normal.

**Q: How do I quickly find a folder?**  
A: Use the **Folder search** field above the tree on the left.

**Q: How do I go back to the real folder of a found entry?**  
A: If the entry is shown in search results, right-click it and use the command that opens its folder context.

**Q: How do I select multiple entries?**  
A: Use **Ctrl** and **Shift**. Simple mouse dragging across rows no longer expands selection.

---

## Entries and fields

**Q: How do I open an entry for editing quickly?**  
A: Double-click the entry, use the edit button above the table, or right-click the entry.

**Q: How do I quickly copy username and password?**  
A: Use the buttons above the table or the entry context menu. Usually one selected entry is required.

**Q: What happens on normal delete?**  
A: The entry goes to **Trash**. There you can restore it or delete it forever.

**Q: What does the star in the table do?**  
A: It adds the entry to **Favorites** or removes it from there.

**Q: Can I open the password generator from the entry editor?**  
A: Yes. The entry editor has a generator button next to the password field.

---

## Comments and attachments

**Q: What is the separate comment window for?**  
A: It opens a larger multiline editor. It is useful for long notes and structured text.

**Q: How do I confirm text in the larger comment window?**  
A: Press **OK** or **Ctrl+Enter**.

**Q: What happens if I press “Cancel” while editing an entry?**  
A: Unsaved changes from that edit session, including newly added attachments, should not be saved.

**Q: What can I do with attachments through right click?**  
A: Open, save as, copy file name, and remove.

---

## Time and time zone

**Q: How does entry time work?**  
A: PassNotes shows entry update time in the selected time zone.

**Q: Where do I choose the time zone?**  
A: **Settings -> General -> Time zone**.

**Q: What happens if I choose the system time zone?**  
A: The app follows Windows time settings.

**Q: Does changing the time zone modify the entries themselves?**  
A: No. It changes time display only.

---

## Themes

**Q: Which themes are available now?**  
A: **Standard**, **Sage Light**, **Arctic White**, and **Midnight Slate**.

**Q: Where do I switch them?**  
A: **Settings -> General -> Theme**.

**Q: Does that affect my data?**  
A: No. Themes affect app appearance only.

---

## Menu, lock, and security

**Q: What is inside the main Menu button?**  
A: Settings, help, import, export, create backup, restore, backups folder, lock, and exit.

**Q: How do I lock the app quickly?**  
A: Use **Menu -> Lock**.

**Q: What do I need to unlock it?**  
A: The master password of the current vault.

**Q: Can the master password be recovered?**  
A: No. Without it, the vault cannot be decrypted.

---

## Backups, import, and another vault

**Q: Where do I make a manual backup?**  
A: **Menu -> Create backup now**.

**Q: Where do I open the backups folder?**  
A: **Menu -> Open backups folder**.

**Q: Where do I restore from a backup?**  
A: **Menu -> Restore from backup...**.

**Q: Where do I read more about backups, import, and another vault?**  
A: On these pages:

- [Vaults, backups, and another vault](./vaults-and-backups.md)
- [Restore and common errors](./recovery.md)

---

## Logs and errors

**Q: Where can I see why an error happened?**  
A: In `%APPDATA%\PassNotes\diagnostic.log` and `%APPDATA%\PassNotes\last_error.txt`.

**Q: What should I include in a bug report?**  
A: The app version or archive id, reproduction steps, and logs. Do not send secrets or passwords.

---

## Quick links

- [User manual](./manual.md)
- [Vaults, backups, and another vault](./vaults-and-backups.md)
- [Restore and common errors](./recovery.md)
- [What may appear later](./future.md)
- [Table of contents](./navigation.md)
