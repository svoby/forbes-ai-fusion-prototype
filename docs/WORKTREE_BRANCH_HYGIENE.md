# Worktree and branch hygiene (local multi-agent)

Use this checklist when several agents or humans work on different GitHub issues at the same time.

## Branch from `origin/master`, not from local HEAD

- Create the issue branch from **`origin/master`** (the shared merge base), not from whatever branch happens to be checked out in another window.
- Stacked branches (PR B built on top of unmerged PR A) hide unrelated diffs and break the **Allowed scope** gate below. Treat them as exceptional and document them in the PR.

## One git worktree per parallel issue

- Each issue gets its **own** `git worktree` checkout on its **own** branch.
- **Never** point two agents at the same mutable working directory for different issues.

## Pre-PR diff gate

Before you open a PR:

1. `git fetch origin`
2. `git diff --name-only origin/master...HEAD`

Every path in that list must be allowed by the issue’s **Allowed scope**. If anything else appears, stop, fix the branch (usually rebase/re-cut from `origin/master`), or get explicit issue/owner approval.

## Stacked branches

If the branch intentionally depends on another open PR, say so **explicitly** in the PR body (base PR, why stack is required). If the issue does not call for stacking, treat an accidental stack as **invalid** and cut a clean branch from `origin/master`.

## Unity CLI tests

- Assume **one** Unity batch test process per machine unless the user explicitly allows parallel runs (Unity commonly locks the project).
- Serialize `tools/run-editmode-tests.ps1` / PlayMode batch runs across agents when they share the same project copy or lock semantics.

## Related

- `AGENTS.md` — GitHub Issue Workflow, merge ownership.
- `docs/PR_POST_OPEN_AGENT_LOOP.md` — after the PR exists, iterate until merge-ready; agents do not merge.
