# PassNotes Desktop user manual

PassNotes Desktop is a local app for storing passwords, notes, comments, and attachments in an encrypted vault.
This page describes only the functionality that already works now.

> Open help with **F1** or **Menu -> Help**.  
> Hotkeys: see [Hotkeys](./hotkeys.md).

---

## Quick start

1. Unlock the app or open your vault.
2. Pick a folder, **Favorites**, **Trash**, or **No folder** on the left.
3. Press **+** above the entries table to create an entry.
4. Fill in the main fields and press **Save**.
5. Use search, favorites, copy actions, and context menus for faster work.

---

## How the main window is organized

### Top bar

- **Menu** — settings, help, import, export, backups, lock, and exit.
- **Entry search** — searches entries in the table on the right. If a context chip is shown next to it, you can immediately see which folder or special section is currently selected. The **x** button clears the query.
- **Buttons next to search** — add, edit, delete, copy username, copy password.
- **Password generator** — opens from its own button on the top bar.
- **Row height** — the slider changes row height in the entries table.

### Left side

- **Folder search** helps find a folder quickly.
- **Favorites** shows entries marked with a star.
- **Trash** shows deleted entries.
- **No folder** shows entries that do not belong to a regular folder.
- **Regular folders** are the main structure for organizing entries.

### Right side

- The **entries table** shows entries for the current context.
- **Double-click** opens entry editing.
- Use **Ctrl** and **Shift** for multi-selection.
- Simple left-mouse dragging across rows no longer expands selection. That is normal current behavior.
- The bottom status bar shows the current context, number of entries, and number of selected entries.

---

## Working with folders and context

- To create a folder, use the **+** button next to folder search or right-click the folder tree.
- The folder tree context menu supports **new**, **rename**, and **delete**.
- If you found an entry through search, you can jump to its real folder through the entry context menu.
- A small context chip may appear next to entry search to show which folder or special section is currently active.
- The **x** on that chip clears the current context.
- If the context is cleared and you type a search query, search runs across all entries.
- If the context is cleared and the query is empty, the entries list may be empty. That is normal.

---

## What the main terms mean

- **Context** — the section you are currently working in: a regular folder, **Favorites**, **Trash**, or **No folder**. It defines which entries appear in the table on the right.
- **Context chip** — the small chip next to entry search. It shows the current context and lets you clear it with the **x**.
- **Favorites** — the special section that collects entries marked with a star.
- **Trash** — the special section with deleted entries. From there an entry can be restored or deleted forever.
- **No folder** — the section for entries that are not attached to a regular folder.
- **Right click / context menu** — the menu you open with the right mouse button. In PassNotes it often contains important actions for folders, entries, and fields.

---

## Working with entries

- **Create entry** — the **+** button above the table or the table context menu.
- **Edit entry** — double-click, the edit button, or the table context menu.
- **Delete entry** — moves the entry to **Trash**.
- **Restore** and **Delete forever** are available in **Trash**.
- The **star** in the table adds or removes the entry from **Favorites**.
- **Copy username** and **Copy password** work for one selected entry.

Normal behavior:

- entry actions may be unavailable while the app is locked;
- some commands depend on the current context;
- with multiple selected entries, only the actions that make sense for multi-selection stay available.

---

## Full comment window

The full comment window already works and is useful for long notes.

How to open it:

1. Open entry creation or entry editing.
2. Right-click the **Comment** field.
3. Choose **Open comment in separate window**.

What it does:

- opens a larger multiline editor for the comment;
- is more convenient for long text, notes, and lists;
- returns the edited text back to the entry form after confirmation.

Normal behavior:

- **OK** applies the text back to the entry form;
- **Cancel** closes the window without applying the changes from that larger editor;
- normal **Enter** inserts a new line;
- **Ctrl+Enter** confirms the comment.

---

## Context menus (right click)

PassNotes uses context menus as a normal part of the interface.

### Right-click on the entries table

Available actions can include:

- add entry;
- edit entry;
- delete entry;
- restore or delete forever in **Trash**;
- add to favorites or remove from favorites;
- go to the entry folder;
- copy username;
- copy password;
- turn on **Show "Updated" column**.

### Right-click on the folder tree

Available actions:

- create folder;
- rename folder;
- delete folder.

### Right-click on the comment field

Available actions:

- cut;
- copy;
- paste;
- select all;
- open comment in a separate window.

### Right-click on the attachments list

Available actions:

- open attachment;
- save as;
- copy file name;
- remove attachment.

Normal behavior:

- disabled commands usually mean the current selection or context does not fit that action;
- many commands are unavailable while the app is locked;
- the set of available commands may differ in search results and in Trash.

---

## Time display

Time display already supports different time zones.

How it works:

- each entry has an update time;
- the **Updated** column is hidden by default;
- you can turn it on through right-click on the table or on the table header;
- the column header shows the current `UTC` offset;
- the header tooltip shows the full time zone name.

Where to configure it:

- **Settings -> General -> Time zone**.

Normal behavior:

- if the system time zone is selected, the app follows Windows settings;
- if another time zone is selected, displayed times are recalculated for that zone;
- changing the time zone changes time display, not the entry contents themselves.

---

## Theme switching

Theme switching is already built into the app.

Where it is:

- **Settings -> General -> Theme**.

Available themes now:

- **Standard**
- **Sage Light**
- **Arctic White**
- **Midnight Slate**

Normal behavior:

- the selected theme is saved as an app setting;
- the main window, dialogs, and built-in help are expected to work within the selected theme;
- changing the theme is an app interface setting, not a data profile.

---

## Search, favorites, and trash

### Entry search

- the top search field filters entries in the table on the right;
- if a context chip is shown, it tells you which folder or special section is currently selected;
- if you clear the context chip with the **x** and type a query, search runs across all entries;
- if no context is selected and the query is empty, the entries list may be empty;
- if you do not remember where the entry is, the simplest approach is to clear the context chip and type the query;
- the **x** button clears the query quickly;
- if an entry is found through search, you can use the context menu to jump to its real folder;
- when search is active, some context-menu actions adjust to search results.

### Folder search

- helps you find a folder in the tree on the left;
- folder search itself does not change entries directly, it helps you navigate faster.

### Favorites

- you can add an entry to favorites through the star in the table or through the context menu;
- the **Favorites** section collects those entries in one place.

### Trash

- normal delete sends entries to trash;
- you can restore an entry from trash;
- you can delete an entry forever;
- you can empty trash completely.

---

## Attachments

Attachments are already supported in the entry editor.

What you can do:

- add attachments;
- open attachments;
- save them elsewhere;
- copy file names;
- remove attachments.

Normal behavior:

- attachments are saved together with the entry;
- if you cancel entry editing, new attachments from that edit session should not be saved;
- if an external viewer keeps a file locked, some operations may fail temporarily.

---

## Password generator

You can open the password generator:

- from the top bar in the main window;
- from the entry editor next to the password field.

It is meant for quickly generating a new password without typing one by hand.

---

## App lock

App lock already works.

Where it is:

- **Menu -> Lock**;
- related unlock actions are available in the matching scenarios.

Normal behavior:

- unlocking requires the master password again;
- while editing, the app tries to preserve context;
- if there were unsaved changes, after locking and returning you may need to confirm or re-check the form state.

---

## Where to read about narrower topics

- [Vaults, backups, and another vault](./vaults-and-backups.md)
- [Restore and common errors](./recovery.md)
- [FAQ / quick guide](./faq.md)
- [Hotkeys](./hotkeys.md)
- [What may appear later](./future.md)
