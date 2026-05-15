# Agent Operating Guide (Unity + Fusion)

This repository is a Unity multiplayer prototype using Photon Fusion. This file is the
shared entry point for Codex, Cursor, Claude, and other coding agents. Tool-specific
rules may add workflow details, but they should not contradict this guide.

## Main Goal

- Deliver a small playable vertical slice quickly.
- Keep architecture simple, testable, and **authority-correct**.
- Prefer small, reversible changes over broad rewrites.

## Source Of Truth

- `docs/AGENT_CONTEXT.md` — task-specific context bundles and current source-of-truth docs.
- `docs/architecture.md` — current architecture, feature map, and script responsibilities.
- `docs/TEST_HARNESS.md` — how EditMode and PlayMode tests work.
- `.cursor/rules/` — Cursor-specific enforcement rules that mirror or refine this guide.

Do not treat old plans, checkpoints, or removed historical docs as current guidance.

## Technical Rules

- **State Authority** owns gameplay outcomes; clients send **intent/input**, not authoritative combat results.
- Run core gameplay simulation in Fusion ticks (`FixedUpdateNetwork`), not render-only `Update`/`LateUpdate`.
- Keep UI presentation-only; UI must not mutate gameplay or networked state.
- Prefer one responsibility per `MonoBehaviour`; extract pure logic into testable C# helpers where practical.
- Do not change serialized defaults, prefab tuning, scenes, or `.meta` files unless the task requires it.
- Milestone 1 avoids custom transport and a custom physics engine.

## Workflow

1. Read the smallest relevant context bundle before editing.
2. Make the smallest working change first.
3. Keep one feature, refactor, or cleanup per change.
4. For behavior changes, add or update focused tests.
5. Validate host + one client when behavior is networked.
6. Report short manual verification notes and any tests that could not be run.

## GitHub Issue Workflow

When the user points to a GitHub issue as the task contract, use the `user-github` MCP
(`mcps/user-github/tools/`) for all GitHub API operations. Always read the tool's JSON
schema before calling it.

### 1 — Fetch and verify

Fetch the issue with `issue_read` / `get`. **Stop and report** (do not improvise) if it is:

- stale — references closed PRs, superseded branches, or removed docs as source of truth;
- incomplete — missing required files, acceptance criteria, or scope definition;
- contradictory with local architecture or rules in this file;
- out of scope — would require changes the issue does not explicitly authorize.

### 2 — Branch

Create the branch named exactly as the issue specifies. If the issue does not name a branch,
use `issue-N-<short-slug>`.

### 3 — Read context first

Read every file listed in the issue's required context bundle before touching any files.
Do not read more than the issue requires unless a file is directly needed to complete the work.

### 4 — Implement only what the issue authorizes

Do not add features, refactors, visual polish, or cleanups outside the stated scope, even
when they seem obviously related. File a follow-up issue instead.

### 5 — Verify the diff before committing

Run `git status` / `git diff` and confirm that only the files the issue explicitly allows
are changed. Stop if unexpected files appear.

### 6 — Commit and push

Committing, pushing, and opening a PR are all expected parts of the issue workflow when the
user opens an issue as the task contract — they do not need a separate explicit ask.
Write the commit message to `.git/COMMIT_EDITMSG` for review, then commit and push.

### 7 — Open the PR

Use `create_pull_request` via the `user-github` MCP:

- **Title:** exactly as the issue specifies.
- **Body:** include the summary the issue requires and `Closes #N`.
- **Base:** `master` unless the issue specifies otherwise.
- **Changed files:** verify with `get_files` or `git diff --name-only` that only
  issue-authorized files are in the PR before opening it.

### 8 — Address review comments

After opening the PR, fetch comments with `pull_request_read` / `get_comments` and
`get_review_comments`. Address any requested changes, push a fixup commit, and confirm
the PR is up to date before handing back to the user.

### GitHub MCP / Windows notes

- PR creation requires a token with `repo` write scope; a 403 means the token needs updating.
- The shell is PowerShell — bash heredocs (`<<'EOF'`) do not work. Pass multi-line PR bodies
  directly to the MCP tool, or write to a temp file (`.git/pr-body.md`) for `gh --body-file`.
- `gh` CLI may not be installed; prefer the MCP for all GitHub operations.

## Cursor Worktrees — Parallel Issue Agents

Cursor Worktrees let multiple agents work on the same repository at the same time without
interfering with each other. Each agent operates in an isolated Git checkout on its own branch.

### Rules for parallel-agent work

- **One issue = one branch = one worktree = one PR.** Never share a branch or worktree across
  issues.
- **Non-overlapping file sets.** Parallel issues must have non-overlapping sets of allowed files.
  An issue that touches `AGENTS.md` and an issue that touches `Assets/Scripts/` are safe to
  run in parallel; two issues that both list `AGENTS.md` as an allowed file are not.
- **Check for conflicts before editing.** At the start of each session, list open PRs
  (`list_pull_requests` via the `user-github` MCP). Stop and report if any open PR already
  modifies a file in this issue's allowed set.
- **Stay within scope.** Agents must not edit files outside the issue's explicitly authorized
  file list, even when they seem obviously related.
- **No guessing on conflicts.** If `master` changes while a PR is open and the diff overlaps
  with this issue's files, stop and report rather than resolving the conflict independently.
  Re-check the diff and confirm the change is still correct before pushing a fixup.
- **Humans merge.** Agents open PRs and address review comments; they do **not** merge PRs.
  The merge owner is always a human reviewer.

### Worktree setup for this repo

This is a Unity project. No install step is needed for agents doing documentation or C# edits.
For agents that run tests in batchmode, close the Editor before starting (see `docs/TEST_HARNESS.md`).

## Git Safety

- Work on the current branch by default.
- Do not create, checkout, switch, merge, rebase, push, or commit unless the user explicitly asks.
- If a branch change seems needed, ask first and explain why.
- Do not run `git add` or otherwise stage changes unless the user explicitly asks. When the user asks you to commit, include only files that belong to the requested task.
- Never merge feature branches into `main`/`master` unless the user explicitly requests that operation.

These rules allow Codex and Cursor to operate in the same repository without surprise branch
changes or unrelated commits.

## Definition Of Done

- Works host + one client where relevant.
- No authority violations.
- No critical console errors.
- Relevant EditMode or PlayMode coverage is green, or the verification gap is stated clearly.
- Final diff is intentional and scoped to the request.

## Post-Implementation Diff Audit

After any feature that needed multiple fix iterations before tests or behavior stabilized,
stop and audit the final diff before claiming the task complete. Green tests are not enough.

The audit must map the diff to the original request, classify non-trivial changes, flag scope
drift, and call out weakened tests, test-only production seams, public API expansion, changed
serialized defaults, duplicate helpers, accidental prefab/scene/meta churn, authority mistakes,
and scene-wide scans such as `FindObjectsByType` in hot gameplay paths.

Full checklist: `.cursor/rules/post-feature-diff-audit.mdc`.

## Verification

For compile/regression work, verify with EditMode tests when feasible.

- If the Unity Editor is open: use **Window -> General -> Test Runner** and run **Edit Mode** tests.
- If the Editor is closed or this is CI/CLI-only, run:

```text
powershell -ExecutionPolicy Bypass -File tools\run-editmode-tests.ps1
```

`tools\Run-EditModeTests.ps1` delegates to the same runner. Results are written to
`TestResults/editmode.xml`; logs are written to `TestResults/unity-editmode.log`.

For PlayMode smoke coverage, use the Unity Test Runner with the Editor open, or run
`tools\run-playmode-tests.ps1` when the Editor is closed. Reuse shared PlayMode helpers from
`FusionPlayModeTestHelpers`; do not copy-paste coroutine helpers.
