# Post-PR agent loop (merge-ready, no agent merge)

This document extends the **GitHub Issue Workflow** in `AGENTS.md` with a repeatable **parent ↔ subagent** loop after a PR exists. It is agent-agnostic: follow the same steps in Cursor, Codex, or CLI-driven agents.

## Goals

- After the PR is opened, **keep iterating** until the PR is **merge-ready** for a human.
- Use **subagents / focused tasks** for triage (CI, review) and the **parent agent** for scoped code fixups and pushes.
- **Agents never merge.** Merge stays with a human (`AGENTS.md`).

## What “done” means (stopping condition)

Pick a policy that matches your branch protection:

1. **Minimum (automation-friendly):** all **required** GitHub checks are green; **no unresolved** review threads that request changes; PR branch is up to date with base if your team requires it.
2. **Stricter:** at least one **human** (or trusted bot) **approval** on GitHub. Agents poll PR state via **GitHub MCP** (`pull_request_read`, `get_reviews`, `get_check_runs`) but cannot replace org policy if approvals must come from people.

“Recommended approve” in chat usually means **(1) + human approval when required**. Agents should state explicitly which bar they used.

## Roles

| Role | Responsibility |
|------|-----------------|
| **Parent agent** | Owns the branch, implements fixups, runs local verification (`docs/TEST_HARNESS.md`), commits, pushes, updates PR body if needed. |
| **CI subagent** | One failing check at a time (or batched): root cause, minimal fix proposal. Prefer repository `ci-investigator` / equivalent. |
| **Review subagent** | Diff / architecture / risk review; lists actionable items only. Prefer `code-review` / `code-reviewer` / equivalent. |

## Loop (repeat until stop condition)

Assume PR number `N` and branch already pushed.

1. **Sync state** (GitHub MCP — Cursor `user-github` server)
   - `pull_request_read` method `get` for PR `N` (`mergeable_state`, `state`, `head`/`base` SHAs).
   - `pull_request_read` method `get_check_runs` for CI status on the head commit.
   - `pull_request_read` method `get_reviews` / `get_review_comments` for open review threads.
   - If mergeable state is `CONFLICTING`, parent resolves conflicts (or stops and asks a human if intent clashes).

2. **CI pass**
   - For each **required** failing check: spawn a **CI subagent** with the failing check name + link to logs + allowed files from the issue/PR scope.
   - Parent applies the minimal fix, pushes, waits for CI (or polls).

3. **Review pass**
   - Fetch review comments / threads via `pull_request_read` (`get_review_comments`, `get_reviews`). Filter **resolved** threads out.
   - Spawn a **review subagent** with the diff scope and the open comments.
   - Parent addresses valid items; **push fixup commits**; reply on threads where appropriate.

4. **Re-run local verification** when the PR touches compilable gameplay or C# (`AGENTS.md` Verification).

5. **Stop** when the chosen stopping condition holds; hand off to a human for **Approve** and **Merge**.

## Cursor-oriented shortcuts

- **Skill (user-local):** `babysit` — “Keep a PR merge-ready…” aligns with steps 2–3 (`SKILL.md` in the user’s Cursor skills). The repo cannot ship that file; clone the intent into this doc (above).
- **Cursor Task tool:** delegate `ci-investigator` and `code-review` / `code-reviewer` subagents with a tight prompt: PR link, allowed file list, and “return a numbered action list only.”
- **Optional automation:** project hooks in `.cursor/hooks.json` can inject a **follow-up** after a subagent stops (`subagentStop` → `followup_message`) or after GitHub MCP creates a PR (`postToolUse` on `create_pull_request` → `additional_context`). Those hooks are **environment-specific** (stdin schema, OS); validate in **Cursor → Hooks** before relying on them. This repo does not ship a default hook so Windows/macOS clones are not broken.

## GitHub integration (this repo)

Use **Cursor GitHub MCP** (`user-github`). Do **not** require or document the `gh` CLI for agents in this repository.

| Task | MCP tools |
|------|-----------|
| Open PR | `create_pull_request` |
| PR summary / mergeability | `pull_request_read` → `get` |
| Changed files / diff | `pull_request_read` → `get_files`, `get_diff` |
| CI checks | `pull_request_read` → `get_check_runs` |
| Review threads / comments | `pull_request_read` → `get_review_comments`, `get_reviews` |
| Submit review comment (not approve) | `pull_request_review_write` → `create` with `event: COMMENT` |
| Issues | `issue_read`, `search_issues`, `issue_write` |
| List open PRs | `list_pull_requests`, `search_pull_requests` |

**Setup:** Cursor → MCP → enable GitHub / `user-github` with a token that has `repo` scope (403 on create usually means the token needs updating). Read each tool’s schema under the MCP descriptors before calling.

**Agents never merge** via `merge_pull_request` unless a human explicitly requests it (`AGENTS.md`).

## Permissions note

- **Approving** a PR via API requires a token with appropriate scope and may violate org policy if only humans may approve. Default posture: **human approval**, agent only prepares the PR.

## Related

- `AGENTS.md` — GitHub Issue Workflow §7–§9.
- `docs/TEST_HARNESS.md` — batch EditMode / PlayMode commands.
