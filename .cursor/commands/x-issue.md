---
description: >-
  Fetch GitHub issue N and execute the repo’s GitHub Issue Workflow (branch, scoped
  implementation, commit/PR via MCP unless the user opts out).
---

Model: Composer / Agent (recommended).
Mode: Agent.

## Issue number (variable input)

Interpret the numeric **GitHub issue ID** from **this chat turn**:

- Prefer text after `/x-issue` on the same line (`/x-issue 9`).
- Accept `#9`, `issue 9`, or a short sentence containing one issue reference (e.g. “Open issue #9 …”).
- Parse to a single positive integer \(N\). If unclear or ambiguous, stop and ask for **one** numeric ID.

Treat this invocation as the user explicitly driving the **GitHub Issue Workflow** described in `AGENTS.md`; that overrides generic “defer branching/commits” reminders **for this workflow only**.

## Goal

Open issue **#N** in this repository and follow **every step** of the **GitHub Issue Workflow** in `AGENTS.md` (“GitHub Issue Workflow”).

Work from the issue as **task contract**:

1. Fetch and verify issue **N** (`issue_read` with `method: get`, …) using the **`user-github` MCP**. **Read each tool’s JSON schema** under `mcps/user-github/tools/` before the first call to that tool.
2. **`owner`** / **`repo`**: Resolve from `git remote get-url origin` (or equivalent) for **this workspace** unless the issue explicitly names another repo—if unclear, confirm with the user.
3. If the issue fails any “stop and report” checks in **§1 — Fetch and verify**, report and stop; do not invent scope.

Then continue exactly as **`AGENTS.md`** describes for steps **2–8** (branch naming, required context reads, scoped implementation, diff verification before commit, commit & push expectations for issue-driven work, PR via MCP, review-loop).

## MCP and Git reminders (local to this repo)

- Prefer **`user-github` MCP** for GitHub reads/writes referenced in **`AGENTS.md`**.
- Respect general repo guides: **`AGENTS.md`**, **`docs/AGENT_CONTEXT.md`**, `docs/architecture.md`, **`docs/TEST_HARNESS.md`**, `.cursor/rules/`.
- **PowerShell**: no bash heredocs for PR bodies—use MCP fields or `@.git/pr-body.md` / inline body as **`AGENTS.md`** notes.
