# PassNotes — AGENTS.md

## 1. Priority of rules
If instructions conflict, use this priority:

1. User request in current chat
2. This AGENTS.md
3. Referenced current project docs
4. Historical context / old chats / old plans

If something was changed later by the user, the newer instruction overrides the older one.

---

## 2. Language
- Always respond to the user in Russian.
- Keep wording clear, direct, and specific.
- Do not use vague confirmations like “вроде нормально”.
- If something is incomplete, say so explicitly.

---

## 3. Task classification
When the user reports a bug, issue, improvement, idea, or change request, first classify it explicitly as one of:

- **новая задача**
- **старая незавершенная задача**
- **будущая задача**

Then state whether it belongs to:
- the current active branch,
- an older branch,
- or a postponed/future branch.

Do this before proposing implementation.

---

## 4. Task intake workflow

### 4.1 Normal task in plain words
For a normal request:
1. Briefly suggest what else should be included to do the task correctly.
2. Wait for confirmation if needed.
3. Then write a clear technical specification (ТЗ).
4. Do not start implementation before confirmation.

The technical specification should stay practical and include:
- goal,
- scope,
- exclusions,
- risks,
- readiness criteria,
- verification,
- expected changed files.

### 4.2 Code / build / runtime errors
If the user sends:
- code,
- logs,
- build errors,
- runtime errors,

do **not** start with a long rewrite of the task.
Immediately analyze root cause and propose the fix path.

### 4.3 Screenshots / videos
Do not analyze screenshots or videos unless the user explicitly asks for analysis.

---

## 5. Execution discipline
- Do not expand scope without need.
- Do not change unrelated code.
- Choose the safest implementation path.
- Prefer targeted fixes over broad refactors.
- If a temporary solution is used, mark it explicitly as temporary.
- Do not present assumptions as facts.
- If a task is risky, say so before implementation.

---

## 6. Baseline integration rule
Any new change in the project must be integrated into the unified baseline system by default.

This applies to everything:
- bugfixes,
- new features,
- improvements,
- UI changes,
- behaviors,
- settings,
- dialogs,
- states,
- support systems,
- and any other project changes.

Do not create isolated local solutions if the change can and should be integrated into the common baseline logic.

If baseline integration is currently undesirable, risky, or premature:
- say that explicitly before implementation;
- explain why;
- describe risks;
- state what temporary approach is acceptable;
- state the correct next step for later baseline integration.

---

## 7. Bug closure rule
A bug, defect, issue, or incomplete feature is considered closed only after:
- the root cause is fixed,
- the result is stable,
- the behavior is reliable.

Partial fixes, workarounds, and symptom-masking do **not** count as full closure.

If the issue is not fully resolved, explicitly state:
- what was fixed,
- what remains unresolved,
- what risks remain,
- what the next correct step is.

Never mark a partial fix as a full resolution.

---

## 8. Naming format
Always use this naming style:

- `Этап X / Подблок X.Y`

For out-of-plan or side tasks, use:
- `Этап 0 / Подблок 0.X`

Do not invent inconsistent naming.

---

## 9. Response and reporting format

### 9.1 Analysis / planning response
When analyzing a task, structure the answer like this:
1. What it relates to (new / old / future task)
2. What else should be included
3. Technical specification (ТЗ)
4. Whether the task should be split into subblocks
5. Wait for confirmation before implementation

When local files are available, inspect them first before drawing conclusions.

### 9.2 After implementation
After each completed block/subblock provide:
1. Header with exact block/subblock name
2. What was done
3. What is normal now
4. What is temporarily acceptable
5. What is already considered a bug / not normal
6. Changed files
7. How to verify
8. Archive only if explicitly requested
9. End with:
   `Делай дальше "<точное название следующего блока/подблока>"`

For UI/theme/layout reviews, explicitly separate:
- `Норма сейчас`
- `Временно допустимо`
- `Не норма / дефект`
- `Ожидаемое финальное состояние`

### 9.3 If implementation is incomplete
Explicitly say:
- the task is only partially done,
- what remains,
- what should be done next.

---

## 10. Hard project invariants
Never break these without explicit user approval:

- Multi-select
- Ctrl+A
- Del
- Drag & drop
- Tray behavior
- Secure import/export/backup flows
- No unencrypted JSON export
- No forbidden “all records” mode
- Click on empty space clears selection but does not reset the right context
- Do not rename exe
- Do not change app data paths / `%APPDATA%`
- Public app name remains `PassNotes Desktop`

If a task risks affecting any invariant, warn before implementation.

---

## 11. Rollback / active base rule
- Always work from the current active base defined by the user.
- If the user explicitly says to roll back, treat the rollback archive/base as the new source of truth.
- Do not continue from older broken experimental branches unless the user explicitly asks for it.
- If the active base is unclear, do not guess silently — say what base you are assuming.

---

## 12. Archive and checkpoint rules
- A checkpoint means **git commit**, not zip archive.
- Do not create commits unless the user explicitly asks.
- Do not create archives unless the user explicitly asks.
- If archives are requested, follow the project naming rules exactly.
- If archive suffix sequencing matters, preserve it exactly.
- Place zip archives in `archives/`, not in the project root.
- Include `RunPassNotes.vbs` in archives unless the user explicitly says otherwise.

---

## 13. Documentation rules
- Treat fixed project docs in `docs/` as part of the working context.
- When a strategy, plan, or rule is officially fixed in docs, follow it until the user explicitly changes it.
- Update docs only when the user asks or when the task explicitly includes documentation sync.

---

## 14. Current context pointer
Do not keep large historical context in this file.

Active working state should be kept in:
- `docs/CURRENT_CONTEXT.md`

Historical completed branches, archives, and older plans should be kept outside this file.

---

## 15. Default working principle
- First classify the task.
- Then decide whether it belongs to the current branch.
- Then produce the correct ТЗ.
- Then implement only after approval.
- Keep the solution aligned with the unified baseline system.
