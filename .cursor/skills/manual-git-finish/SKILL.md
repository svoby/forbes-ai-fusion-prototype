---
name: manual-git-finish
description: >-
  Human-driven wrap-up for a git branch: reconcile with repo rules, delete only
  disposable output folders (TestResults, Logs, _build), commit, push, and merge
  into main/master or the correct base branch. Use when the user asks to finish a
  branch, clean test/build log noise before commit, push and merge, or invokes
  manual-git-finish.
disable-model-invocation: true
---

# Manual Git Finish

Use this skill when the **user explicitly** wants to land work: remove known-safe output directories (see below), commit, push, and merge—either into the repo default branch (`main` / `master`) or into the long-lived branch this work actually branched from.

This is a **manual / human-approved** workflow. It does **not** replace day-to-day agent rules: automated sessions should still follow `.cursor/rules/feature-branching.mdc` (no surprise merges) unless the user is clearly running **this** finish flow.

## 1. Re-read task and repo instructions

Before changing git state:

1. Map remaining work to the **original task** (scope, done criteria).
2. Open and apply relevant repo guidance, e.g.:
   - `.cursor/rules/feature-branching.mdc` — branch naming, what belongs on feature branches.
   - `.cursor/rules/post-feature-diff-audit.mdc` — if the fix took multiple iterations, classify the diff before calling it done.
   - `AGENTS.md` / `CLAUDE.md` — tests, authority, verification.
3. Run or cite verification the task requires (e.g. EditMode tests per `.cursor/rules/cli-verification.mdc`).

Add any checkout-specific checks the user named (CI, PlayMode, host+client smoke).

## 2. Inspect working tree

From repo root:

```bash
git status
git branch --show-current
git remote -v
```

- **Intentional changes** should be staged or clearly listed for commit.
- **Noise** is often limited to test output and local build logs under the folders below. Do **not** treat “everything untracked / everything ignored” as deletable: Unity and other tools keep large **ignored** trees (`Temp/`, `Library/`, etc.) that must **not** be bulk-deleted by a generic `git clean -fdX`.

## 3. Remove only disposable output folders (allowed list)

**Goal:** Delete **only** these directories at the **repository root** (paths match this project’s `.gitignore` conventions):

| Path | Purpose |
|------|---------|
| `TestResults/` | EditMode/PlayMode batch logs, XML, scratch test output |
| `Logs/` | Unity Editor logs (`[Ll]ogs/` at root) |
| `_build/` | Local build output folder |

**Do not** extend this step to `Temp/`, `Library/`, or other ignored Unity/runtime folders unless the user explicitly asks with full knowledge (reimport/rebuild cost, locks).

**Before deleting:** Close Unity (and anything tailing those logs) so files are not locked.

**Preview** (list what would go away):

```powershell
Get-ChildItem -Directory -ErrorAction SilentlyContinue TestResults, Logs, _build
```

**Remove** (PowerShell, from repo root):

```powershell
foreach ($d in 'TestResults', 'Logs', '_build') {
  if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force }
}
```

On macOS/Linux with bash:

```bash
for d in TestResults Logs _build; do [ -d "$d" ] && rm -rf "$d"; done
```

If `Logs/` does not exist but a differently cased folder does on a case-sensitive volume, match the actual directory name; Unity typically uses `Logs` at the project root.

**Optional:** Other stray `*.log` files outside those folders are **out of scope** unless the user names them; do not run blanket `git clean` for this workflow.

## 4. Commit

1. `git diff` / `git diff --staged` — ensure the patch matches the task.
2. Stage: `git add -A` or selective paths.
3. Commit with a message that matches team convention (short subject, body if needed).

## 5. Push

```bash
git push -u origin HEAD
```

If the branch exists remotely, a plain `git push` may suffice.

## 6. Choose merge target

**Default:** merge into the **default trunk** the team uses (`main` or `master`), after fetch:

```bash
git fetch origin
```

Detect default remote branch (when configured):

```bash
git symbolic-ref refs/remotes/origin/HEAD
```

**If work branched from another long-lived branch** (e.g. `develop`, `release/x`):

- Prefer the branch name the user gives.
- Otherwise infer candidates with recent history, e.g. compare where the branch forked: `git merge-base HEAD origin/main` vs `git merge-base HEAD origin/develop` and reconcile with how the branch was created.

When in doubt, **ask the user** which branch should receive the merge or PR target.

## 7. Merge (prefer PR; local merge optional)

**Preferred (review trail):** open a PR on the hosting provider (GitHub MCP or web UI), get review, merge via the GitHub UI.

**Local fast-forward / merge** (only if the user wants it locally):

```bash
git checkout <target-branch>   # e.g. main
git pull origin <target-branch>
git merge --no-ff <feature-branch>   # or rebase policy per team
git push origin <target-branch>
```

Use the team’s policy for **merge vs rebase vs squash**.

## 8. Quick checklist

- [ ] Task scope and repo rules re-read; tests/verification done as required.
- [ ] `TestResults/`, `Logs/`, and `_build/` removed only if the user wanted that cleanup; no broad `git clean` on Unity trees.
- [ ] Commit contains only intentional changes.
- [ ] Pushed; merge target is `main`/`master` or the correct base branch.
- [ ] Landed via PR or an explicit local merge per user instruction.
