# Agent PR Review Checklist

Use this checklist before pushing a final commit and opening a PR. Green tests alone are not enough — the diff must also be clean, scoped, and authority-correct.

## 1 — Diff scope

- [ ] Only files explicitly authorized by the issue are changed (`git diff --name-only`).
- [ ] No accidental edits to `AGENTS.md`, `.cursor/rules/`, Unity scenes, prefabs, `.meta` files, or project settings unless the task explicitly requires it.
- [ ] Prefab / scene / `.meta` changes (if present) are intentional and explained in the PR body.

## 2 — PR body matches reality

- [ ] PR summary reflects the final commit, not an earlier draft or plan.
- [ ] Verification section matches what was actually run (not copy-pasted from a template).
- [ ] `Closes #N` is present.

## 3 — Test coverage

- [ ] Tests cover the runtime behavior being changed, not only the extracted helper or pure-logic path.
- [ ] No test assertions were weakened (looser equality, removed cases, skipped assertions).
- [ ] No production code was added solely to satisfy a test fixture (public hooks, behavior toggles, broad `InternalsVisibleTo`).
- [ ] Unity tests (EditMode and/or PlayMode as relevant) were rerun **after the final commit** if any C#, test, or asset file changed.

## 4 — Fusion authority model

- [ ] Gameplay outcomes (damage, death, respawn, score) are applied only under Host / State Authority.
- [ ] Clients submit input or intent only — no client-authoritative combat results.
- [ ] Core gameplay logic runs in `FixedUpdateNetwork` ticks, not render-only `Update` / `LateUpdate`.
- [ ] No `FindObjectsByType` or full-scene scans on hot simulation paths.

## 5 — Manual verification

- [ ] If PlayMode automation cannot cover input-driven or runtime behavior (e.g. camera feel, targeting, character controller), manual smoke verification was performed (host + one client) and noted in the PR body.
- [ ] Manual smoke checklist (where networked behavior changed):
  - Two clients in one room; both see movement.
  - Target selection works.
  - Spell damage respects authority and range.
  - Death and respawn stay consistent host + client.

## 6 — Public API and serialized defaults

- [ ] No unnecessary `public` surface added when `internal` or test-local would suffice.
- [ ] Serialized fields, inspector defaults, tuning constants, and config values are unchanged unless the task explicitly requires it.

## 7 — Final diff audit (multi-iteration work only)

Required when the feature needed multiple fix iterations before tests or behavior stabilized.

- [ ] Every non-trivial change is classified as **Required**, **Cleanup**, **Suspicious**, or **Unrelated**.
- [ ] Unrelated changes are reverted or moved to a separate PR.
- [ ] Suspicious changes are called out explicitly in the PR body for reviewer attention.

See `.cursor/rules/post-feature-diff-audit.mdc` for the full audit protocol.
