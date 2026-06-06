# AI Agent Git Instructions & Workflow Rules

This document outlines the strict guidelines and workflows the AI Agent must follow regarding version control, file changes, and Git commands in this project.

---

## 🔄 1. The Pre-Commit / Post-Verification Lifecycle

Before making any modifications to the codebase, the AI Agent must follow this lifecycle:

1. **Commit Before Modifying**: Commit any current unstaged changes locally before starting to edit or write files. This creates a clean save point.
2. **Reviewing Changes (The Diff)**:
   - After code modifications are made, display the exact `git diff` in the chat.
   - If the diff is too long/complex, generate a temporary `.diff` file in the project's scratch space or a markdown diff block, and point the user to it.
3. **Ask for Confirmation**: 
   - Ask the user to confirm the changes.
   - Present a terminal UI/interactive confirmation mechanism (e.g., using `ask_question` or similar interactive menu prompts) so the user does not need to manually type "Approve" or "Reject".
   - If the user rejects the changes, **revert** the workspace back to the initial pre-modification commit.

---

## 📦 2. Commit Consolidation & Pushing

Before pushing local changes to remote:

1. **Consolidated Commits**: Consolidate (squash/rebase) the local micro-commits made during the session into a single, cohesive commit.
2. **Descriptive Message**: Write a structured commit message containing a clear, concise title and a detailed description summarizing what was changed.
3. **Push Confirmation**: Present the consolidated commit details to the user, ask for final confirmation, and only then run `git push`.
4. **Manual Trigger**: The user must be able to trigger this consolidation manually at any time by requesting a `git consolidate` command.

---

## 🛠️ 3. Additional Git Rules

### A. Basic Commands
Support and utilize standard commands (`git pull`, `git add`, `git commit`, `git push`) to sync, track, and back up modifications.

### B. History & Reversion
Support reviewing commit history (`git log`) and reverting changes (`git revert` or `git reset`) when requested by the user.

### C. Git Command Consolidation (Interactive Menu)
Whenever the user requests a "git" action or operation, the AI Agent must present a multiple-choice menu (using interactive tools like `ask_question`) containing:
* `status`: Show repository status via `git status`.
* `log`: Show commit history via `git log`.
* `diff`: Show unstaged changes via `git diff`.
* `commit`: Stage modified files, prompt for a commit message, and commit locally.
* `push`: Push local commits to origin.
* `commit & push`: Stage modified files, prompt for a commit message, commit, and push.
* `pull`: Pull changes from origin.
* `consolidate`: Consolidate/squash local commits made during the session into one.
* `consolidate & push`: Consolidate/squash local commits made during the session into one, and push to origin.

### D. Git Confirmation Protocol (Review-First)
The AI Agent must follow the **Approve-First / Review-First** workflow for any Git operations that modify the project's content or repository state (e.g., `git add`, `git commit`, `git pull`, `git push`, `git revert`, `git reset`). 
- Display details of the planned command and its impact clearly.
- Obtain explicit user approval before execution.
- *Note:* Read-only commands that do not alter repository state (e.g., `git log`, `git status`, `git diff`, `git show`) are exempt from prior approval.
