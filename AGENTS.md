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

## Git Safety

- Work on the current branch by default.
- Do not create, checkout, switch, merge, rebase, push, or commit unless the user explicitly asks.
- If a branch change seems needed, ask first and explain why.
- Stage or commit only files that belong to the requested task.
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
